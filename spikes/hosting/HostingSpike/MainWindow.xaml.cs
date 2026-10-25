using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace HostingSpike;

public sealed record PaneTab(PaneView Pane);

/// <summary>One row of the home page's profile list.</summary>
public sealed class ProfileRow
{
    public required ProfileInfo P { get; init; }
    public string Name => P.Name;
    public string Initials => PaneView.MakeInitials(P.Name);
    public required Brush Brush { get; init; }
    public required string Status { get; init; }
    public required string Detail { get; init; }
    public required Brush StatusBrush { get; init; }
    public Geometry? GlyphFill { get; init; }
    public Geometry? GlyphStroke { get; init; }
    public Visibility OpenVis { get; init; }
    public Visibility ShowVis { get; init; }
    public Visibility StartVis { get; init; }
    public Visibility KillVis { get; init; }
    public Visibility OriginalVis => P.IsOriginal ? Visibility.Visible : Visibility.Collapsed;
}

public sealed class ListEntry
{
    public required string Text { get; init; }
    public IntPtr Hwnd { get; init; }
    public uint Pid { get; init; }
    public string Name { get; init; } = "";
    public string Initials { get; init; } = "";
    public Brush? Brush { get; init; }
}

/// <summary>The shell from the design canvas: 40 px title bar with the mode switch (tabs in Tabs mode), panes in the five arrangements, Mica on Windows 11.</summary>
public partial class MainWindow : Window
{
    private ClaudeInstall? _install;
    private ProfileCatalog? _catalog;
    private List<ProfileInfo> _profiles = new();
    /// <summary>Demo mode: made-up profiles instead of the machine's, so screenshots never show real accounts.</summary>
    public List<ProfileInfo>? DemoProfiles { get; set; }
    private LayoutMode _mode = LayoutMode.Columns;
    private int _active;
    private int _focus;
    private PaneView? _maximized;
    private PaneView? _pickFor;
    private bool _homeOpen;
    private bool _relayouting;
    private bool _restoreSession = true;
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _ticks;
    private readonly string _logPath = Path.Combine(AppContext.BaseDirectory, $"spike-{DateTime.Now:yyyyMMdd-HHmmss}.log");

    public ObservableCollection<PaneView> PanesList { get; } = new();
    /// <summary>Tab items wrap the pane: an ItemsControl given UIElements would host them directly and parent them twice.</summary>
    public ObservableCollection<PaneTab> TabsList { get; } = new();
    public IntPtr Hwnd => new WindowInteropHelper(this).Handle;

    // ----- API used by SelfTest -----
    public PaneHost[] Panes => PanesList.Select(p => p.Host).ToArray();
    public HostedWindow? HostedAt(int i) => i < PanesList.Count ? PanesList[i].Hosted : null;
    public void ClearHosted(int i) { if (i < PanesList.Count) PanesList[i].ClearHosted(); }
    public HostMode Mode => MenuModeA.IsChecked ? HostMode.A_Reparent : HostMode.B_Owned;

    public MainWindow()
    {
        InitializeComponent();
        Tabs.ItemsSource = TabsList;
        _tick.Tick += OnTick;
    }

    public void LogLine(string s)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {s}";
        Log.AppendText(line + Environment.NewLine);
        Log.ScrollToEnd();
        File.AppendAllText(_logPath, line + Environment.NewLine);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Theme.ApplyBackdrop(Hwnd, this);
    }

    /// <summary>Windows maximises a window with its frame pushed off-screen. With a custom title bar that frame is our content,
    /// so the content is padded by exactly the off-screen amount while maximised; otherwise the bottom row sits under the taskbar.</summary>
    private void FitMaximisedContent()
    {
        if (WindowState != WindowState.Maximized || !NativeMethods.WorkAreaOf(Hwnd, out var work, out _)) { Root.Margin = new Thickness(0); return; }
        NativeMethods.GetWindowRect(Hwnd, out var r);
        var dpi = VisualTreeHelper.GetDpi(this);
        Root.Margin = new Thickness(
            Math.Max(0, work.Left - r.Left) / dpi.DpiScaleX, Math.Max(0, work.Top - r.Top) / dpi.DpiScaleY,
            Math.Max(0, r.Right - work.Right) / dpi.DpiScaleX, Math.Max(0, r.Bottom - work.Bottom) / dpi.DpiScaleY);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _install = ClaudeFinder.FindInstall();
        _catalog = new ProfileCatalog(LogLine);
        LogLine(_install is null ? "Claude not found" : $"install: {_install.Kind} {_install.Version} {_install.Exe} aumid={_install.Aumid}");
        LogLine($"log file: {_logPath}");
        Relayout();
        _tick.Start();
    }

    // ----- panes and layout -----

    public PaneView AddPane(string? name = null, string? profileId = null, int? color = null)
    {
        var pane = new PaneView(Hwnd, LogLine) { ColorIndex = color ?? PanesList.Count % 10, ProfileId = profileId };
        pane.ColorBrush = (Brush)FindResource("P" + (pane.ColorIndex + 1));
        pane.ProfileName = name ?? $"Pane {PanesList.Count + 1}";
        pane.Activated += p =>
        {
            var i = PanesList.IndexOf(p); if (i < 0) return;
            _active = i;
            if (_relayouting) return; // activation raised by Relayout itself must not start another layout
            // In Focus mode, clicking a strip pane's header makes it the large one.
            if (_mode == LayoutMode.Focus && _focus != i) { _focus = i; Relayout(); }
            else if (_mode == LayoutMode.Tabs) Relayout();
        };
        pane.RemoveRequested += p => { p.Release(); PanesList.Remove(p); TabsList.Remove(TabsList.First(t => t.Pane == p)); if (_maximized == p) _maximized = null; Relayout(); SaveLayout(); };
        pane.StartRequested += p => _ = StartIntoPaneAsync(p);
        pane.MoveRequested += MovePane;
        pane.KillRequested += async p =>
        {
            NativeMethods.GetWindowThreadProcessId(p.TargetHwnd, out var pid);
            var prof = _profiles.FirstOrDefault(x => x.Instance?.Pid == pid);
            if (prof is null) { _profiles = _catalog!.Discover(_install); prof = _profiles.FirstOrDefault(x => x.Instance?.Pid == pid); }
            if (prof is not null) await KillWithConfirmAsync(prof);
        };
        pane.HideRequested += p => { p.IsHidden = true; p.SuspendWindow(true); Relayout(); };
        pane.ChooseWindowRequested += p => { _pickFor = p; OpenPicker(); };
        pane.Changed += _ => UpdateSummary();
        pane.IdentityChanged += p =>
        {
            // Keep a pane's rename or colour change: find the profile behind its window and save it where the home page reads it.
            if (p.TargetHwnd == IntPtr.Zero || _catalog is null || DemoProfiles is not null) return;
            NativeMethods.GetWindowThreadProcessId(p.TargetHwnd, out var pid);
            var prof = _profiles.FirstOrDefault(x => x.Instance?.Pid == pid);
            if (prof is null) { _profiles = _catalog.Discover(_install); prof = _profiles.FirstOrDefault(x => x.Instance?.Pid == pid); }
            if (prof is null) return;
            if (prof.Name != p.ProfileName) _catalog.Rename(prof, p.ProfileName);
            if (prof.Color != p.ColorIndex) _catalog.SetColor(prof, p.ColorIndex);
            LogLine($"saved name and colour for {prof.Id}");
            if (EmptyHint.Visibility == Visibility.Visible) RefreshHome();
        };
        PanesList.Add(pane);
        TabsList.Add(new PaneTab(pane));
        Relayout();
        SaveLayout();
        return pane;
    }

    /// <summary>Swap the pane with its visible neighbour in the given direction; the order is what the arrangement follows.</summary>
    public void MovePane(PaneView pane, int delta)
    {
        var visible = PanesList.Where(p => !p.IsHidden).ToList();
        var vi = visible.IndexOf(pane);
        if (vi < 0 || vi + delta < 0 || vi + delta >= visible.Count) return;
        var other = visible[vi + delta];
        var a = PanesList.IndexOf(pane); var b = PanesList.IndexOf(other);
        (PanesList[a], PanesList[b]) = (PanesList[b], PanesList[a]);
        var ta = TabsList.First(t => t.Pane == pane); var tb = TabsList.First(t => t.Pane == other);
        var ia = TabsList.IndexOf(ta); var ib = TabsList.IndexOf(tb);
        (TabsList[ia], TabsList[ib]) = (TabsList[ib], TabsList[ia]);
        if (_active == a) _active = b; else if (_active == b) _active = a;
        if (_focus == a) _focus = b; else if (_focus == b) _focus = a;
        Relayout();
        SaveLayout();
    }

    // ----- session: remember which profiles were open, dock them again at the next start -----

    private void SaveLayout()
    {
        if (DemoProfiles is not null || !IsLoaded) return;
        var panes = PanesList.Where(p => p.ProfileId is not null).Select(p => new SavedPane(p.ProfileId!, p.ProfileName, p.ColorIndex)).ToList();
        LayoutStore.Save(new SavedLayout(_mode.ToString().ToLowerInvariant(), panes, _restoreSession));
    }

    /// <summary>At start: recreate the saved panes, dock the profiles that are running (revealing hidden windows), leave the rest as Start.</summary>
    public async Task RestoreSessionAsync()
    {
        await Task.Delay(300);
        if (_catalog is null) return;
        var saved = LayoutStore.Load();
        _restoreSession = saved.Restore;
        MenuRestore.IsChecked = _restoreSession;
        if (!_restoreSession || saved.Panes.Count == 0) return;
        if (Enum.TryParse<LayoutMode>(saved.Mode, true, out var m)) ApplyMode(m);
        _profiles = _catalog.Discover(_install);
        foreach (var sp in saved.Panes)
        {
            var p = _profiles.FirstOrDefault(x => string.Equals(x.Id, sp.Id, StringComparison.OrdinalIgnoreCase));
            var pane = AddPane(p?.Name ?? sp.Name, sp.Id, p?.Color ?? sp.Color);
            if (p?.Instance is null) continue;
            var hwnd = RevealedWindow(p);
            if (hwnd != IntPtr.Zero) pane.Attach(hwnd, Mode, MenuToolWindow.IsChecked);
        }
        Relayout();
        LogLine($"session restored: {saved.Panes.Count} panes, {PanesList.Count(x => x.Hosted is not null)} docked");
    }

    /// <summary>The profile's main window, showing it first if it was hidden to the tray.</summary>
    private static IntPtr RevealedWindow(ProfileInfo p)
    {
        if (p.MainWindow != IntPtr.Zero) return p.MainWindow;
        if (p.Instance is null) return IntPtr.Zero;
        var hidden = ClaudeFinder.HiddenMainWindows(p.Instance.Pid, "Chrome_WidgetWin_1").FirstOrDefault();
        if (hidden is null) return IntPtr.Zero;
        NativeMethods.ShowWindow(hidden.Hwnd, 5 /* SW_SHOW */);
        p.MainWindow = hidden.Hwnd;
        return hidden.Hwnd;
    }

    /// <summary>Launch the pane's profile and dock its window when it appears (up to 40 s).</summary>
    private async Task StartIntoPaneAsync(PaneView pane)
    {
        if (pane.ProfileId is null || _catalog is null || DemoProfiles is not null) return;
        _profiles = _catalog.Discover(_install);
        var p = _profiles.FirstOrDefault(x => string.Equals(x.Id, pane.ProfileId, StringComparison.OrdinalIgnoreCase));
        if (p is null) { Info("Profile not found", $"{pane.ProfileName} is no longer in the profile list."); return; }
        pane.MarkStarting();
        if (p.Instance is null) Launch(p);
        for (var i = 0; i < 40 && pane.State == PaneState.Starting; i++)
        {
            await Task.Delay(1000);
            _profiles = _catalog.Discover(_install);
            p = _profiles.FirstOrDefault(x => string.Equals(x.Id, pane.ProfileId, StringComparison.OrdinalIgnoreCase));
            if (p?.Instance is null) continue;
            if (i < 2 && p.MainWindow == IntPtr.Zero) continue; // give a fresh process a moment before revealing anything
            var hwnd = RevealedWindow(p);
            if (hwnd == IntPtr.Zero) continue;
            pane.Attach(hwnd, Mode, MenuToolWindow.IsChecked);
            Relayout();
            return;
        }
        if (pane.State == PaneState.Starting) { pane.MarkNotRunning(); LogLine($"start {pane.ProfileId}: no window after 40 s"); }
    }

    private void OnRestoreToggle(object sender, RoutedEventArgs e) { _restoreSession = MenuRestore.IsChecked; SaveLayout(); }

    /// <summary>Host a window in pane <paramref name="index"/>, creating panes up to it. Used by the picker and the self-test.</summary>
    public HostedWindow? Host(IntPtr target, int index, HostMode mode)
    {
        if (target == IntPtr.Zero || !NativeMethods.IsWindow(target)) { LogLine("nothing selected or window gone"); return null; }
        while (PanesList.Count <= index) AddPane();
        var h = PanesList[index].Attach(target, mode, MenuToolWindow.IsChecked);
        Relayout();
        return h;
    }

    public void Relayout()
    {
        if (_relayouting) return;
        _relayouting = true;
        try { RelayoutCore(); } finally { _relayouting = false; }
    }

    private void RelayoutCore()
    {
        var visible = PanesList.Where(p => !p.IsHidden).ToList();
        Area.Children.Clear();
        Area.ColumnDefinitions.Clear();
        Area.RowDefinitions.Clear();
        // The home page shows when there is nothing to show, or when the user opened it over the panes.
        var showHome = visible.Count == 0 || _homeOpen;
        EmptyHint.Visibility = showHome ? Visibility.Visible : Visibility.Collapsed;
        HomeBack.Visibility = _homeOpen && visible.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Tabs.Visibility = _mode == LayoutMode.Tabs && !showHome ? Visibility.Visible : Visibility.Collapsed;
        Summary.Visibility = _mode == LayoutMode.Tabs && !showHome ? Visibility.Collapsed : Visibility.Visible;
        if (showHome)
        {
            foreach (var p in PanesList) { p.Visibility = Visibility.Collapsed; p.SuspendWindow(true); }
            RefreshHome();
            UpdateSummary();
            return;
        }

        if (_maximized is not null && visible.Contains(_maximized))
        {
            Area.ColumnDefinitions.Add(new ColumnDefinition()); Area.RowDefinitions.Add(new RowDefinition());
            foreach (var p in visible) { p.Visibility = p == _maximized ? Visibility.Visible : Visibility.Collapsed; p.SuspendWindow(p != _maximized); }
            _maximized.Margin = new Thickness(3); Area.Children.Add(_maximized);
            UpdateSummary();
            return;
        }
        _maximized = null;

        var activeIdx = Math.Clamp(visible.IndexOf(PanesList.ElementAtOrDefault(_active) ?? visible.FirstOrDefault()!), 0, Math.Max(visible.Count - 1, 0));
        var focusIdx = Math.Clamp(visible.IndexOf(PanesList.ElementAtOrDefault(_focus) ?? visible.FirstOrDefault()!), 0, Math.Max(visible.Count - 1, 0));
        var plan = LayoutEngine.Plan(_mode, visible.Count, activeIdx, focusIdx);
        foreach (var c in plan.Columns) Area.ColumnDefinitions.Add(new ColumnDefinition { Width = c });
        foreach (var r in plan.Rows) Area.RowDefinitions.Add(new RowDefinition { Height = r });
        for (var i = 0; i < visible.Count; i++)
        {
            var p = visible[i]; var s = plan.Slots[i];
            p.Visibility = s.Visible ? Visibility.Visible : Visibility.Collapsed;
            p.SuspendWindow(!s.Visible);
            Grid.SetRow(p, s.Row); Grid.SetColumn(p, s.Col); Grid.SetRowSpan(p, s.RowSpan); Grid.SetColumnSpan(p, s.ColSpan);
            p.Margin = new Thickness(3);
            Area.Children.Add(p);
            if (_mode == LayoutMode.Tabs && s.Visible) p.IsActive = true;
        }
        foreach (var p in PanesList.Where(p => p.IsHidden)) p.Visibility = Visibility.Collapsed;
        UpdateSummary();
        Dispatcher.BeginInvoke(PlaceAll, DispatcherPriority.Loaded);
    }

    public void PlaceAll()
    {
        if (WindowState == WindowState.Minimized) return;
        foreach (var p in PanesList) if (p.Visibility == Visibility.Visible) p.Place();
    }

    private void UpdateSummary()
    {
        var running = PanesList.Count(p => p.State is PaneState.Running or PaneState.NotResponding or PaneState.PoppedOut);
        var hidden = PanesList.Count(p => p.IsHidden);
        var parts = new List<string> { $"{running} running" };
        if (hidden > 0) parts.Add($"{hidden} hidden");
        var nr = PanesList.Count(p => p.State == PaneState.NotResponding);
        if (nr > 0) parts.Add($"{nr} not responding");
        Summary.Text = string.Join(" · ", parts);
        ShownText.Text = $"Shown {PanesList.Count - hidden} of {PanesList.Count}";
    }

    public void ApplyMode(LayoutMode mode) => SetMode(mode);

    private void SetMode(LayoutMode mode)
    {
        if (_mode == mode) return;
        _mode = mode;
        if (mode == LayoutMode.Focus) _focus = _active;
        (mode switch { LayoutMode.Tabs => ModeTabs, LayoutMode.Columns => ModeColumns, LayoutMode.Rows => ModeRows, LayoutMode.Grid => ModeGrid, _ => ModeFocus }).IsChecked = true;
        Relayout();
        SaveLayout();
    }

    private void OnModeChecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        SetMode(sender == ModeTabs ? LayoutMode.Tabs : sender == ModeColumns ? LayoutMode.Columns : sender == ModeRows ? LayoutMode.Rows : sender == ModeGrid ? LayoutMode.Grid : LayoutMode.Focus);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (ctrl && !shift && e.Key is >= Key.D1 and <= Key.D5) { SetMode((LayoutMode)(e.Key - Key.D1)); e.Handled = true; }
        else if (ctrl && e.Key == Key.Tab && PanesList.Count > 0) { _active = (_active + (shift ? PanesList.Count - 1 : 1)) % PanesList.Count; PanesList[_active].IsActive = true; if (_mode != LayoutMode.Tabs) PanesList[_active].Focus(); Relayout(); e.Handled = true; }
        else if (ctrl && e.Key == Key.K) { OpenPicker(); e.Handled = true; }
        else if (ctrl && !shift && e.Key == Key.H) { ToggleHome(); e.Handled = true; }
        else if (e.Key == Key.Escape && _homeOpen && PanesList.Any(p => !p.IsHidden)) { ToggleHome(); e.Handled = true; }
        else if (ctrl && shift && e.Key == Key.M && PanesList.Count > 0) { _maximized = _maximized is null ? PanesList[_active] : null; Relayout(); e.Handled = true; }
        else if (ctrl && shift && (e.Key is Key.Left or Key.Up) && PanesList.Count > 0) { MovePane(PanesList[Math.Clamp(_active, 0, PanesList.Count - 1)], -1); e.Handled = true; }
        else if (ctrl && shift && (e.Key is Key.Right or Key.Down) && PanesList.Count > 0) { MovePane(PanesList[Math.Clamp(_active, 0, PanesList.Count - 1)], +1); e.Handled = true; }
        else if (ctrl && shift && e.Key == Key.H && PanesList.Count > 0) { var p = PanesList[_active]; p.IsHidden = true; p.SuspendWindow(true); Relayout(); e.Handled = true; }
        else if (ctrl && shift && e.Key == Key.A) { foreach (var p in PanesList) { p.IsHidden = false; p.SuspendWindow(false); } Relayout(); e.Handled = true; }
        else if (e.Key == Key.F11) { ToggleFullscreen(); e.Handled = true; }
        else if (e.Key == Key.Escape && _fullscreen) { ToggleFullscreen(); e.Handled = true; }
        else if (e.Key == Key.Escape && _maximized is not null) { _maximized = null; Relayout(); e.Handled = true; }
    }

    // ----- timers -----

    private void OnTick(object? sender, EventArgs e)
    {
        _ticks++;
        var withMem = _ticks % 3 == 0;
        long total = 0;
        foreach (var p in PanesList)
        {
            p.Sample(withMem);
            if (p.TargetHwnd != IntPtr.Zero && withMem)
            {
                NativeMethods.GetWindowThreadProcessId(p.TargetHwnd, out var pid);
                try { using var pr = Process.GetProcessById((int)pid); total += pr.PrivateMemorySize64; } catch { /* process gone */ }
            }
        }
        if (withMem)
        {
            var gb = total / 1073741824.0;
            MemText.Text = total == 0 ? "" : $"{gb:0.0} GB";
            var installed = Theme.TotalMemoryBytes();
            MemBar.Width = installed > 0 ? Math.Min(64, 64.0 * total / installed) : 0;
        }
        UpdateSummary();
        if (EmptyHint.Visibility == Visibility.Visible && _ticks % 3 == 0) RefreshHome();
    }

    // ----- picker -----

    private void OpenPicker()
    {
        RefreshPicker();
        Picker.IsOpen = true;
        Filter.Focus();
    }

    private void OnShown(object sender, RoutedEventArgs e) { _pickFor = null; OpenPicker(); }

    private void OnHome(object sender, RoutedEventArgs e) => ToggleHome();
    public void ShowHome() { if (!_homeOpen) ToggleHome(); }

    private void ToggleHome()
    {
        _homeOpen = !_homeOpen;
        if (!_homeOpen) foreach (var p in PanesList) if (!p.IsHidden) p.SuspendWindow(false);
        Relayout();
    }

    private void RefreshPicker()
    {
        Instances.Items.Clear();
        foreach (var entry in InstanceEntries().Where(e => !PanesList.Any(p => p.TargetHwnd == e.Hwnd)))
            Instances.Items.Add(new ListEntry { Text = $"{entry.Name} · {entry.Text}", Hwnd = entry.Hwnd, Pid = entry.Pid, Name = entry.Name });
        Windows.Items.Clear();
        var filter = Filter.Text.Trim();
        var taken = PanesList.Select(p => p.TargetHwnd).ToHashSet();
        foreach (var w in ClaudeFinder.AllTopWindows().Where(w => w.Hwnd != Hwnd && !taken.Contains(w.Hwnd) && w.Class != "Chrome_WidgetWin_1" && (filter.Length == 0 || w.Title.Contains(filter, StringComparison.OrdinalIgnoreCase))))
            Windows.Items.Add(new ListEntry { Text = $"{w.Title} · {w.Class}", Hwnd = w.Hwnd, Pid = w.Pid, Name = w.Title.Length > 24 ? w.Title[..24] : w.Title });
        HiddenPanes.ItemsSource = PanesList.Where(p => p.IsHidden).ToList();
        HiddenHeader.Visibility = HiddenPanes.Visibility = PanesList.Any(p => p.IsHidden) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Running instances with the saved profile name and colour, for the picker.</summary>
    private List<ListEntry> InstanceEntries()
    {
        var list = new List<ListEntry>();
        if (_install is null || _catalog is null) return list;
        _profiles = _catalog.Discover(_install);
        foreach (var p in _profiles.Where(p => p.Instance is not null))
        {
            list.Add(new ListEntry
            {
                Text = p.MainWindow == IntPtr.Zero ? $"pid {p.Instance!.Pid} · window hidden" : $"pid {p.Instance!.Pid} · {NativeMethods.Title(p.MainWindow)}",
                Hwnd = p.MainWindow, Pid = p.Instance.Pid, Name = p.Name,
                Initials = PaneView.MakeInitials(p.Name),
                Brush = (Brush)FindResource("P" + (p.Color + 1))
            });
        }
        return list;
    }

    private void RefreshHome()
    {
        if (_catalog is null) return;
        _profiles = DemoProfiles ?? _catalog.Discover(_install);
        var rows = new List<ProfileRow>();
        foreach (var p in _profiles)
        {
            var running = p.Instance is not null;
            var hosted = running && PanesList.Any(x => x.TargetHwnd != IntPtr.Zero && x.TargetHwnd == p.MainWindow);
            var hasWindow = p.MainWindow != IntPtr.Zero;
            string status, detail; string brushKey; Geometry? fill = null, stroke = null;
            if (p.FolderMissing) { status = "Folder not found"; detail = p.Path ?? ""; brushKey = "Warn"; fill = (Geometry)FindResource("GlyphCrashed"); }
            else if (running) { status = "Running"; detail = $"· pid {p.Instance!.Pid}" + (hosted ? " · in a pane" : hasWindow ? " · in its own window" : " · window hidden (quit to the tray)"); brushKey = "Ok"; fill = (Geometry)FindResource("GlyphRunning"); }
            else { status = "Not running"; detail = p.Path is null ? "" : "· " + p.Path.Replace(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%"); brushKey = "Text3"; stroke = (Geometry)FindResource("GlyphRing"); }
            rows.Add(new ProfileRow
            {
                P = p, Brush = (Brush)FindResource("P" + (p.Color + 1)), Status = status, Detail = detail, StatusBrush = (Brush)FindResource(brushKey),
                GlyphFill = fill, GlyphStroke = stroke,
                OpenVis = running && hasWindow && !hosted ? Visibility.Visible : Visibility.Collapsed,
                ShowVis = running && !hasWindow ? Visibility.Visible : Visibility.Collapsed,
                StartVis = !running && !p.FolderMissing ? Visibility.Visible : Visibility.Collapsed,
                KillVis = running ? Visibility.Visible : Visibility.Collapsed,
            });
        }
        HomeProfiles.ItemsSource = rows;
    }

    private static ProfileRow? RowOf(object sender) => sender switch
    {
        Button { Tag: ProfileRow r } => r,
        MenuItem m => (m.Parent as ContextMenu)?.DataContext as ProfileRow,
        _ => null
    };

    private PaneView OpenProfile(ProfileInfo p)
    {
        if (_homeOpen) { _homeOpen = false; foreach (var x in PanesList) if (!x.IsHidden) x.SuspendWindow(false); }
        var pane = AddPane(p.Name, p.Id, p.Color);
        pane.Attach(p.MainWindow, Mode, MenuToolWindow.IsChecked);
        Relayout();
        return pane;
    }

    private void OnRowOpen(object sender, RoutedEventArgs e) { if (RowOf(sender) is { } r && r.P.MainWindow != IntPtr.Zero) OpenProfile(r.P); }

    /// <summary>Dock every running profile that is not in a pane yet, revealing windows hidden to the tray.</summary>
    private void OnOpenAll(object sender, RoutedEventArgs e)
    {
        if (_catalog is null) return;
        _profiles = DemoProfiles ?? _catalog.Discover(_install);
        var opened = 0;
        foreach (var p in _profiles.Where(p => p.Instance is not null))
        {
            if (PanesList.Any(x => x.TargetHwnd != IntPtr.Zero && x.TargetHwnd == p.MainWindow && p.MainWindow != IntPtr.Zero)) continue;
            var hwnd = DemoProfiles is not null ? p.MainWindow : RevealedWindow(p);
            if (hwnd == IntPtr.Zero || PanesList.Any(x => x.TargetHwnd == hwnd)) continue;
            var pane = PanesList.FirstOrDefault(x => x.ProfileId == p.Id && x.Hosted is null && x.State != PaneState.PoppedOut) ?? AddPane(p.Name, p.Id, p.Color);
            pane.Attach(hwnd, Mode, MenuToolWindow.IsChecked);
            opened++;
        }
        if (_homeOpen && PanesList.Any(x => !x.IsHidden)) { _homeOpen = false; foreach (var x in PanesList) if (!x.IsHidden) x.SuspendWindow(false); }
        Relayout();
        LogLine($"open all: {opened} docked");
    }
    private void OnRowStart(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } r) return;
        if (DemoProfiles is not null) { Info("Demo mode", "Made-up profiles can't be started. Run without --demo to work with your real profiles."); return; }
        // Start into a pane: the window docks itself when it appears.
        if (_homeOpen) { _homeOpen = false; foreach (var x in PanesList) if (!x.IsHidden) x.SuspendWindow(false); }
        var pane = AddPane(r.P.Name, r.P.Id, r.P.Color);
        _ = StartIntoPaneAsync(pane);
    }

    /// <summary>A running instance without a visible window: show the hidden one if it still exists, else relaunch the profile so
    /// Electron's single-instance hand-off makes the running instance show itself (what both switchers do).</summary>
    private async void OnRowShow(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } r || r.P.Instance is null) return;
        if (r.P.MainWindow != IntPtr.Zero) { NativeMethods.SetForegroundWindow(r.P.MainWindow); return; }
        if (DemoProfiles is not null) return;
        var hidden = ClaudeFinder.HiddenMainWindows(r.P.Instance.Pid, "Chrome_WidgetWin_1").FirstOrDefault();
        if (hidden is not null)
        {
            NativeMethods.ShowWindow(hidden.Hwnd, 5 /* SW_SHOW */);
            NativeMethods.SetForegroundWindow(hidden.Hwnd);
            LogLine($"show {r.P.Id}: hidden window {hidden.Hwnd:X} shown");
        }
        else
        {
            LogLine($"show {r.P.Id}: no window at all, relaunching for the single-instance hand-off");
            Launch(r.P);
        }
        await Task.Delay(1500);
        RefreshHome();
    }
    private async void OnRowKill(object sender, RoutedEventArgs e) { if (RowOf(sender) is { } r) await KillWithConfirmAsync(r.P); }
    private void OnRowMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.ContextMenu is null) return;
        b.ContextMenu.DataContext = b.Tag; b.ContextMenu.PlacementTarget = b; b.ContextMenu.IsOpen = true;
    }
    private void OnRowRename(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } r) return;
        var dlg = new PromptWindow("Rename " + r.P.Name, "Its sign-in and data stay as they are.", r.P.Name) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var err = ProfileCatalog.ValidateName(dlg.Value, _profiles.Where(p => p != r.P));
        if (err is not null) { Info("Can't use that name", err); return; }
        if (DemoProfiles is not null) r.P.Name = dlg.Value.Trim(); // demo: in memory only, never the real profiles.json
        else _catalog!.Rename(r.P, dlg.Value.Trim());
        foreach (var pane in PanesList) if (pane.TargetHwnd != IntPtr.Zero && pane.TargetHwnd == r.P.MainWindow) pane.ProfileName = r.P.Name;
        RefreshHome();
    }
    private void OnRowColour(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } r) return;
        if (DemoProfiles is not null) r.P.Color = (r.P.Color + 1) % 10;
        else _catalog!.SetColor(r.P, (r.P.Color + 1) % 10);
        foreach (var pane in PanesList) if (pane.TargetHwnd != IntPtr.Zero && pane.TargetHwnd == r.P.MainWindow) { pane.ColorIndex = r.P.Color; pane.ColorBrush = (Brush)FindResource("P" + (r.P.Color + 1)); }
        RefreshHome();
    }
    private void OnRowFolder(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } r) return;
        var path = r.P.Path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude");
        if (Directory.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        else Info("Nothing there yet", "This profile has no folder yet. It is created the first time you start it.");
    }
    private void OnRowDelete(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } r) return;
        if (r.P.IsOriginal) { Info("Can't delete the original account", "The original account is protected. Other profiles can be deleted."); return; }
        if (r.P.Instance is not null) { Info($"Kill {r.P.Name} first", "A running profile can't be deleted. Kill it, then delete."); return; }
        var dlg = new ConfirmWindow($"Delete {r.P.Name}?", "This signs the account out on this PC. Its profile folder, saved sign-in and shortcuts go to the Recycle Bin. Other profiles stay.", "Delete", danger: true, typeToConfirm: r.P.Name) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        if (DemoProfiles is not null) { DemoProfiles.Remove(r.P); LogLine($"demo: removed {r.P.Id} from the made-up list"); RefreshHome(); return; }
        var err = _catalog!.Delete(r.P);
        if (err is not null) Info("Not deleted", err);
        else LogLine($"deleted profile {r.P.Id}");
        RefreshHome();
    }

    private void OnAddAccount(object sender, RoutedEventArgs e)
    {
        if (DemoProfiles is not null) { Info("Demo mode", "Accounts can't be added in demo mode. Run without --demo to work with your real profiles."); return; }
        if (_install is null) { Info("Claude not found", "Claude Desktop was not found on this PC, so a new account can't be started."); return; }
        var dlg = new PromptWindow("Add account", "Opens Claude at the sign-in screen in a new window. Nothing is copied from your other profiles.", "") { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var err = ProfileCatalog.ValidateName(dlg.Value, _profiles);
        if (err is not null) { Info("Can't use that name", err); return; }
        var p = _catalog!.Add(dlg.Value.Trim(), _profiles);
        Launch(p);
        RefreshHome();
    }

    private void Launch(ProfileInfo p)
    {
        if (_install is null) return;
        var psi = p.IsOriginal && _install.Aumid is not null
            ? new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{_install.Aumid}")
            : new ProcessStartInfo(_install.Exe, p.IsOriginal ? "" : $"--user-data-dir=\"{p.Path}\"") { WorkingDirectory = Path.GetDirectoryName(_install.Exe) };
        psi.UseShellExecute = true;
        LogLine($"launch {p.Id}: {psi.FileName} {psi.Arguments.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%")}");
        try { Process.Start(psi); } catch (Exception ex) { Info("Couldn't start " + p.Name, ex.Message); }
    }

    private async Task KillWithConfirmAsync(ProfileInfo p)
    {
        if (p.Instance is null) return;
        var dlg = new ConfirmWindow($"Kill {p.Name}?", "Anything running inside it, such as a background task or a Claude Code session, will stop. Your conversations are kept.", "Kill", danger: true) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        foreach (var pane in PanesList) if (pane.TargetHwnd != IntPtr.Zero && pane.TargetHwnd == p.MainWindow) pane.Release();
        if (DemoProfiles is not null)
        {
            // Demo: the pid is made up, so end the dummy behind the window instead, never a process by that number.
            if (p.MainWindow != IntPtr.Zero && NativeMethods.IsWindow(p.MainWindow))
            {
                NativeMethods.GetWindowThreadProcessId(p.MainWindow, out var dummyPid);
                try { using var d = Process.GetProcessById((int)dummyPid); if (d.ProcessName.Equals("Multipane", StringComparison.OrdinalIgnoreCase)) d.Kill(); } catch { /* demo */ }
            }
            p.Instance = null; p.MainWindow = IntPtr.Zero;
            RefreshHome();
            return;
        }
        await ProfileCatalog.KillAsync(p, LogLine);
        RefreshHome();
    }

    private void Info(string title, string body) => new ConfirmWindow(title, body, "OK", danger: false) { Owner = this }.ShowDialog();

    private void OnFilterChanged(object sender, TextChangedEventArgs e) { if (IsLoaded) RefreshPicker(); }
    private void OnPickDoubleClick(object sender, MouseButtonEventArgs e) => HostPicked();
    private void OnPickHost(object sender, RoutedEventArgs e) => HostPicked();

    private void HostPicked()
    {
        var entry = Instances.SelectedItem as ListEntry ?? Windows.SelectedItem as ListEntry;
        if (entry is null || entry.Hwnd == IntPtr.Zero) { LogLine("pick: nothing selected"); return; }
        Picker.IsOpen = false;
        if (_homeOpen) { _homeOpen = false; foreach (var x in PanesList) if (!x.IsHidden) x.SuspendWindow(false); }
        var prof = _profiles.FirstOrDefault(p => p.Instance?.Pid == entry.Pid);
        var pane = _pickFor ?? AddPane(entry.Name, prof?.Id, prof?.Color);
        if (_pickFor is not null && pane.ProfileName.StartsWith("Pane ", StringComparison.Ordinal)) pane.ProfileName = entry.Name;
        if (prof is not null) { pane.ProfileId = prof.Id; pane.ColorIndex = prof.Color; pane.ColorBrush = (Brush)FindResource("P" + (prof.Color + 1)); }
        pane.Attach(entry.Hwnd, Mode, MenuToolWindow.IsChecked);
        _pickFor = null;
        Relayout();
    }

    private void OnShowHidden(object sender, MouseButtonEventArgs e)
    {
        if (HiddenPanes.SelectedItem is PaneView p) { p.IsHidden = false; p.SuspendWindow(false); Picker.IsOpen = false; Relayout(); }
    }

    // ----- settings menu -----

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        MenuThemeSystem.IsChecked = Theme.Mode == "system"; MenuThemeLight.IsChecked = Theme.Mode == "light"; MenuThemeDark.IsChecked = Theme.Mode == "dark";
        SettingsButton.ContextMenu!.PlacementTarget = SettingsButton; SettingsButton.ContextMenu.IsOpen = true;
    }

    private void OnTheme(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string mode }) return;
        Theme.Apply(Application.Current, mode);
        Theme.ApplyBackdrop(Hwnd, this);
        // Brushes resolved at creation time (badges, stripes) are re-resolved for the new theme.
        foreach (var p in PanesList) p.ColorBrush = (Brush)FindResource("P" + (p.ColorIndex + 1));
        if (EmptyHint.Visibility == Visibility.Visible) RefreshHome();
        LogLine($"appearance: {mode}");
    }
    private void OnApproach(object sender, RoutedEventArgs e) { MenuModeA.IsChecked = sender == MenuModeA; MenuModeB.IsChecked = sender == MenuModeB; }
    private void OnAddPane(object sender, RoutedEventArgs e)
    {
        if (_homeOpen) { _homeOpen = false; foreach (var x in PanesList) if (!x.IsHidden) x.SuspendWindow(false); }
        AddPane();
    }
    private void OnReleaseAll(object sender, RoutedEventArgs e) => ReleaseAll();
    private void OnToggleLog(object sender, RoutedEventArgs e) => Log.Visibility = MenuLog.IsChecked ? Visibility.Visible : Visibility.Collapsed;
    private void OnOpenLogFolder(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_logPath}\"") { UseShellExecute = true });

    private void OnLaunchProfile(object sender, RoutedEventArgs e)
    {
        if (_install is null) { LogLine("no install"); return; }
        var dlg = new PromptWindow("Launch a profile", "Folder for --user-data-dir. Leave empty for the original account.", "") { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var dir = dlg.Value.Trim();
        var psi = dir.Length == 0 && _install.Aumid is not null
            ? new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{_install.Aumid}")
            : new ProcessStartInfo(_install.Exe, dir.Length == 0 ? "" : $"--user-data-dir=\"{dir}\"") { WorkingDirectory = Path.GetDirectoryName(_install.Exe) };
        psi.UseShellExecute = true;
        LogLine($"launch: {psi.FileName} {psi.Arguments}");
        try { Process.Start(psi); } catch (Exception ex) { LogLine("launch failed: " + ex.Message); }
    }

    public void ReleaseAll() { foreach (var p in PanesList) p.Release(); Relayout(); }

    // ----- window plumbing -----

    private void OnMoved(object? sender, EventArgs e) => PlaceAll();

    /// <summary>Keeps the left side (name or tabs) from running under the centred segment.</summary>
    private void OnTitleBarSized(object sender, SizeChangedEventArgs e)
    {
        var half = e.NewSize.Width / 2 - SegmentBox.ActualWidth / 2 - 12;
        LeftBar.MaxWidth = Math.Max(0, half);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        var minimised = WindowState == WindowState.Minimized;
        FitMaximisedContent();
        MaxButton.Content = WindowState == WindowState.Maximized ? "" : "";
        MaxButton.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximise";
        foreach (var p in PanesList) if (p.Visibility == Visibility.Visible && !p.IsHidden) p.SuspendWindow(minimised);
        if (!minimised) Dispatcher.BeginInvoke(PlaceAll, DispatcherPriority.Loaded);
    }

    private bool _fullscreen;
    private Rect _beforeFullscreen;

    /// <summary>F11: cover the whole monitor, taskbar included. Again or Escape restores the previous bounds.</summary>
    private void ToggleFullscreen()
    {
        if (!_fullscreen)
        {
            if (!NativeMethods.WorkAreaOf(Hwnd, out _, out var mon)) return;
            _beforeFullscreen = WindowState == WindowState.Maximized ? Rect.Empty : new Rect(Left, Top, Width, Height);
            WindowState = WindowState.Normal;
            var dpi = VisualTreeHelper.GetDpi(this);
            Left = mon.Left / dpi.DpiScaleX; Top = mon.Top / dpi.DpiScaleY; Width = mon.Width / dpi.DpiScaleX; Height = mon.Height / dpi.DpiScaleY;
            Topmost = true; _fullscreen = true;
        }
        else
        {
            Topmost = false; _fullscreen = false;
            if (_beforeFullscreen.IsEmpty) WindowState = WindowState.Maximized;
            else { Left = _beforeFullscreen.Left; Top = _beforeFullscreen.Top; Width = _beforeFullscreen.Width; Height = _beforeFullscreen.Height; }
        }
        Dispatcher.BeginInvoke(PlaceAll, DispatcherPriority.Loaded);
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaximize(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void OnClose(object sender, RoutedEventArgs e) => Close();
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e) { SaveLayout(); ReleaseAll(); }
}
