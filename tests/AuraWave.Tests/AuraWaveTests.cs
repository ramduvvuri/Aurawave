using System;
using System.IO;
using AuraWave.Configuration;
using AuraWave.DSP;
using AuraWave.Media;
using AuraWave.Models;
using AuraWave.Themes;
using Xunit;

namespace AuraWave.Tests
{
    public class AuraWaveTests
    {
        [Fact]
        public void Test01_FftSineWave_ProducesNormalizedFrequencyPeak()
        {
            var dsp = new DspAnalyzer(1024, 48);
            float[] samples = new float[1024];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = 0.8f * (float)Math.Sin(2.0 * Math.PI * 440.0 * i / 48000.0);

            var frame = dsp.ProcessSamples(samples, 48000, 48, 1.0, 0.2, 0.2, 1.0, 0.001, 0.01, 0.004, 0, 5000, DateTime.UtcNow);
            Assert.InRange(frame.Rms, 0.4f, 0.9f);
            Assert.InRange(frame.Peak, 0.7f, 1.0f);
            Assert.True(frame.MidrangeEnergy > frame.TrebleEnergy);
        }

        [Fact]
        public void Test02_NoiseFloorSuppression_RejectsBackgroundNoise()
        {
            var dsp = new DspAnalyzer(1024, 32);
            float[] samples = new float[1024];
            for (int i = 0; i < samples.Length; i++) samples[i] = 0.0004f;

            var frame = dsp.ProcessSamples(samples, 48000, 32, 1.0, 0.5, 0.2, 1.0, 0.002, 0.01, 0.004, 100, 5000, DateTime.UtcNow);
            Assert.Equal(0f, frame.Rms);
            Assert.False(frame.IsAudioActive);
        }

        [Fact]
        public void Test03_HysteresisStateMachine_RespectsAttackAndReleaseHoldTimers()
        {
            var dsp = new DspAnalyzer(1024, 32);
            float[] loud = new float[1024];
            for (int i = 0; i < loud.Length; i++) loud[i] = 0.25f;
            float[] silent = new float[1024];

            DateTime t0 = DateTime.UtcNow;
            var f1 = dsp.ProcessSamples(loud, 48000, 32, 1.0, 0.5, 0.2, 1.0, 0.001, 0.01, 0.004, 200, 5000, t0);
            Assert.False(f1.IsAudioActive);

            var f2 = dsp.ProcessSamples(loud, 48000, 32, 1.0, 0.5, 0.2, 1.0, 0.001, 0.01, 0.004, 200, 5000, t0.AddMilliseconds(260));
            Assert.True(f2.IsAudioActive);

            var f3 = dsp.ProcessSamples(silent, 48000, 32, 1.0, 0.5, 0.2, 1.0, 0.001, 0.01, 0.004, 200, 5000, t0.AddMilliseconds(2000));
            Assert.True(f3.IsAudioActive);

            var f4 = dsp.ProcessSamples(silent, 48000, 32, 1.0, 0.5, 0.2, 1.0, 0.001, 0.01, 0.004, 200, 5000, t0.AddMilliseconds(6000));
            Assert.False(f4.IsAudioActive);
        }

        [Fact]
        public void Test04_ConfigurationPersistence_SavesAndLoadsAllProperties()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "AuraWaveTests_" + Guid.NewGuid().ToString("N"));
            try
            {
                var svc = new ConfigurationService(tempDir);
                svc.Current.SelectedVisualizer = VisualizerType.Synthwave;
                svc.Current.SelectedTheme = ThemePreset.Ultraviolet;
                svc.Current.WaveformPinned = false;
                svc.Current.NowPlayingPinned = true;
                svc.Save();

                var svc2 = new ConfigurationService(tempDir);
                var loaded = svc2.Load();
                Assert.Equal(VisualizerType.Synthwave, loaded.SelectedVisualizer);
                Assert.Equal(ThemePreset.Ultraviolet, loaded.SelectedTheme);
                Assert.False(loaded.WaveformPinned);
                Assert.True(loaded.NowPlayingPinned);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void Test05_ConfigurationMalformedRecovery_CreatesBackupAndRestoresDefaults()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "AuraWaveTests_" + Guid.NewGuid().ToString("N"));
            try
            {
                var svc = new ConfigurationService(tempDir);
                svc.Save();
                File.WriteAllText(svc.ConfigFilePath, "{broken_json");

                var recovered = svc.Load();
                Assert.NotNull(recovered);
                Assert.Equal(VisualizerType.LiquidSpectrum, recovered.SelectedVisualizer);
                Assert.NotEmpty(Directory.GetFiles(tempDir, "*.bak-*"));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void Test06_ConfigValidation_ClampsOutOfRangeValues()
        {
            var cfg = new AuraWaveConfig
            {
                FrequencyBandCount = 999,
                AutoHideTimeoutSeconds = -5,
                NowPlayingSurfaceOpacity = 5.0
            };
            cfg.ValidateAndClamp();
            Assert.Equal(128, cfg.FrequencyBandCount);
            Assert.Equal(2, cfg.AutoHideTimeoutSeconds);
            Assert.Equal(1.0, cfg.NowPlayingSurfaceOpacity);
        }

        [Fact]
        public void Test07_WidgetGeometry_ClampsToMonitorBoundaries()
        {
            var geom = new WidgetGeometry { Left = -500, Top = 5000, Width = 50, Height = 5000 };
            geom.ClampToMonitor(0, 0, 1920, 1080, 240, 3840, 60, 900);
            Assert.Equal(240, geom.Width);
            Assert.Equal(900, geom.Height);
            Assert.Equal(0, geom.Left);
            Assert.Equal(180, geom.Top);
        }

        [Fact]
        public void Test08_IndependentVisibilityAndPinning_AreNotCoupled()
        {
            var cfg = new AuraWaveConfig
            {
                WaveformVisible = true,
                WaveformPinned = true,
                NowPlayingVisible = false,
                NowPlayingPinned = false
            };
            Assert.True(cfg.WaveformVisible);
            Assert.False(cfg.NowPlayingVisible);
            Assert.NotEqual(cfg.WaveformPinned, cfg.NowPlayingPinned);
        }

        [Fact]
        public void Test09_MediaSessionSnapshot_FormatsTimelineAndProgress()
        {
            var snap = new MediaSessionSnapshot
            {
                Title = "Midnight City",
                Artist = "M83",
                Position = TimeSpan.FromSeconds(65),
                Duration = TimeSpan.FromSeconds(260)
            };
            Assert.Equal(0.25, snap.ProgressRatio, 3);
            Assert.Equal("01:05 / 04:20", snap.FormattedTimeline);
        }

        [Fact]
        public void Test10_MediaWindowTitleParser_ExtractsArtistAndTrackFromBrowserAndSpotify()
        {
            bool ok = WindowsMediaSessionService.TryParseMediaWindowTitle(
                "Spotify",
                "Daft Punk - Veridis Quo",
                out string artist,
                out string title);

            Assert.True(ok);
            Assert.Equal("Daft Punk", artist);
            Assert.Equal("Veridis Quo", title);
        }

        [Fact]
        public void Test11_ThemeManager_ReturnsValidPaletteForAllPresetsAndCustom()
        {
            var cfg = new AuraWaveConfig { SelectedTheme = ThemePreset.Custom, CustomPrimaryHex = "#123456" };
            var pal = ThemeManager.GetActivePalette(cfg);
            Assert.Equal("#123456", pal.PrimaryHex);
        }
    }
}