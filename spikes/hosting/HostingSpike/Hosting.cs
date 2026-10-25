using System.Runtime.InteropServices;
using static HostingSpike.NativeMethods;

namespace HostingSpike;

public enum HostMode { A_Reparent, B_Owned }

/// <summary>What a hosted window looked like before we touched it, so Release can put it back.</summary>
public sealed record Original(uint Style, uint ExStyle, IntPtr Owner, RECT Rect);

/// <summary>One hosted foreign window. Approach A reparents it; approach B keeps it top-level, owned, and placed by us.</summary>
public sealed class HostedWindow
{
    public IntPtr Hwnd { get; }
    public HostMode Mode { get; }
    public Original Before { get; }
    public bool ToolWindowStyle { get; }
    private readonly IntPtr _host;      // host main window (owner for B)
    private readonly IntPtr _pane;      // child HWND of the host for A
    private bool _released;
    private IntPtr _hook;
    private WinEventDelegate? _hookProc; // kept alive for the hook's lifetime
    private bool _snapping;

    private HostedWindow(IntPtr hwnd, HostMode mode, IntPtr host, IntPtr pane, Original before, bool toolWindow)
    {
        Hwnd = hwnd; Mode = mode; _host = host; _pane = pane; Before = before; ToolWindowStyle = toolWindow;
        // Electron draws its own frame and handles its own resize edges, so the user can still drag the window's edges or move it.
        // Watch its location and snap it back to the pane whenever it changes on its own.
        GetWindowThreadProcessId(hwnd, out var pid);
        _hookProc = OnLocationChanged;
        _hook = SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, _hookProc, pid, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    }

    private void OnLocationChanged(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd != Hwnd || idObject != 0 || _released || Hidden || _snapping || _last.Width == 0) return;
        if (Matches(_last)) return;
        _snapping = true;
        try
        {
            var flags = SWP_NOZORDER | SWP_NOACTIVATE | (Mode == HostMode.B_Owned ? SWP_NOOWNERZORDER : 0) | SWP_NOSENDCHANGING;
            if (Mode == HostMode.A_Reparent) SetWindowPos(Hwnd, IntPtr.Zero, 0, 0, _last.Width, _last.Height, flags);
            else SetWindowPos(Hwnd, IntPtr.Zero, _last.Left, _last.Top, _last.Width, _last.Height, flags);
            SnapCount++;
        }
        finally { _snapping = false; }
    }

    /// <summary>How often the window had to be snapped back after moving or resizing itself. Shown in the log for the matrix.</summary>
    public int SnapCount { get; private set; }

    public static HostedWindow Attach(IntPtr hwnd, HostMode mode, IntPtr hostHwnd, IntPtr paneHwnd, bool toolWindowStyle, Action<string> log)
    {
        // A maximised or minimised window keeps that state bit after the styles change and then ignores our rectangle
        // (seen with a maximised Claude window: it sat at its restore bounds inside the pane). Restore it first.
        if (IsZoomed(hwnd) || IsIconic(hwnd)) { ShowWindow(hwnd, SW_RESTORE); log($"attach {hwnd:X}: window was maximised or minimised, restored first"); }
        GetWindowRect(hwnd, out var rect);
        var before = new Original(Style(hwnd), ExStyle(hwnd), GetWindowLongPtr(hwnd, GWLP_HWNDPARENT), rect);
        log($"attach {hwnd:X} mode={mode} style=0x{before.Style:X8} ex=0x{before.ExStyle:X8} owner={before.Owner:X}");

        var style = before.Style & ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
        if (mode == HostMode.A_Reparent)
        {
            style = (style & ~WS_POPUP) | WS_CHILD;
            SetStyle(hwnd, style);
            var prev = SetParent(hwnd, paneHwnd);
            if (prev == IntPtr.Zero && Marshal.GetLastWin32Error() != 0) log($"SetParent failed: {Marshal.GetLastWin32Error()}");
        }
        else
        {
            SetStyle(hwnd, style);
            if (toolWindowStyle) SetExStyle(hwnd, (before.ExStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
            SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, hostHwnd);
            var err = Marshal.GetLastWin32Error();
            if (err != 0) log($"set owner: last error {err}");
        }
        // Apply the frame change; the caller places it right after.
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED | 0x0001 | 0x0002); // NOMOVE|NOSIZE
        log($"after attach style=0x{Style(hwnd):X8} ex=0x{ExStyle(hwnd):X8} parent={GetParent(hwnd):X} owner={GetWindowLongPtr(hwnd, GWLP_HWNDPARENT):X}");
        return new HostedWindow(hwnd, mode, hostHwnd, paneHwnd, before, toolWindowStyle);
    }

    /// <summary>Place over the pane. Screen rectangle in device pixels; for A it is converted to pane-client coordinates (always 0,0).</summary>
    public bool Hidden { get; private set; }
    private RECT _last;

    private bool _mismatchLogged;
    private Action<string>? _placeLog;
    public void LogPlacementTo(Action<string> log) => _placeLog = log;

    /// <summary>
    /// Places only when visible and only when the rectangle changed; a hidden window is never re-shown by placement.
    /// If the app refuses the rectangle (a WM_WINDOWPOSCHANGING handler enforcing its own bounds, as Chromium can), the
    /// placement is repeated with SWP_NOSENDCHANGING, which skips that message, and the outcome is logged once.
    /// </summary>
    public bool Place(RECT screenRect)
    {
        if (_released || Hidden || !IsWindow(Hwnd)) return true;
        if (screenRect.Left == _last.Left && screenRect.Top == _last.Top && screenRect.Width == _last.Width && screenRect.Height == _last.Height) return true;
        _last = screenRect;
        var flags = SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW | (Mode == HostMode.B_Owned ? SWP_NOOWNERZORDER : 0);
        var ok = Mode == HostMode.A_Reparent
            ? SetWindowPos(Hwnd, IntPtr.Zero, 0, 0, screenRect.Width, screenRect.Height, flags)
            : SetWindowPos(Hwnd, IntPtr.Zero, screenRect.Left, screenRect.Top, screenRect.Width, screenRect.Height, flags);
        if (!ok) return false;
        if (Mode == HostMode.B_Owned && !Matches(screenRect))
        {
            GetWindowRect(Hwnd, out var got);
            SetWindowPos(Hwnd, IntPtr.Zero, screenRect.Left, screenRect.Top, screenRect.Width, screenRect.Height, flags | SWP_NOSENDCHANGING);
            var fixedNow = Matches(screenRect);
            if (!_mismatchLogged)
            {
                _mismatchLogged = true;
                _placeLog?.Invoke($"placement refused by {Hwnd:X}: asked ({screenRect.Left},{screenRect.Top} {screenRect.Width}x{screenRect.Height}) got ({got.Left},{got.Top} {got.Width}x{got.Height}); retry with SWP_NOSENDCHANGING {(fixedNow ? "worked" : "did not help")}");
            }
        }
        return true;
    }

    private bool Matches(RECT want)
    {
        GetWindowRect(Hwnd, out var r);
        return Math.Abs(r.Left - want.Left) <= 2 && Math.Abs(r.Top - want.Top) <= 2 && Math.Abs(r.Width - want.Width) <= 2 && Math.Abs(r.Height - want.Height) <= 2;
    }

    /// <summary>Raw DWM cloak call, kept to record its result: across processes it returns E_ACCESSDENIED (finding, see RESULTS.md).</summary>
    public int SetCloak(bool cloak) => Cloak(Hwnd, cloak);

    /// <summary>Hide or show the hosted window. Tries DWM cloaking first; when that is refused, falls back to ShowWindow. Returns which path was used.</summary>
    public string SetHidden(bool hidden)
    {
        Hidden = hidden;
        _last = default; // force a real placement on the next Place after showing
        if (Cloak(Hwnd, hidden) == 0 && IsCloaked(Hwnd) == hidden) return "cloak";
        ShowWindow(Hwnd, hidden ? SW_HIDE : SW_SHOWNA);
        return "showwindow";
    }

    /// <summary>Undo everything: owner or parent, styles, original rectangle.</summary>
    public void Release(Action<string> log)
    {
        if (_released) return;
        _released = true;
        if (_hook != IntPtr.Zero) { UnhookWinEvent(_hook); _hook = IntPtr.Zero; _hookProc = null; }
        if (SnapCount > 0) log($"release {Hwnd:X}: snapped back {SnapCount} times after the window moved or resized itself");
        if (!IsWindow(Hwnd)) { log($"release {Hwnd:X}: window gone"); return; }
        Cloak(Hwnd, false);
        if (Mode == HostMode.A_Reparent)
        {
            SetStyle(Hwnd, Before.Style);
            SetParent(Hwnd, IntPtr.Zero);
        }
        else
        {
            SetWindowLongPtr(Hwnd, GWLP_HWNDPARENT, Before.Owner);
            SetStyle(Hwnd, Before.Style);
            SetExStyle(Hwnd, Before.ExStyle);
        }
        SetWindowPos(Hwnd, IntPtr.Zero, Before.Rect.Left, Before.Rect.Top, Before.Rect.Width, Before.Rect.Height, SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
        log($"release {Hwnd:X}: style=0x{Style(Hwnd):X8} ex=0x{ExStyle(Hwnd):X8} parent={GetParent(Hwnd):X} owner={GetWindowLongPtr(Hwnd, GWLP_HWNDPARENT):X}");
    }
}
