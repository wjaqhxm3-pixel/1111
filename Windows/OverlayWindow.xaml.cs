using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ScreenSealWindows.Helpers;
using ScreenSealWindows.Models;
using ScreenSealWindows.Services;

namespace ScreenSealWindows.Windows;

public partial class OverlayWindow : Window
{
    public OverlayConfiguration Configuration { get; } = new();
    public int WindowIndex { get; }
    public string DisplayName => $"Mosaic #{WindowIndex}";
    public bool IsMenuOpen { get; private set; }
    public WindowManager? Manager { get; set; }
    
    /// <summary>
    /// Stores the base clean image captured exactly once at creation or position change.
    /// This prevents the infinite feedback loop.
    /// </summary>
    public System.Drawing.Bitmap? OriginalSnapshot { get; set; }

    private bool _isLocked;
    private System.Windows.Point _dragStart;
    private bool _isDragging;
    private readonly SettingsService _settingsService;
    private readonly FilterProcessor _filterProcessor = FilterProcessor.Shared;

    public OverlayWindow(int index, SettingsService settingsService)
    {
        InitializeComponent();
        WindowIndex = index;
        _settingsService = settingsService;
        DataContext = this;

        Configuration.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(OverlayConfiguration.MosaicType) ||
                e.PropertyName == nameof(OverlayConfiguration.Intensity) ||
                e.PropertyName == nameof(OverlayConfiguration.SolidColor))
            {
                // Save settings
                if (e.PropertyName == nameof(OverlayConfiguration.MosaicType))
                    _settingsService.Current.DefaultMosaicType = Configuration.MosaicType.ToString();
                else if (e.PropertyName == nameof(OverlayConfiguration.Intensity))
                    _settingsService.Current.DefaultIntensity = Configuration.Intensity;
                else if (e.PropertyName == nameof(OverlayConfiguration.SolidColor))
                    _settingsService.Current.SolidColorHex = System.Drawing.ColorTranslator.ToHtml(Configuration.SolidColor);
                
                _settingsService.Save();
                InvalidateMosaic();
            }
        };

        Loaded += OnLoaded;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseWheel += OnMouseWheel;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);

        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW);
    }

    private void OnLoaded(object sender, RoutedEventArgs e) { }

    private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        AnimateOpacity(CloseButton, 0.9, 150);
        if (!_isLocked)
        {
            AnimateOpacity(ResizeBorder, 0.6, 150);
            AnimateOpacity(CustomResizeGrip, 1.0, 150);
        }
    }

    private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        AnimateOpacity(CloseButton, 0, 300);
        AnimateOpacity(ResizeBorder, 0, 300);
        AnimateOpacity(CustomResizeGrip, 0, 300);
    }

    private static void AnimateOpacity(UIElement element, double targetOpacity, int durationMs)
    {
        var anim = new DoubleAnimation
        {
            To = targetOpacity,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new QuadraticEase()
        };
        element.BeginAnimation(OpacityProperty, anim);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        OriginalSnapshot?.Dispose();
        OriginalSnapshot = null;
    }

    public void InvalidateMosaic()
    {
        if (OriginalSnapshot == null) return;

        using var filtered = _filterProcessor.ApplyFilter(
            OriginalSnapshot,
            Configuration.MosaicType,
            Configuration.Intensity,
            Configuration.SolidColor);

        var hwnd = new WindowInteropHelper(this).Handle;
        var hBitmap = filtered.GetHbitmap();
        try
        {
            var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmapSource.Freeze();
            MosaicImage.Source = bitmapSource;
        }
        finally
        {
            NativeMethods.DeleteObject(hBitmap);
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement fe && IsChildOf(fe, CloseButton)) return;
        if (_isLocked) return;
        _isDragging = true;
        _dragStart = e.GetPosition(this);
        CaptureMouse();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging || _isLocked) return;
        var pos = e.GetPosition(this);
        var diff = pos - _dragStart;
        Left += diff.X;
        Top += diff.Y;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            // Snapshot will be refreshed via WM_EXITSIZEMOVE
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        // Snapshot will be refreshed via WM_EXITSIZEMOVE
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        double delta = e.Delta > 0 ? 2 : -2;
        Configuration.Intensity += delta;
        e.Handled = true;
    }

    public void UpdateMosaicImage(BitmapSource source) => MosaicImage.Source = source;

    public void LockPosition()
    {
        _isLocked = true;
        ResizeMode = ResizeMode.NoResize;
        ResizeBorder.Opacity = 0;
        CustomResizeGrip.Opacity = 0;
    }

    public void UnlockPosition()
    {
        _isLocked = false;
        ResizeMode = ResizeMode.CanResize;
    }

    private void CustomResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isLocked) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(hwnd, NativeMethods.WM_SYSCOMMAND, (IntPtr)(NativeMethods.SC_SIZE + 8), IntPtr.Zero);
        e.Handled = true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        const int WM_EXITSIZEMOVE = 0x0232;

        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = (NativeMethods.MINMAXINFO)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(NativeMethods.MINMAXINFO))!;
            mmi.ptMinTrackSize.X = 10;
            mmi.ptMinTrackSize.Y = 10;
            System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, lParam, true);
            handled = true;
        }
        else if (msg == WM_EXITSIZEMOVE)
        {
            // Moving or Resizing has finished. Refresh the background capture once.
            Manager?.RefreshWindowSnapshot(this);
        }
        return IntPtr.Zero;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();
        menu.Opened += (s, args) => IsMenuOpen = true;
        menu.Closed += (s, args) => IsMenuOpen = false;

        foreach (MosaicType mType in Enum.GetValues<MosaicType>())
        {
            string header = mType switch
            {
                MosaicType.RetroGaming => "👾 Retro Gaming (8/16/32-bit)",
                MosaicType.JpegCompression => "🖼️ JPG Mode (Artifacts)",
                MosaicType.Pixelation => "🟥 Pixelation",
                MosaicType.GaussianBlur => "🌫️ Gaussian Blur",
                _ => mType.ToString()
            };

            var item = new MenuItem { Header = header, IsCheckable = true, IsChecked = Configuration.MosaicType == mType };
            var captured = mType;
            item.Click += (_, _) => Configuration.MosaicType = captured;
            menu.Items.Add(item);
        }

        if (Configuration.MosaicType == MosaicType.SolidColor)
        {
            var colorItem = new MenuItem { Header = "🎨 단일 색상 선택 (Choose Color...)" };
            colorItem.Click += (_, _) =>
            {
                using var dialog = new System.Windows.Forms.ColorDialog();
                dialog.Color = Configuration.SolidColor;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) Configuration.SolidColor = dialog.Color;
            };
            menu.Items.Add(colorItem);
        }

        menu.Items.Add(new Separator());
        var intensityItem = new MenuItem { Header = "Intensity" };
        var slider = new Slider { Minimum = 1, Maximum = 100, Value = Configuration.Intensity, Width = 150 };
        slider.ValueChanged += (_, args) => Configuration.Intensity = args.NewValue;
        intensityItem.Items.Add(slider);
        menu.Items.Add(intensityItem);

        menu.Items.Add(new Separator());
        var lockItem = new MenuItem { Header = _isLocked ? "🔓 Unlock Position" : "🔒 Lock Position" };
        lockItem.Click += (_, _) => { if (_isLocked) UnlockPosition(); else LockPosition(); };
        menu.Items.Add(lockItem);

        menu.Items.Add(new Separator());
        var closeItem = new MenuItem { Header = "✕ Close This Window" };
        closeItem.Click += (_, _) => Close();
        menu.Items.Add(closeItem);

        menu.IsOpen = true;
        e.Handled = true;
    }

    private static bool IsChildOf(DependencyObject child, DependencyObject parent)
    {
        var current = child;
        while (current != null)
        {
            if (current == parent) return true;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return false;
    }
}
