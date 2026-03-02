using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenSealWindows.Models;
using ScreenSealWindows.Services;
using ScreenSealWindows.Helpers;
using System.Drawing;

namespace ScreenSealWindows.Windows;

/// <summary>
/// Manages all overlay windows and handles snapshot-based screen captures.
/// </summary>
public class WindowManager
{
    private readonly List<OverlayWindow> _windows = new();
    private readonly ScreenCaptureService _captureService = new();
    private readonly PresetManager _presetManager = new();
    private readonly SettingsService _settingsService;
    private int _nextIndex = 1;

    public IReadOnlyList<OverlayWindow> Windows => _windows;
    public PresetManager PresetManager => _presetManager;

    public event Action? WindowsChanged;

    public WindowManager(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Creates an overlay window at a specific physical screen position and size.
    /// Used by the SnipWindow drag-to-create flow.
    /// </summary>
    public void CreateWindowAt(double physX, double physY, double physWidth, double physHeight)
    {
        var window = new OverlayWindow(_nextIndex++, _settingsService);
        ApplyDefaultSettings(window);
        window.Closed += (_, _) => RemoveWindow(window);
        
        // Initial setup - need to convert physical to logical for WPF positioning
        var presentationSource = PresentationSource.FromVisual(System.Windows.Application.Current.MainWindow);
        double scaleX = 1.0, scaleY = 1.0;
        if (presentationSource != null)
        {
            scaleX = presentationSource.CompositionTarget.TransformToDevice.M11;
            scaleY = presentationSource.CompositionTarget.TransformToDevice.M22;
        }

        window.Left = physX / scaleX;
        window.Top = physY / scaleY;
        window.Width = physWidth / scaleX;
        window.Height = physHeight / scaleY;
        window.Manager = this;

        _windows.Add(window);
        
        // Show but HIDDEN initially for first capture
        window.Opacity = 0;
        window.Show();
        
        RefreshWindowSnapshot(window);
        window.Opacity = 1.0; // Restored perfectly opaque to prevent bleed-through

        WindowsChanged?.Invoke();
    }

    /// <summary>
    /// Performs a fresh screen capture behind the window and updates its mosaic.
    /// This is called when window is created or finished moving.
    /// </summary>
    public async void RefreshWindowSnapshot(OverlayWindow window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            // 1. Hide window from capture using API (Standard way)
            NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);

            // 2. Small delay for DWM to composite the exclusion
            await System.Threading.Tasks.Task.Delay(20);

            // 3. Get accurate physical screen coordinates using DWM API
            if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out var rect, Marshal.SizeOf<NativeMethods.RECT>()) != 0)
            {
                // Fallback to logical if DWM fails (less accurate)
                HwndSource source = (HwndSource)PresentationSource.FromVisual(window);
                double scaleX = source.CompositionTarget.TransformToDevice.M11;
                double scaleY = source.CompositionTarget.TransformToDevice.M22;
                int px = (int)(window.Left * scaleX);
                int py = (int)(window.Top * scaleY);
                int pw = (int)(window.Width * scaleX);
                int ph = (int)(window.Height * scaleY);
                rect = new NativeMethods.RECT { Left = px, Top = py, Right = px + pw, Bottom = py + ph };
            }

            int x = rect.Left;
            int y = rect.Top;
            int w = rect.Width;
            int h = rect.Height;

            if (w > 0 && h > 0)
            {
                var captured = _captureService.CaptureRegion(x, y, w, h);
                if (captured != null)
                {
                    var old = window.OriginalSnapshot;
                    window.OriginalSnapshot = captured;
                    old?.Dispose();
                    window.InvalidateMosaic();
                }
            }

            // 4. Restore visibility to screenshots
            NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_NONE);
        }
        catch { }
    }

    public void CreateWindow()
    {
        // Default generic window if needed (not usually used in snip flow)
        CreateWindowAt(200, 200, 300, 200);
    }

    private void ApplyDefaultSettings(OverlayWindow window)
    {
        string defaultTypeStr = _settingsService.Current.DefaultMosaicType;
        if (Enum.TryParse<MosaicType>(defaultTypeStr, out var type))
        {
            window.Configuration.MosaicType = type;
        }
        else if (defaultTypeStr == "Crystallize")
        {
            window.Configuration.MosaicType = MosaicType.RetroGaming;
        }
            
        window.Configuration.Intensity = _settingsService.Current.DefaultIntensity;

        try
        {
            window.Configuration.SolidColor = ColorTranslator.FromHtml(_settingsService.Current.SolidColorHex);
        }
        catch
        {
            window.Configuration.SolidColor = Color.Black;
        }
    }

    public void RemoveWindow(OverlayWindow window)
    {
        _windows.Remove(window);
        if (window.IsVisible) window.Close();
        WindowsChanged?.Invoke();
    }

    public void ToggleWindow(OverlayWindow window)
    {
        if (window.IsVisible) window.Hide();
        else window.Show();
        WindowsChanged?.Invoke();
    }

    public void RemoveAllWindows()
    {
        var copy = _windows.ToList();
        _windows.Clear();
        foreach (var w in copy) w.Close();
        WindowsChanged?.Invoke();
    }

    public void SaveCurrentLayout(string name)
    {
        var snapshots = _windows.Select(w => new WindowSnapshot(
            w.Left, w.Top, w.Width, w.Height,
            w.Configuration.MosaicType.ToString(),
            w.Configuration.Intensity
        )).ToList();

        _presetManager.Add(new LayoutPreset(name, snapshots));
    }

    public void LoadPreset(LayoutPreset preset)
    {
        RemoveAllWindows();
        foreach (var snap in preset.Windows)
        {
            var window = new OverlayWindow(_nextIndex++, _settingsService)
            {
                Left = snap.X, Top = snap.Y, Width = snap.Width, Height = snap.Height
            };

            if (Enum.TryParse<MosaicType>(snap.MosaicType, out var mType))
            {
                window.Configuration.MosaicType = mType;
                window.Configuration.Intensity = snap.Intensity;
            }

            window.Closed += (_, _) => RemoveWindow(window);
            _windows.Add(window);
            window.Show();
            RefreshWindowSnapshot(window);
        }
        WindowsChanged?.Invoke();
    }
}
