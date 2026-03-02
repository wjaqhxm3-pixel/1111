using System.IO;
using System.Text.Json;

namespace ScreenSealWindows.Models;

public record WindowSnapshot(
    double X, double Y, double Width, double Height,
    string MosaicType, double Intensity);

public record LayoutPreset(string Name, List<WindowSnapshot> Windows);

/// <summary>
/// Saves and loads layout presets to a JSON file in the user's AppData folder.
/// </summary>
public class PresetManager
{
    private readonly string _filePath;
    private List<LayoutPreset> _presets = new();

    public IReadOnlyList<LayoutPreset> Presets => _presets;

    public PresetManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "ScreenSeal");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "presets.json");
        Load();
    }

    public void Add(LayoutPreset preset)
    {
        // Replace if same name exists
        _presets.RemoveAll(p => p.Name == preset.Name);
        _presets.Add(preset);
        Save();
    }

    public void Remove(string name)
    {
        _presets.RemoveAll(p => p.Name == name);
        Save();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _presets = JsonSerializer.Deserialize<List<LayoutPreset>>(json) ?? new();
            }
        }
        catch
        {
            _presets = new();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_presets, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
