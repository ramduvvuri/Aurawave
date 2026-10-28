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
    /// <summary>
    /// Live 28-bar embedded audio spectrum with smooth attack/decay and the exact
    /// emerald-to-sage-to-warm-taupe color grading from the reference image.
    /// </summary>
    public sealed class EmbeddedMiniSpectrumControl : FrameworkElement
    {
        private readonly double[] _smoothedBars = new double[28];
        private Color _emeraldBright = Color.FromRgb(56, 232, 166);
        private Color _emeraldMid = Color.FromRgb(118, 192, 166);
        private Color _warmTail = Color.FromRgb(138, 124, 116);
        private double _intensity = 1.25;
        private double _idlePhase;

        public void UpdateSpectrum(AudioAnalysisFrame frame, AuraWaveConfig cfg)
        {
            var bands = frame.FrequencyBands ?? Array.Empty<float>();
            var pal = ThemeManager.GetActivePalette(cfg);
            _emeraldBright = ThemeManager.ParseColor(pal.PrimaryHex, Color.FromRgb(56, 232, 166));
            _emeraldMid = Color.FromRgb(118, 192, 166);
            _warmTail = Color.FromRgb(138, 124, 116);
            _intensity = cfg.VisualizerIntensity;
            _idlePhase = (_idlePhase + 0.08) % (Math.PI * 2.0);

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
                    // Boost midrange & treble slightly so all 28 bars dance vividly
                    double freqWeight = 1.0 + (i / (double)barCount) * 0.65;
                    target = Math.Clamp(raw * _intensity * freqWeight * 1.45, 0.08, 1.0);
                }
                else
                {
                    // Gentle organic breathing at rest
                    target = 0.10 + 0.05 * Math.Sin(_idlePhase + i * 0.35);
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
            double barW = 2.8;
            double baselineY = h - 1.0;
            double maxBarH = h - 2.0;

            var brushEmerald = new LinearGradientBrush(
                Color.FromRgb(90, 250, 190),
                _emeraldBright,
                new Point(0, 0),
                new Point(0, 1));
            brushEmerald.Freeze();

            var brushMid = new SolidColorBrush(Color.FromArgb(230, _emeraldMid.R, _emeraldMid.G, _emeraldMid.B));
            brushMid.Freeze();

            var brushTail = new SolidColorBrush(Color.FromArgb(195, _warmTail.R, _warmTail.G, _warmTail.B));
            brushTail.Freeze();

            for (int i = 0; i < barCount; i++)
            {
                double ratio = Math.Clamp(_smoothedBars[i], 0.10, 1.0);
                double barH = Math.Clamp(ratio * maxBarH, 2.8, maxBarH);
                double x = i * step + (step - barW) * 0.5;
                double y = baselineY - barH;

                Brush b = i < 9 ? brushEmerald : (i < 18 ? brushMid : brushTail);
                dc.DrawRoundedRectangle(b, null, new Rect(x, y, barW, barH), 1.4, 1.4);
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

            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

            double pad = 4.5;
            double trackW = w - pad * 2.0;
            double trackH = 3.2;
            double trackY = (h - trackH) * 0.5;

            var bgTrackBrush = new SolidColorBrush(Color.FromRgb(42, 58, 53));
            bgTrackBrush.Freeze();
            dc.DrawRoundedRectangle(bgTrackBrush, null, new Rect(pad, trackY, trackW, trackH), 1.6, 1.6);

            double fillW = Math.Clamp(trackW * _value, 0.0, trackW);
            var fillBrush = new SolidColorBrush(Color.FromRgb(56, 232, 166));
            fillBrush.Freeze();
            if (fillW > 0.5)
            {
                dc.DrawRoundedRectangle(fillBrush, null, new Rect(pad, trackY, fillW, trackH), 1.6, 1.6);
            }

            double thumbX = pad + fillW;
            double thumbY = h * 0.5;

            var glowBrush = new RadialGradientBrush(
                Color.FromArgb(120, 82, 246, 184),
                Color.FromArgb(0, 56, 232, 166));
            glowBrush.Freeze();
            dc.DrawEllipse(glowBrush, null, new Point(thumbX, thumbY), 7.5, 7.5);

            var thumbBrush = new SolidColorBrush(Color.FromRgb(82, 246, 184));
            thumbBrush.Freeze();
            dc.DrawEllipse(thumbBrush, null, new Point(thumbX, thumbY), 4.0, 4.0);
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
        private double _discAngle;
        private byte[]? _lastAppliedArtworkBytes;

        private const string GeometryHeartOutline = "M 8,13.6 C 7.4,13.1 2,8.8 2,5.3 C 2,3.4 3.5,2 5.3,2 C 6.4,2 7.4,2.5 8,3.4 C 8.6,2.5 9.6,2 10.7,2 C 12.5,2 14,3.4 14,5.3 C 14,8.8 8.6,13.1 8,13.6 Z";
        private const string GeometryPrevTrack = "M 3.5,3.5 L 5.2,3.5 L 5.2,12.5 L 3.5,12.5 Z M 6.2,8 L 12.5,3.5 L 12.5,12.5 Z";
        private const string GeometryNextTrack = "M 10.8,3.5 L 12.5,3.5 L 12.5,12.5 L 10.8,12.5 Z M 9.8,8 L 3.5,3.5 L 3.5,12.5 Z";
        private const string GeometryPauseBars = "M 4.4,3.2 L 6.8,3.2 L 6.8,12.8 L 4.4,12.8 Z M 9.2,3.2 L 11.6,3.2 L 11.6,12.8 L 9.2,12.8 Z";
        private const string GeometryPlayTriangle = "M 5.2,3.2 L 12.8,8.0 L 5.2,12.8 Z";
        private const string GeometrySpeakerOn = "M 2.5,6.0 L 5.0,6.0 L 8.2,3.4 L 8.2,12.6 L 5.0,10.0 L 2.5,10.0 Z M 10.2,5.4 C 11.4,6.4 11.4,9.6 10.2,10.6 M 11.8,3.8 C 13.8,5.6 13.8,10.4 11.8,12.2";
        private const string GeometrySpeakerMuted = "M 2.5,6.0 L 5.0,6.0 L 8.2,3.4 L 8.2,12.6 L 5.0,10.0 L 2.5,10.0 Z M 10.4,5.6 L 13.8,10.4 M 13.8,5.6 L 10.4,10.4";
        private const string GeometryQueueList = "M 2.5,4.2 L 3.8,4.2 M 5.6,4.2 L 13.5,4.2 M 2.5,8.0 L 3.8,8.0 M 5.6,8.0 L 13.5,8.0 M 2.5,11.8 L 3.8,11.8 M 5.6,11.8 L 13.5,11.8";
        private const string GeometryGearPetal = "M 8,5.6 C 6.67,5.6 5.6,6.67 5.6,8 C 5.6,9.33 6.67,10.4 8,10.4 C 9.33,10.4 10.4,9.33 10.4,8 C 10.4,6.67 9.33,5.6 8,5.6 Z M 8,2.0 C 8.7,2.0 9.2,2.5 9.3,3.2 C 9.8,3.4 10.3,3.7 10.7,4.1 C 11.3,3.8 12.0,4.0 12.4,4.6 C 12.7,5.2 12.5,5.9 12.0,6.3 C 12.2,6.8 12.3,7.4 12.3,8.0 C 12.3,8.6 12.2,9.2 12.0,9.7 C 12.5,10.1 12.7,10.8 12.4,11.4 C 12.0,12.0 11.3,12.2 10.7,11.9 C 10.3,12.3 9.8,12.6 9.3,12.8 C 9.2,13.5 8.7,14.0 8,14.0 C 7.3,14.0 6.8,13.5 6.7,12.8 C 6.2,12.6 5.7,12.3 5.3,11.9 C 4.7,12.2 4.0,12.0 3.6,11.4 C 3.3,10.8 3.5,10.1 4.0,9.7 C 3.8,9.2 3.7,8.6 3.7,8.0 C 3.7,7.4 3.8,6.8 4.0,6.3 C 3.5,5.9 3.3,5.2 3.6,4.6 C 4.0,4.0 4.7,3.8 5.3,4.1 C 5.7,3.7 6.2,3.4 6.7,3.2 C 6.8,2.5 7.3,2.0 8,2.0 Z";
        private const string GeometryPushPin = "M 5.2,2.2 L 10.8,2.2 L 10.8,3.6 L 9.8,4.4 L 9.8,7.6 L 11.4,9.2 L 11.4,10.4 L 8.7,10.4 L 8.7,14.2 L 7.3,14.2 L 7.3,10.4 L 4.6,10.4 L 4.6,9.2 L 6.2,7.6 L 6.2,4.4 L 5.2,3.6 Z";
        private const string GeometryCloseX = "M 4.6,4.6 L 11.4,11.4 M 11.4,4.6 L 4.6,11.4";

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

            _discRotateTransform = new RotateTransform(0, 29, 29);
            _fallbackSunsetBitmap = GeneratePhotographicSunsetBitmap(112, 112);
            _artworkImageBrush = new ImageBrush(_fallbackSunsetBitmap)
            {
                Stretch = Stretch.UniformToFill
            };
            _artworkHostGrid = CreateAlbumArtworkBox();

            _titleText = new TextBlock
            {
                Text = "Ready for Music",
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
                Text = "Play any song",
                FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(145, 166, 158)),
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
                Width = 205,
                Margin = new Thickness(14, 0, 14, 0)
            };
            trackInfoStack.Children.Add(_titleText);
            trackInfoStack.Children.Add(artistRow);

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
                Background = new SolidColorBrush(Color.FromRgb(56, 232, 166))
            };

            _progressTrackBorder = new Border
            {
                Width = 192,
                Height = 3.5,
                CornerRadius = new CornerRadius(1.75),
                Background = new SolidColorBrush(Color.FromRgb(42, 58, 53)),
                Cursor = Cursors.Hand,
                ToolTip = "Click to Seek Timeline Position",
                Child = _progressFillBorder
            };
            _progressTrackBorder.MouseLeftButtonDown += OnProgressBarMouseLeftButtonDown;

            _timelineText = new TextBlock
            {
                Text = "0:00 / 0:00",
                FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(145, 166, 158)),
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

            // 9 Action Controls
            _favBtn = CreateVectorCircleButton(GeometryHeartOutline, "Save / Remove Track in AuraWave Favourites", 34, false, true, out _favIconPath);
            _favBtn.Click += OnFavouriteButtonClick;

            _prevBtn = CreateVectorCircleButton(GeometryPrevTrack, "Previous Track", 35, true, false, out _);
            _prevBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                _mediaService.SendPreviousTrack();
            };

            _playPauseIconPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(GeometryPauseBars),
                Fill = new SolidColorBrush(Color.FromRgb(6, 20, 16)),
                Width = 15,
                Height = 15,
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
                    Color = Color.FromRgb(56, 232, 166),
                    BlurRadius = 18,
                    ShadowDepth = 0,
                    Opacity = 0.55
                }
            };
            _playPauseBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                _mediaService.SendPlayPause();
            };

            _nextBtn = CreateVectorCircleButton(GeometryNextTrack, "Next Track", 35, true, false, out _);
            _nextBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                _mediaService.SendNextTrack();
            };

            _speakerIconPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(GeometrySpeakerOn),
                Stroke = new SolidColorBrush(Color.FromRgb(226, 238, 233)),
                StrokeThickness = 1.35,
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
                Background = new SolidColorBrush(Color.FromArgb(195, 13, 25, 22)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(65, 145, 166, 158)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 0, 10, 0),
                Margin = new Thickness(5, 0, 5, 0),
                Child = volStack
            };

            _queueItemsStack = new StackPanel();
            _queuePopup = CreateFavouritesQueuePopup();
            _queueListBtn = CreateVectorCircleButton(GeometryQueueList, "Open Saved Favourites & Active Session Panel", 33, false, true, out _);
            _queueListBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                RefreshQueuePopupContent();
                _queuePopup.IsOpen = !_queuePopup.IsOpen;
            };

            _settingsBtn = CreateVectorCircleButton(GeometryGearPetal, "Open AuraWave Settings Studio (Alt+S)", 33, false, true, out _);
            _settingsBtn.Click += (_, _) =>
            {
                RegisterUserInteraction();
                OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
            };

            _pinBtn = CreateVectorCircleButton(GeometryPushPin, "Toggle Pin (Keep music bar permanently visible)", 33, true, false, out _pinIconPath);
            _pinBtn.Click += OnPinButtonClick;

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
                Margin = new Thickness(22, 5, 16, 5),
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

            // Inner ambient radial illumination layer matching image_737a35.jpg
            var innerCompositeGrid = new Grid();
            var ambientPlayGlow = new Ellipse
            {
                Width = 220,
                Height = 76,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 175, 0),
                IsHitTestVisible = false,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(52, 56, 232, 166),
                    Color.FromArgb(0, 8, 20, 17))
            };
            var ambientSpectrumGlow = new Ellipse
            {
                Width = 240,
                Height = 70,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(-60, 0, 0, -10),
                IsHitTestVisible = false,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(40, 56, 232, 166),
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
            innerSurfaceBrush.GradientStops.Add(new GradientStop(Color.FromArgb(248, 8, 17, 15), 0.0));
            innerSurfaceBrush.GradientStops.Add(new GradientStop(Color.FromArgb(248, 12, 29, 24), 0.50));
            innerSurfaceBrush.GradientStops.Add(new GradientStop(Color.FromArgb(248, 7, 15, 13), 1.0));

            var innerBorderBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            innerBorderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(165, 140, 245, 205), 0.0));
            innerBorderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(50, 56, 232, 166), 0.5));
            innerBorderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(120, 56, 232, 166), 1.0));

            _innerCapsuleBorder = new Border
            {
                CornerRadius = new CornerRadius(36),
                Background = innerSurfaceBrush,
                BorderBrush = innerBorderBrush,
                BorderThickness = new Thickness(1.1),
                Child = innerCompositeGrid
            };

            // Outer 3D Frosted Glass Tube Rim with top & side specular reflection
            var outerRimBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            outerRimBrush.GradientStops.Add(new GradientStop(Color.FromArgb(200, 210, 250, 235), 0.0));
            outerRimBrush.GradientStops.Add(new GradientStop(Color.FromArgb(95, 90, 195, 160), 0.35));
            outerRimBrush.GradientStops.Add(new GradientStop(Color.FromArgb(75, 65, 165, 135), 0.70));
            outerRimBrush.GradientStops.Add(new GradientStop(Color.FromArgb(175, 185, 245, 225), 1.0));

            var outerGlassSurface = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            outerGlassSurface.GradientStops.Add(new GradientStop(Color.FromArgb(115, 36, 68, 58), 0.0));
            outerGlassSurface.GradientStops.Add(new GradientStop(Color.FromArgb(85, 14, 30, 25), 0.5));
            outerGlassSurface.GradientStops.Add(new GradientStop(Color.FromArgb(110, 24, 52, 44), 1.0));

            _outerGlassBezel = new Border
            {
                Margin = new Thickness(6),
                Padding = new Thickness(4.5),
                CornerRadius = new CornerRadius(41),
                Background = outerGlassSurface,
                BorderBrush = outerRimBrush,
                BorderThickness = new Thickness(1.3),
                Child = _innerCapsuleBorder,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(10, 75, 56),
                    BlurRadius = 20,
                    ShadowDepth = 0,
                    Opacity = 0.50
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
            _titleText.Text = string.IsNullOrWhiteSpace(snap.Title) ? "Ready for Music" : snap.Title;
            _artistText.Text = string.IsNullOrWhiteSpace(snap.Artist) ? "Play any song" : snap.Artist;
            _timelineText.Text = snap.FormattedTimeline;

            // Decode real album artwork when exposed by Windows GSMTC
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
            Color cPrim = ThemeManager.ParseColor(pal.PrimaryHex, Color.FromRgb(56, 232, 166));

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
                : new SolidColorBrush(Color.FromRgb(145, 166, 158));
            _pinBtn.BorderBrush = pinned
                ? new SolidColorBrush(Color.FromArgb(145, 56, 232, 166))
                : new SolidColorBrush(Color.FromArgb(55, 145, 166, 158));
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
                ? new SolidColorBrush(Color.FromRgb(56, 232, 166))
                : Brushes.Transparent;
            _favIconPath.Stroke = isFav
                ? new SolidColorBrush(Color.FromRgb(56, 232, 166))
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

                private bool _isCircularArtMode = true;
        private RectangleGeometry _artClipGeometry;

        private Grid CreateAlbumArtworkBox()
        {
            _artClipGeometry = new RectangleGeometry(new Rect(0, 0, 54, 54), 27, 27);

            var container = new Grid
            {
                Width = 54,
                Height = 54,
                Margin = new Thickness(2, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = _discRotateTransform,
                Cursor = Cursors.Hand,
                ToolTip = "Click album art to toggle between Rounded Squircle and Full Circle style",
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 12,
                    ShadowDepth = 2,
                    Opacity = 0.65
                }
            };

            var clippedSurface = new Grid
            {
                Width = 54,
                Height = 54,
                Clip = _artClipGeometry
            };

            var imageRect = new Rectangle
            {
                Width = 54,
                Height = 54,
                Fill = _artworkImageBrush
            };

            // Soft inner vignette so edges blend into the dark capsule without a harsh square border
            var vignetteBrush = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.72,
                RadiusY = 0.72
            };
            vignetteBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.55));
            vignetteBrush.GradientStops.Add(new GradientStop(Color.FromArgb(95, 6, 14, 12), 1.0));

            var vignetteRect = new Rectangle
            {
                Width = 54,
                Height = 54,
                Fill = vignetteBrush,
                IsHitTestVisible = false
            };

            clippedSurface.Children.Add(imageRect);
            clippedSurface.Children.Add(vignetteRect);
            container.Children.Add(clippedSurface);

            container.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                _isCircularArtMode = !_isCircularArtMode;
                double r = _isCircularArtMode ? 27.0 : 15.0;
                _artClipGeometry.RadiusX = r;
                _artClipGeometry.RadiusY = r;
            };

            return container;
        }

        /// <summary>
        /// Synthesizes a 112x112 photographic sunset-clouds & twilight city-lights bitmap
        /// matching image_737a35.jpg when no live session thumbnail is present.
        /// </summary>
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

                    // Atmospheric cloud & sunset color gradient
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
                        // Distant twilight city & water horizon
                        double t = (ny - 0.68) / 0.32;
                        double bokeh = ((x * 37 + y * 13) % 29 == 0 && t < 0.55) ? 65.0 : 0.0;
                        r = 38 * (1.0 - t * 0.6) + bokeh;
                        g = 36 * (1.0 - t * 0.6) + bokeh * 0.8;
                        b = 48 * (1.0 - t * 0.5) + bokeh * 0.5;
                    }

                    // Soft natural silhouette of person looking at horizon
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
                Width = 14,
                Height = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };

            badge.Children.Add(new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(Color.FromRgb(56, 232, 166))
            });

            badge.Children.Add(new System.Windows.Shapes.Path
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
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 232, 166)),
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
                Foreground = new SolidColorBrush(Color.FromRgb(56, 232, 166)),
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
                Foreground = new SolidColorBrush(Color.FromRgb(56, 232, 166)),
                Margin = new Thickness(0, 0, 0, 6)
            });

            var favs = _favouritesService.GetAll();
            if (favs.Count == 0)
            {
                _queueItemsStack.Children.Add(new TextBlock
                {
                    Text = "No saved tracks yet. Click the ♥ button on the music bar to save tracks here.",
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(145, 166, 158)),
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
                    Foreground = new SolidColorBrush(Color.FromRgb(145, 166, 158)),
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

        private static Button CreateVectorCircleButton(string pathMarkup, string tooltip, double size, bool filled, bool stroked, out System.Windows.Shapes.Path iconPath)
        {
            iconPath = new System.Windows.Shapes.Path
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
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 145, 166, 158)),
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
                Color.FromRgb(72, 245, 180),
                Color.FromRgb(42, 218, 150),
                new Point(0, 0),
                new Point(0, 1));
            fillGrad.Freeze();

            border.SetValue(Border.BackgroundProperty, fillGrad);
            border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(120, 252, 202)));
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