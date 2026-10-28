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
        EmeraldCapsule = 0,
        ArcticPulse = 1,
        VioletEther = 2,
        Aurora = 3,
        Afterglow = 4,
        Ultraviolet = 5,
        Midnight = 6,
        Monochrome = 7,
        Custom = 8
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
        NowPlaying = 0,
        Waveform = 1
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
        public string Name { get; set; } = "Emerald Capsule";
        public string PrimaryHex { get; set; } = "#2EE59D";
        public string SecondaryHex { get; set; } = "#42E5B0";
        public string AccentHex { get; set; } = "#087F62";
        public string PeakHex { get; set; } = "#A7F3D0";
        public string TextHex { get; set; } = "#F4F9F7";
        public string SecondaryTextHex { get; set; } = "#8FA69E";
        public string SurfaceHex { get; set; } = "#081411";
        public string GlowHex { get; set; } = "#2EE59D";
    }

    public sealed class FavouriteTrackItem
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string SourceApp { get; set; } = string.Empty;
        public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
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

        public string TrackKey => $"{Artist?.Trim() ?? ""}|{Title?.Trim() ?? ""}";

        public double ProgressRatio =>
            Duration.TotalSeconds > 0.5
                ? Math.Clamp(Position.TotalSeconds / Duration.TotalSeconds, 0.0, 1.0)
                : 0.28;

        public string FormattedTimeline =>
            Duration.TotalSeconds > 0.5
                ? $"{FormatTime(Position)} / {FormatTime(Duration)}"
                : "5:42 / 20:14";

        public static string FormatTime(TimeSpan ts)
        {
            if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
            return ts.TotalHours >= 1.0
                ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{(int)ts.Minutes}:{ts.Seconds:D2}";
        }
    }

    public sealed class WidgetGeometry
    {
        public double Left { get; set; } = -1;
        public double Top { get; set; } = -1;
        public double Width { get; set; } = 960;
        public double Height { get; set; } = 88;
        public string MonitorDeviceName { get; set; } = string.Empty;

        public WidgetGeometry Clone() => (WidgetGeometry)MemberwiseClone();

        public void ClampToMonitor(double workLeft, double workTop, double workWidth, double workHeight, double minW, double maxW, double minH, double maxH)
        {
            Width = Math.Clamp(Width, minW, Math.Min(maxW, Math.Max(minW, workWidth - 24)));
            Height = Math.Clamp(Height, minH, Math.Min(maxH, Math.Max(minH, workHeight - 24)));

            if (Left < workLeft || Left + Width > workLeft + workWidth || Left == -1)
            {
                Left = workLeft + Math.Round((workWidth - Width) * 0.5);
            }
            if (Top < workTop || Top + Height > workTop + workHeight || Top == -1)
            {
                Top = workTop + workHeight - Height - 14;
            }

            Left = Math.Clamp(Left, workLeft, Math.Max(workLeft, workLeft + workWidth - Width));
            Top = Math.Clamp(Top, workTop, Math.Max(workTop, workTop + workHeight - Height));
        }

        public static WidgetGeometry CalculateBottomCenterBar(double workLeft, double workTop, double workWidth, double workHeight, double desiredWidth = 960, double desiredHeight = 88, double taskbarClearance = 14)
        {
            double w = Math.Clamp(desiredWidth, 640, Math.Max(640, workWidth - 40));
            double h = Math.Clamp(desiredHeight, 80, 120);
            double left = workLeft + Math.Round((workWidth - w) * 0.5);
            double top = workTop + workHeight - h - taskbarClearance;
            return new WidgetGeometry
            {
                Left = left,
                Top = top,
                Width = w,
                Height = h
            };
        }
    }
}