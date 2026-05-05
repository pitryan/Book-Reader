using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Book_Reader
{
    public class BookMetadata
    {
        public bool IsFavorite { get; set; }
        public int LastReadPage { get; set; } = 0;
    }

    public class BookMetadataManager
    {
        private readonly string _settingsFile;
        private Dictionary<string, BookMetadata> _metadata;

        public BookMetadataManager()
        {
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BookReader");
            _settingsFile = Path.Combine(appDataFolder, "book_metadata.json");
            _metadata = new Dictionary<string, BookMetadata>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(appDataFolder)) Directory.CreateDirectory(appDataFolder);
            LoadSettings();
        }

        private void LoadSettings()
        {
            if (File.Exists(_settingsFile))
            {
                try
                {
                    string json = File.ReadAllText(_settingsFile);
                    var data = JsonSerializer.Deserialize<Dictionary<string, BookMetadata>>(json);
                    if (data != null) _metadata = data;
                }
                catch { }
            }
        }

        private void SaveSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(_metadata, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFile, json);
            }
            catch { }
        }

        public bool IsFavorite(string filePath)
        {
            if (_metadata.TryGetValue(filePath, out var meta)) return meta.IsFavorite;
            return false;
        }

        public void SetFavorite(string filePath, bool isFavorite)
        {
            if (!_metadata.ContainsKey(filePath))
                _metadata[filePath] = new BookMetadata();

            _metadata[filePath].IsFavorite = isFavorite;
            SaveSettings();
        }

        public int GetLastReadPage(string filePath)
        {
            if (_metadata.TryGetValue(filePath, out var meta)) return meta.LastReadPage;
            return 0;
        }

        public void SetLastReadPage(string filePath, int pageIndex)
        {
            if (!_metadata.ContainsKey(filePath))
                _metadata[filePath] = new BookMetadata();

            _metadata[filePath].LastReadPage = pageIndex;
            SaveSettings();
        }

        public void RenameFile(string oldPath, string newPath)
        {
            if (_metadata.TryGetValue(oldPath, out var meta))
            {
                _metadata.Remove(oldPath);
                _metadata[newPath] = meta;
                SaveSettings();
            }
        }

        public void DeleteFile(string filePath)
        {
            if (_metadata.ContainsKey(filePath))
            {
                _metadata.Remove(filePath);
                SaveSettings();
            }
        }
    }
}
