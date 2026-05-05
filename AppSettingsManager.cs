using System;
using System.IO;
using System.Text.Json;

namespace Book_Reader
{
    public class AppSettings
    {
        public string Theme { get; set; } = "Dark"; // "Light" or "Dark"
    }

    public class AppSettingsManager
    {
        private readonly string _settingsFile;
        public AppSettings Current { get; private set; }

        public AppSettingsManager()
        {
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BookReader");
            _settingsFile = Path.Combine(appDataFolder, "app_settings.json");
            Current = new AppSettings();

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
                    var data = JsonSerializer.Deserialize<AppSettings>(json);
                    if (data != null) Current = data;
                }
                catch { }
            }
        }

        public void SaveSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFile, json);
            }
            catch { }
        }
    }
}
