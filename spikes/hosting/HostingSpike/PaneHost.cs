using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace HostingSpike;

/// <summary>A plain Win32 child window inside the WPF layout. Approach A reparents into it; approach B only reads its screen rectangle.</summary>
public sealed class PaneHost : HwndHost
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint exStyle, string className, string? windowName, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hwnd);

    private const uint WS_CLIPCHILDREN = 0x02000000;

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        var h = CreateWindowEx(0, "STATIC", null, NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE | WS_CLIPCHILDREN, 0, 0, 10, 10, hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (h == IntPtr.Zero) throw new InvalidOperationException("CreateWindowEx failed: " + Marshal.GetLastWin32Error());
        return new HandleRef(this, h);
    }

    protected override void DestroyWindowCore(HandleRef hwnd) => DestroyWindow(hwnd.Handle);

    /// <summary>Raised after the native pane window really moved. SizeChanged fires one layout pass too early for GetWindowRect.</summary>
    public event Action? Positioned;

    protected override void OnWindowPositionChanged(Rect rcBoundingBox)
    {
        base.OnWindowPositionChanged(rcBoundingBox);
        Positioned?.Invoke();
    }

    public NativeMethods.RECT ScreenRect()
    {
        NativeMethods.GetWindowRect(Handle, out var r);
        return r;
    }
}
