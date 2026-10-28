using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuraWave.Models;
using AuraWave.Utilities;

namespace AuraWave.Configuration
{
    public sealed class AuraWaveConfig
    {
        // Appearance
        public ThemePreset SelectedTheme { get; set; } = ThemePreset.ArcticPulse;
        public string CustomPrimaryHex { get; set; } = "#00F2FE";
        public string CustomSecondaryHex { get; set; } = "#4FACFE";
        public string CustomAccentHex { get; set; } = "#FF2E93";
        public string CustomPeakHex { get; set; } = "#FFFFFF";
        public double VisualizerIntensity { get; set; } = 1.0;
        public double GlowIntensity { get; set; } = 0.65;
        public double UiScale { get; set; } = 1.0;
        public double WaveformThickness { get; set; } = 2.5;
        public double NowPlayingSurfaceOpacity { get; set; } = 0.88;
        public bool EnableNowPlayingGlass { get; set; } = true;
        public double AnimationIntensity { get; set; } = 1.0;
        public bool EnableParticles { get; set; } = true;
        public bool EnableReflection { get; set; } = true;
        public bool EnableSecondaryWaveform { get; set; } = true;

        // Visualizer
        public VisualizerType SelectedVisualizer { get; set; } = VisualizerType.LiquidSpectrum;
        public int FrequencyBandCount { get; set; } = 48;
        public bool SymmetricSpectrum { get; set; } = false;
        public double AudioSensitivity { get; set; } = 1.25;
        public double SmoothingFactor { get; set; } = 0.68;
        public bool ShowPeakIndicators { get; set; } = true;
        public double PeakDecayRate { get; set; } = 0.18;
        public double BassResponseBoost { get; set; } = 1.15;
        public int ParticleCount { get; set; } = 42;

        // Audio
        public string SelectedAudioDeviceId { get; set; } = string.Empty;
        public double ActivityAttackThreshold { get; set; } = 0.008;
        public double ActivityReleaseThreshold { get; set; } = 0.0035;
        public double NoiseFloorThreshold { get; set; } = 0.002;
        public int AttackHoldMilliseconds { get; set; } = 150;

        // Window & Layout
        public PresentationMode PresentationMode { get; set; } = PresentationMode.AlwaysOnTopOverlay;
        public PlacementPreset WaveformPlacementPreset { get; set; } = PlacementPreset.BottomCenter;
        public bool AlwaysOnTop { get; set; } = true;
        public bool ClickThroughEnabled { get; set; } = true;
        public bool LinkWidgetMovement { get; set; } = false;
        public bool RememberPlacement { get; set; } = true;
        public EditableComponent SelectedEditComponent { get; set; } = EditableComponent.Waveform;

        // Independent Waveform State
        public bool WaveformVisible { get; set; } = true;
        public bool WaveformPinned { get; set; } = true;
        public WidgetGeometry WaveformGeometry { get; set; } = new WidgetGeometry
        {
            Left = 360,
            Top = 820,
            Width = 760,
            Height = 140
        };

        // Independent Now Playing State
        public bool NowPlayingVisible { get; set; } = true;
        public bool NowPlayingPinned { get; set; } = false;
        public WidgetGeometry NowPlayingGeometry { get; set; } = new WidgetGeometry
        {
            Left = 520,
            Top = 690,
            Width = 440,
            Height = 112
        };
        public bool ShowAlbumArt { get; set; } = true;
        public bool EnableRotatingDisc { get; set; } = true;
        public bool ShowTrackTitle { get; set; } = true;
        public bool ShowArtistName { get; set; } = true;
        public bool ShowProgressBar { get; set; } = true;
        public bool ShowPlayPauseControl { get; set; } = true;
        public bool ShowVolumeControl { get; set; } = true;
        public bool ShowPrevNextControls { get; set; } = true;
        public int AutoHideTimeoutSeconds { get; set; } = 5;
        public bool EnableNowPlayingAutoHide { get; set; } = true;

        // Automation & Startup
        public bool StartWithWindows { get; set; } = false;
        public bool AutoShowWaveformOnAudio { get; set; } = true;
        public bool AutoShowNowPlayingOnAudio { get; set; } = true;
        public bool AutoHideWhenAudioStops { get; set; } = true;
        public int AudioStopInactivityDelaySeconds { get; set; } = 6;
        public bool RestorePreviousStateOnStartup { get; set; } = true;
        public bool RunInBackgroundWhenHidden { get; set; } = true;
        public bool MinimizeToTrayOnClose { get; set; } = true;

        public void ValidateAndClamp()
        {
            VisualizerIntensity = Math.Clamp(VisualizerIntensity, 0.2, 3.0);
            GlowIntensity = Math.Clamp(GlowIntensity, 0.0, 1.0);
            UiScale = Math.Clamp(UiScale, 0.6, 2.0);
            WaveformThickness = Math.Clamp(WaveformThickness, 1.0, 8.0);
            NowPlayingSurfaceOpacity = Math.Clamp(NowPlayingSurfaceOpacity, 0.2, 1.0);
            AnimationIntensity = Math.Clamp(AnimationIntensity, 0.1, 2.0);
            FrequencyBandCount = Math.Clamp(FrequencyBandCount, 16, 128);
            AudioSensitivity = Math.Clamp(AudioSensitivity, 0.2, 5.0);
            SmoothingFactor = Math.Clamp(SmoothingFactor, 0.05, 0.95);
            PeakDecayRate = Math.Clamp(PeakDecayRate, 0.02, 1.0);
            BassResponseBoost = Math.Clamp(BassResponseBoost, 0.5, 2.5);
            ParticleCount = Math.Clamp(ParticleCount, 8, 120);
            ActivityAttackThreshold = Math.Clamp(ActivityAttackThreshold, 0.0005, 0.2);
            ActivityReleaseThreshold = Math.Clamp(ActivityReleaseThreshold, 0.0002, ActivityAttackThreshold * 0.9);
            NoiseFloorThreshold = Math.Clamp(NoiseFloorThreshold, 0.0001, 0.05);
            AutoHideTimeoutSeconds = Math.Clamp(AutoHideTimeoutSeconds, 2, 60);
            AudioStopInactivityDelaySeconds = Math.Clamp(AudioStopInactivityDelaySeconds, 2, 120);

            WaveformGeometry ??= new WidgetGeometry { Left = 360, Top = 820, Width = 760, Height = 140 };
            NowPlayingGeometry ??= new WidgetGeometry { Left = 520, Top = 690, Width = 440, Height = 112 };

            WaveformGeometry.Width = Math.Clamp(WaveformGeometry.Width, 240, 3840);
            WaveformGeometry.Height = Math.Clamp(WaveformGeometry.Height, 60, 900);
            NowPlayingGeometry.Width = Math.Clamp(NowPlayingGeometry.Width, 280, 960);
            NowPlayingGeometry.Height = Math.Clamp(NowPlayingGeometry.Height, 84, 320);
        }
    }

    public sealed class ConfigurationService
    {
        private readonly string _configDir;
        private readonly string _configPath;
        private readonly object _sync = new object();

        public AuraWaveConfig Current { get; private set; } = new AuraWaveConfig();
        public event EventHandler<AuraWaveConfig>? ConfigurationChanged;

        public string ConfigFilePath => _configPath;

        public ConfigurationService(string? customDirectory = null)
        {
            _configDir = customDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AuraWave");
            _configPath = Path.Combine(_configDir, "config.json");
        }

        public AuraWaveConfig Load()
        {
            lock (_sync)
            {
                try
                {
                    if (!Directory.Exists(_configDir))
                        Directory.CreateDirectory(_configDir);

                    if (!File.Exists(_configPath))
                    {
                        Current = new AuraWaveConfig();
                        Current.ValidateAndClamp();
                        SaveInternal(Current);
                        return Current;
                    }

                    string json = File.ReadAllText(_configPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true,
                        Converters = { new JsonStringEnumConverter() }
                    };

                    var loaded = JsonSerializer.Deserialize<AuraWaveConfig>(json, options);
                    if (loaded == null)
                        throw new InvalidDataException("Deserialized configuration was null.");

                    loaded.ValidateAndClamp();
                    Current = loaded;
                    return Current;
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Failed to load config.json; backing up corrupted file and restoring defaults.", ex);
                    try
                    {
                        if (File.Exists(_configPath))
                        {
                            string backup = _configPath + $".bak-{DateTime.UtcNow:yyyyMMddHHmmss}";
                            File.Copy(_configPath, backup, overwrite: true);
                        }
                    }
                    catch { }

                    Current = new AuraWaveConfig();
                    Current.ValidateAndClamp();
                    SaveInternal(Current);
                    return Current;
                }
            }
        }

        public void Save()
        {
            lock (_sync)
            {
                Current.ValidateAndClamp();
                SaveInternal(Current);
            }
            ConfigurationChanged?.Invoke(this, Current);
        }

        public void ResetToDefaults()
        {
            lock (_sync)
            {
                Current = new AuraWaveConfig();
                Current.ValidateAndClamp();
                SaveInternal(Current);
            }
            ConfigurationChanged?.Invoke(this, Current);
        }

        private void SaveInternal(AuraWaveConfig cfg)
        {
            try
            {
                if (!Directory.Exists(_configDir))
                    Directory.CreateDirectory(_configDir);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                string tempPath = _configPath + ".tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(cfg, options));
                File.Copy(tempPath, _configPath, overwrite: true);
                File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to save configuration.", ex);
            }
        }
    }
}