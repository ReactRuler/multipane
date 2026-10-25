using System.Diagnostics;
using System.IO;

namespace HostingSpike;

/// <summary>--demo: three dummy windows hosted under made-up profile names, so the shell can be looked at (and screenshotted) without Claude.</summary>
public static class Demo
{
    public static async Task RunAsync(MainWindow win, string? mode)
    {
        if (mode is not null && mode != "home" && Enum.TryParse<LayoutMode>(mode, true, out var m)) win.ApplyMode(m);
        if (mode == "home") win.ApplyMode(LayoutMode.Grid);
        var exe = Environment.ProcessPath!;
        var names = new[] { "Personal", "Business", "Studio" };
        var procs = names.Select(n => Process.Start(exe, $"--dummy {n}")).ToList();
        win.Closed += (_, _) => { foreach (var p in procs) { try { if (!p.HasExited) p.Kill(); } catch { /* demo */ } } };
        await Task.Delay(600);
        for (var i = 0; i < names.Length; i++)
        {
            IntPtr hwnd = IntPtr.Zero;
            for (var t = 0; t < 50 && hwnd == IntPtr.Zero; t++)
            {
                hwnd = ClaudeFinder.AllTopWindows().FirstOrDefault(w => w.Title == "Dummy " + names[i])?.Hwnd ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero) await Task.Delay(100);
            }
            if (hwnd == IntPtr.Zero) continue;
            var pane = win.AddPane(names[i]);
            pane.Attach(hwnd, win.Mode, true);
        }
        win.AddPane("Lab"); // an empty pane shows the placeholder
        // Made-up profiles for the home page, so no real account appears in a screenshot.
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        ProfileInfo Fake(string id, string name, int color, string? path, uint pid, IntPtr hwnd) =>
            new() { Id = id, Name = name, Color = color, Path = path, Instance = pid == 0 ? null : new ClaudeInstance(pid, path, ""), MainWindow = hwnd };
        var hosted = win.PanesList.Where(p => p.TargetHwnd != IntPtr.Zero).Select(p => p.TargetHwnd).ToList();
        win.DemoProfiles = new List<ProfileInfo>
        {
            Fake("original", "Personal", 0, null, 4120, hosted.ElementAtOrDefault(0)),
            Fake("Account 2", "Business", 1, Path.Combine(local, "ClaudeProfile-Account 2"), 5331, hosted.ElementAtOrDefault(1)),
            Fake("Account 3", "Studio", 2, Path.Combine(local, "ClaudeProfile-Account 3"), 6874, hosted.ElementAtOrDefault(2)),
            Fake("Account 4", "Lab", 3, Path.Combine(local, "ClaudeProfile-Account 4"), 0, IntPtr.Zero),
            Fake("Account 5", "Client A", 4, Path.Combine(local, "ClaudeProfile-Account 5"), 0, IntPtr.Zero),
        };
        win.Relayout();
        if (mode == "home") win.ShowHome();
        win.Activate();
    }
}
