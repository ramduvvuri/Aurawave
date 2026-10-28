using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AuraWave.Models;
using AuraWave.Utilities;

namespace AuraWave.Services
{
    public sealed class FavouritesService
    {
        private readonly string _filePath;
        private readonly object _sync = new object();
        private List<FavouriteTrackItem> _items = new List<FavouriteTrackItem>();

        public event EventHandler? FavouritesChanged;

        public FavouritesService(string? customDirectory = null)
        {
            string dir = customDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AuraWave");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "favourites.json");
            Load();
        }

        public IReadOnlyList<FavouriteTrackItem> GetAll()
        {
            lock (_sync)
            {
                return _items.OrderByDescending(x => x.SavedAtUtc).ToList();
            }
        }

        public bool IsFavourite(MediaSessionSnapshot? snap)
        {
            if (snap == null || string.IsNullOrWhiteSpace(snap.Title)) return false;
            string key = snap.TrackKey;
            lock (_sync)
            {
                return _items.Any(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            }
        }

        public bool ToggleFavourite(MediaSessionSnapshot snap)
        {
            if (string.IsNullOrWhiteSpace(snap.Title)) return false;
            string key = snap.TrackKey;
            bool isNowFav;

            lock (_sync)
            {
                var existing = _items.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    _items.Remove(existing);
                    isNowFav = false;
                }
                else
                {
                    _items.Add(new FavouriteTrackItem
                    {
                        Key = key,
                        Title = snap.Title,
                        Artist = snap.Artist,
                        SourceApp = snap.SourceAppId,
                        SavedAtUtc = DateTime.UtcNow
                    });
                    isNowFav = true;
                }
                SaveInternal();
            }

            FavouritesChanged?.Invoke(this, EventArgs.Empty);
            return isNowFav;
        }

        public void RemoveByKey(string key)
        {
            lock (_sync)
            {
                _items.RemoveAll(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
                SaveInternal();
            }
            FavouritesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Load()
        {
            lock (_sync)
            {
                try
                {
                    if (File.Exists(_filePath))
                    {
                        string json = File.ReadAllText(_filePath);
                        _items = JsonSerializer.Deserialize<List<FavouriteTrackItem>>(json) ?? new List<FavouriteTrackItem>();
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("Failed to load favourites.json: " + ex.Message);
                    _items = new List<FavouriteTrackItem>();
                }
            }
        }

        private void SaveInternal()
        {
            try
            {
                string json = JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Failed to save favourites.json: " + ex.Message);
            }
        }
    }
}