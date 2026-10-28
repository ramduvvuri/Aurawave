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
    /// <summary>
    /// Renders the exact 28-bar embedded audio spectrum from the reference image:
    /// Bright emerald bars on the left/low-mid frequencies transitioning smoothly into
    /// muted warm slate-taupe bars on the high-frequency tail.
    /// </summary>
    public sealed class EmbeddedMiniSpectrumControl : FrameworkElement
    {
        private static readonly double[] ReferenceSilhouette = new double[]
        {
            0.26, 0.52, 0.78, 0.96, 0.70, 0.45, 0.34, 0.25,
            0.40, 0.64, 0.50, 0.38, 0.28, 0.20, 0.26, 0.18,
            0.22, 0.16, 0.24, 0.18, 0.28, 0.22, 0.42, 0.50,
            0.46, 0.32, 0.20, 0.14
        };

        private float[] _bands = Array.Empty<float>();
        private Color _emeraldBright = Color.FromRgb(53, 226, 164);
        private Color _emeraldMid = Color.FromRgb(118, 187, 163);
        private Color _warmTail = Color.FromRgb(124, 114, 108);
        private double _intensity = 1.15;
        private double _phase;

        public void UpdateSpectrum(AudioAnalysisFrame frame, AuraWaveConfig cfg)
        {
            _bands = frame.FrequencyBands ?? Array.Empty<float>();
            var pal = ThemeManager.GetActivePalette(cfg);
            _emeraldBright = ThemeManager.ParseColor(pal.PrimaryHex, Color.FromRgb(46, 229, 157));
            _emeraldMid = Color.FromRgb(118, 187, 163);
            _warmTail = Color.FromRgb(124, 114, 108);
            _intensity = cfg.VisualizerIntensity;
            if (frame.IsAudioActive)
            {
                _phase = (_phase + 0.18) % (Math.PI * 2.0);
            }
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 10 || h <= 6) return;

            int barCount = 28;
            double step = w / barCount;
            double barW = 2.8;
            double baselineY = h - 1.0;
            double maxBarH = h - 2.0;

            var brushEmerald = new LinearGradientBrush(
                Color.FromRgb(82, 246, 184),
                _emeraldBright,
                new Point(0, 0),
                new Point(0, 1));
            brushEmerald.Freeze();

            var brushMid = new SolidColorBrush(Color.FromArgb(225, _emeraldMid.R, _emeraldMid.G, _emeraldMid.B));
            brushMid.Freeze();

            var brushTail = new SolidColorBrush(Color.FromArgb(185, _warmTail.R, _warmTail.G, _warmTail.B));
            brushTail.Freeze();

            for (int i = 0; i < barCount; i++)
            {
                float liveVal = 0f;
                if (_bands.Length > 0)
                {
                    int idx = (i * _bands.Length) / barCount;
                    liveVal = _bands[Math.Clamp(idx, 0, _bands.Length - 1)];
                }

                double refHeightRatio = ReferenceSilhouette[i % ReferenceSilhouette.Length];
                double dynamicRatio;
                if (liveVal > 0.015f)
                {
                    double mod = 0.85 + 0.15 * Math.Sin(_phase + i * 0.45);
                    dynamicRatio = Math.Clamp((refHeightRatio * 0.35 + liveVal * _intensity * 0.85) * mod, 0.12, 1.0);
                }
                else
                {
                    // Preserves the exact aesthetic silhouette from the reference image at quiet/idle volumes
                    dynamicRatio = refHeightRatio;
                }

                double barH = Math.Clamp(dynamicRatio * maxBarH, 3.0, maxBarH);
                double x = i * step + (step - barW) * 0.5;
                double y = baselineY - barH;

                Brush b = i < 7 ? brushEmerald : (i < 15 ? brushMid : brushTail);
                dc.DrawRoundedRectangle(b, null, new Rect(x, y, barW, barH), 1.4, 1.4);
            }
        }
    }

    /// <summary>
    /// Custom-rendered emerald volume track with glowing circular thumb inside the dark volume pill.
    /// Supports left-click dragging and mouse-wheel volume adjustment.
    /// </summary>
    public sealed class EmeraldVolumeBarControl : FrameworkElement
    {
        private double _value = 0.65;
        private bool _isDragging;

        public event EventHandler<double>? VolumeChanged;
        public event EventHandler? InteractionStarted;
        public event EventHandler? InteractionEnded;

        public double Value
        {
            get => _value;
            set
            {
                _value = Math.Clamp(value, 0.0, 1.0);
                InvalidateVisual();
            }
        }

        public EmeraldVolumeBarControl()
        {
            Width = 54;
            Height = 18;
            Cursor = Cursors.Hand;
            ToolTip = "System Output Volume (Drag or Scroll)";
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            e.Handled = true;
            _isDragging = true;
            CaptureMouse();
            InteractionStarted?.Invoke(this, EventArgs.Empty);
            UpdateFromPoint(e.GetPosition(this));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging)
            {
                UpdateFromPoint(e.GetPosition(this));
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_isDragging)
            {
                e.Handled = true;
                _isDragging = false;
                ReleaseMouseCapture();
                UpdateFromPoint(e.GetPosition(this));
                InteractionEnded?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            e.Handled = true;
            double delta = e.Delta > 0 ? 0.05 : -0.05;
            Value = Math.Clamp(_value + delta, 0.0, 1.0);
            VolumeChanged?.Invoke(this, _value);
        }

        private void UpdateFromPoint(Point pt)
        {
            double pad = 4.5;
            double usable = Math.Max(1.0, ActualWidth - pad * 2.0);
            Value = Math.Clamp((pt.X - pad) / usable, 0.0, 1.0);
            VolumeChanged?.Invoke(this, _value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 8 || h <= 4) return;

            // Transparent hit-test surface
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

            double pad = 4.5;
            double trackW = w - pad * 2.0;
            double trackH = 3.2;
            double trackY = (h - trackH) * 0.5;

            var bgTrackBrush = new SolidColorBrush(Color.FromRgb(40, 56, 51));
            bgTrackBrush.Freeze();
            dc.DrawRoundedRectangle(bgTrackBrush, null, new Rect(pad, trackY, trackW, trackH), 1.6, 1.6);

            double fillW = Math.Clamp(trackW * _value, 0.0, trackW);
            var fillBrush = new SolidColorBrush(Color.FromRgb(46, 229, 157));
            fillBrush.Freeze();
            if (fillW > 0.5)
            {
                dc.DrawRoundedRectangle(fillBrush, null, new Rect(pad, trackY, fillW, trackH), 1.6, 1.6);
            }

            double thumbX = pad + fillW;
            double thumbY = h * 0.5;

            var glowBrush = new RadialGradientBrush(
                Color.FromArgb(115, 72, 240, 178),
                Color.FromArgb(0, 46, 229, 157));
            glowBrush.Freeze();
            dc.DrawEllipse(glowBrush, null, new Point(thumbX, thumbY), 7.5, 7.5);

            var thumbBrush = new SolidColorBrush(Color.FromRgb(72, 240, 178));
            thumbBrush.Freeze();
            dc.DrawEllipse(thumbBrush, null, new Point(thumbX, thumbY), 4.2, 4.2);
        }
    }

    public sealed class NowPlayingWidgetWindow : Window
    {
        private readonly ConfigurationService _configService;
        private readonly WindowsMediaSessionService _mediaService;
        private readonly WasapiLoopbackEngine _audioEngine;
        private readonly FavouritesService _favouritesService;

        private readonly Border _outerGlassBezel;
        private readonly Border _innerCapsuleBorder;
        private readonly Grid _artworkHostGrid;
        private readonly RotateTransform _discRotateTransform;
        private readonly TextBlock _titleText;
        private readonly TextBlock _artistText;
        private readonly Grid _spotifySourceBadge;
        private readonly EmbeddedMiniSpectrumControl _miniSpectrum;
        private readonly Border _progressTrackBorder;
        private readonly Border _progressFillBorder;
        private readonly TextBlock _timelineText;
        private readonly StackPanel _spectrumAndProgressPanel;

        private readonly Button _favBtn;
        private readonly Path _favIconPath;
        private readonly Button _prevBtn;
        private readonly Button _playPauseBtn;
        private readonly Path _playPauseIconPath;
        private readonly Button _nextBtn;
        private readonly Border _volumePillBorder;
        private readonly Button _muteBtn;
        private readonly Path _speakerIconPath;
        private readonly EmeraldVolumeBarControl _volumeBar;
        private readonly Button _queueListBtn;
        private readonly Button _settingsBtn;
        private readonly Button _pinBtn;
        private readonly Path _pinIconPath;
        private readonly Button _closeHideBtn;

        private readonly Popup _queuePopup;
        private readonly StackPanel _queueItemsStack;
        private readonly DispatcherTimer _autoHideTimer;

        private bool _isEditMode;
        private bool _isInteractingWithVolume;
        private bool _isDraggingWindow;
        private bool _isMuted;
        private double _discAngle;

        // Crisp SVG Vector Path Data Strings
        private const string GeometryHeartOutline = "M 8,13.6 C 7.4,13.1 2,8.8 2,5.3 C 2,3.4 3.5,2 5.3,2 C 6.4,2 7.4,2.5 8,3.4 C 8.6,2.5 9.6,2 10.7,2 C 12.5,2 14,3.4 14,5.3 C 14,8.8 8.6,13.1 8,13.6 Z";
        private const string GeometryPrevTrack = "M 3.5,3.5 L 5.2,3.5 L 5.2,12.5 L 3.5,12.5 Z M 6.2,8 L 12.5,3.5 L 12.5,12.5 Z";
        private const string GeometryNextTrack = "M 10.8,3.5 L 12.5,3.5 L 12.5,12.5 L 10.8,12.5 Z M 9.8,8 L 3.5,3.5 L 3.5,12.5 Z";
        private const string GeometryPauseBars = "M 4.2,3.2 L 6.8,3.2 L 6.8,12.8 L 4.2,12.8 Z M 9.2,3.2 L 11.8,3.2 L 11.8,12.8 L 9.2,12.8 Z";
        private const string GeometryPlayTriangle = "M 4.8,3.0 L 13.0,8.0 L 4.8,13.0 Z";
        private const string GeometrySpeakerOn = "M 2.5,6.0 L 5.2,6.0 L 8.5,3.2 L 8.5,12.8 L 5.2,10.0 L 2.5,10.0 Z M 10.5,5.2 C 11.5,6.0 11.5,10.0 10.5,10.8 M 12.2,3.6 C 14.0,5.2 14.0,10.8 12.2,12.4";
        private const string GeometrySpeakerMuted = "M 2.5,6.0 L 5.2,6.0 L 8.5,3.2 L 8.5,12.8 L 5.2,10.0 L 2.5,10.0 Z M 10.5,5.5 L 14.0,10.5 M 14.0,5.5 L 10.5,10.5";
        private const string GeometryQueueList = "M 2.5,4.2 L 3.8,4.2 M 5.5,4.2 L 13.5,4.2 M 2.5,8.0 L 3.8,8.0 M 5.5,8.0 L 13.5,8.0 M 2.5,11.8 L 3.8,11.8 M 5.5,11.8 L 13.5,11.8";
        private const string GeometryGear = "M 8,5.4 C 6.56,5.4 5.4,6.56 5.4,8 C 5.4,9.44 6.56,10.6 8,10.6 C 9.44,10.6 10.6,9.44 10.6,8 C 10.6,6.56 9.44,5.4 8,5.4 Z M 7.0,1.8 L 9.0,1.8 L 9.4,3.3 L 10.7,3.9 L 12.1,3.1 L 13.4,4.4 L 12.6,5.8 L 13.2,7.1 L 14.7,7.5 L 14.7,9.5 L 13.2,9.9 L 12.6,11.2 L 13.4,12.6 L 12.1,13.9 L 10.7,13.1 L 9.4,13.7 L 9.0,15.2 L 7.0,15.2 L 6.6,13.7 L 5.3,13.1 L 3.9,13.9 L 2.6,12.6 L 3.4,11.2 L 2.8,9.9 L 1.3,9.5 L 1.3,7.5 L 2.8,7.1 L 3.4,5.8 L 2.6,4.4 L 3.9,3.1 L 5.3,3.9 L 6.6,3.3 Z";
        private const string GeometryPushPin = "M 5.2,2.2 L 10.8,2.2 L 10.8,3.6 L 9.8,4.4 L 9.8,7.6 L 11.4,9.2 L 11.4,10.4 L 8.7,10.4 L 8.7,14.2 L 7.3,14.2 L 7.3,10.4 L 4.6,10.4 L 4.6,9.2 L 6.2,7.6 L 6.2,4.4 L 5.2,3.6 Z";
        private const string GeometryCloseX = "M 4.2,4.2 L 11.8,11.8 M 11.8,4.2 L 4.2,11.8";

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
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            // ================================================================
            // SECTION A: 56x56 Sunset Silhouette Album Artwork (1:1 Reference)
            // ================================================================
            _discRotateTransform = new RotateTransform(0, 28, 28);
            _artworkHostGrid = CreateSunsetAlbumArtworkSection();

            // ================================================================
            // SECTION B: Track Title + Spotify-Style Emerald Source Icon + Artist
            // ================================================================
            _titleText = new TextBlock
            {
                Text = "Uncover the Silent Beauty",
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(244, 249, 247)),
                FontSize = 14.5,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 205
            };

            _spotifySourceBadge = CreateSpotifySourceBadge();

            _artistText = new TextBlock
            {
                Text = "Meadow",
                FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(143, 166, 158)),
                FontSize = 12.0,
                FontWeight = FontWeights.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 180,
                VerticalAlignment = VerticalAlignment.Center
            };

            var artistRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            artistRow.Children.Add(_spotifySourceBadge);
            artistRow.Children.Add(_artistText);

            var trackInfoStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Width = 200,
                Margin = new Thickness(14, 0, 14, 0)
            };
            trackInfoStack.Children.Add(_titleText);
            trackInfoStack.Children.Add(artistRow);

            // ================================================================
            // SECTION C & D: 28-Bar Spectrum + Seekable Progress + Timeline
            // ================================================================
            _miniSpectrum = new EmbeddedMiniSpectrumControl
            {
                Width = 192,
                Height = 22,
                Margin = new Thickness(0, 2, 0, 5)
            };

            _progressFillBorder = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 54,
                Height = 3.5,
                CornerRadius = new CornerRadius(1.75),
                Background = new SolidColorBrush(Color.FromRgb(46, 229, 157))
            };

            _progressTrackBorder = new Border
            {
                Width = 192,
                Height = 3.5,
                CornerRadius = new CornerRadius(1.75),
                Background = new SolidColorBrush(Color.FromRgb(42, 57, 53)),
                Cursor = Cursors.Hand,
                ToolTip = "Click to Seek Timeline Position",
                Child = _progressFillBorder
            };
            _progressTrackBorder.MouseLeftButtonDown += OnProgressBarMouseLeftButtonDown;

            _timelineText = new TextBlock
            {
                Text = "5:42 / 20:14",
                FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(143, 166, 158)),
                FontSize = 10.5,
                FontWeight = FontWeights.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };

            _spectrumAndProgressPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 196,
                Margin = new Thickness(2, 2, 16, 0)
            };
            _spectrumAndProgressPanel.Children.Add(_miniSpectrum);
            _spectrumAndProgressPanel.Children.Add(_progressTrackBorder);
            _spectrumAndProgressPanel.Children.Add(_timelineText);

            // ================================================================
            // SECTION E: 9 Vector Action Controls Matching image_737a35.jpg
            // ================================================================

            // 1. Favourite (Heart Outline / Solid Vector)
            _favBtn = CreateVectorCircleButton(GeometryHeartOutline, "Save / Remove Track in AuraWave Favourites", 34, false, true, out _favIconPath);
            _favBtn.Click += OnFavouriteButtonClick;

            // 2. Previous Track (|◀)
            _prevBtn = CreateVectorCircleButton(GeometryPrevTrack, "Previous Track", 35, true, false, out _);
            _prevBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                _mediaService.SendPreviousTrack();
            };

            // 3. Focal Emerald Play/Pause Circle (46x46 with Neon Emerald Glow)
            _playPauseIconPath = new Path
            {
                Data = Geometry.Parse(GeometryPauseBars),
                Fill = new SolidColorBrush(Color.FromRgb(6, 20, 16)),
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _playPauseBtn = new Button
            {
                Width = 46,
                Height = 46,
                Margin = new Thickness(5, 0, 5, 0),
                ToolTip = "Play / Pause Active Media Session",
                Cursor = Cursors.Hand,
                Template = CreateEmeraldPlayButtonTemplate(),
                Content = _playPauseIconPath,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(46, 229, 157),
                    BlurRadius = 16,
                    ShadowDepth = 0,
                    Opacity = 0.45
                }
            };
            _playPauseBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                _mediaService.SendPlayPause();
            };

            // 4. Next Track (▶|)
            _nextBtn = CreateVectorCircleButton(GeometryNextTrack, "Next Track", 35, true, false, out _);
            _nextBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                _mediaService.SendNextTrack();
            };

            // 5. Custom Dark Volume Pill (Speaker Vector + Emerald Bar & Glowing Thumb)
            _speakerIconPath = new Path
            {
                Data = Geometry.Parse(GeometrySpeakerOn),
                Stroke = new SolidColorBrush(Color.FromRgb(226, 238, 233)),
                StrokeThickness = 1.4,
                Fill = new SolidColorBrush(Color.FromRgb(226, 238, 233)),
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center
            };

            _muteBtn = new Button
            {
                Content = _speakerIconPath,
                ToolTip = "Mute / Unmute Windows Output Volume",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 4, 0),
                Template = CreateTransparentButtonTemplate()
            };
            _muteBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                _isMuted = _audioEngine.ToggleMute();
                _speakerIconPath.Data = Geometry.Parse(_isMuted ? GeometrySpeakerMuted : GeometrySpeakerOn);
            };

            _volumeBar = new EmeraldVolumeBarControl
            {
                Value = _audioEngine.GetMasterVolumeScalar(),
                VerticalAlignment = VerticalAlignment.Center
            };
            _volumeBar.InteractionStarted += (_, _) =>
            {
                _isInteractingWithVolume = true;
                _autoHideTimer.Stop();
            };
            _volumeBar.InteractionEnded += (_, _) =>
            {
                _isInteractingWithVolume = false;
                RestartAutoHideTimerIfUnpinned();
            };
            _volumeBar.VolumeChanged += (_, newLevel) =>
            {
                _audioEngine.SetMasterVolumeScalar((float)newLevel);
            };

            var volStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            volStack.Children.Add(_muteBtn);
            volStack.Children.Add(_volumeBar);

            _volumePillBorder = new Border
            {
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = new SolidColorBrush(Color.FromArgb(190, 14, 26, 23)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(65, 143, 166, 158)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 0, 10, 0),
                Margin = new Thickness(5, 0, 5, 0),
                Child = volStack
            };

            // 6. Queue / Saved Favourites Button (≡•)
            _queueItemsStack = new StackPanel();
            _queuePopup = CreateFavouritesQueuePopup();
            _queueListBtn = CreateVectorCircleButton(GeometryQueueList, "Open Saved Favourites & Active Session Panel", 33, false, true, out _);
            _queueListBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                RefreshQueuePopupContent();
                _queuePopup.IsOpen = !_queuePopup.IsOpen;
            };

            // 7. Settings Gear Button (⚙)
            _settingsBtn = CreateVectorCircleButton(GeometryGear, "Open AuraWave Settings Studio (Alt+S)", 33, false, true, out _);
            _settingsBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
            };

            // 8. Vertical Pushpin Button (📌)
            _pinBtn = CreateVectorCircleButton(GeometryPushPin, "Toggle Pin (Keep music bar permanently visible)", 33, true, false, out _pinIconPath);
            _pinBtn.Click += OnPinButtonClick;

            // 9. Close / Hide Button (✕)
            _closeHideBtn = CreateVectorCircleButton(GeometryCloseX, "Hide Music Bar to System Tray (Restore via Alt+P or Tray Icon)", 33, false, true, out _);
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
                Margin = new Thickness(12, 6, 14, 6),
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

            // Inner Dark Emerald-Obsidian Recessed Capsule
            var innerSurfaceBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            innerSurfaceBrush.GradientStops.Add(new GradientStop(Color.FromArgb(245, 9, 20, 18), 0.0));
            innerSurfaceBrush.GradientStops.Add(new GradientStop(Color.FromArgb(245, 13, 31, 26), 0.48));
            innerSurfaceBrush.GradientStops.Add(new GradientStop(Color.FromArgb(245, 8, 17, 15), 1.0));

            var innerBorderBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            innerBorderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(145, 110, 235, 190), 0.0));
            innerBorderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(55, 46, 229, 157), 0.5));
            innerBorderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(95, 53, 214, 160), 1.0));

            _innerCapsuleBorder = new Border
            {
                CornerRadius = new CornerRadius(37),
                Background = innerSurfaceBrush,
                BorderBrush = innerBorderBrush,
                BorderThickness = new Thickness(1.1),
                Child = barGrid
            };

            // Outer Frosted Glass Specular Bezel matching image_737a35.jpg
            var outerRimBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            outerRimBrush.GradientStops.Add(new GradientStop(Color.FromArgb(175, 185, 245, 225), 0.0));
            outerRimBrush.GradientStops.Add(new GradientStop(Color.FromArgb(65, 80, 175, 145), 0.5));
            outerRimBrush.GradientStops.Add(new GradientStop(Color.FromArgb(120, 95, 205, 170), 1.0));

            _outerGlassBezel = new Border
            {
                Margin = new Thickness(5),
                Padding = new Thickness(3.5),
                CornerRadius = new CornerRadius(41),
                Background = new SolidColorBrush(Color.FromArgb(75, 18, 42, 36)),
                BorderBrush = outerRimBrush,
                BorderThickness = new Thickness(1.2),
                Child = _innerCapsuleBorder,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(18, 110, 82),
                    BlurRadius = 18,
                    ShadowDepth = 0,
                    Opacity = 0.42
                }
            };

            Content = _outerGlassBezel;

            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(Math.Clamp(_configService.Current.AutoHideTimeoutSeconds, 2, 60))
            };
            _autoHideTimer.Tick += OnAutoHideTimerTick;

            ApplyGeometryFromConfig();
            ApplyAppearanceFromConfig();
            UpdatePinButtonVisual();
            UpdateMediaSnapshot(_mediaService.CurrentSession);

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
                _configService.Current.CompactBarMode ? 780 : 960,
                88,
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
            _artistText.Text = string.IsNullOrWhiteSpace(snap.Artist) ? "Meadow" : snap.Artist;
            _timelineText.Text = snap.FormattedTimeline;

            double trackW = _progressTrackBorder.Width > 0 ? _progressTrackBorder.Width : 192.0;
            _progressFillBorder.Width = Math.Clamp(trackW * snap.ProgressRatio, 4.0, trackW);

            bool playing = snap.PlaybackStatus == PlaybackState.Playing;
            _playPauseIconPath.Data = Geometry.Parse(playing ? GeometryPauseBars : GeometryPlayTriangle);

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
                _discAngle = (_discAngle + 1.2) % 360.0;
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
            Color cPrim = ThemeManager.ParseColor(pal.PrimaryHex, Color.FromRgb(46, 229, 157));

            _progressFillBorder.Background = new SolidColorBrush(cPrim);
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
                640, 1600, 78, 140);

            Width = g.Width;
            Height = g.Height;
            Left = g.Left;
            Top = g.Top;
        }

        public void SyncNativeStyles()
        {
            var cfg = _configService.Current;
            bool effectiveClickThrough = cfg.ClickThroughEnabled && !_isEditMode;
            Topmost = cfg.PresentationMode == PresentationMode.AlwaysOnTopOverlay && cfg.AlwaysOnTop;
            NativeWindowInterop.ApplyOverlayWindowStyles(this, effectiveClickThrough, cfg.AlwaysOnTop, cfg.PresentationMode);
        }

        private void OnProgressBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            RegisterUserInteraction();
            Point pt = e.GetPosition(_progressTrackBorder);
            double w = Math.Max(1.0, _progressTrackBorder.ActualWidth);
            _mediaService.SeekToRatio(pt.X / w);
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
            _pinIconPath.Fill = pinned
                ? new SolidColorBrush(Colors.White)
                : new SolidColorBrush(Color.FromRgb(143, 166, 158));
            _pinBtn.BorderBrush = pinned
                ? new SolidColorBrush(Color.FromArgb(145, 46, 229, 157))
                : new SolidColorBrush(Color.FromArgb(55, 143, 166, 158));
            _pinBtn.Background = pinned
                ? new SolidColorBrush(Color.FromArgb(190, 20, 42, 36))
                : new SolidColorBrush(Color.FromArgb(165, 14, 26, 23));
            _pinBtn.ToolTip = pinned
                ? "Pinned — Music Bar stays visible (Click to Unpin)"
                : $"Unpinned — Auto-hides after {_configService.Current.AutoHideTimeoutSeconds}s of inactivity (Click to Pin)";
        }

        private void UpdateFavouriteButtonVisual(bool isFav)
        {
            _favIconPath.Fill = isFav
                ? new SolidColorBrush(Color.FromRgb(46, 229, 157))
                : Brushes.Transparent;
            _favIconPath.Stroke = isFav
                ? new SolidColorBrush(Color.FromRgb(46, 229, 157))
                : new SolidColorBrush(Color.FromRgb(226, 238, 233));
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
            if (!cfg.NowPlayingPinned && cfg.EnableNowPlayingAutoHide && !_isInteractingWithVolume && !_isDraggingWindow && !_isEditMode && !_queuePopup.IsOpen)
            {
                _autoHideTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(cfg.AutoHideTimeoutSeconds, 2, 60));
                _autoHideTimer.Start();
            }
        }

        private void OnAutoHideTimerTick(object? sender, EventArgs e)
        {
            _autoHideTimer.Stop();
            var cfg = _configService.Current;
            if (cfg.NowPlayingPinned || !cfg.EnableNowPlayingAutoHide || IsMouseOver || _isInteractingWithVolume || _isDraggingWindow || _isEditMode || _queuePopup.IsOpen)
            {
                return;
            }
            HideBarRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnCapsuleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || _isInteractingWithVolume)
                return;

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
                if (source is ButtonBase || source is EmeraldVolumeBarControl)
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

        /// <summary>
        /// Renders the 56x56 rounded-square sunset clouds + silhouette album artwork matching image_737a35.jpg.
        /// </summary>
        private Grid CreateSunsetAlbumArtworkSection()
        {
            var container = new Grid
            {
                Width = 56,
                Height = 56,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = _discRotateTransform
            };

            var skyBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            skyBrush.GradientStops.Add(new GradientStop(Color.FromRgb(126, 152, 176), 0.0));
            skyBrush.GradientStops.Add(new GradientStop(Color.FromRgb(234, 178, 132), 0.48));
            skyBrush.GradientStops.Add(new GradientStop(Color.FromRgb(198, 118, 86), 0.72));
            skyBrush.GradientStops.Add(new GradientStop(Color.FromRgb(24, 32, 44), 1.0));

            var artCanvas = new Canvas
            {
                Width = 56,
                Height = 56,
                Clip = new RectangleGeometry(new Rect(0, 0, 56, 56), 11, 11),
                Background = skyBrush
            };

            // Warm sunset glow near horizon
            var sunGlow = new Ellipse
            {
                Width = 36,
                Height = 20,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(210, 255, 220, 165),
                    Color.FromArgb(0, 255, 160, 100))
            };
            Canvas.SetLeft(sunGlow, 10);
            Canvas.SetTop(sunGlow, 22);
            artCanvas.Children.Add(sunGlow);

            // Distant city/water horizon lights
            var horizonBand = new Rectangle
            {
                Width = 56,
                Height = 16,
                Fill = new SolidColorBrush(Color.FromArgb(165, 22, 30, 42))
            };
            Canvas.SetLeft(horizonBand, 0);
            Canvas.SetTop(horizonBand, 40);
            artCanvas.Children.Add(horizonBand);

            // Center person silhouette (head + shoulders matching image_737a35.jpg)
            var silhouetteHead = new Ellipse
            {
                Width = 10,
                Height = 11,
                Fill = new SolidColorBrush(Color.FromRgb(18, 16, 20))
            };
            Canvas.SetLeft(silhouetteHead, 23);
            Canvas.SetTop(silhouetteHead, 25);
            artCanvas.Children.Add(silhouetteHead);

            var silhouetteShoulders = new Path
            {
                Data = Geometry.Parse("M 17,56 L 19,38 C 20,36 24,35 28,35 C 32,35 36,36 37,38 L 39,56 Z"),
                Fill = new SolidColorBrush(Color.FromRgb(15, 15, 20))
            };
            artCanvas.Children.Add(silhouetteShoulders);

            var frameBorder = new Border
            {
                Width = 56,
                Height = 56,
                CornerRadius = new CornerRadius(11),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                BorderThickness = new Thickness(0.8)
            };

            container.Children.Add(artCanvas);
            container.Children.Add(frameBorder);
            return container;
        }

        private static Grid CreateSpotifySourceBadge()
        {
            var badge = new Grid
            {
                Width = 14,
                Height = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };

            badge.Children.Add(new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(Color.FromRgb(46, 229, 157))
            });

            badge.Children.Add(new Path
            {
                Data = Geometry.Parse("M 3.6,5.2 C 6.2,4.4 8.8,4.6 10.6,5.6 M 4.2,7.4 C 6.3,6.8 8.3,7.0 9.8,7.8 M 4.8,9.5 C 6.4,9.0 7.9,9.2 9.0,9.8"),
                Stroke = new SolidColorBrush(Color.FromRgb(7, 18, 15)),
                StrokeThickness = 1.25,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });

            return badge;
        }

        private Popup CreateFavouritesQueuePopup()
        {
            var border = new Border
            {
                Width = 320,
                MaxHeight = 280,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(248, 8, 20, 17)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 229, 157)),
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
                Foreground = new SolidColorBrush(Color.FromRgb(46, 229, 157)),
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
                Foreground = new SolidColorBrush(Color.FromRgb(46, 229, 157)),
                Margin = new Thickness(0, 0, 0, 6)
            });

            var favs = _favouritesService.GetAll();
            if (favs.Count == 0)
            {
                _queueItemsStack.Children.Add(new TextBlock
                {
                    Text = "No saved tracks yet. Click the ♥ button on the music bar to save tracks here.",
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(143, 166, 158)),
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
                    Foreground = new SolidColorBrush(Color.FromRgb(143, 166, 158)),
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

        private static Button CreateVectorCircleButton(string pathMarkup, string tooltip, double size, bool filled, bool stroked, out Path iconPath)
        {
            iconPath = new Path
            {
                Data = Geometry.Parse(pathMarkup),
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = filled ? new SolidColorBrush(Color.FromRgb(235, 244, 240)) : Brushes.Transparent,
                Stroke = stroked ? new SolidColorBrush(Color.FromRgb(226, 238, 233)) : null,
                StrokeThickness = stroked ? 1.45 : 0.0,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };

            return new Button
            {
                Content = iconPath,
                ToolTip = tooltip,
                Width = size,
                Height = size,
                Margin = new Thickness(3.5, 0, 3.5, 0),
                Background = new SolidColorBrush(Color.FromArgb(175, 16, 30, 26)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 143, 166, 158)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Template = CreateCircularButtonTemplate(size * 0.5)
            };
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
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(23));

            var fillGrad = new LinearGradientBrush(
                Color.FromRgb(64, 242, 174),
                Color.FromRgb(36, 214, 146),
                new Point(0, 0),
                new Point(0, 1));
            fillGrad.Freeze();

            border.SetValue(Border.BackgroundProperty, fillGrad);
            border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(110, 250, 196)));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1.0));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            template.VisualTree = border;
            return template;
        }

        private static ControlTemplate CreateTransparentButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            template.VisualTree = border;
            return template;
        }
    }
}