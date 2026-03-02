using System.IO;
using System.Text.Json;
using ScreenSealWindows.Models;

namespace ScreenSealWindows.Services;

public class SettingsService
{
    private readonly string _filePath;
    public AppSettings Current { get; private set; }

    public SettingsService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "ScreenSeal");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "settings.json");
        Current = Load();
    }

    public AppSettings Load()
    {
        if (!File.Exists(_filePath)) return new AppSettings();
        try
        {
            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }
}
