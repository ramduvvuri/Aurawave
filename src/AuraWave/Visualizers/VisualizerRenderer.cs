using System;
using System.Windows;
using System.Windows.Media;
using AuraWave.Configuration;
using AuraWave.Models;
using AuraWave.Themes;

namespace AuraWave.Visualizers
{
    public sealed class VisualizerRenderer
    {
        private double _phase;

        public void Render(DrawingContext dc, double width, double height, AudioAnalysisFrame frame, AuraWaveConfig cfg)
        {
            if (width <= 8 || height <= 8) return;

            var palette = ThemeManager.GetActivePalette(cfg);
            Color cPrimary = ThemeManager.ParseColor(palette.PrimaryHex, Color.FromRgb(0, 242, 254));
            Color cSecondary = ThemeManager.ParseColor(palette.SecondaryHex, Color.FromRgb(79, 172, 254));
            Color cAccent = ThemeManager.ParseColor(palette.AccentHex, Color.FromRgb(155, 81, 224));
            Color cPeak = ThemeManager.ParseColor(palette.PeakHex, Colors.White);

            _phase += 0.045 * cfg.AnimationIntensity * (1.0 + frame.BassEnergy * 1.5);

            switch (cfg.SelectedVisualizer)
            {
                case VisualizerType.LiquidSpectrum:
                    RenderLiquidSpectrum(dc, width, height, frame, cfg, cPrimary, cSecondary, cAccent, cPeak);
                    break;
                case VisualizerType.AuroraWave:
                    RenderAuroraWave(dc, width, height, frame, cfg, cPrimary, cSecondary, cAccent);
                    break;
                case VisualizerType.NeonParticles:
                    RenderNeonParticles(dc, width, height, frame, cfg, cPrimary, cSecondary, cAccent);
                    break;
                case VisualizerType.LiquidLava:
                    RenderLiquidLava(dc, width, height, frame, cfg, cPrimary, cSecondary, cAccent);
                    break;
                case VisualizerType.MinimalPulse:
                    RenderMinimalPulse(dc, width, height, frame, cfg, cPrimary, cSecondary);
                    break;
                case VisualizerType.RetroEqualizer:
                    RenderRetroEqualizer(dc, width, height, frame, cfg, cPrimary, cSecondary, cAccent);
                    break;
                case VisualizerType.GhostMonochrome:
                    RenderLiquidSpectrum(dc, width, height, frame, cfg, Color.FromRgb(241, 245, 249), Color.FromRgb(148, 163, 184), Color.FromRgb(203, 213, 225), Colors.White);
                    break;
                case VisualizerType.RadialSpectrum:
                    RenderRadialSpectrum(dc, width, height, frame, cfg, cPrimary, cSecondary, cAccent);
                    break;
                case VisualizerType.Synthwave:
                    RenderSynthwave(dc, width, height, frame, cfg, cPrimary, cSecondary, cAccent);
                    break;
            }
        }

        private void RenderLiquidSpectrum(DrawingContext dc, double w, double h, AudioAnalysisFrame frame, AuraWaveConfig cfg, Color c1, Color c2, Color c3, Color cPeak)
        {
            var bands = frame.FrequencyBands;
            int n = bands.Length > 0 ? bands.Length : 48;
            double usableH = cfg.EnableReflection ? h * 0.72 : h * 0.88;
            double baselineY = cfg.EnableReflection ? h * 0.76 : h * 0.94;
            double step = w / n;
            double barW = Math.Max(2.0, step * 0.66);

            var mainBrush = new LinearGradientBrush(c1, c2, new Point(0, 1), new Point(0, 0));
            mainBrush.Freeze();
            var reflectBrush = new SolidColorBrush(Color.FromArgb((byte)(70 * cfg.GlowIntensity), c2.R, c2.G, c2.B));
            reflectBrush.Freeze();
            var peakBrush = new SolidColorBrush(cPeak);
            peakBrush.Freeze();

            for (int i = 0; i < n; i++)
            {
                int srcIdx = cfg.SymmetricSpectrum
                    ? Math.Abs((i - n / 2) * 2) % n
                    : i;

                float val = bands.Length > srcIdx ? bands[srcIdx] : 0.03f;
                double barH = Math.Clamp(val * cfg.VisualizerIntensity * usableH, 3.0, usableH);
                double x = i * step + (step - barW) * 0.5;
                double y = baselineY - barH;
                double radius = Math.Min(barW * 0.5, 4.0);

                dc.DrawRoundedRectangle(mainBrush, null, new Rect(x, y, barW, barH), radius, radius);

                if (cfg.ShowPeakIndicators && frame.PeakHoldBands.Length > srcIdx)
                {
                    double peakH = Math.Clamp(frame.PeakHoldBands[srcIdx] * cfg.VisualizerIntensity * usableH, 3.0, usableH);
                    double py = Math.Max(2.0, baselineY - peakH - 4.0);
                    dc.DrawRoundedRectangle(peakBrush, null, new Rect(x, py, barW, 2.5), 1.0, 1.0);
                }

                if (cfg.EnableReflection)
                {
                    double refH = barH * 0.28;
                    dc.DrawRoundedRectangle(reflectBrush, null, new Rect(x, baselineY + 3.0, barW, refH), radius, radius);
                }
            }
        }

        private void RenderAuroraWave(DrawingContext dc, double w, double h, AudioAnalysisFrame frame, AuraWaveConfig cfg, Color c1, Color c2, Color c3)
        {
            double midY = h * 0.55;
            var samples = frame.WaveformSamples;
            int count = samples.Length > 0 ? samples.Length : 64;

            DrawWaveRibbon(dc, w, midY, h * 0.38, samples, count, c1, cfg.WaveformThickness, 0.0, 1.0);
            if (cfg.EnableSecondaryWaveform)
            {
                DrawWaveRibbon(dc, w, midY, h * 0.28, samples, count, c2, Math.Max(1.5, cfg.WaveformThickness * 0.75), 0.9, 0.65);
                DrawWaveRibbon(dc, w, midY, h * 0.20, samples, count, c3, Math.Max(1.0, cfg.WaveformThickness * 0.55), 1.8, 0.45);
            }
        }

        private void DrawWaveRibbon(DrawingContext dc, double w, double midY, double amp, float[] samples, int count, Color color, double thickness, double phaseOffset, double opacity)
        {
            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                for (int i = 0; i < count; i++)
                {
                    double t = (double)i / (count - 1);
                    double x = t * w;
                    float s = samples.Length > i ? samples[i] : 0f;
                    double env = Math.Sin(t * Math.PI);
                    double wave = (s * 0.75 + Math.Sin(t * 8.0 + _phase + phaseOffset) * 0.15) * amp * env;
                    Point pt = new Point(x, midY + wave);
                    if (i == 0) ctx.BeginFigure(pt, false, false);
                    else ctx.LineTo(pt, true, true);
                }
            }
            geom.Freeze();
            var brush = new SolidColorBrush(Color.FromArgb((byte)(255 * opacity), color.R, color.G, color.B));
            brush.Freeze();
            var pen = new Pen(brush, thickness);
            pen.Freeze();
            dc.DrawGeometry(null, pen, geom);
        }

        private void RenderNeonParticles(DrawingContext dc, double w, double h, AudioAnalysisFrame frame, AuraWaveConfig cfg, Color c1, Color c2, Color c3)
        {
            int count = Math.Clamp(cfg.ParticleCount, 12, 120);
            var bands = frame.FrequencyBands;
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / count;
                float energy = bands.Length > 0 ? bands[i % bands.Length] : frame.Rms;
                double x = ((t * w) + Math.Sin(_phase * 0.5 + i) * 18.0 + w) % w;
                double y = h * 0.85 - (energy * cfg.VisualizerIntensity * h * 0.72) - ((Math.Cos(_phase + i * 1.7) + 1.0) * 10.0);
                y = Math.Clamp(y, 6.0, h - 6.0);
                double r = 2.0 + energy * 7.5;

                Color col = (i % 3 == 0) ? c1 : ((i % 3 == 1) ? c2 : c3);
                var b = new SolidColorBrush(Color.FromArgb(215, col.R, col.G, col.B));
                b.Freeze();
                dc.DrawEllipse(b, null, new Point(x, y), r, r);
            }
        }

        private void RenderLiquidLava(DrawingContext dc, double w, double h, AudioAnalysisFrame frame, AuraWaveConfig cfg, Color c1, Color c2, Color c3)
        {
            int blobs = 7;
            for (int i = 0; i < blobs; i++)
            {
                double t = (double)(i + 0.5) / blobs;
                float bandVal = frame.FrequencyBands.Length > i * 4 ? frame.FrequencyBands[i * 4] : frame.BassEnergy;
                double cx = t * w + Math.Sin(_phase * 0.6 + i * 1.3) * (w * 0.05);
                double cy = h * 0.68 - bandVal * h * 0.35;
                double rx = (w / blobs) * (0.45 + bandVal * 0.45);
                double ry = h * (0.18 + bandVal * 0.28);

                Color c = (i % 2 == 0) ? c1 : c2;
                var grad = new RadialGradientBrush(
                    Color.FromArgb(200, c.R, c.G, c.B),
                    Color.FromArgb(0, c3.R, c3.G, c3.B));
                grad.Freeze();
                dc.DrawEllipse(grad, null, new Point(cx, cy), rx, ry);
            }
        }

        private void RenderMinimalPulse(DrawingContext dc, double w, double h, AudioAnalysisFrame frame, AuraWaveConfig cfg, Color c1, Color c2)
        {
            double midY = h * 0.5;
            var geom = new StreamGeometry();
            int pts = 64;
            using (var ctx = geom.Open())
            {
                for (int i = 0; i < pts; i++)
                {
                    double t = (double)i / (pts - 1);
                    double x = t * w;
                    float val = frame.FrequencyBands.Length > 0 ? frame.FrequencyBands[(i * frame.FrequencyBands.Length) / pts] : frame.Rms;
                    double dy = Math.Sin(t * Math.PI) * val * h * 0.35 * (i % 2 == 0 ? 1 : -1);
                    Point p = new Point(x, midY + dy);
                    if (i == 0) ctx.BeginFigure(p, false, false);
                    else ctx.LineTo(p, true, true);
                }
            }
            geom.Freeze();
            var brush = new LinearGradientBrush(c1, c2, new Point(0, 0), new Point(1, 0));
            brush.Freeze();
            var pen = new Pen(brush, cfg.WaveformThickness);
            pen.Freeze();
            dc.DrawGeometry(null, pen, geom);
        }

        private void RenderRetroEqualizer(DrawingContext dc, double w, double h, AudioAnalysisFrame frame, AuraWaveConfig cfg, Color c1, Color c2, Color c3)
        {
            int cols = 28;
            int maxSegments = 12;
            double colStep = w / cols;
            double segW = Math.Max(3.0, colStep * 0.72);
            double segH = (h * 0.85) / maxSegments;

            var bLow = new SolidColorBrush(c1); bLow.Freeze();
            var bMid = new SolidColorBrush(c2); bMid.Freeze();
            var bHigh = new SolidColorBrush(c3); bHigh.Freeze();

            for (int c = 0; c < cols; c++)
            {
                float val = frame.FrequencyBands.Length > 0
                    ? frame.FrequencyBands[(c * frame.FrequencyBands.Length) / cols]
                    : 0f;
                int lit = Math.Clamp((int)Math.Round(val * cfg.VisualizerIntensity * maxSegments), 1, maxSegments);
                double x = c * colStep + (colStep - segW) * 0.5;

                for (int s = 0; s < lit; s++)
                {
                    double y = h * 0.92 - (s + 1) * segH;
                    Brush b = s < 6 ? bLow : (s < 9 ? bMid : bHigh);
                    dc.DrawRectangle(b, null, new Rect(x, y, segW, Math.Max(2.0, segH - 2.0)));
                }
            }
        }

        private void RenderRadialSpectrum(DrawingContext dc, double w, double h, AudioAnalysisFrame frame, AuraWaveConfig cfg, Color c1, Color c2, Color c3)
        {
            Point center = new Point(w * 0.5, h * 0.5);
            double baseR = Math.Min(w, h) * (0.22 + frame.BassEnergy * 0.06);
            int spokes = 48;

            var penBrush = new LinearGradientBrush(c1, c3, new Point(0, 0), new Point(1, 1));
            penBrush.Freeze();
            var pen = new Pen(penBrush, Math.Max(2.0, cfg.WaveformThickness));
            pen.Freeze();

            for (int i = 0; i < spokes; i++)
            {
                double angle = (2.0 * Math.PI * i / spokes) + _phase * 0.15;
                float val = frame.FrequencyBands.Length > 0 ? frame.FrequencyBands[i % frame.FrequencyBands.Length] : 0.05f;
                double len = 4.0 + val * cfg.VisualizerIntensity * Math.Min(w, h) * 0.24;

                Point p1 = new Point(center.X + Math.Cos(angle) * baseR, center.Y + Math.Sin(angle) * baseR);
                Point p2 = new Point(center.X + Math.Cos(angle) * (baseR + len), center.Y + Math.Sin(angle) * (baseR + len));
                dc.DrawLine(pen, p1, p2);
            }
        }

        private void RenderSynthwave(DrawingContext dc, double w, double h, AudioAnalysisFrame frame, AuraWaveConfig cfg, Color c1, Color c2, Color c3)
        {
            double horizonY = h * 0.62;
            double sunR = Math.Min(w, h) * (0.24 + frame.BassEnergy * 0.05);
            var sunGrad = new LinearGradientBrush(c3, c1, new Point(0, 0), new Point(0, 1));
            sunGrad.Freeze();
            dc.DrawEllipse(sunGrad, null, new Point(w * 0.5, horizonY), sunR, sunR);

            RenderLiquidSpectrum(dc, w, h, frame, cfg, c1, c2, c3, Colors.White);
        }
    }
}