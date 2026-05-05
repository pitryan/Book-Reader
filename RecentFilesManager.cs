using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Book_Reader
{
    public class RecentFile
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public DateTime LastOpened { get; set; }
        public string ThumbnailPath { get; set; } = "";

        // UI Binding Properties
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsFavorite { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore]
        public string FavoriteIcon => IsFavorite ? "❤️" : "🤍";
        
        [System.Text.Json.Serialization.JsonIgnore]
        public string FormattedDate => LastOpened.ToString("dd MMM yyyy, HH:mm");
    }

    public class RecentFilesManager
    {
        private readonly string _appDataFolder;
        private readonly string _settingsFile;
        private readonly string _thumbnailsFolder;

        public RecentFilesManager()
        {
            _appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BookReader");
            _settingsFile = Path.Combine(_appDataFolder, "recent_files.json");
            _thumbnailsFolder = Path.Combine(_appDataFolder, "Thumbnails");

            if (!Directory.Exists(_appDataFolder)) Directory.CreateDirectory(_appDataFolder);
            if (!Directory.Exists(_thumbnailsFolder)) Directory.CreateDirectory(_thumbnailsFolder);
        }

        public string ThumbnailsFolder => _thumbnailsFolder;

        public List<RecentFile> GetRecentFiles()
        {
            if (!File.Exists(_settingsFile)) return new List<RecentFile>();

            try
            {
                string json = File.ReadAllText(_settingsFile);
                var files = JsonSerializer.Deserialize<List<RecentFile>>(json);
                // Return only files that still exist on disk
                return files?.Where(f => File.Exists(f.FilePath)).OrderByDescending(f => f.LastOpened).ToList() ?? new List<RecentFile>();
            }
            catch
            {
                return new List<RecentFile>();
            }
        }

        public void AddOrUpdateFile(string filePath, string thumbnailPath)
        {
            var files = GetRecentFiles();
            var existing = files.FirstOrDefault(f => f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.LastOpened = DateTime.Now;
                if (!string.IsNullOrEmpty(thumbnailPath)) existing.ThumbnailPath = thumbnailPath;
            }
            else
            {
                files.Add(new RecentFile
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    LastOpened = DateTime.Now,
                    ThumbnailPath = thumbnailPath
                });
            }

            // Keep only top 30 recent files
            files = files.OrderByDescending(f => f.LastOpened).Take(30).ToList();

            try
            {
                string json = JsonSerializer.Serialize(files, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFile, json);
            }
            catch { /* Ignore saving errors */ }
        }
    }
}
