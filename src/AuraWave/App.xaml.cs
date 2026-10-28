using System;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using AuraWave.Audio;
using AuraWave.Configuration;
using AuraWave.DSP;
using AuraWave.Media;
using AuraWave.Models;
using AuraWave.SystemTray;
using AuraWave.Utilities;
using AuraWave.Windows;

namespace AuraWave
{
    public partial class App : Application
    {
        private Mutex? _singleInstanceMutex;
        private ConfigurationService? _configService;
        private DspAnalyzer? _dspAnalyzer;
        private WasapiLoopbackEngine? _audioEngine;
        private WindowsMediaSessionService? _mediaService;
        private WaveformOverlayWindow? _waveformWindow;
        private NowPlayingWidgetWindow? _nowPlayingWindow;
        private SettingsWindow? _settingsWindow;
        private SystemTrayController? _trayController;
        private DispatcherTimer? _renderTimer;
        private HwndSource? _hotkeyHwndSource;
        private bool _isEditMode;
        private bool _lastAudioActiveState;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _singleInstanceMutex = new Mutex(true, "Local\\AuraWaveNext_SingleInstance_Mutex", out bool createdNew);
            if (!createdNew)
            {
                Shutdown(0);
                return;
            }

            AppLogger.Info("AuraWave starting up.");

            _configService = new ConfigurationService();
            var cfg = _configService.Load();

            _dspAnalyzer = new DspAnalyzer(1024, cfg.FrequencyBandCount);
            _audioEngine = new WasapiLoopbackEngine(_configService, _dspAnalyzer);
            _mediaService = new WindowsMediaSessionService();

            _waveformWindow = new WaveformOverlayWindow(_configService);
            _waveformWindow.EditModeSelected += (_, _) => _configService.Current.SelectedEditComponent = EditableComponent.Waveform;
            _waveformWindow.ExitEditModeRequested += (_, _) => ToggleEditMode();

            _nowPlayingWindow = new NowPlayingWidgetWindow(_configService, _mediaService, _audioEngine);
            _nowPlayingWindow.EditModeSelected += (_, _) => _configService.Current.SelectedEditComponent = EditableComponent.NowPlaying;
            _nowPlayingWindow.OpenSettingsRequested += (_, _) => OpenOrFocusSettings();

            _trayController = new SystemTrayController(_configService);
            _trayController.ToggleWaveformRequested += (_, _) => ToggleWaveformVisibility();
            _trayController.ToggleNowPlayingRequested += (_, _) => ToggleNowPlayingVisibility();
            _trayController.ToggleClickThroughRequested += (_, _) => ToggleClickThrough();
            _trayController.ToggleEditModeRequested += (_, _) => ToggleEditMode();
            _trayController.OpenSettingsRequested += (_, _) => OpenOrFocusSettings();
            _trayController.ApplyChangesRequested += (_, _) => ApplyConfigurationToWindows();
            _trayController.QuitRequested += (_, _) => CleanShutdown();

            _waveformWindow.Show();
            _nowPlayingWindow.Show();
            ApplyConfigurationToWindows();

            RegisterGlobalHotkeys();

            _mediaService.SessionUpdated += (_, snap) =>
            {
                Dispatcher.BeginInvoke(() => _nowPlayingWindow?.UpdateMediaSnapshot(snap));
            };

            _audioEngine.Start();
            _mediaService.Start();

            _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS UI synchronization
            };
            _renderTimer.Tick += OnRenderFrameTick;
            _renderTimer.Start();
        }

        private void OnRenderFrameTick(object? sender, EventArgs e)
        {
            if (_audioEngine == null || _configService == null || _waveformWindow == null || _nowPlayingWindow == null || _mediaService == null)
                return;

            var frame = _audioEngine.LatestFrame;
            _mediaService.NotifyAudioActivity(frame.IsAudioActive);

            if (frame.IsAudioActive != _lastAudioActiveState)
            {
                _lastAudioActiveState = frame.IsAudioActive;
                HandleAutomaticAudioVisibility(frame.IsAudioActive);
            }

            if (_waveformWindow.IsVisible)
            {
                _waveformWindow.UpdateAudioFrame(frame);
            }

            if (_nowPlayingWindow.IsVisible)
            {
                _nowPlayingWindow.StepDiscAnimation(_mediaService.CurrentSession.PlaybackStatus == PlaybackState.Playing || frame.IsAudioActive);
            }
        }

        private void HandleAutomaticAudioVisibility(bool audioStarted)
        {
            if (_configService == null) return;
            var cfg = _configService.Current;

            if (audioStarted)
            {
                if (cfg.AutoShowWaveformOnAudio) cfg.WaveformVisible = true;
                if (cfg.AutoShowNowPlayingOnAudio) cfg.NowPlayingVisible = true;
                ApplyConfigurationToWindows();
            }
            else if (cfg.AutoHideWhenAudioStops)
            {
                if (!cfg.WaveformPinned) cfg.WaveformVisible = false;
                if (!cfg.NowPlayingPinned) cfg.NowPlayingVisible = false;
                ApplyConfigurationToWindows();
            }
        }

        private void ApplyConfigurationToWindows()
        {
            if (_configService == null || _waveformWindow == null || _nowPlayingWindow == null) return;
            var cfg = _configService.Current;

            if (cfg.WaveformVisible) _waveformWindow.Show();
            else _waveformWindow.Hide();

            if (cfg.NowPlayingVisible) _nowPlayingWindow.Show();
            else _nowPlayingWindow.Hide();

            _waveformWindow.SyncNativeStyles();
            _nowPlayingWindow.ApplyAppearanceFromConfig();
        }

        private void ToggleWaveformVisibility()
        {
            if (_configService == null) return;
            _configService.Current.WaveformVisible = !_configService.Current.WaveformVisible;
            _configService.Save();
            ApplyConfigurationToWindows();
        }

        private void ToggleNowPlayingVisibility()
        {
            if (_configService == null) return;
            _configService.Current.NowPlayingVisible = !_configService.Current.NowPlayingVisible;
            _configService.Save();
            ApplyConfigurationToWindows();
        }

        private void ToggleAllVisibility()
        {
            if (_configService == null) return;
            bool anyVisible = _configService.Current.WaveformVisible || _configService.Current.NowPlayingVisible;
            _configService.Current.WaveformVisible = !anyVisible;
            _configService.Current.NowPlayingVisible = !anyVisible;
            _configService.Save();
            ApplyConfigurationToWindows();
        }

        private void ToggleClickThrough()
        {
            if (_configService == null) return;
            _configService.Current.ClickThroughEnabled = !_configService.Current.ClickThroughEnabled;
            _configService.Save();
            ApplyConfigurationToWindows();
        }

        private void ToggleEditMode()
        {
            _isEditMode = !_isEditMode;
            _waveformWindow?.SetEditMode(_isEditMode);
            _nowPlayingWindow?.SetEditMode(_isEditMode);
            _configService?.Save();
        }

        private void CyclePresentationMode()
        {
            if (_configService == null) return;
            int next = ((int)_configService.Current.PresentationMode + 1) % 3;
            _configService.Current.PresentationMode = (PresentationMode)next;
            _configService.Save();
            ApplyConfigurationToWindows();
        }

        private void ResizeSelectedWidget(double scaleMultiplier)
        {
            if (_configService == null || _waveformWindow == null || _nowPlayingWindow == null) return;
            if (_configService.Current.SelectedEditComponent == EditableComponent.NowPlaying)
            {
                _nowPlayingWindow.Width = Math.Clamp(_nowPlayingWindow.Width * scaleMultiplier, 280, 960);
                _nowPlayingWindow.Height = Math.Clamp(_nowPlayingWindow.Height * scaleMultiplier, 84, 320);
            }
            else
            {
                _waveformWindow.Width = Math.Clamp(_waveformWindow.Width * scaleMultiplier, 240, 3840);
                _waveformWindow.Height = Math.Clamp(_waveformWindow.Height * scaleMultiplier, 60, 900);
            }
            _configService.Save();
        }

        private void OpenOrFocusSettings()
        {
            if (_configService == null || _audioEngine == null || _mediaService == null) return;
            if (_settingsWindow == null || !_settingsWindow.IsLoaded)
            {
                _settingsWindow = new SettingsWindow(_configService, _audioEngine, _mediaService, ApplyConfigurationToWindows);
                _settingsWindow.Closed += (_, _) => _settingsWindow = null;
                _settingsWindow.Show();
            }
            else
            {
                _settingsWindow.Activate();
            }
        }

        private void RegisterGlobalHotkeys()
        {
            if (_waveformWindow == null) return;
            IntPtr hwnd = new WindowInteropHelper(_waveformWindow).Handle;
            _hotkeyHwndSource = HwndSource.FromHwnd(hwnd);
            _hotkeyHwndSource?.AddHook(WndProc);

            uint alt = NativeWindowInterop.MOD_ALT | NativeWindowInterop.MOD_NOREPEAT;
            uint ctrlAlt = NativeWindowInterop.MOD_CONTROL | NativeWindowInterop.MOD_ALT | NativeWindowInterop.MOD_NOREPEAT;

            NativeWindowInterop.RegisterHotKey(hwnd, 9001, alt, 0x43); // Alt + C
            NativeWindowInterop.RegisterHotKey(hwnd, 9002, alt, 0x41); // Alt + A
            NativeWindowInterop.RegisterHotKey(hwnd, 9003, alt, 0x53); // Alt + S
            NativeWindowInterop.RegisterHotKey(hwnd, 9004, alt, 0x4D); // Alt + M
            NativeWindowInterop.RegisterHotKey(hwnd, 9005, alt, 0x57); // Alt + W
            NativeWindowInterop.RegisterHotKey(hwnd, 9006, alt, 0x50); // Alt + P
            NativeWindowInterop.RegisterHotKey(hwnd, 9007, ctrlAlt, 0x45); // Ctrl + Alt + E
            NativeWindowInterop.RegisterHotKey(hwnd, 9008, ctrlAlt, 0xBB); // Ctrl + Alt + Plus (OEM_PLUS)
            NativeWindowInterop.RegisterHotKey(hwnd, 9009, ctrlAlt, 0x6B); // Ctrl + Alt + NumpadPlus
            NativeWindowInterop.RegisterHotKey(hwnd, 9010, ctrlAlt, 0xBD); // Ctrl + Alt + Minus (OEM_MINUS)
            NativeWindowInterop.RegisterHotKey(hwnd, 9011, ctrlAlt, 0x6D); // Ctrl + Alt + NumpadMinus
            NativeWindowInterop.RegisterHotKey(hwnd, 9012, alt, 0x51); // Alt + Q
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeWindowInterop.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                switch (id)
                {
                    case 9001: ToggleClickThrough(); handled = true; break;
                    case 9002: ToggleAllVisibility(); handled = true; break;
                    case 9003: OpenOrFocusSettings(); handled = true; break;
                    case 9004: CyclePresentationMode(); handled = true; break;
                    case 9005: ToggleWaveformVisibility(); handled = true; break;
                    case 9006: ToggleNowPlayingVisibility(); handled = true; break;
                    case 9007: ToggleEditMode(); handled = true; break;
                    case 9008:
                    case 9009: ResizeSelectedWidget(1.10); handled = true; break;
                    case 9010:
                    case 9011: ResizeSelectedWidget(0.90); handled = true; break;
                    case 9012: CleanShutdown(); handled = true; break;
                }
            }
            return IntPtr.Zero;
        }

        private void CleanShutdown()
        {
            AppLogger.Info("AuraWave shutting down cleanly.");
            _renderTimer?.Stop();

            if (_waveformWindow != null)
            {
                IntPtr hwnd = new WindowInteropHelper(_waveformWindow).Handle;
                for (int id = 9001; id <= 9012; id++)
                {
                    NativeWindowInterop.UnregisterHotKey(hwnd, id);
                }
            }

            _configService?.Save();
            _mediaService?.Dispose();
            _audioEngine?.Dispose();
            _trayController?.Dispose();

            _settingsWindow?.Close();
            _nowPlayingWindow?.Close();
            _waveformWindow?.Close();

            Shutdown(0);
        }
    }
}