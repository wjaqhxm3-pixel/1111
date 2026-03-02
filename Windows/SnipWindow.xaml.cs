using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ScreenSealWindows.Windows;

/// <summary>
/// Full-screen transparent overlay for selecting a region by dragging.
/// Similar to the Windows Snipping Tool capture mode.
/// </summary>
public partial class SnipWindow : Window
{
    private System.Windows.Point _startPoint;
    private ScreenSealWindows.Helpers.NativeMethods.POINT _startPhysical;
    private bool _isDragging;

    /// <summary>
    /// Fired when user finishes selecting a region. Provides the screen-space rectangle.
    /// </summary>
    public event Action<Rect>? RegionSelected;

    public SnipWindow(System.Windows.Forms.Screen screen)
    {
        InitializeComponent();

        // Position window to cover the specific screen
        // Use logical units for Left/Top/Width/Height
        // Screen.Bounds is in physical pixels, but WPF Windows use logical units
        // We can just use the screen's working area or bounds and assume WPF handles scaling
        // if we set the window to the monitor where it should be.
        
        this.Left = screen.Bounds.Left;
        this.Top = screen.Bounds.Top;
        this.Width = screen.Bounds.Width;
        this.Height = screen.Bounds.Height;

        // Note: setting Left/Top/Width/Height might be affected by DPI if not careful.
        // A better way is to set them and then force position via SourceInitialized.

        SourceInitialized += (s, e) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            Helpers.NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 
                screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height,
                0);
        };

        KeyDown += OnKeyDown;
        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMoveHandler;
        MouseLeftButtonUp += OnMouseUp;
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _startPoint = e.GetPosition(SnipCanvas);
        ScreenSealWindows.Helpers.NativeMethods.GetCursorPos(out _startPhysical);

        SelectionRect.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRect, _startPoint.X);
        Canvas.SetTop(SelectionRect, _startPoint.Y);
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
        InstructionText.Visibility = Visibility.Collapsed;
        CaptureMouse();
    }

    private void OnMouseMoveHandler(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging) return;

        var current = e.GetPosition(SnipCanvas);
        var x = Math.Min(_startPoint.X, current.X);
        var y = Math.Min(_startPoint.Y, current.Y);
        var w = Math.Abs(current.X - _startPoint.X);
        var h = Math.Abs(current.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = w;
        SelectionRect.Height = h;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();

        var current = e.GetPosition(SnipCanvas);
        var x = Math.Min(_startPoint.X, current.X);
        var y = Math.Min(_startPoint.Y, current.Y);
        var w = Math.Abs(current.X - _startPoint.X);
        var h = Math.Abs(current.Y - _startPoint.Y);

        // Minimum size check
        if (w >= 20 && h >= 20)
        {
            // Use PointToScreen to get PHYSICAL coordinates for the captured region
            var physicalTopLeft = PointToScreen(new System.Windows.Point(x, y));
            var physicalBottomRight = PointToScreen(new System.Windows.Point(x + w, y + h));

            var screenRect = new Rect(
                physicalTopLeft.X, 
                physicalTopLeft.Y, 
                physicalBottomRight.X - physicalTopLeft.X, 
                physicalBottomRight.Y - physicalTopLeft.Y);

            RegionSelected?.Invoke(screenRect);
        }

        Close();
    }
}
