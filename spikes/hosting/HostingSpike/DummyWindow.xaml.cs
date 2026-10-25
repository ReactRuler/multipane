using System.Windows;
using System.Windows.Input;

namespace HostingSpike;

/// <summary>Stands in for a Claude window in self-tests and manual trials: typing, focus, a popup, a file dialog, a hang, a crash.</summary>
public partial class DummyWindow : Window
{
    public DummyWindow(string name)
    {
        InitializeComponent();
        Title = "Dummy " + name;
        Heading.Text = Title;
        Status.Text = "Type here to test keyboard input.";
        // "Stubborn" mimics an app that enforces its own bounds in WM_WINDOWPOSCHANGING, and starts maximised, like a snapped Claude window.
        if (name == "Stubborn")
        {
            WindowState = WindowState.Maximized;
            SourceInitialized += (_, _) => System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle)!.AddHook(StubbornHook);
        }
        // Every dummy answers WM_APP+1 by resizing itself, the way a user dragging Electron's own resize edge would.
        SourceInitialized += (_, _) => System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle)!.AddHook(SelfResizeHook);
    }

    public const int WM_SELF_RESIZE = 0x8000 + 1;

    private static IntPtr SelfResizeHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_SELF_RESIZE) return IntPtr.Zero;
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 520, 380, NativeMethods.SWP_NOZORDER | 0x0002 /* NOMOVE */);
        handled = true;
        return IntPtr.Zero;
    }

    private static IntPtr StubbornHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_WINDOWPOSCHANGING = 0x0046;
        if (msg != WM_WINDOWPOSCHANGING) return IntPtr.Zero;
        var pos = System.Runtime.InteropServices.Marshal.PtrToStructure<WINDOWPOS>(lParam);
        if ((pos.flags & 0x0001) == 0) { pos.cx = Math.Min(pos.cx, 900); pos.cy = Math.Min(pos.cy, 600); System.Runtime.InteropServices.Marshal.StructureToPtr(pos, lParam, false); }
        return IntPtr.Zero;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct WINDOWPOS { public IntPtr hwnd, hwndInsertAfter; public int x, y, cx, cy; public uint flags; }

    private void OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => Status.Text = $"{Input.Text.Length} characters";
    private void OnFocus(object sender, KeyboardFocusChangedEventArgs e) => Status.Text = Input.IsKeyboardFocused ? "Input has keyboard focus" : "Input lost focus";
    private void OnPopup(object sender, RoutedEventArgs e) => MessageBox.Show(this, "An owned popup, like Claude's dialogs.", Title);
    private void OnFileDialog(object sender, RoutedEventArgs e) => new Microsoft.Win32.OpenFileDialog().ShowDialog(this);
    private void OnHang(object sender, RoutedEventArgs e) => Thread.Sleep(5000);
    private void OnCrash(object sender, RoutedEventArgs e) => Environment.Exit(5);
}
