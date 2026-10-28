#nullable disable
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using global::Windows.Media.Control;
using global::Windows.Storage.Streams;
using AuraWave.Models;
using AuraWave.Utilities;

namespace AuraWave.Media
{
    public sealed class WindowsMediaSessionService : IDisposable
    {
        private CancellationTokenSource? _cts;
        private Task? _pollTask;
        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
        private string _lastArtworkCacheKey = string.Empty;
        private byte[]? _cachedArtworkBytes;
        private bool _isAudioCurrentlyActive;

        public MediaSessionSnapshot CurrentSession { get; private set; } = new MediaSessionSnapshot
        {
            SourceAppId = "Windows Audio",
            Title = "No Active Media Session",
            Artist = "Play music in Spotify, Brave, Chrome, or Edge",
            PlaybackStatus = PlaybackState.Stopped,
            Position = TimeSpan.Zero,
            Duration = TimeSpan.Zero
        };

        public event EventHandler<MediaSessionSnapshot>? SessionUpdated;

        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;
        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        public void Start()
        {
            Stop();
            _cts = new CancellationTokenSource();
            _pollTask = Task.Run(() => PollLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _pollTask?.Wait(300);
            }
            catch { }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }

        public void NotifyAudioActivity(bool isActive)
        {
            _isAudioCurrentlyActive = isActive;
        }

        public void SeekToRatio(double ratio)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    ratio = Math.Clamp(ratio, 0.0, 1.0);
                    var session = _sessionManager?.GetCurrentSession();
                    if (session != null && CurrentSession.Duration.TotalSeconds > 1.0)
                    {
                        TimeSpan target = TimeSpan.FromSeconds(CurrentSession.Duration.TotalSeconds * ratio);
                        await session.TryChangePlaybackPositionAsync(target.Ticks);
                        CurrentSession.Position = target;
                        SessionUpdated?.Invoke(this, CurrentSession);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("GSMTC SeekToRatio warning: " + ex.Message);
                }
            });
        }

        public bool SendPlayPause()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var session = _sessionManager?.GetCurrentSession();
                    if (session != null)
                    {
                        bool ok = await session.TryTogglePlayPauseAsync();
                        if (ok) return;
                    }
                }
                catch { }

                keybd_event(VK_MEDIA_PLAY_PAUSE, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                keybd_event(VK_MEDIA_PLAY_PAUSE, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
            });
            return true;
        }

        public bool SendNextTrack()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var session = _sessionManager?.GetCurrentSession();
                    if (session != null)
                    {
                        bool ok = await session.TrySkipNextAsync();
                        if (ok) return;
                    }
                }
                catch { }

                keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
            });
            return true;
        }

        public bool SendPreviousTrack()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var session = _sessionManager?.GetCurrentSession();
                    if (session != null)
                    {
                        bool ok = await session.TrySkipPreviousAsync();
                        if (ok) return;
                    }
                }
                catch { }

                keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
            });
            return true;
        }

        private async Task PollLoopAsync(CancellationToken token)
        {
            try
            {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Warn("GSMTC RequestAsync initialization warning: " + ex.Message);
            }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var session = _sessionManager?.GetCurrentSession();
                    if (session != null)
                    {
                        var props = await session.TryGetMediaPropertiesAsync();
                        var playbackInfo = session.GetPlaybackInfo();
                        var timeline = session.GetTimelineProperties();

                        string title = props?.Title?.Trim() ?? string.Empty;
                        string artist = props?.Artist?.Trim() ?? string.Empty;
                        string album = props?.AlbumTitle?.Trim() ?? string.Empty;
                        string sourceApp = session.SourceAppUserModelId ?? "Media Session";

                        string artKey = $"{sourceApp}|{artist}|{title}|{album}";
                        if (artKey != _lastArtworkCacheKey)
                        {
                            _lastArtworkCacheKey = artKey;
                            _cachedArtworkBytes = await TryReadThumbnailBytesAsync(props?.Thumbnail);
                        }

                        var status = PlaybackState.Stopped;
                        if (playbackInfo != null)
                        {
                            status = playbackInfo.PlaybackStatus switch
                            {
                                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackState.Playing,
                                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackState.Paused,
                                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => PlaybackState.Stopped,
                                _ => _isAudioCurrentlyActive ? PlaybackState.Playing : PlaybackState.Paused
                            };
                        }

                        TimeSpan duration = TimeSpan.Zero;
                        TimeSpan position = TimeSpan.Zero;
                        if (timeline != null)
                        {
                            duration = timeline.EndTime - timeline.StartTime;
                            position = timeline.Position;
                            if (status == PlaybackState.Playing && timeline.LastUpdatedTime != default)
                            {
                                TimeSpan sinceUpdate = DateTimeOffset.UtcNow - timeline.LastUpdatedTime;
                                if (sinceUpdate > TimeSpan.Zero && sinceUpdate < TimeSpan.FromMinutes(10))
                                {
                                    position += sinceUpdate;
                                }
                            }
                            if (duration > TimeSpan.Zero && position > duration)
                                position = duration;
                        }

                        var snap = new MediaSessionSnapshot
                        {
                            SourceAppId = CleanSourceAppName(sourceApp),
                            Title = string.IsNullOrWhiteSpace(title) ? "Live Audio Stream" : title,
                            Artist = string.IsNullOrWhiteSpace(artist) ? CleanSourceAppName(sourceApp) : artist,
                            AlbumTitle = album,
                            ArtworkBytes = _cachedArtworkBytes,
                            PlaybackStatus = status,
                            Position = position < TimeSpan.Zero ? TimeSpan.Zero : position,
                            Duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration,
                            SupportsPlayPause = (playbackInfo?.Controls != null ? (playbackInfo.Controls.IsPlayEnabled || playbackInfo.Controls.IsPauseEnabled) : true),
                            SupportsNext = playbackInfo?.Controls?.IsNextEnabled ?? true,
                            SupportsPrevious = playbackInfo?.Controls?.IsPreviousEnabled ?? true
                        };

                        CurrentSession = snap;
                        SessionUpdated?.Invoke(this, snap);
                    }
                    else
                    {
                        FallbackWindowTitleDiscovery();
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("GSMTC poll cycle warning: " + ex.Message);
                    FallbackWindowTitleDiscovery();
                }

                await Task.Delay(400, token);
            }
        }

        private static async Task<byte[]?> TryReadThumbnailBytesAsync(global::Windows.Storage.Streams.IRandomAccessStreamReference? thumbRef)
        {
            if (thumbRef == null) return null;
            try
            {
                using var winrtStream = await thumbRef.OpenReadAsync();
                using var netStream = winrtStream.AsStreamForRead();
                using var ms = new MemoryStream();
                await netStream.CopyToAsync(ms);
                return ms.Length > 0 ? ms.ToArray() : null;
            }
            catch
            {
                return null;
            }
        }

        private void FallbackWindowTitleDiscovery()
        {
            string[] mediaProcessNames = new[]
            {
                "Spotify", "Music.UI", "vlc", "foobar2000", "MusicBee", "AIMP", "iTunes",
                "brave", "chrome", "msedge", "firefox"
            };

            string foundTitle = string.Empty;
            string foundArtist = string.Empty;
            string foundSource = string.Empty;

            foreach (string procName in mediaProcessNames)
            {
                Process[] procs = Process.GetProcessesByName(procName);
                foreach (var p in procs)
                {
                    try
                    {
                        string wt = p.MainWindowTitle;
                        if (!string.IsNullOrWhiteSpace(wt) && wt.Length > 2)
                        {
                            if (TryParseMediaWindowTitle(procName, wt, out string artist, out string title))
                            {
                                foundTitle = title;
                                foundArtist = artist;
                                foundSource = procName;
                                break;
                            }
                        }
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
                if (!string.IsNullOrEmpty(foundTitle)) break;
            }

            var snap = new MediaSessionSnapshot
            {
                SourceAppId = string.IsNullOrEmpty(foundSource) ? "Windows Audio" : foundSource,
                Title = string.IsNullOrEmpty(foundTitle)
                    ? (_isAudioCurrentlyActive ? "System Audio Active" : "Ready for Music")
                    : foundTitle,
                Artist = string.IsNullOrEmpty(foundArtist)
                    ? (_isAudioCurrentlyActive ? "WASAPI Loopback Stream" : "Play any song in Spotify or Browser")
                    : foundArtist,
                PlaybackStatus = _isAudioCurrentlyActive ? PlaybackState.Playing : PlaybackState.Paused,
                Position = TimeSpan.Zero,
                Duration = TimeSpan.Zero
            };

            CurrentSession = snap;
            SessionUpdated?.Invoke(this, snap);
        }

        private static string CleanSourceAppName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Media";
            if (raw.Contains("Spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
            if (raw.Contains("Chrome", StringComparison.OrdinalIgnoreCase)) return "Chrome";
            if (raw.Contains("Brave", StringComparison.OrdinalIgnoreCase)) return "Brave";
            if (raw.Contains("Edge", StringComparison.OrdinalIgnoreCase)) return "Edge";
            if (raw.Contains("Firefox", StringComparison.OrdinalIgnoreCase)) return "Firefox";
            int bang = raw.IndexOf('!');
            if (bang > 0) raw = raw.Substring(0, bang);
            int dot = raw.LastIndexOf('.');
            if (dot > 0 && dot < raw.Length - 1) raw = raw.Substring(dot + 1);
            return raw;
        }

        public static bool TryParseMediaWindowTitle(string processName, string windowTitle, out string artist, out string title)
        {
            artist = string.Empty;
            title = string.Empty;

            string cleaned = windowTitle.Trim();
            string[] browserSuffixes = new[]
            {
                " - YouTube - Brave", " - YouTube - Google Chrome", " - YouTube - Microsoft\u200B Edge", " - YouTube — Mozilla Firefox",
                " - Brave", " - Google Chrome", " - Microsoft Edge", " - Microsoft\u200B Edge", " — Mozilla Firefox", " - VLC media player"
            };

            foreach (var suffix in browserSuffixes)
            {
                if (cleaned.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    cleaned = cleaned.Substring(0, cleaned.Length - suffix.Length).Trim();
                    break;
                }
            }

            if (string.Equals(cleaned, "Spotify Free", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleaned, "Spotify Premium", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleaned, "Spotify", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleaned, "New Tab", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int dashIdx = cleaned.IndexOf(" - ", StringComparison.Ordinal);
            if (dashIdx > 0 && dashIdx < cleaned.Length - 3)
            {
                artist = cleaned.Substring(0, dashIdx).Trim();
                title = cleaned.Substring(dashIdx + 3).Trim();
                return true;
            }

            title = cleaned;
            artist = processName;
            return !string.IsNullOrWhiteSpace(title);
        }

        public void Dispose() => Stop();
    }
}