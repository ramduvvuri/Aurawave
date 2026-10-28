using System;
using System.IO;
using AuraWave.Configuration;
using AuraWave.DSP;
using AuraWave.Media;
using AuraWave.Models;
using AuraWave.Services;
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

            var f4 = dsp.ProcessSamples(silent, 48000, 32, 1.0, 0.5, 0.2, 1.0, 0.001, 0.01, 0.004, 200, 5000, t0.AddMilliseconds(7500));
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
                svc.Current.SelectedTheme = ThemePreset.EmeraldCapsule;
                svc.Current.WaveformVisible = false;
                svc.Current.NowPlayingPinned = true;
                svc.Save();

                var svc2 = new ConfigurationService(tempDir);
                var loaded = svc2.Load();
                Assert.Equal(VisualizerType.Synthwave, loaded.SelectedVisualizer);
                Assert.Equal(ThemePreset.EmeraldCapsule, loaded.SelectedTheme);
                Assert.False(loaded.WaveformVisible);
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
                Assert.Equal(ThemePreset.EmeraldCapsule, recovered.SelectedTheme);
                Assert.False(recovered.WaveformVisible);
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
            var geom = new WidgetGeometry { Left = -500, Top = 5000, Width = 200, Height = 500 };
            geom.ClampToMonitor(0, 0, 1920, 1040, 640, 1600, 78, 140);
            Assert.Equal(640, geom.Width);
            Assert.Equal(140, geom.Height);
            Assert.InRange(geom.Left, 0, 1920 - 640);
            Assert.InRange(geom.Top, 0, 1040 - 140);
        }

        [Fact]
        public void Test08_DefaultConfig_MatchesOneToOneEmeraldCapsuleDimensions()
        {
            var cfg = new AuraWaveConfig();
            Assert.False(cfg.WaveformVisible);
            Assert.False(cfg.AutoShowWaveformOnAudio);
            Assert.True(cfg.NowPlayingVisible);
            Assert.True(cfg.ShowEmbeddedSpectrum);
            Assert.Equal(1020, cfg.NowPlayingGeometry.Width);
            Assert.Equal(90, cfg.NowPlayingGeometry.Height);
            Assert.Equal(ThemePreset.EmeraldCapsule, cfg.SelectedTheme);
        }

        [Fact]
        public void Test09_MediaSessionSnapshot_FormatsTimelineAndProgress()
        {
            var snap = new MediaSessionSnapshot
            {
                Title = "Uncover the Silent Beauty",
                Artist = "Meadow",
                Position = TimeSpan.FromSeconds(342),
                Duration = TimeSpan.FromSeconds(1214)
            };
            Assert.Equal("5:42 / 20:14", snap.FormattedTimeline);
            Assert.InRange(snap.ProgressRatio, 0.28, 0.29);
        }

        [Fact]
        public void Test10_MediaWindowTitleParser_ExtractsArtistAndTrackFromBrowserAndSpotify()
        {
            bool ok = WindowsMediaSessionService.TryParseMediaWindowTitle(
                "Spotify",
                "Meadow - Uncover the Silent Beauty",
                out string artist,
                out string title);

            Assert.True(ok);
            Assert.Equal("Meadow", artist);
            Assert.Equal("Uncover the Silent Beauty", title);
        }

        [Fact]
        public void Test11_ThemeManager_EmeraldCapsuleIsDefaultAndHasCorrectColors()
        {
            var cfg = new AuraWaveConfig();
            var pal = ThemeManager.GetActivePalette(cfg);
            Assert.Equal("#35D6A0", pal.PrimaryHex);
            Assert.Equal("#42E5B0", pal.SecondaryHex);
            Assert.Equal("#07120F", pal.SurfaceHex);
        }

        [Fact]
        public void Test12_CalculateBottomCenterBar_PositionsHorizontallyCenteredAboveTaskbar()
        {
            var geom = WidgetGeometry.CalculateBottomCenterBar(0, 0, 1920, 1032, 1020, 90, 14);
            Assert.Equal(1020, geom.Width);
            Assert.Equal(90, geom.Height);
            Assert.Equal(450, geom.Left); // (1920 - 960) / 2
            Assert.Equal(928, geom.Top);  // 1032 - 88 - 14
        }

        [Fact]
        public void Test13_FavouritesService_TogglesAndPersistsFavouriteTracks()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "AuraWaveFavs_" + Guid.NewGuid().ToString("N"));
            try
            {
                var favSvc = new FavouritesService(tempDir);
                var snap = new MediaSessionSnapshot { Title = "Uncover the Silent Beauty", Artist = "Meadow", SourceAppId = "Spotify" };

                Assert.False(favSvc.IsFavourite(snap));
                Assert.True(favSvc.ToggleFavourite(snap));
                Assert.True(favSvc.IsFavourite(snap));

                var favSvc2 = new FavouritesService(tempDir);
                Assert.True(favSvc2.IsFavourite(snap));
                Assert.Single(favSvc2.GetAll());

                Assert.False(favSvc2.ToggleFavourite(snap));
                Assert.False(favSvc2.IsFavourite(snap));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }
    }
}