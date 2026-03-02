using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScreenSealWindows.Models;

/// <summary>
/// Per-window configuration for mosaic type and intensity.
/// </summary>
public class OverlayConfiguration : INotifyPropertyChanged
{
    private MosaicType _mosaicType = MosaicType.Pixelation;
    private double _intensity = 20.0;
    private System.Drawing.Color _solidColor = System.Drawing.Color.Black;

    public MosaicType MosaicType
    {
        get => _mosaicType;
        set { _mosaicType = value; OnPropertyChanged(); }
    }

    public System.Drawing.Color SolidColor
    {
        get => _solidColor;
        set { _solidColor = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Intensity of the mosaic effect.
    /// For Pixelation: pixel scale (5–100).
    /// For GaussianBlur: blur radius (1–100).
    /// For Crystallize: cell size (5–100).
    /// </summary>
    public double Intensity
    {
        get => _intensity;
        set
        {
            _intensity = Math.Clamp(value, 0.0, 100.0);
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
