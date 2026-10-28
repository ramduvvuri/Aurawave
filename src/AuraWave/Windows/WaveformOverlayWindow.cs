using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AuraWave.Configuration;
using AuraWave.Models;
using AuraWave.Visualizers;

namespace AuraWave.Windows
{
    public sealed class WaveformCanvasControl : FrameworkElement
    {
        private readonly VisualizerRenderer _renderer = new VisualizerRenderer();
        public AudioAnalysisFrame CurrentFrame { get; set; } = new AudioAnalysisFrame();
        public AuraWaveConfig? Config { get; set; }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (Config == null) return;
            _renderer.Render(drawingContext, ActualWidth, ActualHeight, CurrentFrame, Config);
        }
    }

    public sealed class WaveformOverlayWindow : Window
    {
        private readonly ConfigurationService _configService;
        private readonly WaveformCanvasControl _canvasControl;
        private readonly Border _editOverlayBorder;
        private readonly TextBlock _editBannerText;
        private bool _isEditMode;

        public event EventHandler? EditModeSelected;
        public event EventHandler? ExitEditModeRequested;

        public WaveformOverlayWindow(ConfigurationService configService)
        {
            _configService = configService;

            Title = "AuraWave Waveform Overlay";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;

            var rootGrid = new Grid { Background = Brushes.Transparent };

            _canvasControl = new WaveformCanvasControl
            {
                Config = _configService.Current
            };
            rootGrid.Children.Add(_canvasControl);

            _editBannerText = new TextBlock
            {
                Text = "WAVEFORM EDIT MODE â€” Drag to move | Ctrl+Alt+Plus/Minus to resize | Click to select",
                Foreground = new SolidColorBrush(Color.FromRgb(0, 242, 254)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(10, 6, 10, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            var exitBtn = new Button
            {
                Content = "Exit Edit Mode",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 10, 0),
                Padding = new Thickness(8, 2, 8, 2),
                Background = new SolidColorBrush(Color.FromArgb(200, 15, 23, 42)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 242, 254)),
                Cursor = Cursors.Hand
            };
            exitBtn.Click += (_, _) => ExitEditModeRequested?.Invoke(this, EventArgs.Empty);

            var editGrid = new Grid();
            editGrid.Children.Add(_editBannerText);
            editGrid.Children.Add(exitBtn);

            _editOverlayBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 242, 254)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(35, 15, 23, 42)),
                Visibility = Visibility.Collapsed,
                Child = editGrid
            };
            rootGrid.Children.Add(_editOverlayBorder);

            Content = rootGrid;

            ApplyGeometryFromConfig();

            SourceInitialized += (_, _) => SyncNativeStyles();
            MouseLeftButtonDown += OnWindowMouseDown;
            LocationChanged += (_, _) => SaveGeometryIfReady();
            SizeChanged += (_, _) => SaveGeometryIfReady();
        }

        public void UpdateAudioFrame(AudioAnalysisFrame frame)
        {
            _canvasControl.CurrentFrame = frame;
            _canvasControl.Config = _configService.Current;
            _canvasControl.InvalidateVisual();
        }

        public void SetEditMode(bool enabled)
        {
            _isEditMode = enabled;
            _editOverlayBorder.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            ResizeMode = enabled ? ResizeMode.CanResizeWithGrip : ResizeMode.NoResize;
            SyncNativeStyles();
        }

        public void ApplyPlacementPreset(PlacementPreset preset)
        {
            double screenW = SystemParameters.WorkArea.Width;
            double screenH = SystemParameters.WorkArea.Height;
            double leftOff = SystemParameters.WorkArea.Left;
            double topOff = SystemParameters.WorkArea.Top;

            switch (preset)
            {
                case PlacementPreset.BottomCenter:
                    Width = Math.Min(780, screenW * 0.6);
                    Height = 140;
                    Left = leftOff + (screenW - Width) * 0.5;
                    Top = topOff + screenH - Height - 18;
                    break;
                case PlacementPreset.BottomLeft:
                    Left = leftOff + 24;
                    Top = topOff + screenH - Height - 18;
                    break;
                case PlacementPreset.BottomRight:
                    Left = leftOff + screenW - Width - 24;
                    Top = topOff + screenH - Height - 18;
                    break;
                case PlacementPreset.TopCenter:
                    Left = leftOff + (screenW - Width) * 0.5;
                    Top = topOff + 18;
                    break;
                case PlacementPreset.TopLeft:
                    Left = leftOff + 24;
                    Top = topOff + 18;
                    break;
                case PlacementPreset.TopRight:
                    Left = leftOff + screenW - Width - 24;
                    Top = topOff + 18;
                    break;
                case PlacementPreset.Center:
                    Left = leftOff + (screenW - Width) * 0.5;
                    Top = topOff + (screenH - Height) * 0.5;
                    break;
                case PlacementPreset.FullWidthAmbient:
                    Width = screenW - 32;
                    Height = 150;
                    Left = leftOff + 16;
                    Top = topOff + screenH - Height - 8;
                    break;
            }
            SaveGeometryIfReady();
        }

        public void ApplyGeometryFromConfig()
        {
            var g = _configService.Current.WaveformGeometry;
            g.ClampToMonitor(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight,
                240, 3840, 60, 900);

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

        private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isEditMode && e.ChangedButton == MouseButton.Left)
            {
                EditModeSelected?.Invoke(this, EventArgs.Empty);
                try { DragMove(); } catch { }
            }
        }

        private void SaveGeometryIfReady()
        {
            if (!IsLoaded) return;
            var g = _configService.Current.WaveformGeometry;
            g.Left = Left;
            g.Top = Top;
            g.Width = Width;
            g.Height = Height;
        }
    }
}