using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using ScreenSealWindows.Models;
using ScreenSealWindows.Services;
using ScreenSealWindows.Windows;
using Application = System.Windows.Application;

namespace ScreenSealWindows;

public partial class App : Application
{
    private NotifyIcon? _trayIcon;
    private WindowManager _windowManager = null!;
    private GlobalHotkeyService _hotkeyService = null!;
    private SettingsService _settingsService = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Force high quality DPI scaling for multi-monitor setups
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
        
        base.OnStartup(e);

        _settingsService = new SettingsService();
        _windowManager = new WindowManager(_settingsService);
        _windowManager.WindowsChanged += RebuildTrayMenu;

        // Initialize global hotkeys
        _hotkeyService = new GlobalHotkeyService();
        _hotkeyService.Initialize(_settingsService.Current);
        _hotkeyService.CreateHotkeyPressed += OnCreateHotkey;
        _hotkeyService.ClearHotkeyPressed += OnClearHotkey;

        SetupTrayIcon();
    }

    /// <summary>
    /// Ctrl+Shift+M pressed: open the snip selection window.
    /// </summary>
    private void OnCreateHotkey()
    {
        Dispatcher.Invoke(() =>
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            var snipWindows = new List<SnipWindow>();

            foreach (var screen in screens)
            {
                var sw = new SnipWindow(screen);
                snipWindows.Add(sw);

                sw.RegionSelected += rect =>
                {
                    // Close all snip windows
                    foreach (var w in snipWindows.ToList()) w.Close();
                    _windowManager.CreateWindowAt(rect.X, rect.Y, rect.Width, rect.Height);
                };

                sw.Closed += (s, e) =>
                {
                    // If one closes (e.g. Escape), close others
                    foreach (var w in snipWindows.ToList()) 
                    {
                        if (w != s && w.IsVisible) w.Close();
                    }
                };
            }

            foreach (var w in snipWindows) w.Show();
        });
    }

    /// <summary>
    /// Ctrl+Shift+C pressed: remove all mosaic windows.
    /// </summary>
    private void OnClearHotkey()
    {
        Dispatcher.Invoke(() => _windowManager.RemoveAllWindows());
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Visible = true,
            Text = "ScreenSeal  (Ctrl+Shift+M: 생성 / Ctrl+Shift+C: 모두 삭제)",
            Icon = SystemIcons.Shield
        };

        _trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                OnCreateHotkey(); // Left-click also opens snip mode
            }
        };

        RebuildTrayMenu();
    }

    private void RebuildTrayMenu()
    {
        var menu = new ContextMenuStrip();

        // --- Header ---
        var headerItem = new ToolStripLabel("🛡️ ScreenSeal")
        {
            Font = new Font("Segoe UI", 10, System.Drawing.FontStyle.Bold)
        };
        menu.Items.Add(headerItem);
        menu.Items.Add(new ToolStripSeparator());

        // --- New Mosaic (Snip mode) ---
        var newItem = new ToolStripMenuItem("➕ New Mosaic (Ctrl+Shift+M)");
        newItem.Click += (_, _) => Dispatcher.Invoke(OnCreateHotkey);
        menu.Items.Add(newItem);

        // --- Clear All ---
        var clearItem = new ToolStripMenuItem("🗑️ Clear All (Ctrl+Shift+C)");
        clearItem.Click += (_, _) => Dispatcher.Invoke(() => _windowManager.RemoveAllWindows());
        menu.Items.Add(clearItem);

        menu.Items.Add(new ToolStripSeparator());

        // --- Window list ---
        if (_windowManager.Windows.Count > 0)
        {
            var windowsHeader = new ToolStripLabel($"Windows ({_windowManager.Windows.Count}):");
            menu.Items.Add(windowsHeader);

            foreach (var window in _windowManager.Windows)
            {
                var wItem = new ToolStripMenuItem(
                    $"{(window.IsVisible ? "👁" : "👁‍🗨")} {window.DisplayName}")
                {
                    Checked = window.IsVisible
                };
                var captured = window;
                wItem.Click += (_, _) => Dispatcher.Invoke(() => _windowManager.ToggleWindow(captured));
                menu.Items.Add(wItem);
            }

            menu.Items.Add(new ToolStripSeparator());
        }

        // --- Presets ---
        var presetsMenu = new ToolStripMenuItem("📋 Presets");

        if (_windowManager.Windows.Count > 0)
        {
            var saveItem = new ToolStripMenuItem("💾 Save Current Layout...");
            saveItem.Click += (_, _) => Dispatcher.Invoke(() =>
            {
                var name = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter a name for this preset:", "Save Preset",
                    $"Preset {_windowManager.PresetManager.Presets.Count + 1}");
                if (!string.IsNullOrWhiteSpace(name))
                    _windowManager.SaveCurrentLayout(name);
            });
            presetsMenu.DropDownItems.Add(saveItem);
        }

        if (_windowManager.PresetManager.Presets.Count > 0)
        {
            presetsMenu.DropDownItems.Add(new ToolStripSeparator());

            foreach (var preset in _windowManager.PresetManager.Presets)
            {
                var loadItem = new ToolStripMenuItem($"📁 {preset.Name}");
                var capturedPreset = preset;
                loadItem.Click += (_, _) => Dispatcher.Invoke(() => _windowManager.LoadPreset(capturedPreset));
                presetsMenu.DropDownItems.Add(loadItem);
            }
        }

        menu.Items.Add(presetsMenu);
        menu.Items.Add(new ToolStripSeparator());

        // --- Settings (Custom Hotkeys) ---
        var settingsItem = new ToolStripMenuItem("⚙️ Settings");
        settingsItem.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            var sw = new SettingsWindow(_settingsService, () => {
                _hotkeyService.Dispose();
                _hotkeyService = new GlobalHotkeyService();
                _hotkeyService.Initialize(_settingsService.Current);
                _hotkeyService.CreateHotkeyPressed += OnCreateHotkey;
                _hotkeyService.ClearHotkeyPressed += OnClearHotkey;
                RebuildTrayMenu();
            });
            sw.Show();
        });
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());

        // --- Run on Startup ---
        var startupItem = new ToolStripMenuItem("🚀 Run on Startup")
        {
            Checked = StartupHelper.IsStartupEnabled(),
            CheckOnClick = true
        };
        startupItem.CheckedChanged += (_, _) =>
        {
            StartupHelper.SetStartupEnabled(startupItem.Checked);
        };
        menu.Items.Add(startupItem);

        menu.Items.Add(new ToolStripSeparator());

        // --- Shortcut info ---
        var infoItem = new ToolStripLabel("⌨️ Ctrl+Shift+M: New / Ctrl+Shift+C: Clear")
        {
            ForeColor = System.Drawing.Color.Gray,
            Font = new Font("Segoe UI", 8)
        };
        menu.Items.Add(infoItem);

        menu.Items.Add(new ToolStripSeparator());

        // --- Quit ---
        var quitItem = new ToolStripMenuItem("🚪 Quit ScreenSeal");
        quitItem.Click += (_, _) =>
        {
            _windowManager.RemoveAllWindows();
            _hotkeyService.Dispose();
            _trayIcon!.Visible = false;
            _trayIcon.Dispose();
            Dispatcher.Invoke(() => Shutdown());
        };
        menu.Items.Add(quitItem);

        _trayIcon!.ContextMenuStrip = menu;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
