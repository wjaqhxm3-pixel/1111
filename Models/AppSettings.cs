namespace ScreenSealWindows.Models;

public class AppSettings
{
    // Modifier flags (Win32 MOD_CTRL=0x0002, MOD_SHIFT=0x0004)
    public uint CreateHotkeyModifiers { get; set; } = 0x0002 | 0x0004; // Ctrl+Shift
    public uint CreateHotkeyKey { get; set; } = 0x4D;                 // M

    public uint ClearHotkeyModifiers { get; set; } = 0x0002 | 0x0004;  // Ctrl+Shift
    public uint ClearHotkeyKey { get; set; } = 0x43;                  // C

    public string SolidColorHex { get; set; } = "#000000";

    public string DefaultMosaicType { get; set; } = "Pixelation";
    public double DefaultIntensity { get; set; } = 20.0;
}
