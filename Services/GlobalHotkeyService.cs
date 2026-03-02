using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ScreenSealWindows.Services;

/// <summary>
/// Registers and manages system-wide global hotkeys using Win32 RegisterHotKey.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const uint MOD_CTRL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;

    // Virtual key codes
    private const uint VK_M = 0x4D;  // 'M' key
    private const uint VK_C = 0x43;  // 'C' key

    // Hotkey IDs
    public const int HOTKEY_CREATE = 1;
    public const int HOTKEY_CLEAR = 2;

    private const int WM_HOTKEY = 0x0312;

    private IntPtr _hwnd;
    private HwndSource? _source;
    private bool _registered;

    public event Action? CreateHotkeyPressed;
    public event Action? ClearHotkeyPressed;

    /// <summary>
    /// Must be called after a WPF window is loaded to get an HWND for hotkey registration.
    /// We create a hidden message-only window for this purpose.
    /// </summary>
    public void Initialize(Models.AppSettings settings)
    {
        // Create a hidden window to receive hotkey messages
        var parameters = new HwndSourceParameters("ScreenSealHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0 // invisible
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        _hwnd = _source.Handle;

        // Register Create Hotkey
        RegisterHotKey(_hwnd, HOTKEY_CREATE, settings.CreateHotkeyModifiers | MOD_NOREPEAT, settings.CreateHotkeyKey);

        // Register Clear All Hotkey
        RegisterHotKey(_hwnd, HOTKEY_CLEAR, settings.ClearHotkeyModifiers | MOD_NOREPEAT, settings.ClearHotkeyKey);

        _registered = true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            switch (id)
            {
                case HOTKEY_CREATE:
                    CreateHotkeyPressed?.Invoke();
                    handled = true;
                    break;
                case HOTKEY_CLEAR:
                    ClearHotkeyPressed?.Invoke();
                    handled = true;
                    break;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HOTKEY_CREATE);
            UnregisterHotKey(_hwnd, HOTKEY_CLEAR);
            _registered = false;
        }
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
    }
}
