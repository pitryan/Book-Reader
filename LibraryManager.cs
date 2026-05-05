using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Book_Reader
{
    public class LibraryManager
    {
        private readonly string _settingsFile;
        public List<string> WatchedFolders { get; private set; }

        public LibraryManager()
        {
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BookReader");
            _settingsFile = Path.Combine(appDataFolder, "library_settings.json");
            WatchedFolders = new List<string>();

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
                    var folders = JsonSerializer.Deserialize<List<string>>(json);
                    if (folders != null) WatchedFolders = folders.Where(Directory.Exists).ToList();
                }
                catch { }
            }
        }

        private void SaveSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(WatchedFolders, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFile, json);
            }
            catch { }
        }

        public void AddFolder(string folderPath)
        {
            if (!WatchedFolders.Contains(folderPath, StringComparer.OrdinalIgnoreCase) && Directory.Exists(folderPath))
            {
                WatchedFolders.Add(folderPath);
                SaveSettings();
            }
        }

        public void RemoveFolder(string folderPath)
        {
            var match = WatchedFolders.FirstOrDefault(f => f.Equals(folderPath, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                WatchedFolders.Remove(match);
                SaveSettings();
            }
        }

        public List<RecentFile> ScanAllBooks()
        {
            var books = new List<RecentFile>();

            foreach (var folder in WatchedFolders)
            {
                if (!Directory.Exists(folder)) continue;

                try
                {
                    // Scan recursively for PDFs
                    var pdfFiles = Directory.GetFiles(folder, "*.pdf", SearchOption.AllDirectories);
                    
                    foreach (var file in pdfFiles)
                    {
                        books.Add(new RecentFile
                        {
                            FilePath = file,
                            FileName = Path.GetFileName(file),
                            LastOpened = File.GetCreationTime(file) // Fallback for sorting
                        });
                    }
                }
                catch
                {
                    // Ignore permissions or access errors for specific folders
                }
            }

            return books.OrderBy(b => b.FileName).ToList();
        }
    }
}
