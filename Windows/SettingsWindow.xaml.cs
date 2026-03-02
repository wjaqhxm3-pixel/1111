using System;
using System.Windows;
using System.Windows.Input;
using ScreenSealWindows.Models;
using ScreenSealWindows.Services;

namespace ScreenSealWindows.Windows;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly Action _onSaved;

    public SettingsWindow(SettingsService settingsService, Action onSaved)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _settings = settingsService.Current;
        _onSaved = onSaved;

        UpdateBoxes();
    }

    private void UpdateBoxes()
    {
        CreateHotkeyBox.Text = FormatHotkey(_settings.CreateHotkeyModifiers, _settings.CreateHotkeyKey);
        ClearHotkeyBox.Text = FormatHotkey(_settings.ClearHotkeyModifiers, _settings.ClearHotkeyKey);
    }

    private string FormatHotkey(uint modifiers, uint key)
    {
        string modStr = "";
        if ((modifiers & 0x0002) != 0) modStr += "Ctrl + ";
        if ((modifiers & 0x0004) != 0) modStr += "Shift + ";
        if ((modifiers & 0x0001) != 0) modStr += "Alt + ";
        
        var wpfKey = KeyInterop.KeyFromVirtualKey((int)key);
        return modStr + wpfKey.ToString();
    }

    private void ProcessHotkey(System.Windows.Input.KeyEventArgs e, Action<uint, uint> setter)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LWin || key == Key.RWin)
        {
            return; // Only set when a non-modifier key is pressed
        }

        uint mods = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= 0x0002;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= 0x0004;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= 0x0001;

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        setter(mods, vk);
        UpdateBoxes();
    }

    private void CreateHotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        ProcessHotkey(e, (mods, key) => {
            _settings.CreateHotkeyModifiers = mods;
            _settings.CreateHotkeyKey = key;
        });
    }

    private void ClearHotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        ProcessHotkey(e, (mods, key) => {
            _settings.ClearHotkeyModifiers = mods;
            _settings.ClearHotkeyKey = key;
        });
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settingsService.Save();
        _onSaved();
        Close();
    }
}
