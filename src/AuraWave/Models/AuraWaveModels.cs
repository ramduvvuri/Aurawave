using System;

namespace AuraWave.Models
{
    public enum VisualizerType
    {
        LiquidSpectrum = 0,
        AuroraWave = 1,
        NeonParticles = 2,
        LiquidLava = 3,
        MinimalPulse = 4,
        RetroEqualizer = 5,
        GhostMonochrome = 6,
        RadialSpectrum = 7,
        Synthwave = 8
    }

    public enum ThemePreset
    {
        ArcticPulse = 0,
        VioletEther = 1,
        Aurora = 2,
        Afterglow = 3,
        Ultraviolet = 4,
        Midnight = 5,
        Monochrome = 6,
        Custom = 7
    }

    public enum PresentationMode
    {
        AlwaysOnTopOverlay = 0,
        NormalDesktopWindow = 1,
        DesktopBackgroundWorkerW = 2
    }

    public enum PlacementPreset
    {
        BottomCenter = 0,
        BottomLeft = 1,
        BottomRight = 2,
        TopCenter = 3,
        TopLeft = 4,
        TopRight = 5,
        Center = 6,
        FullWidthAmbient = 7,
        CustomPosition = 8
    }

    public enum EditableComponent
    {
        Waveform = 0,
        NowPlaying = 1
    }

    public enum PlaybackState
    {
        Closed = 0,
        Opened = 1,
        Changing = 2,
        Stopped = 3,
        Playing = 4,
        Paused = 5
    }

    public sealed class ThemePalette
    {
        public string Name { get; set; } = "Arctic Pulse";
        public string PrimaryHex { get; set; } = "#00F2FE";
        public string SecondaryHex { get; set; } = "#4FACFE";
        public string AccentHex { get; set; } = "#9B51E0";
        public string PeakHex { get; set; } = "#E0F7FA";
        public string TextHex { get; set; } = "#F8FAFC";
        public string GlowHex { get; set; } = "#00E5FF";
    }

    public sealed class AudioAnalysisFrame
    {
        public float Rms { get; set; }
        public float Peak { get; set; }
        public float BassEnergy { get; set; }
        public float MidrangeEnergy { get; set; }
        public float TrebleEnergy { get; set; }
        public float[] FrequencyBands { get; set; } = Array.Empty<float>();
        public float[] PeakHoldBands { get; set; } = Array.Empty<float>();
        public float[] WaveformSamples { get; set; } = Array.Empty<float>();
        public bool IsAudioActive { get; set; }
        public bool IsBeatDetected { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class MediaSessionSnapshot
    {
        public string SourceAppId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string AlbumTitle { get; set; } = string.Empty;
        public byte[]? ArtworkBytes { get; set; }
        public PlaybackState PlaybackStatus { get; set; } = PlaybackState.Stopped;
        public TimeSpan Duration { get; set; } = TimeSpan.Zero;
        public TimeSpan Position { get; set; } = TimeSpan.Zero;
        public bool SupportsPlayPause { get; set; } = true;
        public bool SupportsNext { get; set; } = true;
        public bool SupportsPrevious { get; set; } = true;
        public bool HasActiveSession => !string.IsNullOrWhiteSpace(Title) || PlaybackStatus == PlaybackState.Playing;

        public double ProgressRatio =>
            Duration.TotalSeconds > 0.5
                ? Math.Clamp(Position.TotalSeconds / Duration.TotalSeconds, 0.0, 1.0)
                : 0.0;

        public string FormattedTimeline =>
            $"{FormatTime(Position)} / {FormatTime(Duration)}";

        public static string FormatTime(TimeSpan ts)
        {
            if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
            return ts.TotalHours >= 1.0
                ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
    }

    public sealed class WidgetGeometry
    {
        public double Left { get; set; } = 360;
        public double Top { get; set; } = 820;
        public double Width { get; set; } = 760;
        public double Height { get; set; } = 140;
        public string MonitorDeviceName { get; set; } = string.Empty;

        public WidgetGeometry Clone() => (WidgetGeometry)MemberwiseClone();

        public void ClampToMonitor(double workLeft, double workTop, double workWidth, double workHeight, double minW, double maxW, double minH, double maxH)
        {
            Width = Math.Clamp(Width, minW, Math.Min(maxW, Math.Max(minW, workWidth)));
            Height = Math.Clamp(Height, minH, Math.Min(maxH, Math.Max(minH, workHeight)));
            Left = Math.Clamp(Left, workLeft, Math.Max(workLeft, workLeft + workWidth - Width));
            Top = Math.Clamp(Top, workTop, Math.Max(workTop, workTop + workHeight - Height));
        }
    }
}