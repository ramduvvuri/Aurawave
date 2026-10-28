using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuraWave.Audio;
using AuraWave.Configuration;
using AuraWave.Media;
using AuraWave.Models;
using AuraWave.Services;
using AuraWave.Utilities;

namespace AuraWave.Windows
{
    public sealed class SettingsWindow : Window
    {
        private readonly ConfigurationService _configService;
        private readonly WasapiLoopbackEngine _audioEngine;
        private readonly WindowsMediaSessionService _mediaService;
        private readonly Action _applyLiveSettingsCallback;
        private readonly ContentControl _sectionHost;

        public SettingsWindow(
            ConfigurationService configService,
            WasapiLoopbackEngine audioEngine,
            WindowsMediaSessionService mediaService,
            Action applyLiveSettingsCallback)
        {
            _configService = configService;
            _audioEngine = audioEngine;
            _mediaService = mediaService;
            _applyLiveSettingsCallback = applyLiveSettingsCallback;

            Title = "AuraWave â€” Complete Settings & Companion Studio";
            Width = 980;
            Height = 680;
            MinWidth = 760;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResize;
            Background = new SolidColorBrush(Color.FromRgb(10, 15, 28));
            Foreground = Brushes.White;

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var navBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(16)
            };

            var navStack = new StackPanel();
            navStack.Children.Add(new TextBlock
            {
                Text = "AURAWAVE",
                FontSize = 20,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 242, 254)),
                Margin = new Thickness(4, 4, 0, 2)
            });
            navStack.Children.Add(new TextBlock
            {
                Text = "Desktop Audio Companion",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(4, 0, 0, 18)
            });

            _sectionHost = new ContentControl();

            string[] sections = new[]
            {
                "1. Appearance",
                "2. Visualizers",
                "3. Audio Engine",
                "4. Window & Layout",
                "5. Now Playing",
                "6. Automation",
                "7. Shortcuts",
                "8. General & Diagnostics"
            };

            for (int i = 0; i < sections.Length; i++)
            {
                int idx = i;
                var btn = new Button
                {
                    Content = sections[i],
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(12, 10, 12, 10),
                    Margin = new Thickness(0, 0, 0, 6),
                    Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                    Foreground = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(80, 0, 242, 254)),
                    BorderThickness = new Thickness(1)
                };
                btn.Click += (_, _) => ShowSection(idx);
                navStack.Children.Add(btn);
            }

            navBorder.Child = navStack;
            Grid.SetColumn(navBorder, 0);

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(28, 22, 28, 22),
                Content = _sectionHost
            };
            Grid.SetColumn(scrollViewer, 1);

            mainGrid.Children.Add(navBorder);
            mainGrid.Children.Add(scrollViewer);
            Content = mainGrid;

            ShowSection(0);
        }

        private void ShowSection(int index)
        {
            var cfg = _configService.Current;
            var panel = new StackPanel();

            switch (index)
            {
                case 0:
                    AddHeader(panel, "Appearance & Theme Studio", "Customize color palettes, glow intensity, waveform thickness, and glass surfaces.");
                    AddEnumCombo(panel, "Color Theme Preset", cfg.SelectedTheme, val => { cfg.SelectedTheme = val; SaveAndApply(); });
                    AddSlider(panel, "Visualizer Intensity", 0.2, 3.0, cfg.VisualizerIntensity, val => { cfg.VisualizerIntensity = val; SaveAndApply(); });
                    AddSlider(panel, "Glow Intensity", 0.0, 1.0, cfg.GlowIntensity, val => { cfg.GlowIntensity = val; SaveAndApply(); });
                    AddSlider(panel, "Waveform Line Thickness", 1.0, 8.0, cfg.WaveformThickness, val => { cfg.WaveformThickness = val; SaveAndApply(); });
                    AddSlider(panel, "Now Playing Surface Opacity", 0.2, 1.0, cfg.NowPlayingSurfaceOpacity, val => { cfg.NowPlayingSurfaceOpacity = val; SaveAndApply(); });
                    AddCheck(panel, "Enable Reflection Under Spectrum", cfg.EnableReflection, val => { cfg.EnableReflection = val; SaveAndApply(); });
                    AddCheck(panel, "Enable Secondary Layered Waveform", cfg.EnableSecondaryWaveform, val => { cfg.EnableSecondaryWaveform = val; SaveAndApply(); });
                    break;

                case 1:
                    AddHeader(panel, "Audio Visualizers", "Switch between 9 real-time FFT and waveform visualizers.");
                    AddEnumCombo(panel, "Active Visualizer Mode", cfg.SelectedVisualizer, val => { cfg.SelectedVisualizer = val; SaveAndApply(); });
                    AddSlider(panel, "Frequency Band Density", 16, 128, cfg.FrequencyBandCount, val => { cfg.FrequencyBandCount = (int)Math.Round(val); SaveAndApply(); });
                    AddSlider(panel, "Audio Sensitivity", 0.2, 5.0, cfg.AudioSensitivity, val => { cfg.AudioSensitivity = val; SaveAndApply(); });
                    AddSlider(panel, "Temporal Smoothing Factor", 0.05, 0.95, cfg.SmoothingFactor, val => { cfg.SmoothingFactor = val; SaveAndApply(); });
                    AddSlider(panel, "Bass Response Boost", 0.5, 2.5, cfg.BassResponseBoost, val => { cfg.BassResponseBoost = val; SaveAndApply(); });
                    AddCheck(panel, "Symmetric Spectrum Mode", cfg.SymmetricSpectrum, val => { cfg.SymmetricSpectrum = val; SaveAndApply(); });
                    AddCheck(panel, "Show Peak-Hold Indicators", cfg.ShowPeakIndicators, val => { cfg.ShowPeakIndicators = val; SaveAndApply(); });
                    break;

                case 2:
                    AddHeader(panel, "WASAPI Loopback Audio Engine", "Real-time system audio capture and hysteresis activity thresholds.");
                    AddInfoLine(panel, "Current Output Endpoint", _audioEngine.CurrentDeviceName);
                    AddInfoLine(panel, "Capture Engine Status", _audioEngine.CaptureStatusText);
                    AddInfoLine(panel, "Audio Activity State", _audioEngine.LatestFrame.IsAudioActive ? "Active (Music Detected)" : "Idle / Silent");
                    AddSlider(panel, "Attack Threshold (RMS)", 0.001, 0.08, cfg.ActivityAttackThreshold, val => { cfg.ActivityAttackThreshold = val; SaveAndApply(); });
                    AddSlider(panel, "Release Threshold (RMS)", 0.0005, 0.05, cfg.ActivityReleaseThreshold, val => { cfg.ActivityReleaseThreshold = val; SaveAndApply(); });
                    AddSlider(panel, "Noise Floor Gate", 0.0002, 0.02, cfg.NoiseFloorThreshold, val => { cfg.NoiseFloorThreshold = val; SaveAndApply(); });
                    AddActionButton(panel, "Restart WASAPI Loopback Capture", () => _audioEngine.Start());
                    break;

                case 3:
                    AddHeader(panel, "Window & Overlay Layout", "Independent visibility, pinning, click-through, and placement presets.");
                    AddEnumCombo(panel, "Presentation Mode", cfg.PresentationMode, val => { cfg.PresentationMode = val; SaveAndApply(); });
                    AddEnumCombo(panel, "Waveform Placement Preset", cfg.WaveformPlacementPreset, val => { cfg.WaveformPlacementPreset = val; SaveAndApply(); });
                    AddCheck(panel, "Show Transparent Waveform Overlay", cfg.WaveformVisible, val => { cfg.WaveformVisible = val; SaveAndApply(); });
                    AddCheck(panel, "Pin Waveform (Never Auto-Hide)", cfg.WaveformPinned, val => { cfg.WaveformPinned = val; SaveAndApply(); });
                    AddCheck(panel, "Show Floating Now Playing Widget", cfg.NowPlayingVisible, val => { cfg.NowPlayingVisible = val; SaveAndApply(); });
                    AddCheck(panel, "Pin Now Playing Widget", cfg.NowPlayingPinned, val => { cfg.NowPlayingPinned = val; SaveAndApply(); });
                    AddCheck(panel, "Enable Click-Through on Waveform (Alt+C)", cfg.ClickThroughEnabled, val => { cfg.ClickThroughEnabled = val; SaveAndApply(); });
                    AddCheck(panel, "Keep Overlay Always-On-Top", cfg.AlwaysOnTop, val => { cfg.AlwaysOnTop = val; SaveAndApply(); });
                    break;

                case 4:
                    AddHeader(panel, "Now Playing Companion Widget", "Configure rotating disc artwork, metadata fields, and auto-hide timeout.");
                    AddCheck(panel, "Show Album Art / Vinyl Disc", cfg.ShowAlbumArt, val => { cfg.ShowAlbumArt = val; SaveAndApply(); });
                    AddCheck(panel, "Enable Rotating Vinyl Disc Animation", cfg.EnableRotatingDisc, val => { cfg.EnableRotatingDisc = val; SaveAndApply(); });
                    AddCheck(panel, "Show Track Title", cfg.ShowTrackTitle, val => { cfg.ShowTrackTitle = val; SaveAndApply(); });
                    AddCheck(panel, "Show Artist Name", cfg.ShowArtistName, val => { cfg.ShowArtistName = val; SaveAndApply(); });
                    AddCheck(panel, "Show Playback Progress Bar", cfg.ShowProgressBar, val => { cfg.ShowProgressBar = val; SaveAndApply(); });
                    AddCheck(panel, "Show System Output Volume Slider", cfg.ShowVolumeControl, val => { cfg.ShowVolumeControl = val; SaveAndApply(); });
                    AddCheck(panel, "Enable Contextual Toolbar Auto-Hide", cfg.EnableNowPlayingAutoHide, val => { cfg.EnableNowPlayingAutoHide = val; SaveAndApply(); });
                    AddSlider(panel, "Auto-Hide Timeout (Seconds)", 2, 30, cfg.AutoHideTimeoutSeconds, val => { cfg.AutoHideTimeoutSeconds = (int)Math.Round(val); SaveAndApply(); });
                    break;

                case 5:
                    AddHeader(panel, "Automation & Windows Startup", "Automatic music detection and per-user Windows login startup.");
                    AddCheck(panel, "Start AuraWave at Windows Login (HKCU Run)", cfg.StartWithWindows, val =>
                    {
                        cfg.StartWithWindows = val;
                        StartupRegistrationService.SetStartupEnabled(val);
                        SaveAndApply();
                    });
                    AddCheck(panel, "Automatically Show Waveform When Music Starts", cfg.AutoShowWaveformOnAudio, val => { cfg.AutoShowWaveformOnAudio = val; SaveAndApply(); });
                    AddCheck(panel, "Automatically Show Now Playing When Music Starts", cfg.AutoShowNowPlayingOnAudio, val => { cfg.AutoShowNowPlayingOnAudio = val; SaveAndApply(); });
                    AddCheck(panel, "Automatically Hide Unpinned Widgets When Audio Stops", cfg.AutoHideWhenAudioStops, val => { cfg.AutoHideWhenAudioStops = val; SaveAndApply(); });
                    AddSlider(panel, "Audio Stop Inactivity Delay (Seconds)", 2, 60, cfg.AudioStopInactivityDelaySeconds, val => { cfg.AudioStopInactivityDelaySeconds = (int)Math.Round(val); SaveAndApply(); });
                    break;

                case 6:
                    AddHeader(panel, "Global Keyboard Shortcuts", "Registered system-wide Windows hotkeys.");
                    AddInfoLine(panel, "Alt + C", "Toggle Waveform Click-Through Mode");
                    AddInfoLine(panel, "Alt + A", "Show or Hide All AuraWave Components");
                    AddInfoLine(panel, "Alt + S", "Open or Focus This Settings Window");
                    AddInfoLine(panel, "Alt + M", "Cycle Presentation Mode (Overlay / Desktop / WorkerW)");
                    AddInfoLine(panel, "Alt + W", "Toggle Waveform Visibility Independently");
                    AddInfoLine(panel, "Alt + P", "Toggle Now Playing Visibility Independently");
                    AddInfoLine(panel, "Ctrl + Alt + E", "Enter or Exit Interactive Edit & Resize Mode");
                    AddInfoLine(panel, "Ctrl + Alt + Plus", "Increase Selected Widget Size (+10%)");
                    AddInfoLine(panel, "Ctrl + Alt + Minus", "Decrease Selected Widget Size (-10%)");
                    AddInfoLine(panel, "Alt + Q", "Cleanly Quit AuraWave");
                    break;

                case 7:
                    AddHeader(panel, "General, Persistence & Diagnostics", "Configuration file paths, log locations, and factory reset.");
                    AddInfoLine(panel, "Application Version", "AuraWave 1.0.0 (Windows x64)");
                    AddInfoLine(panel, "Configuration File", _configService.ConfigFilePath);
                    AddInfoLine(panel, "Diagnostic Log File", AppLogger.LogFilePath);
                    AddInfoLine(panel, "Active Media Source", _mediaService.CurrentSession.SourceAppId);
                    AddActionButton(panel, "Restore Default Settings", () =>
                    {
                        _configService.ResetToDefaults();
                        _applyLiveSettingsCallback();
                        ShowSection(7);
                    });
                    break;
            }

            _sectionHost.Content = panel;
        }

        private void SaveAndApply()
        {
            _configService.Save();
            _applyLiveSettingsCallback();
        }

        private static void AddHeader(StackPanel parent, string title, string subtitle)
        {
            parent.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 4)
            });
            parent.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 20)
            });
        }

        private static void AddInfoLine(StackPanel parent, string label, string value)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), FontWeight = FontWeights.SemiBold };
            var val = new TextBlock { Text = value, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(lbl, 0);
            Grid.SetColumn(val, 1);
            grid.Children.Add(lbl);
            grid.Children.Add(val);
            parent.Children.Add(grid);
        }

        private static void AddCheck(StackPanel parent, string label, bool initial, Action<bool> onChanged)
        {
            var cb = new CheckBox
            {
                Content = label,
                IsChecked = initial,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 12),
                FontSize = 13
            };
            cb.Checked += (_, _) => onChanged(true);
            cb.Unchecked += (_, _) => onChanged(false);
            parent.Children.Add(cb);
        }

        private static void AddSlider(StackPanel parent, string label, double min, double max, double initial, Action<double> onChanged)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            var valText = new TextBlock
            {
                Text = $"{label}: {initial:F2}",
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 4)
            };
            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = initial,
                Width = 420,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            slider.ValueChanged += (_, e) =>
            {
                valText.Text = $"{label}: {e.NewValue:F2}";
                onChanged(e.NewValue);
            };
            sp.Children.Add(valText);
            sp.Children.Add(slider);
            parent.Children.Add(sp);
        }

        private static void AddEnumCombo<TEnum>(StackPanel parent, string label, TEnum initial, Action<TEnum> onChanged) where TEnum : struct, Enum
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            sp.Children.Add(new TextBlock { Text = label, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 4) });
            var combo = new ComboBox
            {
                Width = 280,
                HorizontalAlignment = HorizontalAlignment.Left,
                ItemsSource = Enum.GetValues(typeof(TEnum)),
                SelectedItem = initial
            };
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is TEnum selected)
                    onChanged(selected);
            };
            sp.Children.Add(combo);
            parent.Children.Add(sp);
        }

        private static void AddActionButton(StackPanel parent, string text, Action onClick)
        {
            var btn = new Button
            {
                Content = text,
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 8, 0, 12),
                Background = new SolidColorBrush(Color.FromRgb(0, 242, 254)),
                Foreground = new SolidColorBrush(Color.FromRgb(10, 15, 28)),
                FontWeight = FontWeights.Bold
            };
            btn.Click += (_, _) => onClick();
            parent.Children.Add(btn);
        }
    }
}