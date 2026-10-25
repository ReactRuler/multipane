using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace HostingSpike;

public sealed record ClaudeInstall(string Kind, string Exe, string Version, string? Aumid);

public sealed record ClaudeInstance(uint Pid, string? ProfileDir, string ImagePath)
{
    public string Label => ProfileDir is null ? "original" : Path.GetFileName(ProfileDir.TrimEnd('\\'));
}

public sealed record TopWindow(IntPtr Hwnd, uint Pid, string Class, string Title);

/// <summary>Finds Claude.exe and running Claude Desktop instances the way docs/tech/03 describes.</summary>
public static class ClaudeFinder
{
    private static readonly Regex UserDataDir = new("--user-data-dir=(?:\"([^\"]+)\"|(\\S+))", RegexOptions.Compiled);

    public static ClaudeInstall? FindInstall()
    {
        // Store build: registry package repository, highest version wins.
        const string pkgKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
        using (var key = Registry.CurrentUser.OpenSubKey(pkgKey))
        {
            if (key is not null)
            {
                ClaudeInstall? best = null; Version? bestVer = null;
                foreach (var name in key.GetSubKeyNames().Where(n => n.StartsWith("Claude_", StringComparison.OrdinalIgnoreCase)))
                {
                    using var sub = key.OpenSubKey(name);
                    var root = sub?.GetValue("PackageRootFolder") as string;
                    if (root is null) continue;
                    var exe = Path.Combine(root, "app", "Claude.exe");
                    if (!File.Exists(exe)) continue;
                    var parts = name.Split('_');
                    if (parts.Length < 2 || !Version.TryParse(parts[1], out var ver)) continue;
                    if (bestVer is null || ver > bestVer)
                    {
                        bestVer = ver;
                        var family = parts[0] + "_" + parts[^1];
                        best = new ClaudeInstall("Store", exe, ver.ToString(), family + "!Claude");
                    }
                }
                if (best is not null) return best;
            }
        }
        // Installer build: known roots, newest Claude.exe within depth 2.
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var rootDir in new[] { Path.Combine(local, "AnthropicClaude"), Path.Combine(local, "Programs", "Claude"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Claude") })
        {
            if (!Directory.Exists(rootDir)) continue;
            var exe = Directory.EnumerateFiles(rootDir, "Claude.exe", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 2 })
                .Select(p => new FileInfo(p)).OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
            if (exe is not null) return new ClaudeInstall("Installer", exe.FullName, FileVersionInfo.GetVersionInfo(exe.FullName).FileVersion ?? "?", null);
        }
        return null;
    }

    /// <summary>Root Claude Desktop processes: image under the install root, not a --type= helper, not the Claude Code CLI.</summary>
    public static List<ClaudeInstance> FindInstances(ClaudeInstall install)
    {
        var installRoot = Path.GetDirectoryName(Path.GetDirectoryName(install.Exe))!; // package root or app-<ver> parent
        var family = install.Kind == "Store" ? Path.GetFileName(installRoot).Split('_')[0] : Path.GetFileName(installRoot);
        var result = new List<ClaudeInstance>();
        foreach (var p in Process.GetProcessesByName("claude"))
        {
            using (p)
            {
                var image = NativeMethods.ImagePath((uint)p.Id);
                if (image is null) continue;
                // Same install family, any version: an instance still on the previous build after an update counts.
                var parentOfApp = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(image)) ?? "");
                var sameFamily = install.Kind == "Store"
                    ? parentOfApp.StartsWith(family + "_", StringComparison.OrdinalIgnoreCase)
                    : image.StartsWith(installRoot, StringComparison.OrdinalIgnoreCase);
                if (!sameFamily) continue;
                var cmd = NativeMethods.CommandLine((uint)p.Id);
                if (cmd is null || cmd.Contains("--type=", StringComparison.Ordinal)) continue;
                var m = UserDataDir.Match(cmd);
                var dir = m.Success ? (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value) : null;
                result.Add(new ClaudeInstance((uint)p.Id, dir, image));
            }
        }
        return result;
    }

    /// <summary>Windows the host has stripped the frame from; they still count as main windows.</summary>
    public static readonly HashSet<IntPtr> KnownHosted = new();

    /// <summary>
    /// A Claude instance has more than one Chrome_WidgetWin_1 window: the main window and the hidden quick-entry bar.
    /// Only a resizable, maximisable top-level window (or one we already host) is a main window; the largest wins.
    /// </summary>
    private static bool IsMainCandidate(IntPtr h, uint pid, string? requiredClass, bool wantVisible, out TopWindow? w)
    {
        w = null;
        NativeMethods.GetWindowThreadProcessId(h, out var wp);
        if (wp != pid) return false;
        var visible = NativeMethods.IsWindowVisible(h) || NativeMethods.IsCloaked(h);
        if (visible != wantVisible) return false;
        var hosted = KnownHosted.Contains(h); // owned by us and frame-stripped, yet still the main window
        if (!hosted && NativeMethods.GetWindow(h, NativeMethods.GW_OWNER) != IntPtr.Zero) return false;
        var cls = NativeMethods.ClassName(h);
        if (requiredClass is not null && cls != requiredClass) return false;
        var title = NativeMethods.Title(h);
        if (title.Length == 0) return false;
        var style = NativeMethods.Style(h);
        const uint mainStyle = NativeMethods.WS_THICKFRAME | NativeMethods.WS_MAXIMIZEBOX;
        if ((style & mainStyle) != mainStyle && !hosted) return false;
        w = new TopWindow(h, pid, cls, title);
        return true;
    }

    private static List<TopWindow> Candidates(uint pid, string? requiredClass, bool visible)
    {
        var list = new List<(TopWindow w, long area)>();
        NativeMethods.EnumWindows((h, _) =>
        {
            if (IsMainCandidate(h, pid, requiredClass, visible, out var w) && w is not null)
            {
                NativeMethods.GetWindowRect(h, out var r);
                list.Add((w, (long)Math.Max(r.Width, 1) * Math.Max(r.Height, 1)));
            }
            return true;
        }, IntPtr.Zero);
        return list.OrderByDescending(x => x.area).Select(x => x.w).ToList();
    }

    /// <summary>Visible main windows of a process, largest first. For Claude the class is Chrome_WidgetWin_1.</summary>
    public static List<TopWindow> MainWindows(uint pid, string? requiredClass) => Candidates(pid, requiredClass, visible: true);

    /// <summary>Hidden main windows of a process (an instance that quit to the tray keeps its main window, hidden). The quick-entry bar is excluded.</summary>
    public static List<TopWindow> HiddenMainWindows(uint pid, string? requiredClass) => Candidates(pid, requiredClass, visible: false);

    public static List<TopWindow> AllTopWindows()
    {
        var list = new List<TopWindow>();
        NativeMethods.EnumWindows((h, _) =>
        {
            if (!NativeMethods.IsWindowVisible(h)) return true;
            if (NativeMethods.GetWindow(h, NativeMethods.GW_OWNER) != IntPtr.Zero) return true;
            var title = NativeMethods.Title(h);
            if (title.Length == 0) return true;
            NativeMethods.GetWindowThreadProcessId(h, out var pid);
            list.Add(new TopWindow(h, pid, NativeMethods.ClassName(h), title));
            return true;
        }, IntPtr.Zero);
        return list;
    }
}
