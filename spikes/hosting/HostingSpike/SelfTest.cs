using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using static HostingSpike.NativeMethods;

namespace HostingSpike;

/// <summary>
/// Automated check of the hosting mechanics against two dummy windows: attach (A and B), placement after a resize,
/// cloaking, release, and that the target processes survive. Real-Claude rows of the matrix stay manual.
/// </summary>
public static class SelfTest
{
    private sealed record Check(string Name, bool Pass, string Detail);

    public static async Task RunAsync(MainWindow win)
    {
        var checks = new List<Check>();
        var exe = Environment.ProcessPath!;
        var dummies = new List<Process>();
        void Add(string name, bool pass, string detail) { checks.Add(new Check(name, pass, detail)); win.LogLine($"{(pass ? "PASS" : "FAIL")} {name}: {detail}"); }

        try
        {
            await Task.Delay(500);
            dummies.Add(Process.Start(exe, "--dummy One"));
            dummies.Add(Process.Start(exe, "--dummy Two"));
            var one = await WaitForWindowAsync("Dummy One");
            var two = await WaitForWindowAsync("Dummy Two");
            Add("dummies appeared", one != IntPtr.Zero && two != IntPtr.Zero, $"one={one:X} two={two:X}");
            if (one == IntPtr.Zero || two == IntPtr.Zero) return;

            win.Activate();
            var hA = win.Host(one, 0, HostMode.A_Reparent)!;
            var hB = win.Host(two, 1, HostMode.B_Owned)!;
            await Task.Delay(400);

            Add("A: styles stripped", (Style(one) & (WS_CAPTION | WS_THICKFRAME)) == 0 && (Style(one) & WS_CHILD) != 0, $"style=0x{Style(one):X8}");
            Add("A: parent is pane 1", GetParent(one) == win.Panes[0].Handle, $"parent={GetParent(one):X} pane={win.Panes[0].Handle:X}");
            Add("B: styles stripped, still top-level", (Style(two) & (WS_CAPTION | WS_THICKFRAME)) == 0 && (Style(two) & WS_CHILD) == 0, $"style=0x{Style(two):X8}");
            Add("B: owner is host", GetWindowLongPtr(two, GWLP_HWNDPARENT) == win.Hwnd, $"owner={GetWindowLongPtr(two, GWLP_HWNDPARENT):X} host={win.Hwnd:X}");
            Add("B: tool window style applied", (ExStyle(two) & WS_EX_TOOLWINDOW) != 0, $"ex=0x{ExStyle(two):X8}");

            Add("A: placed over pane 1", RectMatches(one, win.Panes[0].ScreenRect()), Describe(one, win.Panes[0].ScreenRect()));
            Add("B: placed over pane 2", RectMatches(two, win.Panes[1].ScreenRect()), Describe(two, win.Panes[1].ScreenRect()));

            win.Width += 240; win.Height += 60;
            await Task.Delay(400);
            Add("A: follows resize", RectMatches(one, win.Panes[0].ScreenRect()), Describe(one, win.Panes[0].ScreenRect()));
            Add("B: follows resize", RectMatches(two, win.Panes[1].ScreenRect()), Describe(two, win.Panes[1].ScreenRect()));

            win.Left += 120;
            await Task.Delay(400);
            Add("B: follows move", RectMatches(two, win.Panes[1].ScreenRect()), Describe(two, win.Panes[1].ScreenRect()));

            var hr = hB.SetCloak(true); await Task.Delay(100);
            Add("finding: DWM cloak across processes", true, $"hr=0x{hr:X8} cloaked={IsCloaked(two)} (0x80070005 = refused; informational)");
            hB.SetCloak(false);
            var path = hB.SetHidden(true); await Task.Delay(200);
            Add("B: hide", !IsWindowVisible(two), $"via {path}, visible={IsWindowVisible(two)}");
            path = hB.SetHidden(false); await Task.Delay(200);
            win.PlaceAll(); await Task.Delay(200);
            Add("B: show again, placed", IsWindowVisible(two) && RectMatches(two, win.Panes[1].ScreenRect()), $"via {path}, " + Describe(two, win.Panes[1].ScreenRect()));
            path = hA.SetHidden(true); await Task.Delay(200);
            Add("A: hide", !IsWindowVisible(one), $"via {path}, visible={IsWindowVisible(one)}");
            hA.SetHidden(false); win.PlaceAll(); await Task.Delay(200);
            Add("A: show again, placed", IsWindowVisible(one) && RectMatches(one, win.Panes[0].ScreenRect()), Describe(one, win.Panes[0].ScreenRect()));

            SetForegroundWindow(two); await Task.Delay(300);
            var fg = GetForegroundWindow();
            // Informational: Windows refuses foreground changes while the user is typing elsewhere, so this depends on the machine's state.
            Add("B: can take foreground (informational)", true, $"{(fg == two ? "yes" : "refused by Windows at that moment")} foreground={fg:X} target={two:X}");

            // The target resizes itself (what dragging Electron's own resize edge does); the host must snap it back.
            PostMessage(two, (uint)DummyWindow.WM_SELF_RESIZE, IntPtr.Zero, IntPtr.Zero); await Task.Delay(400);
            Add("B: snapped back after the window resized itself", RectMatches(two, win.Panes[1].ScreenRect()) && hB.SnapCount > 0, $"snaps={hB.SnapCount} " + Describe(two, win.Panes[1].ScreenRect()));
            PostMessage(one, (uint)DummyWindow.WM_SELF_RESIZE, IntPtr.Zero, IntPtr.Zero); await Task.Delay(400);
            Add("A: snapped back after the window resized itself", RectMatches(one, win.Panes[0].ScreenRect()) && hA.SnapCount > 0, $"snaps={hA.SnapCount} " + Describe(one, win.Panes[0].ScreenRect()));

            win.WindowState = WindowState.Minimized; await Task.Delay(500);
            Add("B: hidden with the minimised host (owner rule)", !IsWindowVisible(two), $"visible={IsWindowVisible(two)}");
            win.WindowState = WindowState.Normal; await Task.Delay(600);
            Add("B: visible and placed after restore", IsWindowVisible(two) && RectMatches(two, win.Panes[1].ScreenRect()), Describe(two, win.Panes[1].ScreenRect()));

            // Maximised, the host must stay inside the monitor's work area (not under the taskbar) and the panes must follow.
            win.WindowState = WindowState.Maximized; await Task.Delay(600);
            WorkAreaOf(win.Hwnd, out var work, out _);
            var pr = win.Panes[1].ScreenRect();
            Add("maximised: panes stay inside the work area (not under the taskbar)", pr.Left >= work.Left && pr.Top >= work.Top && pr.Right <= work.Right && pr.Bottom <= work.Bottom, $"pane=({pr.Left},{pr.Top})-({pr.Right},{pr.Bottom}) work=({work.Left},{work.Top})-({work.Right},{work.Bottom})");
            Add("B: placed after maximise", RectMatches(two, win.Panes[1].ScreenRect()), Describe(two, win.Panes[1].ScreenRect()));
            win.WindowState = WindowState.Normal; await Task.Delay(600);

            // Reorder: moving pane 2 back swaps the two columns; the windows follow their panes.
            win.MovePane(win.PanesList[1], -1); await Task.Delay(500);
            Add("move back: window two now in the first column", RectMatches(two, win.Panes[0].ScreenRect()) && RectMatches(one, win.Panes[1].ScreenRect()), Describe(two, win.Panes[0].ScreenRect()));
            win.MovePane(win.PanesList[0], +1); await Task.Delay(500);
            Add("move forward: order restored", RectMatches(one, win.Panes[0].ScreenRect()) && RectMatches(two, win.Panes[1].ScreenRect()), Describe(one, win.Panes[0].ScreenRect()));

            // Tabs mode: exactly one of the two hosted windows visible, placed over the pane; switching back shows both. (A crash lived here.)
            win.ApplyMode(LayoutMode.Tabs); await Task.Delay(500);
            var visibleCount = (IsWindowVisible(one) ? 1 : 0) + (IsWindowVisible(two) ? 1 : 0);
            Add("tabs: one window visible", visibleCount == 1, $"visible={visibleCount}");
            win.ApplyMode(LayoutMode.Columns); await Task.Delay(500);
            Add("columns again: both visible and placed", IsWindowVisible(one) && IsWindowVisible(two) && RectMatches(two, win.Panes[1].ScreenRect()), Describe(two, win.Panes[1].ScreenRect()));

            var beforeA = hA.Before; var beforeB = hB.Before;
            win.ReleaseAll();
            await Task.Delay(400);
            Add("A: released styles", Style(one) == beforeA.Style && GetParent(one) == IntPtr.Zero, $"style=0x{Style(one):X8} expected=0x{beforeA.Style:X8} parent={GetParent(one):X}");
            Add("B: released styles and owner", Style(two) == beforeB.Style && ExStyle(two) == beforeB.ExStyle && GetWindowLongPtr(two, GWLP_HWNDPARENT) == beforeB.Owner, $"style=0x{Style(two):X8} ex=0x{ExStyle(two):X8} owner={GetWindowLongPtr(two, GWLP_HWNDPARENT):X}");
            Add("B: back at its original rectangle", RectMatches(two, beforeB.Rect), Describe(two, beforeB.Rect));
            Add("targets alive after release", dummies.All(d => !d.HasExited) && IsWindow(one) && IsWindow(two), $"exited: {string.Join(",", dummies.Select(d => d.HasExited))}");

            // Host-crash simulation for B: attach again, then drop our side without Release. The owned window must survive.
            var hB2 = win.Host(two, 1, HostMode.B_Owned)!;
            await Task.Delay(300);
            SetWindowLongPtr(two, GWLP_HWNDPARENT, IntPtr.Zero); // what the OS does when the owner handle dies
            Add("B: survives losing its owner", IsWindow(two) && !dummies[1].HasExited, "owner cleared, window still there");
            hB2.Release(win.LogLine);
            win.ClearHosted(1);

            // A window that starts maximised and clamps its own size in WM_WINDOWPOSCHANGING (what a snapped Claude window did).
            dummies.Add(Process.Start(exe, "--dummy Stubborn"));
            var stubborn = await WaitForWindowAsync("Dummy Stubborn");
            Add("stubborn dummy appeared, maximised", stubborn != IntPtr.Zero && IsZoomed(stubborn), $"hwnd={stubborn:X} zoomed={IsZoomed(stubborn)}");
            if (stubborn != IntPtr.Zero)
            {
                var hS = win.Host(stubborn, 1, HostMode.B_Owned)!;
                await Task.Delay(500);
                Add("B: maximised window restored before hosting", !IsZoomed(stubborn) && (Style(stubborn) & WS_MAXIMIZE) == 0, $"zoomed={IsZoomed(stubborn)} style=0x{Style(stubborn):X8}");
                Add("B: size-clamping window still fills the pane", RectMatches(stubborn, win.Panes[1].ScreenRect()), Describe(stubborn, win.Panes[1].ScreenRect()));
                hS.Release(win.LogLine);
                win.ClearHosted(1);
            }

            // A quick-entry-like window (not resizable, not maximisable) must never be taken for a main window; a hosted one still must.
            dummies.Add(Process.Start(exe, "--dummy Bar"));
            var bar = await WaitForWindowAsync("Dummy Bar");
            if (bar != IntPtr.Zero)
            {
                GetWindowThreadProcessId(bar, out var barPid);
                GetWindowThreadProcessId(one, out var onePid);
                Add("finder: quick-entry-like window is not a main window", ClaudeFinder.MainWindows(barPid, null).Count == 0, $"candidates={ClaudeFinder.MainWindows(barPid, null).Count}");
                Add("finder: a normal window is", ClaudeFinder.MainWindows(onePid, null).Count == 1, $"candidates={ClaudeFinder.MainWindows(onePid, null).Count}");
                var hOne = win.Host(one, 0, HostMode.B_Owned)!;
                await Task.Delay(300);
                Add("finder: a hosted (frame-stripped) window still counts", ClaudeFinder.MainWindows(onePid, null).Count == 1, $"candidates={ClaudeFinder.MainWindows(onePid, null).Count}");
                hOne.Release(win.LogLine); win.ClearHosted(0);
            }

            // A large frameless window (what a crashed host leaves behind) is still a main window, and release gives it a frame back.
            dummies.Add(Process.Start(exe, "--dummy Stripped"));
            var stripped = await WaitForWindowAsync("Dummy Stripped");
            if (stripped != IntPtr.Zero)
            {
                await Task.Delay(300);
                GetWindowThreadProcessId(stripped, out var sPid);
                Add("finder: frameless main window left by a crashed host is found", (Style(stripped) & WS_THICKFRAME) == 0 && ClaudeFinder.MainWindows(sPid, null).Count == 1, $"style=0x{Style(stripped):X8} candidates={ClaudeFinder.MainWindows(sPid, null).Count}");
                var hS = win.Host(stripped, 0, HostMode.B_Owned)!;
                await Task.Delay(300);
                hS.Release(win.LogLine); win.ClearHosted(0);
                await Task.Delay(300);
                Add("release restores a standard frame on it", (Style(stripped) & (WS_CAPTION | WS_THICKFRAME)) == (WS_CAPTION | WS_THICKFRAME), $"style=0x{Style(stripped):X8}");
            }

            // Layout engine shapes, pure maths.
            var g3 = LayoutEngine.Plan(LayoutMode.Grid, 3, 0, 0);
            var g5 = LayoutEngine.Plan(LayoutMode.Grid, 5, 0, 0);
            var f4 = LayoutEngine.Plan(LayoutMode.Focus, 4, 0, 1);
            Add("layout: grid 3 = 2 + 1 full width", g3.Columns.Length == 2 && g3.Rows.Length == 2 && g3.Slots[2].ColSpan == 2, $"cols={g3.Columns.Length} rows={g3.Rows.Length} last span={g3.Slots[2].ColSpan}");
            Add("layout: grid 5 = 3 + 2 at 1.5 cells", g5.Columns.Length == 6 && g5.Slots[0].ColSpan == 2 && g5.Slots[3].ColSpan == 3, $"units={g5.Columns.Length} first span={g5.Slots[0].ColSpan} last span={g5.Slots[3].ColSpan}");
            Add("layout: focus 4 = main spanning 3 rows + strip", f4.Rows.Length == 3 && f4.Slots[1].RowSpan == 3 && f4.Slots[0].Col == 1, $"rows={f4.Rows.Length} main span={f4.Slots[1].RowSpan}");
        }
        catch (Exception ex)
        {
            Add("exception", false, ex.ToString());
        }
        finally
        {
            foreach (var d in dummies) { try { if (!d.HasExited) d.Kill(); } catch { /* spike: nothing to do */ } }
            Directory.CreateDirectory(MainWindow.LogDir);
            var path = Path.Combine(MainWindow.LogDir, $"selftest-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var sb = new StringBuilder();
            sb.AppendLine($"Hosting spike self-test - {DateTime.Now:yyyy-MM-dd HH:mm} - {Environment.OSVersion} - DPI-aware per monitor v2 - targets: two dummy windows of this spike, never Claude");
            foreach (var c in checks) sb.AppendLine($"{(c.Pass ? "PASS" : "FAIL")}  {c.Name}  - {c.Detail}");
            var failed = checks.Count(c => !c.Pass);
            sb.AppendLine($"{checks.Count - failed} passed, {failed} failed");
            File.WriteAllText(path, sb.ToString());
            win.LogLine($"self-test written to {path}");
            await Task.Delay(300);
            Application.Current.Shutdown(failed == 0 ? 0 : 1);
        }
    }

    private static async Task<IntPtr> WaitForWindowAsync(string title)
    {
        for (var i = 0; i < 100; i++)
        {
            var w = ClaudeFinder.AllTopWindows().FirstOrDefault(w => w.Title == title);
            if (w is not null) return w.Hwnd;
            await Task.Delay(100);
        }
        return IntPtr.Zero;
    }

    private static bool RectMatches(IntPtr hwnd, RECT want)
    {
        GetWindowRect(hwnd, out var r);
        return Math.Abs(r.Left - want.Left) <= 2 && Math.Abs(r.Top - want.Top) <= 2 && Math.Abs(r.Width - want.Width) <= 2 && Math.Abs(r.Height - want.Height) <= 2;
    }

    private static string Describe(IntPtr hwnd, RECT want)
    {
        GetWindowRect(hwnd, out var r);
        return $"window=({r.Left},{r.Top} {r.Width}x{r.Height}) pane=({want.Left},{want.Top} {want.Width}x{want.Height})";
    }
}
