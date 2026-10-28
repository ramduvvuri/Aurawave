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
        private readonly Action _resetPositionCallback;
        private readonly ContentControl _sectionHost;

        public SettingsWindow(
            ConfigurationService configService,
            WasapiLoopbackEngine audioEngine,
            WindowsMediaSessionService mediaService,
            Action applyLiveSettingsCallback,
            Action resetPositionCallback)
        {
            _configService = configService;
            _audioEngine = audioEngine;
            _mediaService = mediaService;
            _applyLiveSettingsCallback = applyLiveSettingsCallback;
            _resetPositionCallback = resetPositionCallback;

            Title = "AuraWave — Emerald Floating Music Bar Settings";
            Width = 960;
            Height = 660;
            MinWidth = 760;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResize;
            Background = new SolidColorBrush(Color.FromRgb(7, 18, 15));
            Foreground = Brushes.White;

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(235) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var navBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(11, 27, 22)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 53, 214, 160)),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(16)
            };

            var navStack = new StackPanel();
            navStack.Children.Add(new TextBlock
            {
                Text = "AURAWAVE",
                FontSize = 20,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(Color.FromRgb(53, 214, 160)),
                Margin = new Thickness(4, 4, 0, 2)
            });
            navStack.Children.Add(new TextBlock
            {
                Text = "Emerald Floating Music Bar",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(145, 164, 158)),
                Margin = new Thickness(4, 0, 0, 18)
            });

            _sectionHost = new ContentControl();

            string[] sections = new[]
            {
                "1. Appearance & Emerald Theme",
                "2. Embedded Spectrum & DSP",
                "3. Audio Capture Engine",
                "4. Bar Position & Pinning",
                "5. Music Bar Controls",
                "6. Auto-Hide & Startup",
                "7. Keyboard Shortcuts",
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
                    Background = new SolidColorBrush(Color.FromRgb(16, 38, 31)),
                    Foreground = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(90, 53, 214, 160)),
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
                    AddHeader(panel, "Appearance & Emerald Theme", "Configure the emerald-green floating capsule surface, opacity, and accents.");
                    AddEnumCombo(panel, "Color Theme Preset (Default: EmeraldCapsule)", cfg.SelectedTheme, val => { cfg.SelectedTheme = val; SaveAndApply(); });
                    AddSlider(panel, "Capsule Surface Opacity", 0.35, 1.0, cfg.NowPlayingSurfaceOpacity, val => { cfg.NowPlayingSurfaceOpacity = val; SaveAndApply(); });
                    AddSlider(panel, "Embedded Spectrum Intensity", 0.2, 3.0, cfg.VisualizerIntensity, val => { cfg.VisualizerIntensity = val; SaveAndApply(); });
                    AddSlider(panel, "Outer Emerald Glow Intensity", 0.0, 1.0, cfg.GlowIntensity, val => { cfg.GlowIntensity = val; SaveAndApply(); });
                    break;

                case 1:
                    AddHeader(panel, "Embedded Spectrum & Optional Standalone Mode", "Configure the real-time audio spectrum inside the music bar.");
                    AddCheck(panel, "Show Embedded Mini Audio Spectrum Inside Music Bar", cfg.ShowEmbeddedSpectrum, val => { cfg.ShowEmbeddedSpectrum = val; SaveAndApply(); });
                    AddSlider(panel, "Audio Sensitivity", 0.2, 5.0, cfg.AudioSensitivity, val => { cfg.AudioSensitivity = val; SaveAndApply(); });
                    AddSlider(panel, "Temporal Smoothing Factor", 0.05, 0.95, cfg.SmoothingFactor, val => { cfg.SmoothingFactor = val; SaveAndApply(); });
                    AddSlider(panel, "Bass Response Boost", 0.5, 2.5, cfg.BassResponseBoost, val => { cfg.BassResponseBoost = val; SaveAndApply(); });
                    AddCheck(panel, "Enable Optional Separate Desktop Waveform (Disabled by Default)", cfg.WaveformVisible, val => { cfg.WaveformVisible = val; SaveAndApply(); });
                    break;

                case 2:
                    AddHeader(panel, "WASAPI Loopback Audio Engine", "Real-time system audio capture and hysteresis activity thresholds.");
                    AddInfoLine(panel, "Current Output Endpoint", _audioEngine.CurrentDeviceName);
                    AddInfoLine(panel, "Capture Engine Status", _audioEngine.CaptureStatusText);
                    AddInfoLine(panel, "Audio Activity State", _audioEngine.LatestFrame.IsAudioActive ? "Active (Music Playing)" : "Idle / Silent");
                    AddSlider(panel, "Attack Threshold (RMS)", 0.001, 0.08, cfg.ActivityAttackThreshold, val => { cfg.ActivityAttackThreshold = val; SaveAndApply(); });
                    AddSlider(panel, "Release Threshold (RMS)", 0.0005, 0.05, cfg.ActivityReleaseThreshold, val => { cfg.ActivityReleaseThreshold = val; SaveAndApply(); });
                    AddActionButton(panel, "Restart WASAPI Loopback Capture", () => _audioEngine.Start());
                    break;

                case 3:
                    AddHeader(panel, "Music Bar Position, Pinning & Overlay Mode", "Position the floating bar at the bottom center above the Windows taskbar.");
                    AddActionButton(panel, "Reset Music Bar Position to Bottom Center (Above Taskbar)", () => _resetPositionCallback());
                    AddCheck(panel, "Show Floating Emerald Music Bar", cfg.NowPlayingVisible, val => { cfg.NowPlayingVisible = val; SaveAndApply(); });
                    AddCheck(panel, "Pin Music Bar (Keep Visible & Disable 5s Auto-Hide)", cfg.NowPlayingPinned, val => { cfg.NowPlayingPinned = val; SaveAndApply(); });
                    AddCheck(panel, "Always-On-Top Overlay Mode", cfg.AlwaysOnTop, val => { cfg.AlwaysOnTop = val; SaveAndApply(); });
                    AddCheck(panel, "Compact Bar Mode (For Smaller Screens)", cfg.CompactBarMode, val => { cfg.CompactBarMode = val; SaveAndApply(); });
                    AddCheck(panel, "Lock Click-Through Mode (Unlock anytime via Alt+C or Tray)", cfg.ClickThroughEnabled, val => { cfg.ClickThroughEnabled = val; SaveAndApply(); });
                    break;

                case 4:
                    AddHeader(panel, "Music Bar Elements & Controls", "Customize visible sections inside the emerald floating capsule.");
                    AddCheck(panel, "Show Rounded Album Art & Vinyl Ring", cfg.ShowAlbumArt, val => { cfg.ShowAlbumArt = val; SaveAndApply(); });
                    AddCheck(panel, "Enable Rotating Disc Animation During Playback", cfg.EnableRotatingDisc, val => { cfg.EnableRotatingDisc = val; SaveAndApply(); });
                    AddCheck(panel, "Show Track Title", cfg.ShowTrackTitle, val => { cfg.ShowTrackTitle = val; SaveAndApply(); });
                    AddCheck(panel, "Show Artist Name & Source Indicator", cfg.ShowArtistName, val => { cfg.ShowArtistName = val; SaveAndApply(); });
                    AddCheck(panel, "Show Playback Progress Line & Elapsed/Total Time", cfg.ShowProgressBar, val => { cfg.ShowProgressBar = val; SaveAndApply(); });
                    AddCheck(panel, "Show System Output Volume Pill & Mute Control", cfg.ShowVolumeControl, val => { cfg.ShowVolumeControl = val; SaveAndApply(); });
                    AddCheck(panel, "Show Previous / Next Track Buttons", cfg.ShowPrevNextControls, val => { cfg.ShowPrevNextControls = val; SaveAndApply(); });
                    break;

                case 5:
                    AddHeader(panel, "Five-Second Auto-Hide & Windows Startup", "Configure unpinned inactivity hiding and automatic startup.");
                    AddCheck(panel, "Enable Auto-Hide When Unpinned", cfg.EnableNowPlayingAutoHide, val => { cfg.EnableNowPlayingAutoHide = val; SaveAndApply(); });
                    AddSlider(panel, "Auto-Hide Timeout (Seconds — Default: 5s)", 2, 30, cfg.AutoHideTimeoutSeconds, val => { cfg.AutoHideTimeoutSeconds = (int)Math.Round(val); SaveAndApply(); });
                    AddCheck(panel, "Automatically Show Music Bar When Music Starts", cfg.AutoShowNowPlayingOnAudio, val => { cfg.AutoShowNowPlayingOnAudio = val; SaveAndApply(); });
                    AddCheck(panel, "Start AuraWave at Windows Login (HKCU Run)", cfg.StartWithWindows, val =>
                    {
                        cfg.StartWithWindows = val;
                        StartupRegistrationService.SetStartupEnabled(val);
                        SaveAndApply();
                    });
                    break;

                case 6:
                    AddHeader(panel, "Global Keyboard Shortcuts", "Registered system-wide Windows hotkeys.");
                    AddInfoLine(panel, "Alt + P / Alt + A", "Show or Hide the Emerald Music Bar");
                    AddInfoLine(panel, "Alt + S", "Open or Focus This Settings Window");
                    AddInfoLine(panel, "Alt + C", "Toggle Click-Through Interaction Lock");
                    AddInfoLine(panel, "Ctrl + Alt + E", "Enter or Exit Interactive Resize Mode");
                    AddInfoLine(panel, "Ctrl + Alt + Plus / Minus", "Increase or Decrease Music Bar Size");
                    AddInfoLine(panel, "Alt + W", "Toggle Optional Standalone Waveform");
                    AddInfoLine(panel, "Alt + Q", "Cleanly Quit AuraWave");
                    break;

                case 7:
                    AddHeader(panel, "General, Persistence & Diagnostics", "Configuration file paths, log locations, and factory reset.");
                    AddInfoLine(panel, "Application Version", "AuraWave 1.1.0 — Emerald Floating Music Bar (win-x64)");
                    AddInfoLine(panel, "Configuration File", _configService.ConfigFilePath);
                    AddInfoLine(panel, "Diagnostic Log File", AppLogger.LogFilePath);
                    AddActionButton(panel, "Restore Default Emerald Music Bar Settings", () =>
                    {
                        _configService.ResetToDefaults();
                        _resetPositionCallback();
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
                FontSize = 21,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(242, 247, 245)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            parent.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(145, 164, 158)),
                Margin = new Thickness(0, 0, 0, 20)
            });
        }

        private static void AddInfoLine(StackPanel parent, string label, string value)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(145, 164, 158)), FontWeight = FontWeights.SemiBold };
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
                Background = new SolidColorBrush(Color.FromRgb(53, 214, 160)),
                Foreground = new SolidColorBrush(Color.FromRgb(7, 18, 15)),
                FontWeight = FontWeights.Bold
            };
            btn.Click += (_, _) => onClick();
            parent.Children.Add(btn);
        }
    }
}