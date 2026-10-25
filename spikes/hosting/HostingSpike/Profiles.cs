using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HostingSpike;

public sealed class ProfileInfo
{
    public required string Id { get; init; }           // "original" or the folder name, e.g. "Account 2"
    public required string Name { get; set; }          // display label
    public int Color { get; set; }                      // palette index 0..9
    public string? Path { get; init; }                  // null for the original account
    public bool IsOriginal => Path is null;
    public bool FolderMissing { get; init; }
    public string Source { get; init; } = "";
    public ClaudeInstance? Instance { get; set; }       // running instance, when found
    public IntPtr MainWindow { get; set; }
}

/// <summary>
/// Finds profiles the way docs/tech/06 describes (both switcher layouts, read-only), keeps the spike's own labels and colours in
/// %LOCALAPPDATA%\Multipane\profiles.json, and offers add, rename, delete and kill. Never writes inside a profile folder.
/// </summary>
public sealed class ProfileCatalog
{
    private static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string StoreDir = System.IO.Path.Combine(Local, "Multipane");
    private static readonly string StoreFile = System.IO.Path.Combine(StoreDir, "profiles.json");
    private readonly Dictionary<string, (string? name, int? color)> _own = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string> _log;

    public ProfileCatalog(Action<string> log)
    {
        _log = log;
        try
        {
            if (File.Exists(StoreFile))
            {
                var root = JsonNode.Parse(File.ReadAllText(StoreFile))?["profiles"]?.AsArray();
                foreach (var n in root ?? new JsonArray())
                    if (n?["id"]?.GetValue<string>() is { } id) _own[id] = (n["name"]?.GetValue<string>(), n["color"]?.GetValue<int>());
            }
        }
        catch (Exception ex) { _log("profiles.json unreadable, ignored: " + ex.Message); }
    }

    private void Save()
    {
        Directory.CreateDirectory(StoreDir);
        var arr = new JsonArray();
        foreach (var (id, v) in _own) arr.Add(new JsonObject { ["id"] = id, ["name"] = v.name, ["color"] = v.color });
        var doc = new JsonObject { ["version"] = 1, ["profiles"] = arr };
        var tmp = StoreFile + ".tmp";
        File.WriteAllText(tmp, doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, StoreFile, true);
    }

    /// <summary>All profiles, with the old switcher's labels and colours as defaults and this app's own on top. Running state attached.</summary>
    public List<ProfileInfo> Discover(ClaudeInstall? install)
    {
        var list = new List<ProfileInfo>();
        var (labels, colors) = ReadSwitcherSettings();
        list.Add(new ProfileInfo { Id = "original", Name = "Original account", Color = 0, Path = null, Source = install?.Kind ?? "" });

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddFolder(string dir, string source)
        {
            var id = System.IO.Path.GetFileName(dir.TrimEnd('\\'));
            if (id.StartsWith('.') || !seen.Add(id)) return;
            list.Add(new ProfileInfo { Id = id, Name = labels.GetValueOrDefault(id, id), Color = colors.GetValueOrDefault(id, -1), Path = dir, Source = source });
        }
        foreach (var d in SafeDirs(Local, "ClaudeProfile-*")) AddFolder(d, "Claude Profile Switcher 2.0");
        foreach (var d in SafeDirs(System.IO.Path.Combine(Local, "ClaudeProfiles"), "*"))
        {
            var attr = File.GetAttributes(d);
            if ((attr & FileAttributes.ReparsePoint) != 0) continue; // junction left by the 2.0 migration; the flat folder is listed already
            AddFolder(d, "ClaudeProfiles layout");
        }
        // Apply this app's own labels and colours, then fill colours least-used-first.
        foreach (var p in list)
            if (_own.TryGetValue(p.Id, out var own)) { if (own.name is not null) p.Name = own.name; if (own.color is not null) p.Color = own.color.Value; }
        var used = new int[10];
        foreach (var p in list) if (p.Color is >= 0 and < 10) used[p.Color]++;
        foreach (var p in list) if (p.Color < 0 || p.Color > 9) { p.Color = Array.IndexOf(used, used.Min()); used[p.Color]++; }

        // Running instances by normalised folder.
        if (install is not null)
        {
            foreach (var inst in ClaudeFinder.FindInstances(install))
            {
                var p = inst.ProfileDir is null
                    ? list[0]
                    : list.FirstOrDefault(x => x.Path is not null && Same(x.Path, inst.ProfileDir));
                if (p is null)
                {
                    var dir = inst.ProfileDir!;
                    p = new ProfileInfo { Id = System.IO.Path.GetFileName(dir.TrimEnd('\\')), Name = System.IO.Path.GetFileName(dir.TrimEnd('\\')), Color = -1, Path = dir, Source = "found running" };
                    p.Color = Array.IndexOf(used, used.Min()); used[p.Color]++;
                    list.Add(p);
                }
                p.Instance = inst;
                p.MainWindow = ClaudeFinder.MainWindows(inst.Pid, "Chrome_WidgetWin_1").FirstOrDefault()?.Hwnd ?? IntPtr.Zero;
            }
        }
        return list;
    }

    private static IEnumerable<string> SafeDirs(string root, string pattern)
    {
        try { return Directory.Exists(root) ? Directory.EnumerateDirectories(root, pattern) : Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    private static bool Same(string a, string b) => string.Equals(System.IO.Path.GetFullPath(a).TrimEnd('\\'), System.IO.Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    private (Dictionary<string, string> labels, Dictionary<string, int> colors) ReadSwitcherSettings()
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var colors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var file = System.IO.Path.Combine(Local, "ClaudeProfiles", "settings.json");
        try
        {
            if (!File.Exists(file)) return (labels, colors);
            using var doc = JsonDocument.Parse(File.ReadAllBytes(file)); // read-only; the BOM the switchers write is skipped by the parser
            if (doc.RootElement.TryGetProperty("Labels", out var l)) foreach (var e in l.EnumerateObject()) labels[e.Name] = e.Value.GetString() ?? e.Name;
            if (doc.RootElement.TryGetProperty("Colors", out var c)) foreach (var e in c.EnumerateObject()) if (e.Value.TryGetInt32(out var i)) colors[e.Name] = i;
        }
        catch (Exception ex) { _log("old switcher settings unreadable, ignored: " + ex.Message); }
        return (labels, colors);
    }

    // ----- changes -----

    public void Rename(ProfileInfo p, string name) { _own[p.Id] = (name, _own.GetValueOrDefault(p.Id).color ?? p.Color); p.Name = name; Save(); }
    public void SetColor(ProfileInfo p, int color) { _own[p.Id] = (_own.GetValueOrDefault(p.Id).name, color); p.Color = color; Save(); }

    public static string? ValidateName(string name, IEnumerable<ProfileInfo> existing)
    {
        name = name.Trim();
        if (name.Length == 0) return "Type a name.";
        if (name.Length > 40) return "Keep it under 40 characters.";
        if (name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.') || name.StartsWith('.')) return "No \\ / : * ? \" < > | and no leading or trailing dot.";
        if (string.Equals(name, "Add account", StringComparison.OrdinalIgnoreCase)) return "That name is reserved.";
        if (existing.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(p.Id, name, StringComparison.OrdinalIgnoreCase))) return "A profile with that name exists.";
        return null;
    }

    /// <summary>Creates an empty folder in the 2.0 flat layout with the lowest free "Account N" id across both layouts.</summary>
    public ProfileInfo Add(string name, IEnumerable<ProfileInfo> existing)
    {
        var ids = existing.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var n = 2;
        while (ids.Contains($"Account {n}") || Directory.Exists(System.IO.Path.Combine(Local, $"ClaudeProfile-Account {n}")) || Directory.Exists(System.IO.Path.Combine(Local, "ClaudeProfiles", $"Account {n}"))) n++;
        var id = $"Account {n}";
        var dir = System.IO.Path.Combine(Local, "ClaudeProfile-" + id);
        Directory.CreateDirectory(dir);
        _log($"created profile folder {dir}");
        var p = new ProfileInfo { Id = id, Name = name, Color = -1, Path = dir, Source = "created here" };
        _own[id] = (name, null); Save();
        return p;
    }

    /// <summary>Refuses the original and running profiles and anything outside the two layouts; sends the folder to the Recycle Bin.</summary>
    public string? Delete(ProfileInfo p)
    {
        if (p.IsOriginal) return "The original account can't be deleted.";
        if (p.Instance is not null) return $"Kill {p.Name} first.";
        var dir = p.Path!;
        var full = System.IO.Path.GetFullPath(dir).TrimEnd('\\');
        var okFlat = full.StartsWith(System.IO.Path.Combine(Local, "ClaudeProfile-"), StringComparison.OrdinalIgnoreCase);
        var okNested = full.StartsWith(System.IO.Path.Combine(Local, "ClaudeProfiles") + "\\", StringComparison.OrdinalIgnoreCase);
        if (!(okFlat || okNested) || p.Id.Contains('\\') || p.Id.Contains('/') || p.Id.StartsWith('.')) return $"Refusing to delete {full}.";
        if (Directory.Exists(full))
        {
            var op = new NativeMethods.SHFILEOPSTRUCT { wFunc = NativeMethods.FO_DELETE, pFrom = full + "\0\0", fFlags = (ushort)(NativeMethods.FOF_ALLOWUNDO | NativeMethods.FOF_NOCONFIRMATION | NativeMethods.FOF_SILENT | NativeMethods.FOF_NOERRORUI) };
            var r = NativeMethods.SHFileOperation(ref op);
            if (r != 0 || op.fAnyOperationsAborted) return $"Windows refused to delete the folder (code {r}).";
        }
        _own.Remove(p.Id); Save();
        _log($"deleted profile {p.Id} (folder sent to the Recycle Bin)");
        return null;
    }

    /// <summary>Close gracefully, then end the whole tree: Claude's own Quit only hides to the tray, so the second step is the usual one.</summary>
    public static async Task KillAsync(ProfileInfo p, Action<string> log)
    {
        if (p.Instance is null) return;
        var pid = (int)p.Instance.Pid;
        if (p.MainWindow != IntPtr.Zero) NativeMethods.PostMessage(p.MainWindow, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(100);
            try { Process.GetProcessById(pid); } catch (ArgumentException) { log($"kill {p.Id}: exited after WM_CLOSE"); return; }
        }
        try { using var proc = Process.GetProcessById(pid); proc.Kill(entireProcessTree: true); log($"kill {p.Id}: process tree terminated"); }
        catch (Exception ex) { log($"kill {p.Id}: {ex.Message}"); }
    }
}
