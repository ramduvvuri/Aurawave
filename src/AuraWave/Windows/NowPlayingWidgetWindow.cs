using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using AuraWave.Audio;
using AuraWave.Configuration;
using AuraWave.Media;
using AuraWave.Models;
using AuraWave.Services;
using AuraWave.Themes;

namespace AuraWave.Windows
{
    public sealed class EmbeddedMiniSpectrumControl : FrameworkElement
    {
        private float[] _bands = Array.Empty<float>();
        private Color _primaryColor = Color.FromRgb(53, 214, 160);
        private Color _secondaryColor = Color.FromRgb(66, 229, 176);
        private Color _tailColor = Color.FromRgb(145, 164, 158);
        private double _intensity = 1.0;

        public void UpdateSpectrum(AudioAnalysisFrame frame, AuraWaveConfig cfg)
        {
            _bands = frame.FrequencyBands ?? Array.Empty<float>();
            var pal = ThemeManager.GetActivePalette(cfg);
            _primaryColor = ThemeManager.ParseColor(pal.PrimaryHex, Color.FromRgb(53, 214, 160));
            _secondaryColor = ThemeManager.ParseColor(pal.SecondaryHex, Color.FromRgb(66, 229, 176));
            _tailColor = ThemeManager.ParseColor(pal.SecondaryTextHex, Color.FromRgb(145, 164, 158));
            _intensity = cfg.VisualizerIntensity;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 10 || h <= 6) return;

            int barCount = 26;
            double step = w / barCount;
            double barW = Math.Clamp(step * 0.48, 2.2, 4.0);
            double baselineY = h - 2.0;
            double maxBarH = h - 4.0;

            var activeBrush = new LinearGradientBrush(_secondaryColor, _primaryColor, new Point(0, 0), new Point(0, 1));
            activeBrush.Freeze();
            var midBrush = new SolidColorBrush(Color.FromArgb(210, _primaryColor.R, _primaryColor.G, _primaryColor.B));
            midBrush.Freeze();
            var tailBrush = new SolidColorBrush(Color.FromArgb(145, _tailColor.R, _tailColor.G, _tailColor.B));
            tailBrush.Freeze();

            for (int i = 0; i < barCount; i++)
            {
                float raw = 0f;
                if (_bands.Length > 0)
                {
                    int idx = (i * _bands.Length) / barCount;
                    raw = _bands[Math.Clamp(idx, 0, _bands.Length - 1)];
                }

                // Gentle profile curve matching the reference emerald equalizer silhouette
                double envelope = i < 8
                    ? 0.35 + 0.65 * Math.Sin((i / 8.0) * Math.PI)
                    : 0.25 + 0.45 * Math.Exp(-(i - 8) * 0.09);

                double normalized = Math.Clamp(raw * _intensity * 1.15, 0.0, 1.0);
                double minIdle = 2.5 + envelope * 3.5;
                double barH = Math.Clamp(minIdle + normalized * (maxBarH - minIdle), 2.5, maxBarH);

                double x = i * step + (step - barW) * 0.5;
                double y = baselineY - barH;

                Brush barBrush = i < 12 ? activeBrush : (i < 19 ? midBrush : tailBrush);
                dc.DrawRoundedRectangle(barBrush, null, new Rect(x, y, barW, barH), barW * 0.5, barW * 0.5);
            }
        }
    }

    public sealed class NowPlayingWidgetWindow : Window
    {
        private readonly ConfigurationService _configService;
        private readonly WindowsMediaSessionService _mediaService;
        private readonly WasapiLoopbackEngine _audioEngine;
        private readonly FavouritesService _favouritesService;

        private readonly Border _outerGlowBorder;
        private readonly Border _capsuleBorder;
        private readonly Grid _artworkHostGrid;
        private readonly RotateTransform _discRotateTransform;
        private readonly TextBlock _titleText;
        private readonly TextBlock _artistText;
        private readonly Ellipse _sourceBadgeDot;
        private readonly EmbeddedMiniSpectrumControl _miniSpectrum;
        private readonly Border _progressTrackBorder;
        private readonly Border _progressFillBorder;
        private readonly TextBlock _timelineText;
        private readonly StackPanel _spectrumAndProgressPanel;

        private readonly Button _favBtn;
        private readonly Button _prevBtn;
        private readonly Button _playPauseBtn;
        private readonly TextBlock _playPauseIconText;
        private readonly Button _nextBtn;
        private readonly Border _volumePillBorder;
        private readonly Button _muteBtn;
        private readonly Slider _volumeSlider;
        private readonly Button _queueListBtn;
        private readonly Button _settingsBtn;
        private readonly Button _pinBtn;
        private readonly Button _closeHideBtn;

        private readonly Popup _queuePopup;
        private readonly StackPanel _queueItemsStack;
        private readonly DispatcherTimer _autoHideTimer;

        private bool _isEditMode;
        private bool _isInteractingWithSlider;
        private bool _isDraggingWindow;
        private bool _isMuted;
        private double _discAngle;

        public event EventHandler? OpenSettingsRequested;
        public event EventHandler? EditModeSelected;
        public event EventHandler? HideBarRequested;

        public NowPlayingWidgetWindow(
            ConfigurationService configService,
            WindowsMediaSessionService mediaService,
            WasapiLoopbackEngine audioEngine,
            FavouritesService favouritesService)
        {
            _configService = configService;
            _mediaService = mediaService;
            _audioEngine = audioEngine;
            _favouritesService = favouritesService;

            Title = "AuraWave Music Bar";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;

            // SECTION A: Album Art (58x58 rounded square with subtle rotating vinyl disc accent)
            _discRotateTransform = new RotateTransform(0, 29, 29);
            _artworkHostGrid = CreateAlbumArtworkSection();

            // SECTION B: Track Title + Green Source Badge + Artist
            _titleText = new TextBlock
            {
                Text = "Uncover the Silent Beauty",
                Foreground = new SolidColorBrush(Color.FromRgb(242, 247, 245)),
                FontSize = 14.5,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 215
            };

            _sourceBadgeDot = new Ellipse
            {
                Width = 9,
                Height = 9,
                Fill = new SolidColorBrush(Color.FromRgb(53, 214, 160)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };

            _artistText = new TextBlock
            {
                Text = "Waiting for music...",
                Foreground = new SolidColorBrush(Color.FromRgb(145, 164, 158)),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 195,
                VerticalAlignment = VerticalAlignment.Center
            };

            var artistRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 3, 0, 0)
            };
            artistRow.Children.Add(_sourceBadgeDot);
            artistRow.Children.Add(_artistText);

            var trackInfoStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Width = 215,
                Margin = new Thickness(14, 0, 12, 0)
            };
            trackInfoStack.Children.Add(_titleText);
            trackInfoStack.Children.Add(artistRow);

            // SECTION C & D: Embedded Mini Audio Spectrum + Progress Line + Timeline
            _miniSpectrum = new EmbeddedMiniSpectrumControl
            {
                Width = 175,
                Height = 26,
                Margin = new Thickness(0, 0, 0, 4)
            };

            _progressFillBorder = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 48,
                Height = 3.5,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromRgb(53, 214, 160))
            };

            _progressTrackBorder = new Border
            {
                Width = 175,
                Height = 3.5,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromArgb(120, 26, 46, 40)),
                Child = _progressFillBorder
            };

            _timelineText = new TextBlock
            {
                Text = "0:00 / 0:00",
                Foreground = new SolidColorBrush(Color.FromRgb(145, 164, 158)),
                FontSize = 10.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };

            _spectrumAndProgressPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 180,
                Margin = new Thickness(4, 2, 14, 0)
            };
            _spectrumAndProgressPanel.Children.Add(_miniSpectrum);
            _spectrumAndProgressPanel.Children.Add(_progressTrackBorder);
            _spectrumAndProgressPanel.Children.Add(_timelineText);

            // SECTION E: 9 Action Controls in Exact Reference Order
            // 1. Favourite (Heart)
            _favBtn = CreateCircularIconButton("\u2661", "Save / Remove Track in AuraWave Favourites", 32);
            _favBtn.Click += OnFavouriteButtonClick;

            // 2. Previous Track
            _prevBtn = CreateCircularIconButton("\u23EE", "Previous Track", 32);
            _prevBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                _mediaService.SendPreviousTrack();
            };

            // 3. Focal Emerald Play/Pause Button
            _playPauseIconText = new TextBlock
            {
                Text = "\u23F8",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(7, 18, 15)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _playPauseBtn = new Button
            {
                Width = 42,
                Height = 42,
                Margin = new Thickness(5, 0, 5, 0),
                ToolTip = "Play / Pause Active Media Session",
                Cursor = Cursors.Hand,
                Template = CreateEmeraldPlayButtonTemplate(),
                Content = _playPauseIconText
            };
            _playPauseBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                _mediaService.SendPlayPause();
            };

            // 4. Next Track
            _nextBtn = CreateCircularIconButton("\u23ED", "Next Track", 32);
            _nextBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                _mediaService.SendNextTrack();
            };

            // 5. Volume Pill (Mute Toggle + Live WASAPI Endpoint Volume Slider)
            _muteBtn = new Button
            {
                Content = "\uD83D\uDD0A",
                ToolTip = "Mute / Unmute Windows Output Volume",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(210, 224, 219)),
                BorderThickness = new Thickness(0),
                FontSize = 12,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 2, 4, 2)
            };
            _muteBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                _isMuted = _audioEngine.ToggleMute();
                _muteBtn.Content = _isMuted ? "\uD83D\uDD07" : "\uD83D\uDD0A";
            };

            _volumeSlider = new Slider
            {
                Minimum = 0.0,
                Maximum = 1.0,
                Value = _audioEngine.GetMasterVolumeScalar(),
                Width = 64,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0),
                ToolTip = "System Output Volume (WASAPI)"
            };
            _volumeSlider.PreviewMouseLeftButtonDown += (_, _) =>
            {
                _isInteractingWithSlider = true;
                _autoHideTimer.Stop();
            };
            _volumeSlider.PreviewMouseLeftButtonUp += (_, _) =>
            {
                _isInteractingWithSlider = false;
                RestartAutoHideTimerIfUnpinned();
            };
            _volumeSlider.ValueChanged += (_, e) =>
            {
                if (_isInteractingWithSlider)
                {
                    _audioEngine.SetMasterVolumeScalar((float)e.NewValue);
                }
            };

            var volStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            volStack.Children.Add(_muteBtn);
            volStack.Children.Add(_volumeSlider);

            _volumePillBorder = new Border
            {
                CornerRadius = new CornerRadius(15),
                Background = new SolidColorBrush(Color.FromArgb(180, 14, 31, 26)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 53, 214, 160)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 8, 4),
                Margin = new Thickness(4, 0, 4, 0),
                Child = volStack
            };

            // 6. Queue / Saved Favourites Panel Button
            _queueItemsStack = new StackPanel();
            _queuePopup = CreateFavouritesQueuePopup();
            _queueListBtn = CreateCircularIconButton("\u2630", "Open Saved Favourites & Active Session Panel", 32);
            _queueListBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                RefreshQueuePopupContent();
                _queuePopup.IsOpen = !_queuePopup.IsOpen;
            };

            // 7. Settings Button
            _settingsBtn = CreateCircularIconButton("\u2699", "Open AuraWave Settings Studio (Alt+S)", 32);
            _settingsBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
            };

            // 8. Pin Button (Fixed State Machine)
            _pinBtn = CreateCircularIconButton("\uD83D\uDCCC", "Toggle Pin (Keep music bar permanently visible)", 32);
            _pinBtn.Click += OnPinButtonClick;

            // 9. Close / Hide Button (Hides to System Tray without terminating process)
            _closeHideBtn = CreateCircularIconButton("\u2715", "Hide Music Bar to System Tray (Restore via Alt+P or Tray Icon)", 32);
            _closeHideBtn.Click += (_, _) =>
            {
                _autoHideTimer.Stop();
                _queuePopup.IsOpen = false;
                HideBarRequested?.Invoke(this, EventArgs.Empty);
            };

            var actionsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            actionsRow.Children.Add(_favBtn);
            actionsRow.Children.Add(_prevBtn);
            actionsRow.Children.Add(_playPauseBtn);
            actionsRow.Children.Add(_nextBtn);
            actionsRow.Children.Add(_volumePillBorder);
            actionsRow.Children.Add(_queueListBtn);
            actionsRow.Children.Add(_settingsBtn);
            actionsRow.Children.Add(_pinBtn);
            actionsRow.Children.Add(_closeHideBtn);

            var barGrid = new Grid
            {
                Margin = new Thickness(14, 8, 14, 8),
                VerticalAlignment = VerticalAlignment.Center
            };
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(_artworkHostGrid, 0);
            Grid.SetColumn(trackInfoStack, 1);
            Grid.SetColumn(_spectrumAndProgressPanel, 2);
            Grid.SetColumn(actionsRow, 3);

            barGrid.Children.Add(_artworkHostGrid);
            barGrid.Children.Add(trackInfoStack);
            barGrid.Children.Add(_spectrumAndProgressPanel);
            barGrid.Children.Add(actionsRow);

            _capsuleBorder = new Border
            {
                CornerRadius = new CornerRadius(42),
                Background = new SolidColorBrush(Color.FromArgb(238, 7, 18, 15)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(140, 53, 214, 160)),
                BorderThickness = new Thickness(1.2),
                Child = barGrid
            };

            _outerGlowBorder = new Border
            {
                Margin = new Thickness(6),
                CornerRadius = new CornerRadius(44),
                BorderBrush = new SolidColorBrush(Color.FromArgb(45, 66, 229, 176)),
                BorderThickness = new Thickness(1.5),
                Child = _capsuleBorder,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(53, 214, 160),
                    BlurRadius = 14,
                    ShadowDepth = 0,
                    Opacity = 0.28
                }
            };

            Content = _outerGlowBorder;

            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(Math.Clamp(_configService.Current.AutoHideTimeoutSeconds, 2, 60))
            };
            _autoHideTimer.Tick += OnAutoHideTimerTick;

            ApplyGeometryFromConfig();
            ApplyAppearanceFromConfig();
            UpdatePinButtonVisual();

            SourceInitialized += (_, _) => SyncNativeStyles();
            MouseEnter += (_, _) =>
            {
                _autoHideTimer.Stop();
                Opacity = 1.0;
            };
            MouseLeave += (_, _) => RestartAutoHideTimerIfUnpinned();
            MouseLeftButtonDown += OnCapsuleMouseLeftButtonDown;
            LocationChanged += (_, _) => SaveGeometryIfReady();
            SizeChanged += (_, _) => SaveGeometryIfReady();
        }

        public void SetPinned(bool pinned)
        {
            _configService.Current.NowPlayingPinned = pinned;
            if (pinned)
            {
                _autoHideTimer.Stop();
                Opacity = 1.0;
                if (!_configService.Current.NowPlayingVisible)
                {
                    _configService.Current.NowPlayingVisible = true;
                    Show();
                }
            }
            else
            {
                RestartAutoHideTimerIfUnpinned();
            }

            UpdatePinButtonVisual();
            _configService.Save();
        }

        public void ResetToBottomCenter()
        {
            var geom = WidgetGeometry.CalculateBottomCenterBar(
                SystemParameters.WorkArea.Left,
                SystemParameters.WorkArea.Top,
                SystemParameters.WorkArea.Width,
                SystemParameters.WorkArea.Height,
                _configService.Current.CompactBarMode ? 760 : 920,
                92,
                14);

            _configService.Current.NowPlayingGeometry = geom;
            Width = geom.Width;
            Height = geom.Height;
            Left = geom.Left;
            Top = geom.Top;
            _configService.Save();
        }

        public void UpdateAudioFrame(AudioAnalysisFrame frame)
        {
            if (_configService.Current.ShowEmbeddedSpectrum)
            {
                _miniSpectrum.UpdateSpectrum(frame, _configService.Current);
            }
            bool isPlaying = _mediaService.CurrentSession.PlaybackStatus == PlaybackState.Playing || frame.IsAudioActive;
            StepDiscAnimation(isPlaying);
        }

        public void UpdateMediaSnapshot(MediaSessionSnapshot snap)
        {
            var cfg = _configService.Current;
            _titleText.Text = string.IsNullOrWhiteSpace(snap.Title) ? "Uncover the Silent Beauty" : snap.Title;
            _artistText.Text = string.IsNullOrWhiteSpace(snap.Artist) ? "AuraWave Live Audio" : snap.Artist;
            _timelineText.Text = snap.FormattedTimeline;

            double trackW = _progressTrackBorder.Width > 0 ? _progressTrackBorder.Width : 175.0;
            _progressFillBorder.Width = Math.Clamp(trackW * snap.ProgressRatio, 4.0, trackW);

            bool playing = snap.PlaybackStatus == PlaybackState.Playing;
            _playPauseIconText.Text = playing ? "\u23F8" : "\u25B6";

            bool isFav = _favouritesService.IsFavourite(snap);
            UpdateFavouriteButtonVisual(isFav);

            _artworkHostGrid.Visibility = cfg.ShowAlbumArt ? Visibility.Visible : Visibility.Collapsed;
            _titleText.Visibility = cfg.ShowTrackTitle ? Visibility.Visible : Visibility.Collapsed;
            _artistText.Visibility = cfg.ShowArtistName ? Visibility.Visible : Visibility.Collapsed;
            _miniSpectrum.Visibility = cfg.ShowEmbeddedSpectrum ? Visibility.Visible : Visibility.Collapsed;
            _progressTrackBorder.Visibility = cfg.ShowProgressBar ? Visibility.Visible : Visibility.Collapsed;
            _timelineText.Visibility = cfg.ShowProgressBar ? Visibility.Visible : Visibility.Collapsed;
            _volumePillBorder.Visibility = cfg.ShowVolumeControl && !cfg.CompactBarMode ? Visibility.Visible : Visibility.Collapsed;
            _prevBtn.Visibility = cfg.ShowPrevNextControls ? Visibility.Visible : Visibility.Collapsed;
            _nextBtn.Visibility = cfg.ShowPrevNextControls ? Visibility.Visible : Visibility.Collapsed;
        }

        public void StepDiscAnimation(bool isPlaying)
        {
            if (isPlaying && _configService.Current.EnableRotatingDisc)
            {
                _discAngle = (_discAngle + 1.4) % 360.0;
                _discRotateTransform.Angle = _discAngle;
            }
        }

        public void SetEditMode(bool enabled)
        {
            _isEditMode = enabled;
            ResizeMode = enabled ? ResizeMode.CanResizeWithGrip : ResizeMode.NoResize;
            ApplyAppearanceFromConfig();
            SyncNativeStyles();
        }

        public void ApplyAppearanceFromConfig()
        {
            var cfg = _configService.Current;
            var pal = ThemeManager.GetActivePalette(cfg);
            Color cPrim = ThemeManager.ParseColor(pal.PrimaryHex, Color.FromRgb(53, 214, 160));
            Color cSurf = ThemeManager.ParseColor(pal.SurfaceHex, Color.FromRgb(7, 18, 15));
            byte alpha = (byte)Math.Clamp(cfg.NowPlayingSurfaceOpacity * 255, 110, 250);

            _capsuleBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, cSurf.R, cSurf.G, cSurf.B));
            _capsuleBorder.BorderBrush = new SolidColorBrush(_isEditMode
                ? Color.FromRgb(66, 229, 176)
                : Color.FromArgb(145, cPrim.R, cPrim.G, cPrim.B));
            _capsuleBorder.BorderThickness = new Thickness(_isEditMode ? 2.0 : 1.2);

            _progressFillBorder.Background = new SolidColorBrush(cPrim);
            _sourceBadgeDot.Fill = new SolidColorBrush(cPrim);

            _autoHideTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(cfg.AutoHideTimeoutSeconds, 2, 60));
            UpdatePinButtonVisual();
        }

        public void ApplyGeometryFromConfig()
        {
            var g = _configService.Current.NowPlayingGeometry;
            if (g.Left < 0 || g.Top < 0)
            {
                ResetToBottomCenter();
                return;
            }

            g.ClampToMonitor(
                SystemParameters.WorkArea.Left,
                SystemParameters.WorkArea.Top,
                SystemParameters.WorkArea.Width,
                SystemParameters.WorkArea.Height,
                540, 1600, 76, 160);

            Width = g.Width;
            Height = g.Height;
            Left = g.Left;
            Top = g.Top;
        }

        public void SyncNativeStyles()
        {
            var cfg = _configService.Current;
            // Keep buttons genuinely clickable unless user explicitly locked click-through AND is not in edit mode
            bool effectiveClickThrough = cfg.ClickThroughEnabled && !_isEditMode;
            Topmost = cfg.PresentationMode == PresentationMode.AlwaysOnTopOverlay && cfg.AlwaysOnTop;
            NativeWindowInterop.ApplyOverlayWindowStyles(this, effectiveClickThrough, cfg.AlwaysOnTop, cfg.PresentationMode);
        }

        private void OnPinButtonClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            SetPinned(!_configService.Current.NowPlayingPinned);
        }

        private void OnFavouriteButtonClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            RegisterUserInteraction();
            bool isNowFav = _favouritesService.ToggleFavourite(_mediaService.CurrentSession);
            UpdateFavouriteButtonVisual(isNowFav);
            if (_queuePopup.IsOpen) RefreshQueuePopupContent();
        }

        private void UpdatePinButtonVisual()
        {
            bool pinned = _configService.Current.NowPlayingPinned;
            _pinBtn.Foreground = pinned
                ? new SolidColorBrush(Color.FromRgb(53, 214, 160))
                : new SolidColorBrush(Color.FromRgb(145, 164, 158));
            _pinBtn.BorderBrush = pinned
                ? new SolidColorBrush(Color.FromRgb(53, 214, 160))
                : new SolidColorBrush(Color.FromArgb(65, 145, 164, 158));
            _pinBtn.Background = pinned
                ? new SolidColorBrush(Color.FromArgb(55, 53, 214, 160))
                : new SolidColorBrush(Color.FromArgb(140, 16, 34, 29));
            _pinBtn.ToolTip = pinned
                ? "Pinned — Music Bar stays visible (Click to Unpin)"
                : $"Unpinned — Auto-hides after {_configService.Current.AutoHideTimeoutSeconds}s of inactivity (Click to Pin)";
        }

        private void UpdateFavouriteButtonVisual(bool isFav)
        {
            _favBtn.Content = isFav ? "\u2665" : "\u2661";
            _favBtn.Foreground = isFav
                ? new SolidColorBrush(Color.FromRgb(53, 214, 160))
                : new SolidColorBrush(Color.FromRgb(210, 224, 219));
            _favBtn.BorderBrush = isFav
                ? new SolidColorBrush(Color.FromRgb(53, 214, 160))
                : new SolidColorBrush(Color.FromArgb(65, 145, 164, 158));
        }

        private void RegisterUserInteraction()
        {
            _autoHideTimer.Stop();
            Opacity = 1.0;
            RestartAutoHideTimerIfUnpinned();
        }

        public void RestartAutoHideTimerIfUnpinned()
        {
            _autoHideTimer.Stop();
            var cfg = _configService.Current;
            if (!cfg.NowPlayingPinned && cfg.EnableNowPlayingAutoHide && !_isInteractingWithSlider && !_isDraggingWindow && !_isEditMode && !_queuePopup.IsOpen)
            {
                _autoHideTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(cfg.AutoHideTimeoutSeconds, 2, 60));
                _autoHideTimer.Start();
            }
        }

        private void OnAutoHideTimerTick(object? sender, EventArgs e)
        {
            _autoHideTimer.Stop();
            var cfg = _configService.Current;

            // Strict guard: never auto-hide if pinned, hovered, dragging, adjusting volume, or popup open
            if (cfg.NowPlayingPinned || !cfg.EnableNowPlayingAutoHide || IsMouseOver || _isInteractingWithSlider || _isDraggingWindow || _isEditMode || _queuePopup.IsOpen)
            {
                return;
            }

            HideBarRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnCapsuleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || _isInteractingWithSlider)
                return;

            // Do not start window drag if the user clicked inside an interactive Button or Slider
            if (IsInteractiveChild(e.OriginalSource as DependencyObject))
                return;

            _autoHideTimer.Stop();
            _isDraggingWindow = true;
            EditModeSelected?.Invoke(this, EventArgs.Empty);
            try
            {
                DragMove();
            }
            catch { }
            finally
            {
                _isDraggingWindow = false;
                SaveGeometryIfReady();
                _configService.Save();
                RestartAutoHideTimerIfUnpinned();
            }
        }

        private static bool IsInteractiveChild(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is ButtonBase || source is Slider || source is Thumb)
                    return true;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
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

        private Grid CreateAlbumArtworkSection()
        {
            var container = new Grid
            {
                Width = 58,
                Height = 58,
                VerticalAlignment = VerticalAlignment.Center
            };

            var rotatingRing = new Ellipse
            {
                Width = 58,
                Height = 58,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 53, 214, 160)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection(new double[] { 6, 3 }),
                RenderTransform = _discRotateTransform
            };

            var artBorder = new Border
            {
                Width = 52,
                Height = 52,
                CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(Color.FromArgb(140, 53, 214, 160)),
                BorderThickness = new Thickness(1),
                Background = new LinearGradientBrush(
                    Color.FromRgb(18, 52, 43),
                    Color.FromRgb(8, 22, 18),
                    new Point(0, 0),
                    new Point(1, 1))
            };

            var noteIcon = new TextBlock
            {
                Text = "\u266B",
                FontSize = 22,
                Foreground = new SolidColorBrush(Color.FromRgb(53, 214, 160)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            artBorder.Child = noteIcon;
            container.Children.Add(rotatingRing);
            container.Children.Add(artBorder);
            return container;
        }

        private Popup CreateFavouritesQueuePopup()
        {
            var border = new Border
            {
                Width = 320,
                MaxHeight = 280,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromArgb(245, 7, 18, 15)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(53, 214, 160)),
                BorderThickness = new Thickness(1.2),
                Padding = new Thickness(14),
                Child = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = _queueItemsStack
                }
            };

            var popup = new Popup
            {
                PlacementTarget = this,
                Placement = PlacementMode.Top,
                VerticalOffset = -8,
                AllowsTransparency = true,
                StaysOpen = false,
                Child = border
            };
            popup.Closed += (_, _) => RestartAutoHideTimerIfUnpinned();
            return popup;
        }

        private void RefreshQueuePopupContent()
        {
            _queueItemsStack.Children.Clear();
            var snap = _mediaService.CurrentSession;

            _queueItemsStack.Children.Add(new TextBlock
            {
                Text = "ACTIVE MEDIA SESSION",
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(53, 214, 160)),
                Margin = new Thickness(0, 0, 0, 4)
            });

            _queueItemsStack.Children.Add(new TextBlock
            {
                Text = $"{snap.Title} — {snap.Artist} ({snap.SourceAppId})",
                FontSize = 12,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });

            _queueItemsStack.Children.Add(new TextBlock
            {
                Text = "SAVED FAVOURITES (♥)",
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(53, 214, 160)),
                Margin = new Thickness(0, 0, 0, 6)
            });

            var favs = _favouritesService.GetAll();
            if (favs.Count == 0)
            {
                _queueItemsStack.Children.Add(new TextBlock
                {
                    Text = "No saved tracks yet. Click the ♥ button on the music bar to save tracks here.",
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(145, 164, 158)),
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            foreach (var item in favs)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var txt = new TextBlock
                {
                    Text = $"{item.Title} — {item.Artist}",
                    Foreground = Brushes.White,
                    FontSize = 11.5,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };

                string keyToRemove = item.Key;
                var delBtn = new Button
                {
                    Content = "\u2715",
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.FromRgb(145, 164, 158)),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(6, 0, 0, 0)
                };
                delBtn.Click += (_, _) =>
                {
                    _favouritesService.RemoveByKey(keyToRemove);
                    UpdateFavouriteButtonVisual(_favouritesService.IsFavourite(_mediaService.CurrentSession));
                    RefreshQueuePopupContent();
                };

                Grid.SetColumn(txt, 0);
                Grid.SetColumn(delBtn, 1);
                row.Children.Add(txt);
                row.Children.Add(delBtn);
                _queueItemsStack.Children.Add(row);
            }
        }

        private static Button CreateCircularIconButton(string symbol, string tooltip, double size)
        {
            var btn = new Button
            {
                Content = symbol,
                ToolTip = tooltip,
                Width = size,
                Height = size,
                Margin = new Thickness(3, 0, 3, 0),
                Background = new SolidColorBrush(Color.FromArgb(150, 16, 34, 29)),
                Foreground = new SolidColorBrush(Color.FromRgb(210, 224, 219)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(65, 145, 164, 158)),
                BorderThickness = new Thickness(1),
                FontSize = 12.5,
                Cursor = Cursors.Hand,
                Template = CreateCircularButtonTemplate(size * 0.5)
            };
            return btn;
        }

        private static ControlTemplate CreateCircularButtonTemplate(double radius)
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            template.VisualTree = border;
            return template;
        }

        private static ControlTemplate CreateEmeraldPlayButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(21));
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(53, 214, 160)));
            border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(66, 229, 176)));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1.2));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            template.VisualTree = border;
            return template;
        }
    }
}