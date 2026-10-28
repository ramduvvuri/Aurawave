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
using AuraWave.Services;
using AuraWave.SystemTray;
using AuraWave.Utilities;
using AuraWave.Windows;

namespace AuraWave
{
    public partial class App : Application
    {
        private Mutex? _singleInstanceMutex;
        private ConfigurationService? _configService;
        private FavouritesService? _favouritesService;
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

            AppLogger.Info("AuraWave Emerald Floating Music Bar starting up.");

            _configService = new ConfigurationService();
            var cfg = _configService.Load();
            _favouritesService = new FavouritesService(_configService.ConfigDirectory);

            _dspAnalyzer = new DspAnalyzer(1024, cfg.FrequencyBandCount);
            _audioEngine = new WasapiLoopbackEngine(_configService, _dspAnalyzer);
            _mediaService = new WindowsMediaSessionService();

            _nowPlayingWindow = new NowPlayingWidgetWindow(_configService, _mediaService, _audioEngine, _favouritesService);
            _nowPlayingWindow.EditModeSelected += (_, _) => _configService.Current.SelectedEditComponent = EditableComponent.NowPlaying;
            _nowPlayingWindow.OpenSettingsRequested += (_, _) => OpenOrFocusSettings();
            _nowPlayingWindow.HideBarRequested += (_, _) =>
            {
                _configService.Current.NowPlayingVisible = false;
                _nowPlayingWindow.Hide();
                _configService.Save();
            };

            _waveformWindow = new WaveformOverlayWindow(_configService);
            _waveformWindow.EditModeSelected += (_, _) => _configService.Current.SelectedEditComponent = EditableComponent.Waveform;
            _waveformWindow.ExitEditModeRequested += (_, _) => ToggleEditMode();

            _trayController = new SystemTrayController(_configService);
            _trayController.ToggleNowPlayingRequested += (_, _) => ToggleNowPlayingVisibility();
            _trayController.TogglePinRequested += (_, _) =>
            {
                if (_nowPlayingWindow != null && _configService != null)
                {
                    _nowPlayingWindow.SetPinned(!_configService.Current.NowPlayingPinned);
                }
            };
            _trayController.ResetPositionRequested += (_, _) =>
            {
                _configService.Current.NowPlayingVisible = true;
                _nowPlayingWindow?.ResetToBottomCenter();
                ApplyConfigurationToWindows();
            };
            _trayController.ToggleWaveformRequested += (_, _) => ToggleWaveformVisibility();
            _trayController.ToggleClickThroughRequested += (_, _) => ToggleClickThrough();
            _trayController.ToggleEditModeRequested += (_, _) => ToggleEditMode();
            _trayController.OpenSettingsRequested += (_, _) => OpenOrFocusSettings();
            _trayController.ApplyChangesRequested += (_, _) => ApplyConfigurationToWindows();
            _trayController.QuitRequested += (_, _) => CleanShutdown();

            // Show ONLY the primary integrated Emerald Music Bar by default
            _nowPlayingWindow.Show();
            if (cfg.WaveformVisible)
            {
                _waveformWindow.Show();
            }
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
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _renderTimer.Tick += OnRenderFrameTick;
            _renderTimer.Start();
        }

        private void OnRenderFrameTick(object? sender, EventArgs e)
        {
            if (_audioEngine == null || _configService == null || _nowPlayingWindow == null || _mediaService == null)
                return;

            var frame = _audioEngine.LatestFrame;
            _mediaService.NotifyAudioActivity(frame.IsAudioActive);

            if (frame.IsAudioActive != _lastAudioActiveState)
            {
                _lastAudioActiveState = frame.IsAudioActive;
                HandleAutomaticAudioVisibility(frame.IsAudioActive);
            }

            if (_nowPlayingWindow.IsVisible)
            {
                _nowPlayingWindow.UpdateAudioFrame(frame);
            }

            if (_waveformWindow != null && _waveformWindow.IsVisible)
            {
                _waveformWindow.UpdateAudioFrame(frame);
            }
        }

        private void HandleAutomaticAudioVisibility(bool audioStarted)
        {
            if (_configService == null || _nowPlayingWindow == null) return;
            var cfg = _configService.Current;

            if (audioStarted)
            {
                if (cfg.AutoShowNowPlayingOnAudio && !cfg.NowPlayingVisible)
                {
                    cfg.NowPlayingVisible = true;
                    ApplyConfigurationToWindows();
                    _nowPlayingWindow.RestartAutoHideTimerIfUnpinned();
                }
            }
            else if (cfg.AutoHideWhenAudioStops)
            {
                if (!cfg.NowPlayingPinned)
                {
                    cfg.NowPlayingVisible = false;
                    ApplyConfigurationToWindows();
                }
            }
        }

        private void ApplyConfigurationToWindows()
        {
            if (_configService == null || _nowPlayingWindow == null || _waveformWindow == null) return;
            var cfg = _configService.Current;

            if (cfg.NowPlayingVisible)
            {
                _nowPlayingWindow.Show();
                _nowPlayingWindow.Opacity = 1.0;
            }
            else
            {
                _nowPlayingWindow.Hide();
            }

            if (cfg.WaveformVisible)
            {
                _waveformWindow.Show();
                _waveformWindow.SyncNativeStyles();
            }
            else
            {
                _waveformWindow.Hide();
            }

            _nowPlayingWindow.ApplyAppearanceFromConfig();
            _nowPlayingWindow.SyncNativeStyles();
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
            if (_configService == null || _nowPlayingWindow == null) return;
            _configService.Current.NowPlayingVisible = !_configService.Current.NowPlayingVisible;
            _configService.Save();
            ApplyConfigurationToWindows();
            if (_configService.Current.NowPlayingVisible)
            {
                _nowPlayingWindow.RestartAutoHideTimerIfUnpinned();
            }
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
            _nowPlayingWindow?.SetEditMode(_isEditMode);
            _waveformWindow?.SetEditMode(_isEditMode);
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
            if (_configService == null || _nowPlayingWindow == null || _waveformWindow == null) return;
            if (_configService.Current.SelectedEditComponent == EditableComponent.Waveform && _waveformWindow.IsVisible)
            {
                _waveformWindow.Width = Math.Clamp(_waveformWindow.Width * scaleMultiplier, 240, 3840);
                _waveformWindow.Height = Math.Clamp(_waveformWindow.Height * scaleMultiplier, 60, 900);
            }
            else
            {
                _nowPlayingWindow.Width = Math.Clamp(_nowPlayingWindow.Width * scaleMultiplier, 540, 1600);
                _nowPlayingWindow.Height = Math.Clamp(_nowPlayingWindow.Height * scaleMultiplier, 76, 160);
            }
            _configService.Save();
        }

        private void OpenOrFocusSettings()
        {
            if (_configService == null || _audioEngine == null || _mediaService == null) return;
            if (_settingsWindow == null || !_settingsWindow.IsLoaded)
            {
                _settingsWindow = new SettingsWindow(
                    _configService,
                    _audioEngine,
                    _mediaService,
                    ApplyConfigurationToWindows,
                    () =>
                    {
                        _configService.Current.NowPlayingVisible = true;
                        _nowPlayingWindow?.ResetToBottomCenter();
                        ApplyConfigurationToWindows();
                    });
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
            if (_nowPlayingWindow == null) return;
            IntPtr hwnd = new WindowInteropHelper(_nowPlayingWindow).Handle;
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
            NativeWindowInterop.RegisterHotKey(hwnd, 9008, ctrlAlt, 0xBB); // Ctrl + Alt + Plus
            NativeWindowInterop.RegisterHotKey(hwnd, 9009, ctrlAlt, 0x6B); // Ctrl + Alt + NumpadPlus
            NativeWindowInterop.RegisterHotKey(hwnd, 9010, ctrlAlt, 0xBD); // Ctrl + Alt + Minus
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
                    case 9002: ToggleNowPlayingVisibility(); handled = true; break;
                    case 9003: OpenOrFocusSettings(); handled = true; break;
                    case 9004: CyclePresentationMode(); handled = true; break;
                    case 9005: ToggleWaveformVisibility(); handled = true; break;
                    case 9006: ToggleNowPlayingVisibility(); handled = true; break;
                    case 9007: ToggleEditMode(); handled = true; break;
                    case 9008:
                    case 9009: ResizeSelectedWidget(1.08); handled = true; break;
                    case 9010:
                    case 9011: ResizeSelectedWidget(0.92); handled = true; break;
                    case 9012: CleanShutdown(); handled = true; break;
                }
            }
            return IntPtr.Zero;
        }

        private void CleanShutdown()
        {
            AppLogger.Info("AuraWave shutting down cleanly.");
            _renderTimer?.Stop();

            if (_nowPlayingWindow != null)
            {
                IntPtr hwnd = new WindowInteropHelper(_nowPlayingWindow).Handle;
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
            _waveformWindow?.Close();
            _nowPlayingWindow?.Close();

            Shutdown(0);
        }
    }
}