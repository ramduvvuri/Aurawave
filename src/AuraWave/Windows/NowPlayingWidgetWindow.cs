using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AuraWave.Audio;
using AuraWave.Configuration;
using AuraWave.Media;
using AuraWave.Models;
using AuraWave.Themes;

namespace AuraWave.Windows
{
    public sealed class NowPlayingWidgetWindow : Window
    {
        private readonly ConfigurationService _configService;
        private readonly WindowsMediaSessionService _mediaService;
        private readonly WasapiLoopbackEngine _audioEngine;

        private readonly Border _cardBorder;
        private readonly RotateTransform _discRotateTransform;
        private readonly Grid _discVisualGrid;
        private readonly TextBlock _titleText;
        private readonly TextBlock _artistText;
        private readonly TextBlock _timelineText;
        private readonly ProgressBar _progressBar;
        private readonly Button _playPauseBtn;
        private readonly Button _prevBtn;
        private readonly Button _nextBtn;
        private readonly Button _pinBtn;
        private readonly Slider _volumeSlider;
        private readonly StackPanel _contextualToolbar;
        private readonly DispatcherTimer _autoHideTimer;

        private bool _isEditMode;
        private bool _isInteractingWithSlider;
        private double _discAngle;

        public event EventHandler? OpenSettingsRequested;
        public event EventHandler? EditModeSelected;

        public NowPlayingWidgetWindow(
            ConfigurationService configService,
            WindowsMediaSessionService mediaService,
            WasapiLoopbackEngine audioEngine)
        {
            _configService = configService;
            _mediaService = mediaService;
            _audioEngine = audioEngine;

            Title = "AuraWave Now Playing";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;

            _discRotateTransform = new RotateTransform(0, 32, 32);
            _discVisualGrid = CreateVinylDiscElement();

            _titleText = new TextBlock
            {
                Text = "Waiting for Media...",
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            _artistText = new TextBlock
            {
                Text = "Play music in any browser or player",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 11.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 5)
            };

            _progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 1,
                Value = 0,
                Height = 4,
                Background = new SolidColorBrush(Color.FromArgb(80, 51, 65, 85)),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 242, 254)),
                BorderThickness = new Thickness(0)
            };

            _timelineText = new TextBlock
            {
                Text = "00:00 / 00:00",
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                FontSize = 10,
                Margin = new Thickness(0, 3, 0, 0)
            };

            _prevBtn = CreateTransportButton("|<", "Previous Track");
            _prevBtn.Click += (_, _) => _mediaService.SendPreviousTrack();

            _playPauseBtn = CreateTransportButton("Play/Pause", "Play or Pause Active Media Session");
            _playPauseBtn.Click += (_, _) => _mediaService.SendPlayPause();

            _nextBtn = CreateTransportButton(">|", "Next Track");
            _nextBtn.Click += (_, _) => _mediaService.SendNextTrack();

            _volumeSlider = new Slider
            {
                Minimum = 0,
                Maximum = 1,
                Value = _audioEngine.GetMasterVolumeScalar(),
                Width = 68,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                ToolTip = "System Output Volume (WASAPI)"
            };
            _volumeSlider.PreviewMouseLeftButtonDown += (_, _) => _isInteractingWithSlider = true;
            _volumeSlider.PreviewMouseLeftButtonUp += (_, _) =>
            {
                _isInteractingWithSlider = false;
                RestartAutoHideTimer();
            };
            _volumeSlider.ValueChanged += (_, e) =>
            {
                if (_isInteractingWithSlider)
                {
                    _audioEngine.SetMasterVolumeScalar((float)e.NewValue);
                }
            };

            _pinBtn = CreateSmallToolbarButton(_configService.Current.NowPlayingPinned ? "Pinned" : "Pin");
            _pinBtn.Click += (_, _) =>
            {
                _configService.Current.NowPlayingPinned = !_configService.Current.NowPlayingPinned;
                _pinBtn.Content = _configService.Current.NowPlayingPinned ? "Pinned" : "Pin";
                _configService.Save();
            };

            var settingsBtn = CreateSmallToolbarButton("Settings");
            settingsBtn.Click += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);

            _contextualToolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 2, 2)
            };
            _contextualToolbar.Children.Add(_pinBtn);
            _contextualToolbar.Children.Add(settingsBtn);

            var centerStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 10, 0)
            };
            centerStack.Children.Add(_titleText);
            centerStack.Children.Add(_artistText);
            centerStack.Children.Add(_progressBar);
            centerStack.Children.Add(_timelineText);

            var controlsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            controlsRow.Children.Add(_prevBtn);
            controlsRow.Children.Add(_playPauseBtn);
            controlsRow.Children.Add(_nextBtn);
            controlsRow.Children.Add(_volumeSlider);

            var rightGrid = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            rightGrid.Children.Add(_contextualToolbar);
            rightGrid.Children.Add(controlsRow);

            var layoutGrid = new Grid { Margin = new Thickness(12, 10, 12, 10) };
            layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(_discVisualGrid, 0);
            Grid.SetColumn(centerStack, 1);
            Grid.SetColumn(rightGrid, 2);

            layoutGrid.Children.Add(_discVisualGrid);
            layoutGrid.Children.Add(centerStack);
            layoutGrid.Children.Add(rightGrid);

            _cardBorder = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(224, 11, 17, 32)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(95, 0, 242, 254)),
                BorderThickness = new Thickness(1.2),
                Child = layoutGrid
            };

            Content = _cardBorder;

            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(_configService.Current.AutoHideTimeoutSeconds)
            };
            _autoHideTimer.Tick += OnAutoHideTimerTick;

            ApplyGeometryFromConfig();
            ApplyAppearanceFromConfig();

            MouseEnter += (_, _) =>
            {
                _autoHideTimer.Stop();
                _contextualToolbar.Visibility = Visibility.Visible;
                Opacity = 1.0;
            };
            MouseLeave += (_, _) => RestartAutoHideTimer();
            MouseLeftButtonDown += OnCardMouseLeftButtonDown;
            LocationChanged += (_, _) => SaveGeometryIfReady();
            SizeChanged += (_, _) => SaveGeometryIfReady();
        }

        public void UpdateMediaSnapshot(MediaSessionSnapshot snap)
        {
            var cfg = _configService.Current;
            _titleText.Text = snap.Title;
            _artistText.Text = snap.Artist;
            _progressBar.Value = snap.ProgressRatio;
            _timelineText.Text = snap.FormattedTimeline;
            _playPauseBtn.Content = snap.PlaybackStatus == PlaybackState.Playing ? "Pause" : "Play";

            _discVisualGrid.Visibility = cfg.ShowAlbumArt ? Visibility.Visible : Visibility.Collapsed;
            _titleText.Visibility = cfg.ShowTrackTitle ? Visibility.Visible : Visibility.Collapsed;
            _artistText.Visibility = cfg.ShowArtistName ? Visibility.Visible : Visibility.Collapsed;
            _progressBar.Visibility = cfg.ShowProgressBar ? Visibility.Visible : Visibility.Collapsed;
            _volumeSlider.Visibility = cfg.ShowVolumeControl ? Visibility.Visible : Visibility.Collapsed;
            _prevBtn.Visibility = cfg.ShowPrevNextControls ? Visibility.Visible : Visibility.Collapsed;
            _nextBtn.Visibility = cfg.ShowPrevNextControls ? Visibility.Visible : Visibility.Collapsed;
        }

        public void StepDiscAnimation(bool isPlaying)
        {
            if (isPlaying && _configService.Current.EnableRotatingDisc)
            {
                _discAngle = (_discAngle + 1.8) % 360.0;
                _discRotateTransform.Angle = _discAngle;
            }
        }

        public void SetEditMode(bool enabled)
        {
            _isEditMode = enabled;
            ResizeMode = enabled ? ResizeMode.CanResizeWithGrip : ResizeMode.NoResize;
            ApplyAppearanceFromConfig();
        }

        public void ApplyAppearanceFromConfig()
        {
            var cfg = _configService.Current;
            var pal = ThemeManager.GetActivePalette(cfg);
            Color cPrim = ThemeManager.ParseColor(pal.PrimaryHex, Color.FromRgb(0, 242, 254));
            byte alpha = (byte)Math.Clamp(cfg.NowPlayingSurfaceOpacity * 255, 60, 250);

            _cardBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, 11, 17, 32));
            _cardBorder.BorderBrush = new SolidColorBrush(_isEditMode ? Colors.Cyan : Color.FromArgb(110, cPrim.R, cPrim.G, cPrim.B));
            _cardBorder.BorderThickness = new Thickness(_isEditMode ? 2.0 : 1.2);
            _progressBar.Foreground = new SolidColorBrush(cPrim);
            _pinBtn.Content = cfg.NowPlayingPinned ? "Pinned" : "Pin";
            _autoHideTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(cfg.AutoHideTimeoutSeconds, 2, 60));
        }

        public void ApplyGeometryFromConfig()
        {
            var g = _configService.Current.NowPlayingGeometry;
            g.ClampToMonitor(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight,
                280, 960, 84, 320);

            Width = g.Width;
            Height = g.Height;
            Left = g.Left;
            Top = g.Top;
        }

        private void RestartAutoHideTimer()
        {
            _autoHideTimer.Stop();
            var cfg = _configService.Current;
            if (!cfg.NowPlayingPinned && cfg.EnableNowPlayingAutoHide && !_isInteractingWithSlider && !_isEditMode)
            {
                _autoHideTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(cfg.AutoHideTimeoutSeconds, 2, 60));
                _autoHideTimer.Start();
            }
        }

        private void OnAutoHideTimerTick(object? sender, EventArgs e)
        {
            _autoHideTimer.Stop();
            if (_configService.Current.NowPlayingPinned || _isInteractingWithSlider || _isEditMode)
                return;

            _contextualToolbar.Visibility = Visibility.Collapsed;
            Opacity = 0.78;
        }

        private void OnCardMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && !_isInteractingWithSlider)
            {
                EditModeSelected?.Invoke(this, EventArgs.Empty);
                try { DragMove(); } catch { }
            }
        }

        private void SaveGeometryIfReady()
        {
            if (!IsLoaded) return;
            var g = _configService.Current.NowPlayingGeometry;
            g.Left = Left;
            g.Top = Top;
            g.Width = Width;
            g.Height = Height;
        }

        private Grid CreateVinylDiscElement()
        {
            var grid = new Grid
            {
                Width = 64,
                Height = 64,
                RenderTransform = _discRotateTransform
            };

            var outerDisc = new Ellipse
            {
                Width = 64,
                Height = 64,
                Fill = new RadialGradientBrush(Color.FromRgb(30, 41, 59), Color.FromRgb(9, 13, 22)),
                Stroke = new SolidColorBrush(Color.FromRgb(0, 242, 254)),
                StrokeThickness = 1.5
            };

            var groove = new Ellipse
            {
                Width = 46,
                Height = 46,
                Stroke = new SolidColorBrush(Color.FromArgb(90, 148, 163, 184)),
                StrokeThickness = 1.0,
                StrokeDashArray = new DoubleCollection(new double[] { 4, 2 })
            };

            var centerLabel = new Ellipse
            {
                Width = 22,
                Height = 22,
                Fill = new LinearGradientBrush(Color.FromRgb(0, 242, 254), Color.FromRgb(155, 81, 224), 45.0)
            };

            var spindleHole = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(Color.FromRgb(11, 17, 32))
            };

            grid.Children.Add(outerDisc);
            grid.Children.Add(groove);
            grid.Children.Add(centerLabel);
            grid.Children.Add(spindleHole);
            return grid;
        }

        private static Button CreateTransportButton(string label, string tooltip)
        {
            return new Button
            {
                Content = label,
                ToolTip = tooltip,
                Margin = new Thickness(3, 0, 3, 0),
                Padding = new Thickness(8, 4, 8, 4),
                Background = new SolidColorBrush(Color.FromArgb(180, 30, 41, 59)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 0, 242, 254)),
                BorderThickness = new Thickness(1),
                FontSize = 11,
                Cursor = Cursors.Hand
            };
        }

        private static Button CreateSmallToolbarButton(string text)
        {
            return new Button
            {
                Content = text,
                Margin = new Thickness(4, 0, 0, 4),
                Padding = new Thickness(6, 1, 6, 1),
                Background = new SolidColorBrush(Color.FromArgb(140, 15, 23, 42)),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 242, 254)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 0, 242, 254)),
                BorderThickness = new Thickness(1),
                FontSize = 10,
                Cursor = Cursors.Hand
            };
        }
    }
}