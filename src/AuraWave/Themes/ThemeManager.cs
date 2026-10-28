using System.Collections.Generic;
using System.Windows.Media;
using AuraWave.Configuration;
using AuraWave.Models;

namespace AuraWave.Themes
{
    public static class ThemeManager
    {
        private static readonly Dictionary<ThemePreset, ThemePalette> BuiltInPalettes = new Dictionary<ThemePreset, ThemePalette>
        {
            [ThemePreset.EmeraldCapsule] = new ThemePalette
            {
                Name = "Emerald Capsule",
                PrimaryHex = "#35D6A0",
                SecondaryHex = "#42E5B0",
                AccentHex = "#087F62",
                PeakHex = "#A7F3D0",
                TextHex = "#F2F7F5",
                SecondaryTextHex = "#91A49E",
                SurfaceHex = "#07120F",
                GlowHex = "#35D6A0"
            },
            [ThemePreset.ArcticPulse] = new ThemePalette
            {
                Name = "Arctic Pulse",
                PrimaryHex = "#00F2FE",
                SecondaryHex = "#4FACFE",
                AccentHex = "#7F7FD5",
                PeakHex = "#E0F7FA",
                TextHex = "#F8FAFC",
                SecondaryTextHex = "#94A3B8",
                SurfaceHex = "#0B1120",
                GlowHex = "#00E5FF"
            },
            [ThemePreset.VioletEther] = new ThemePalette
            {
                Name = "Violet Ether",
                PrimaryHex = "#A18CD1",
                SecondaryHex = "#7F53AC",
                AccentHex = "#647DEE",
                PeakHex = "#F3E5F5",
                TextHex = "#F8FAFC",
                SecondaryTextHex = "#94A3B8",
                SurfaceHex = "#0F1026",
                GlowHex = "#B388FF"
            },
            [ThemePreset.Aurora] = new ThemePalette
            {
                Name = "Aurora",
                PrimaryHex = "#00FF87",
                SecondaryHex = "#60EFFF",
                AccentHex = "#087F62",
                PeakHex = "#E8F5E9",
                TextHex = "#F2F7F5",
                SecondaryTextHex = "#91A49E",
                SurfaceHex = "#07120F",
                GlowHex = "#00E676"
            },
            [ThemePreset.Afterglow] = new ThemePalette
            {
                Name = "Afterglow",
                PrimaryHex = "#FF6FD8",
                SecondaryHex = "#3813C2",
                AccentHex = "#FF8E53",
                PeakHex = "#FCE4EC",
                TextHex = "#F8FAFC",
                SecondaryTextHex = "#CBD5E1",
                SurfaceHex = "#160B1E",
                GlowHex = "#FF4081"
            },
            [ThemePreset.Ultraviolet] = new ThemePalette
            {
                Name = "Ultraviolet",
                PrimaryHex = "#C471ED",
                SecondaryHex = "#12C2E9",
                AccentHex = "#F64F59",
                PeakHex = "#EDE7F6",
                TextHex = "#F8FAFC",
                SecondaryTextHex = "#94A3B8",
                SurfaceHex = "#120B24",
                GlowHex = "#D500F9"
            },
            [ThemePreset.Midnight] = new ThemePalette
            {
                Name = "Midnight",
                PrimaryHex = "#3A7BD5",
                SecondaryHex = "#00D2FF",
                AccentHex = "#1E3C72",
                PeakHex = "#BBDEFB",
                TextHex = "#E2E8F0",
                SecondaryTextHex = "#94A3B8",
                SurfaceHex = "#090D16",
                GlowHex = "#2979FF"
            },
            [ThemePreset.Monochrome] = new ThemePalette
            {
                Name = "Monochrome",
                PrimaryHex = "#F1F5F9",
                SecondaryHex = "#94A3B8",
                AccentHex = "#CBD5E1",
                PeakHex = "#FFFFFF",
                TextHex = "#F8FAFC",
                SecondaryTextHex = "#94A3B8",
                SurfaceHex = "#0F172A",
                GlowHex = "#E2E8F0"
            }
        };

        public static ThemePalette GetActivePalette(AuraWaveConfig config)
        {
            if (config.SelectedTheme == ThemePreset.Custom)
            {
                return new ThemePalette
                {
                    Name = "Custom",
                    PrimaryHex = config.CustomPrimaryHex,
                    SecondaryHex = config.CustomSecondaryHex,
                    AccentHex = config.CustomAccentHex,
                    PeakHex = config.CustomPeakHex,
                    TextHex = "#F2F7F5",
                    SecondaryTextHex = "#91A49E",
                    SurfaceHex = "#07120F",
                    GlowHex = config.CustomPrimaryHex
                };
            }
            return BuiltInPalettes.TryGetValue(config.SelectedTheme, out var p)
                ? p
                : BuiltInPalettes[ThemePreset.EmeraldCapsule];
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            try
            {
                object? obj = ColorConverter.ConvertFromString(hex);
                if (obj is Color c) return c;
            }
            catch { }
            return fallback;
        }
    }
}