using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
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
        private readonly double[] _smoothedBars = new double[28];
        private Color _emeraldBright = Color.FromRgb(64, 242, 176);
        private Color _emeraldMid = Color.FromRgb(132, 208, 182);
        private Color _warmTail = Color.FromRgb(156, 144, 136);
        private double _intensity = 1.25;
        private double _idlePhase;

        public void UpdateSpectrum(AudioAnalysisFrame frame, AuraWaveConfig cfg)
        {
            var bands = frame.FrequencyBands ?? Array.Empty<float>();
            var pal = ThemeManager.GetActivePalette(cfg);
            _emeraldBright = ThemeManager.ParseColor(pal.PrimaryHex, Color.FromRgb(64, 242, 176));
            _emeraldMid = Color.FromRgb(132, 208, 182);
            _warmTail = Color.FromRgb(156, 144, 136);
            _intensity = cfg.VisualizerIntensity;
            _idlePhase = (_idlePhase + 0.07) % (Math.PI * 2.0);

            int barCount = _smoothedBars.Length;
            for (int i = 0; i < barCount; i++)
            {
                float raw = 0f;
                if (bands.Length > 0)
                {
                    int idx = (i * bands.Length) / barCount;
                    raw = bands[Math.Clamp(idx, 0, bands.Length - 1)];
                }

                double target;
                if (frame.IsAudioActive || raw > 0.004f)
                {
                    double freqWeight = 1.0 + (i / (double)barCount) * 0.65;
                    target = Math.Clamp(raw * _intensity * freqWeight * 1.5, 0.10, 1.0);
                }
                else
                {
                    target = 0.12 + 0.05 * Math.Sin(_idlePhase + i * 0.35);
                }

                double prev = _smoothedBars[i];
                double alpha = target > prev ? 0.45 : 0.22;
                _smoothedBars[i] = prev + (target - prev) * alpha;
            }

            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 10 || h <= 6) return;

            int barCount = _smoothedBars.Length;
            double step = w / barCount;
            double barW = 3.0;
            double baselineY = h - 1.0;
            double maxBarH = h - 2.0;

            var brushEmerald = new LinearGradientBrush(
                Color.FromRgb(110, 255, 204),
                _emeraldBright,
                new Point(0, 0),
                new Point(0, 1));
            brushEmerald.Freeze();

            var brushMid = new SolidColorBrush(Color.FromArgb(240, _emeraldMid.R, _emeraldMid.G, _emeraldMid.B));
            brushMid.Freeze();

            var brushTail = new SolidColorBrush(Color.FromArgb(210, _warmTail.R, _warmTail.G, _warmTail.B));
            brushTail.Freeze();

            for (int i = 0; i < barCount; i++)
            {
                double ratio = Math.Clamp(_smoothedBars[i], 0.10, 1.0);
                double barH = Math.Clamp(ratio * maxBarH, 3.0, maxBarH);
                double x = i * step + (step - barW) * 0.5;
                double y = baselineY - barH;

                Brush b = i < 9 ? brushEmerald : (i < 18 ? brushMid : brushTail);
                dc.DrawRoundedRectangle(b, null, new Rect(x, y, barW, barH), 1.5, 1.5);
            }
        }
    }

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
            Width = 60;
            Height = 20;
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
            double pad = 5.0;
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

            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

            double pad = 5.0;
            double trackW = w - pad * 2.0;
            double trackH = 3.6;
            double trackY = (h - trackH) * 0.5;

            var bgTrackBrush = new SolidColorBrush(Color.FromRgb(48, 68, 62));
            bgTrackBrush.Freeze();
            dc.DrawRoundedRectangle(bgTrackBrush, null, new Rect(pad, trackY, trackW, trackH), 1.8, 1.8);

            double fillW = Math.Clamp(trackW * _value, 0.0, trackW);
            var fillBrush = new SolidColorBrush(Color.FromRgb(64, 242, 176));
            fillBrush.Freeze();
            if (fillW > 0.5)
            {
                dc.DrawRoundedRectangle(fillBrush, null, new Rect(pad, trackY, fillW, trackH), 1.8, 1.8);
            }

            double thumbX = pad + fillW;
            double thumbY = h * 0.5;

            var glowBrush = new RadialGradientBrush(
                Color.FromArgb(145, 100, 255, 198),
                Color.FromArgb(0, 64, 242, 176));
            glowBrush.Freeze();
            dc.DrawEllipse(glowBrush, null, new Point(thumbX, thumbY), 8.5, 8.5);

            var thumbBrush = new SolidColorBrush(Color.FromRgb(120, 255, 206));
            thumbBrush.Freeze();
            dc.DrawEllipse(thumbBrush, null, new Point(thumbX, thumbY), 4.4, 4.4);
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
        private readonly ImageBrush _artworkImageBrush;
        private readonly BitmapSource _fallbackSunsetBitmap;
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
        private readonly System.Windows.Shapes.Path _favIconPath;
        private readonly Button _prevBtn;
        private readonly Button _playPauseBtn;
        private readonly System.Windows.Shapes.Path _playPauseIconPath;
        private readonly Button _nextBtn;
        private readonly Border _volumePillBorder;
        private readonly Button _muteBtn;
        private readonly System.Windows.Shapes.Path _speakerIconPath;
        private readonly EmeraldVolumeBarControl _volumeBar;
        private readonly Button _queueListBtn;
        private readonly Button _settingsBtn;
        private readonly Button _pinBtn;
        private readonly System.Windows.Shapes.Path _pinIconPath;
        private readonly Button _closeHideBtn;

        private readonly Popup _queuePopup;
        private readonly StackPanel _queueItemsStack;
        private readonly DispatcherTimer _autoHideTimer;

        private bool _isEditMode;
        private bool _isInteractingWithVolume;
        private bool _isDraggingWindow;
        private bool _isMuted;
        private bool _isCircularArtMode = true;
        private RectangleGeometry _artClipGeometry;
        private double _discAngle;
        private byte[]? _lastAppliedArtworkBytes;

        // Studio-Grade 24x24 Cubic Bezier Vector Icon Geometries
        private const string GeometryHeartOutline = "M 12,21.35 L 10.55,20.03 C 5.4,15.36 2,12.27 2,8.5 C 2,5.41 4.42,3 7.5,3 C 9.24,3 10.91,3.81 12,5.08 C 13.09,3.81 14.76,3 16.5,3 C 19.58,3 22,5.41 22,8.5 C 22,12.27 18.6,15.36 13.45,20.03 L 12,21.35 Z";
        private const string GeometryPrevTrack = "M 6,6 C 6,5.45 6.45,5 7,5 C 7.55,5 8,5.45 8,6 L 8,18 C 8,18.55 7.55,19 7,19 C 6.45,19 6,18.55 6,18 Z M 10.2,12.8 C 9.65,12.4 9.65,11.6 10.2,11.2 L 17.4,6.1 C 18.06,5.63 19,6.1 19,6.9 L 19,17.1 C 19,17.9 18.06,18.37 17.4,17.9 Z";
        private const string GeometryNextTrack = "M 16,6 C 16,5.45 16.45,5 17,5 C 17.55,5 18,5.45 18,6 L 18,18 C 18,18.55 17.55,19 17,19 C 16.45,19 16,18.55 16,18 Z M 13.8,12.8 L 6.6,17.9 C 5.94,18.37 5,17.9 5,17.1 L 5,6.9 C 5,6.1 5.94,5.63 6.6,6.1 L 13.8,11.2 C 14.35,11.6 14.35,12.4 13.8,12.8 Z";
        private const string GeometryPauseBars = "M 7,5 C 6.3,5 5.8,5.5 5.8,6.2 L 5.8,17.8 C 5.8,18.5 6.3,19 7,19 L 9.2,19 C 9.9,19 10.4,18.5 10.4,17.8 L 10.4,6.2 C 10.4,5.5 9.9,5 9.2,5 Z M 14.8,5 C 14.1,5 13.6,5.5 13.6,6.2 L 13.6,17.8 C 13.6,18.5 14.1,19 14.8,19 L 17,19 C 17.7,19 18.2,18.5 18.2,17.8 L 18.2,6.2 C 18.2,5.5 17.7,5 17,5 Z";
        private const string GeometryPlayTriangle = "M 7.5,5.2 C 7.5,4.35 8.45,3.85 9.15,4.32 L 18.6,10.82 C 19.22,11.25 19.22,12.15 18.6,12.58 L 9.15,19.08 C 8.45,19.55 7.5,19.05 7.5,18.2 Z";
        private const string GeometrySpeakerOn = "M 11,5 L 6,9 L 2,9 L 2,15 L 6,15 L 11,19 L 11,5 Z M 15.54,8.46 C 16.48,9.4 17,10.67 17,12 C 17,13.33 16.48,14.6 15.54,15.54 M 19.07,4.93 C 20.95,6.81 22,9.35 22,12 C 22,14.65 20.95,17.19 19.07,19.07";
        private const string GeometrySpeakerMuted = "M 11,5 L 6,9 L 2,9 L 2,15 L 6,15 L 11,19 L 11,5 Z M 22,9 L 16,15 M 16,9 L 22,15";
        private const string GeometryQueueList = "M 8,6 L 21,6 M 8,12 L 21,12 M 8,18 L 21,18 M 3.5,6 L 3.6,6 M 3.5,12 L 3.6,12 M 3.5,18 L 3.6,18";
        private const string GeometryGearPetal = "M 12,15.2 C 13.77,15.2 15.2,13.77 15.2,12 C 15.2,10.23 13.77,8.8 12,8.8 C 10.23,8.8 8.8,10.23 8.8,12 C 8.8,13.77 10.23,15.2 12,15.2 Z M 19.4,15 C 19.2,15.3 19.3,15.7 19.5,16 L 19.6,16.1 C 20,16.5 20,17.1 19.6,17.5 L 17.5,19.6 C 17.1,20 16.5,20 16.1,19.6 L 16,19.5 C 15.7,19.3 15.3,19.2 15,19.4 C 14.7,19.5 14.5,19.8 14.5,20.2 L 14.5,20.5 C 14.5,21.1 14,21.5 13.5,21.5 L 10.5,21.5 C 10,21.5 9.5,21.1 9.5,20.5 L 9.5,20.2 C 9.5,19.8 9.3,19.5 9,19.4 C 8.7,19.2 8.3,19.3 8,19.5 L 7.9,19.6 C 7.5,20 6.9,20 6.5,19.6 L 4.4,17.5 C 4,17.1 4,16.5 4.4,16.1 L 4.5,16 C 4.7,15.7 4.8,15.3 4.6,15 C 4.5,14.7 4.2,14.5 3.8,14.5 L 3.5,14.5 C 2.9,14.5 2.5,14 2.5,13.5 L 2.5,10.5 C 2.5,10 2.9,9.5 3.5,9.5 L 3.8,9.5 C 4.2,9.5 4.5,9.3 4.6,9 C 4.8,8.7 4.7,8.3 4.5,8 L 4.4,7.9 C 4,7.5 4,6.9 4.4,6.5 L 6.5,4.4 C 6.9,4 7.5,4 7.9,4.4 L 8,4.5 C 8.3,4.7 8.7,4.8 9,4.6 C 9.3,4.5 9.5,4.2 9.5,3.8 L 9.5,3.5 C 9.5,2.9 10,2.5 10.5,2.5 L 13.5,2.5 C 14,2.5 14.5,2.9 14.5,3.5 L 14.5,3.8 C 14.5,4.2 14.7,4.5 15,4.6 C 15.3,4.8 15.7,4.7 16,4.5 L 16.1,4.4 C 16.5,4 17.1,4 17.5,4.4 L 19.6,6.5 C 20,6.9 20,7.5 19.6,7.9 L 19.5,8 C 19.3,8.3 19.2,8.7 19.4,9 C 19.5,9.3 19.8,9.5 20.2,9.5 L 20.5,9.5 C 21.1,9.5 21.5,10 21.5,10.5 L 21.5,13.5 C 21.5,14 21.1,14.5 20.5,14.5 L 20.2,14.5 C 19.8,14.5 19.5,14.7 19.4,15 Z";
        private const string GeometryPushPin = "M 9,3 L 15,3 C 15.55,3 16,3.45 16,4 C 16,4.55 15.55,5 15,5 L 14,5 L 14,10 L 16.6,12.6 C 16.85,12.85 17,13.2 17,13.55 C 17,14.35 16.35,15 15.55,15 L 13,15 L 13,21 C 13,21.55 12.55,22 12,22 C 11.45,22 11,21.55 11,21 L 11,15 L 8.45,15 C 7.65,15 7,14.35 7,13.55 C 7,13.2 7.15,12.85 7.4,12.6 L 10,10 L 10,5 L 9,5 C 8.45,5 8,4.55 8,4 C 8,3.45 8.45,3 9,3 Z";
        private const string GeometryCloseX = "M 18,6 L 6,18 M 6,6 L 18,18";

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

            _discRotateTransform = new RotateTransform(0, 28, 28);
            _fallbackSunsetBitmap = GeneratePhotographicSunsetBitmap(112, 112);
            _artworkImageBrush = new ImageBrush(_fallbackSunsetBitmap)
            {
                Stretch = Stretch.UniformToFill
            };
            _artworkHostGrid = CreateAlbumArtworkBox();

            // High-Contrast Popping Track Title + Artist
            _titleText = new TextBlock
            {
                Text = "Ready for Music",
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                Foreground = Brushes.White,
                FontSize = 15.0,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 215,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 6,
                    ShadowDepth = 1,
                    Opacity = 0.85
                }
            };

            _spotifySourceBadge = CreateSpotifySourceBadge();

            _artistText = new TextBlock
            {
                Text = "Play any song",
                FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(196, 218, 210)),
                FontSize = 12.5,
                FontWeight = FontWeights.Medium,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 190,
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
                Width = 215,
                Margin = new Thickness(16, 0, 18, 0)
            };
            trackInfoStack.Children.Add(_titleText);
            trackInfoStack.Children.Add(artistRow);

            // Spacious 28-Bar Spectrum + Seekable Progress + Timeline
            _miniSpectrum = new EmbeddedMiniSpectrumControl
            {
                Width = 204,
                Height = 23,
                Margin = new Thickness(0, 2, 0, 5)
            };

            _progressFillBorder = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 58,
                Height = 3.8,
                CornerRadius = new CornerRadius(1.9),
                Background = new SolidColorBrush(Color.FromRgb(64, 242, 176))
            };

            _progressTrackBorder = new Border
            {
                Width = 204,
                Height = 3.8,
                CornerRadius = new CornerRadius(1.9),
                Background = new SolidColorBrush(Color.FromRgb(46, 66, 60)),
                Cursor = Cursors.Hand,
                ToolTip = "Click to Seek Timeline Position",
                Child = _progressFillBorder
            };
            _progressTrackBorder.MouseLeftButtonDown += OnProgressBarMouseLeftButtonDown;

            _timelineText = new TextBlock
            {
                Text = "0:00 / 0:00",
                FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(186, 210, 202)),
                FontSize = 11.0,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };

            _spectrumAndProgressPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 208,
                Margin = new Thickness(4, 2, 20, 0)
            };
            _spectrumAndProgressPanel.Children.Add(_miniSpectrum);
            _spectrumAndProgressPanel.Children.Add(_progressTrackBorder);
            _spectrumAndProgressPanel.Children.Add(_timelineText);

            // 9 Rich Studio Vector Controls with Generous Spacing
            _favBtn = CreateVectorCircleButton(GeometryHeartOutline, "Save / Remove Track in AuraWave Favourites", 36, 15.5, false, true, out _favIconPath);
            _favBtn.Click += OnFavouriteButtonClick;

            _prevBtn = CreateVectorCircleButton(GeometryPrevTrack, "Previous Track", 36, 15.5, true, false, out _);
            _prevBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                _mediaService.SendPreviousTrack();
            };

            _playPauseIconPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(GeometryPauseBars),
                Fill = new SolidColorBrush(Color.FromRgb(5, 18, 14)),
                Width = 17,
                Height = 17,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _playPauseBtn = new Button
            {
                Width = 48,
                Height = 48,
                Margin = new Thickness(7, 0, 7, 0),
                ToolTip = "Play / Pause Active Media Session",
                Cursor = Cursors.Hand,
                Template = CreateEmeraldPlayButtonTemplate(),
                Content = _playPauseIconPath,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(64, 242, 176),
                    BlurRadius = 20,
                    ShadowDepth = 0,
                    Opacity = 0.62
                }
            };
            _playPauseBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                _mediaService.SendPlayPause();
            };

            _nextBtn = CreateVectorCircleButton(GeometryNextTrack, "Next Track", 36, 15.5, true, false, out _);
            _nextBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                _mediaService.SendNextTrack();
            };

            _speakerIconPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(GeometrySpeakerOn),
                Stroke = new SolidColorBrush(Color.FromRgb(244, 250, 248)),
                StrokeThickness = 1.75,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = new SolidColorBrush(Color.FromRgb(244, 250, 248)),
                Width = 15.5,
                Height = 15.5,
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
                Margin = new Thickness(2, 0, 6, 0),
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
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Color.FromArgb(215, 14, 29, 25)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(95, 130, 185, 168)),
                BorderThickness = new Thickness(1.1),
                Padding = new Thickness(11, 0, 11, 0),
                Margin = new Thickness(7, 0, 7, 0),
                Child = volStack
            };

            _queueItemsStack = new StackPanel();
            _queuePopup = CreateFavouritesQueuePopup();
            _queueListBtn = CreateVectorCircleButton(GeometryQueueList, "Open Saved Favourites & Active Session Panel", 35, 15.0, false, true, out _);
            _queueListBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                RefreshQueuePopupContent();
                _queuePopup.IsOpen = !_queuePopup.IsOpen;
            };

            _settingsBtn = CreateVectorCircleButton(GeometryGearPetal, "Open AuraWave Settings Studio (Alt+S)", 35, 15.5, false, true, out _);
            _settingsBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
            };

            _pinBtn = CreateVectorCircleButton(GeometryPushPin, "Toggle Pin (Keep music bar permanently visible)", 35, 15.0, true, false, out _pinIconPath);
            _pinBtn.Click += OnPinButtonClick;

            _closeHideBtn = CreateVectorCircleButton(GeometryCloseX, "Hide Music Bar to System Tray (Restore via Alt+P or Tray Icon)", 35, 14.0, false, true, out _);
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
                Margin = new Thickness(22, 6, 18, 6),
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

            var innerCompositeGrid = new Grid();
            var ambientPlayGlow = new Ellipse
            {
                Width = 230,
                Height = 78,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 185, 0),
                IsHitTestVisible = false,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(56, 64, 242, 176),
                    Color.FromArgb(0, 8, 20, 17))
            };
            var ambientSpectrumGlow = new Ellipse
            {
                Width = 250,
                Height = 72,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(-50, 0, 0, -10),
                IsHitTestVisible = false,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(44, 64, 242, 176),
                    Color.FromArgb(0, 8, 20, 17))
            };
            innerCompositeGrid.Children.Add(ambientSpectrumGlow);
            innerCompositeGrid.Children.Add(ambientPlayGlow);
            innerCompositeGrid.Children.Add(barGrid);

            var innerSurfaceBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            innerSurfaceBrush.GradientStops.Add(new GradientStop(Color.FromArgb(250, 7, 16, 14), 0.0));
            innerSurfaceBrush.GradientStops.Add(new GradientStop(Color.FromArgb(250, 12, 28, 23), 0.50));
            innerSurfaceBrush.GradientStops.Add(new GradientStop(Color.FromArgb(250, 6, 14, 12), 1.0));

            var innerBorderBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            innerBorderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(175, 150, 248, 210), 0.0));
            innerBorderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(55, 64, 242, 176), 0.5));
            innerBorderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(125, 64, 242, 176), 1.0));

            _innerCapsuleBorder = new Border
            {
                CornerRadius = new CornerRadius(37),
                Background = innerSurfaceBrush,
                BorderBrush = innerBorderBrush,
                BorderThickness = new Thickness(1.15),
                Child = innerCompositeGrid
            };

            var outerRimBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            outerRimBrush.GradientStops.Add(new GradientStop(Color.FromArgb(210, 215, 252, 238), 0.0));
            outerRimBrush.GradientStops.Add(new GradientStop(Color.FromArgb(100, 95, 200, 165), 0.35));
            outerRimBrush.GradientStops.Add(new GradientStop(Color.FromArgb(80, 70, 170, 140), 0.70));
            outerRimBrush.GradientStops.Add(new GradientStop(Color.FromArgb(185, 190, 248, 228), 1.0));

            var outerGlassSurface = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            outerGlassSurface.GradientStops.Add(new GradientStop(Color.FromArgb(120, 38, 72, 62), 0.0));
            outerGlassSurface.GradientStops.Add(new GradientStop(Color.FromArgb(90, 14, 30, 25), 0.5));
            outerGlassSurface.GradientStops.Add(new GradientStop(Color.FromArgb(115, 26, 56, 48), 1.0));

            _outerGlassBezel = new Border
            {
                Margin = new Thickness(6),
                Padding = new Thickness(4.5),
                CornerRadius = new CornerRadius(42),
                Background = outerGlassSurface,
                BorderBrush = outerRimBrush,
                BorderThickness = new Thickness(1.3),
                Child = _innerCapsuleBorder,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(10, 80, 60),
                    BlurRadius = 20,
                    ShadowDepth = 0,
                    Opacity = 0.52
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
                _configService.Current.CompactBarMode ? 820 : 1020,
                90,
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
            _titleText.Text = string.IsNullOrWhiteSpace(snap.Title) ? "Ready for Music" : snap.Title;
            _artistText.Text = string.IsNullOrWhiteSpace(snap.Artist) ? "Play any song" : snap.Artist;
            _timelineText.Text = snap.FormattedTimeline;

            if (snap.ArtworkBytes != null && snap.ArtworkBytes.Length > 0)
            {
                if (!ReferenceEquals(snap.ArtworkBytes, _lastAppliedArtworkBytes))
                {
                    _lastAppliedArtworkBytes = snap.ArtworkBytes;
                    try
                    {
                        using var ms = new MemoryStream(snap.ArtworkBytes);
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();
                        _artworkImageBrush.ImageSource = bmp;
                    }
                    catch
                    {
                        _artworkImageBrush.ImageSource = _fallbackSunsetBitmap;
                    }
                }
            }
            else if (_lastAppliedArtworkBytes != null)
            {
                _lastAppliedArtworkBytes = null;
                _artworkImageBrush.ImageSource = _fallbackSunsetBitmap;
            }

            double trackW = _progressTrackBorder.Width > 0 ? _progressTrackBorder.Width : 204.0;
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

        /// <summary>
        /// Ultra-smooth slow rotation: 0.35 degrees per 16ms frame (~17.1 seconds per revolution).
        /// </summary>
        public void StepDiscAnimation(bool isPlaying)
        {
            if (isPlaying && _configService.Current.EnableRotatingDisc)
            {
                _discAngle = (_discAngle + 0.35) % 360.0;
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
            Color cPrim = ThemeManager.ParseColor(pal.PrimaryHex, Color.FromRgb(64, 242, 176));

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
                ? new SolidColorBrush(Color.FromRgb(64, 242, 176))
                : new SolidColorBrush(Color.FromRgb(210, 228, 222));
            _pinBtn.BorderBrush = pinned
                ? new SolidColorBrush(Color.FromArgb(185, 64, 242, 176))
                : new SolidColorBrush(Color.FromArgb(85, 130, 185, 168));
            _pinBtn.Background = pinned
                ? new SolidColorBrush(Color.FromArgb(225, 22, 48, 40))
                : new SolidColorBrush(Color.FromArgb(210, 14, 29, 25));
            _pinBtn.ToolTip = pinned
                ? "Pinned — Music Bar stays visible (Click to Unpin)"
                : $"Unpinned — Auto-hides after {_configService.Current.AutoHideTimeoutSeconds}s of inactivity (Click to Pin)";
        }

        private void UpdateFavouriteButtonVisual(bool isFav)
        {
            _favIconPath.Fill = isFav
                ? new SolidColorBrush(Color.FromRgb(64, 242, 176))
                : Brushes.Transparent;
            _favIconPath.Stroke = isFav
                ? new SolidColorBrush(Color.FromRgb(64, 242, 176))
                : new SolidColorBrush(Color.FromRgb(244, 250, 248));
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

        private bool IsInteractiveChild(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is ButtonBase || source is EmeraldVolumeBarControl || ReferenceEquals(source, _artworkHostGrid))
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

        private Grid CreateAlbumArtworkBox()
        {
            _artClipGeometry = new RectangleGeometry(new Rect(0, 0, 56, 56), 28, 28);

            var container = new Grid
            {
                Width = 56,
                Height = 56,
                Margin = new Thickness(2, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = _discRotateTransform,
                Cursor = Cursors.Hand,
                ToolTip = "Click album cover to switch between Rotating Vinyl Circle and Rounded Squircle",
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 12,
                    ShadowDepth = 2,
                    Opacity = 0.70
                }
            };

            var clippedSurface = new Grid
            {
                Width = 56,
                Height = 56,
                Clip = _artClipGeometry
            };

            var imageRect = new Rectangle
            {
                Width = 56,
                Height = 56,
                Fill = _artworkImageBrush
            };

            var vignetteBrush = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.72,
                RadiusY = 0.72
            };
            vignetteBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.58));
            vignetteBrush.GradientStops.Add(new GradientStop(Color.FromArgb(105, 5, 12, 10), 1.0));

            var vignetteRect = new Rectangle
            {
                Width = 56,
                Height = 56,
                Fill = vignetteBrush,
                IsHitTestVisible = false
            };

            // Subtle vinyl sheen arc so slow rotation is always perceptible
            var sheenBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            sheenBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.35));
            sheenBrush.GradientStops.Add(new GradientStop(Color.FromArgb(28, 200, 255, 235), 0.50));
            sheenBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.65));

            var sheenRect = new Rectangle
            {
                Width = 56,
                Height = 56,
                Fill = sheenBrush,
                IsHitTestVisible = false
            };

            clippedSurface.Children.Add(imageRect);
            clippedSurface.Children.Add(sheenRect);
            clippedSurface.Children.Add(vignetteRect);
            container.Children.Add(clippedSurface);

            container.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                _isCircularArtMode = !_isCircularArtMode;
                double r = _isCircularArtMode ? 28.0 : 14.0;
                _artClipGeometry.RadiusX = r;
                _artClipGeometry.RadiusY = r;
            };

            return container;
        }

        private static BitmapSource GeneratePhotographicSunsetBitmap(int width, int height)
        {
            var wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            byte[] pixels = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                double ny = (double)y / (height - 1);
                for (int x = 0; x < width; x++)
                {
                    double nx = (double)x / (width - 1);

                    double cloudWave = Math.Sin(nx * 6.5 + Math.Cos(ny * 5.0) * 1.4) * 0.5 + 0.5;
                    double sunDist = Math.Sqrt(Math.Pow(nx - 0.45, 2) + Math.Pow(ny - 0.54, 2));
                    double sunBloom = Math.Max(0.0, 1.0 - sunDist * 2.2);

                    double r, g, b;
                    if (ny < 0.68)
                    {
                        double t = ny / 0.68;
                        r = 92 + t * 145 + sunBloom * 45 - cloudWave * 22 * (1.0 - t);
                        g = 128 + t * 48 + sunBloom * 30 - cloudWave * 18;
                        b = 165 - t * 45 + cloudWave * 15;
                    }
                    else
                    {
                        double t = (ny - 0.68) / 0.32;
                        double bokeh = ((x * 37 + y * 13) % 29 == 0 && t < 0.55) ? 65.0 : 0.0;
                        r = 38 * (1.0 - t * 0.6) + bokeh;
                        g = 36 * (1.0 - t * 0.6) + bokeh * 0.8;
                        b = 48 * (1.0 - t * 0.5) + bokeh * 0.5;
                    }

                    double dxHead = (nx - 0.51) * 1.15;
                    double dyHead = (ny - 0.53);
                    double headDist = Math.Sqrt(dxHead * dxHead + dyHead * dyHead);

                    double neckDist = Math.Abs(nx - 0.51);
                    bool inNeck = ny >= 0.58 && ny <= 0.65 && neckDist < 0.045;

                    double shoulderHalfW = 0.16 * Math.Sin(Math.Clamp((ny - 0.62) / 0.38, 0.0, 1.0) * Math.PI * 0.55);
                    bool inBody = ny >= 0.63 && Math.Abs(nx - 0.51) < shoulderHalfW;

                    if (headDist < 0.082 || inNeck || inBody)
                    {
                        r = 16;
                        g = 15;
                        b = 20;
                    }

                    int idx = (y * width + x) * 4;
                    pixels[idx + 0] = (byte)Math.Clamp(b, 0, 255);
                    pixels[idx + 1] = (byte)Math.Clamp(g, 0, 255);
                    pixels[idx + 2] = (byte)Math.Clamp(r, 0, 255);
                    pixels[idx + 3] = 255;
                }
            }

            wb.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            wb.Freeze();
            return wb;
        }

        private static Grid CreateSpotifySourceBadge()
        {
            var badge = new Grid
            {
                Width = 15,
                Height = 15,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };

            badge.Children.Add(new Ellipse
            {
                Width = 15,
                Height = 15,
                Fill = new SolidColorBrush(Color.FromRgb(64, 242, 176))
            });

            badge.Children.Add(new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 3.8,5.5 C 6.5,4.6 9.2,4.8 11.2,5.8 M 4.4,7.8 C 6.6,7.1 8.8,7.3 10.4,8.2 M 5.1,10.0 C 6.8,9.5 8.4,9.7 9.6,10.3"),
                Stroke = new SolidColorBrush(Color.FromRgb(6, 18, 15)),
                StrokeThickness = 1.35,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });

            return badge;
        }

        private Popup CreateFavouritesQueuePopup()
        {
            var border = new Border
            {
                Width = 330,
                MaxHeight = 280,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(250, 8, 20, 17)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(64, 242, 176)),
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
                Foreground = new SolidColorBrush(Color.FromRgb(64, 242, 176)),
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
                Foreground = new SolidColorBrush(Color.FromRgb(64, 242, 176)),
                Margin = new Thickness(0, 0, 0, 6)
            });

            var favs = _favouritesService.GetAll();
            if (favs.Count == 0)
            {
                _queueItemsStack.Children.Add(new TextBlock
                {
                    Text = "No saved tracks yet. Click the ♥ button on the music bar to save tracks here.",
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(176, 198, 190)),
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
                    Foreground = new SolidColorBrush(Color.FromRgb(176, 198, 190)),
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

        private static Button CreateVectorCircleButton(string pathMarkup, string tooltip, double size, double iconSize, bool filled, bool stroked, out System.Windows.Shapes.Path iconPath)
        {
            iconPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(pathMarkup),
                Width = iconSize,
                Height = iconSize,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = filled ? new SolidColorBrush(Color.FromRgb(244, 250, 248)) : Brushes.Transparent,
                Stroke = stroked ? new SolidColorBrush(Color.FromRgb(244, 250, 248)) : null,
                StrokeThickness = stroked ? 1.85 : 0.0,
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
                Margin = new Thickness(4.5, 0, 4.5, 0),
                Background = new SolidColorBrush(Color.FromArgb(215, 14, 29, 25)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 130, 185, 168)),
                BorderThickness = new Thickness(1.1),
                Cursor = Cursors.Hand,
                Template = CreateCircularButtonTemplate(size * 0.5)
            };
        }

        private static ControlTemplate CreateCircularButtonTemplate(double radius)
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "CircleBorder";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            template.VisualTree = border;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(240, 24, 52, 44)), "CircleBorder"));
            hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(200, 64, 242, 176)), "CircleBorder"));
            template.Triggers.Add(hoverTrigger);

            return template;
        }

        private static ControlTemplate CreateEmeraldPlayButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "PlayBorder";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(24));

            var fillGrad = new LinearGradientBrush(
                Color.FromRgb(82, 248, 186),
                Color.FromRgb(38, 214, 148),
                new Point(0, 0),
                new Point(0, 1));
            fillGrad.Freeze();

            border.SetValue(Border.BackgroundProperty, fillGrad);
            border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(155, 255, 218)));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1.2));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            template.VisualTree = border;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, Brushes.White, "PlayBorder"));
            template.Triggers.Add(hoverTrigger);

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