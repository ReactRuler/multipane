using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HostingSpike;

public enum PaneState { Empty, Starting, Running, NotResponding, PoppedOut, Gone }

/// <summary>One pane: stripe, 30 px header with badge, state glyph and memory, then either the hosted window or a placeholder.</summary>
public partial class PaneView : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<PaneView>? Activated;
    public event Action<PaneView>? RemoveRequested;
    public event Action<PaneView>? HideRequested;
    public event Action<PaneView>? ChooseWindowRequested;
    public event Action<PaneView>? KillRequested;
    /// <summary>The user renamed or recoloured this pane; the shell persists it to the profile.</summary>
    public event Action<PaneView>? IdentityChanged;
    /// <summary>The pane knows its profile and the user pressed Start.</summary>
    public event Action<PaneView>? StartRequested;
    /// <summary>Profile behind this pane, when known (restored session or opened from the home page).</summary>
    public string? ProfileId { get; set; }
    public event Action<PaneView>? Changed;

    private string _name = "Pane";
    private string _initials = "PA";
    private Brush _color = Brushes.Gray;
    private PaneState _state = PaneState.Empty;
    private string _mem = "";
    private bool _isActive;
    private DateTime _goneAt;
    private IntPtr _target;
    private HostMode _mode;
    private bool _toolWindow;
    private readonly Action<string> _log;
    private readonly IntPtr _hostHwnd;

    public HostedWindow? Hosted { get; private set; }
    public IntPtr TargetHwnd => _target;
    public bool IsHidden { get; set; }
    public int ColorIndex { get; set; }

    public PaneView(IntPtr hostHwnd, Action<string> log)
    {
        _hostHwnd = hostHwnd; _log = log;
        InitializeComponent();
        DataContext = this;
        Host.Positioned += () => Place();
        UpdatePlaceholder();
    }

    public string ProfileName { get => _name; set { _name = value; _initials = MakeInitials(value); Notify(); Notify(nameof(Initials)); Changed?.Invoke(this); } }
    public string Initials => _initials;
    public Brush ColorBrush { get => _color; set { _color = value; Notify(); Changed?.Invoke(this); } }
    public string MemText { get => _mem; private set { _mem = value; Notify(); } }
    public PaneState State { get => _state; private set { _state = value; Notify(); Notify(nameof(StateText)); Notify(nameof(StateBrush)); Notify(nameof(GlyphFill)); Notify(nameof(GlyphStroke)); UpdatePlaceholder(); Changed?.Invoke(this); } }
    public bool IsActive { get => _isActive; set { if (_isActive == value) return; _isActive = value; Notify(); if (value) Activated?.Invoke(this); } }

    public string StateText => State switch
    {
        PaneState.Empty => "Not running",
        PaneState.Starting => "Starting",
        PaneState.Running => "Running",
        PaneState.NotResponding => "Not responding",
        PaneState.PoppedOut => "Running · in its own window",
        PaneState.Gone => $"Window closed · {_goneAt:HH:mm}",
        _ => ""
    };

    public Brush StateBrush => (Brush)FindResource(State switch
    {
        PaneState.Running or PaneState.PoppedOut => "Ok",
        PaneState.NotResponding or PaneState.Starting => "Warn",
        _ => "Text3"
    });

    public Geometry? GlyphFill => State switch
    {
        PaneState.Running or PaneState.PoppedOut => (Geometry)FindResource("GlyphRunning"),
        PaneState.Gone => (Geometry)FindResource("GlyphKilled"),
        _ => null
    };

    public Geometry? GlyphStroke => State switch
    {
        PaneState.Empty => (Geometry)FindResource("GlyphRing"),
        PaneState.Starting => (Geometry)FindResource("GlyphRing"),
        PaneState.NotResponding => Geometry.Combine((Geometry)FindResource("GlyphRing"), (Geometry)FindResource("GlyphClockHand"), GeometryCombineMode.Union, null),
        _ => null
    };

    public string PlaceholderTitle => State switch
    {
        PaneState.Empty when ProfileId is not null => $"{ProfileName} is not running",
        PaneState.Empty => $"{ProfileName} has no window yet",
        PaneState.Starting => $"Starting {ProfileName}",
        PaneState.PoppedOut => $"{ProfileName} is running in its own window",
        PaneState.Gone => $"{ProfileName}'s window closed at {_goneAt:HH:mm}",
        _ => ""
    };

    public string PlaceholderBody => State switch
    {
        PaneState.Empty when ProfileId is not null => "Start it here. Its window docks into this pane as soon as it appears.",
        PaneState.Empty => "Choose a running window to host here.",
        PaneState.Starting => "Waiting for the Claude window. Usually 2 to 6 seconds.",
        PaneState.PoppedOut => "Everything in it is fine; it just isn't docked. Dock back returns it to this slot.",
        PaneState.Gone => "The process ended or closed its window. The slot is kept so nothing shifts.",
        _ => ""
    };

    public void MarkStarting() => State = PaneState.Starting;
    public void MarkNotRunning() { if (State == PaneState.Starting) State = PaneState.Empty; }

    private void UpdatePlaceholder()
    {
        var showPlaceholder = State is PaneState.Empty or PaneState.Starting or PaneState.PoppedOut or PaneState.Gone;
        Placeholder.Visibility = showPlaceholder ? Visibility.Visible : Visibility.Collapsed;
        Host.Visibility = showPlaceholder ? Visibility.Hidden : Visibility.Visible;
        DockBackButton.Visibility = State == PaneState.PoppedOut ? Visibility.Visible : Visibility.Collapsed;
        PopOutButton.Visibility = State is PaneState.Running or PaneState.NotResponding ? Visibility.Visible : Visibility.Collapsed;
        MenuPopOut.Header = State == PaneState.PoppedOut ? "Dock back" : "Pop out";
        PrimaryAction.Content = State switch { PaneState.PoppedOut => "Dock back", PaneState.Gone => "Choose another window", PaneState.Starting => "Cancel", PaneState.Empty when ProfileId is not null => "Start", _ => "Choose a window" };
        PlaceholderGlyph.Data = State switch
        {
            PaneState.Gone => Geometry.Parse("M4,4 L24,4 A3,3 0 0 1 27,7 L27,24 A3,3 0 0 1 24,27 L7,27 A3,3 0 0 1 4,24 L4,7 A3,3 0 0 1 7,4 Z"),
            PaneState.PoppedOut => Geometry.Parse("M12,5 L5,5 L5,23 L23,23 L23,16 M16,5 L23,5 L23,12 M23,5 L13,15"),
            _ => Geometry.Parse("M14,4 A10,10 0 1 0 14,24 A10,10 0 1 0 14,4 Z")
        };
        PlaceholderGlyph.StrokeDashArray = State == PaneState.Starting ? new DoubleCollection { 3.5, 2.5 } : null;
        Notify(nameof(PlaceholderTitle)); Notify(nameof(PlaceholderBody));
    }

    // ----- hosting -----

    public HostedWindow Attach(IntPtr hwnd, HostMode mode, bool toolWindow)
    {
        Hosted?.Release(_log);
        _target = hwnd; _mode = mode; _toolWindow = toolWindow;
        if (Host.Handle == IntPtr.Zero) { Window.GetWindow(this)?.UpdateLayout(); } // a pane added this instant has no native window until it is laid out
        if (Host.Handle == IntPtr.Zero) _log("pane host window not created yet; approach A would reparent to the desktop");
        Hosted = HostedWindow.Attach(hwnd, mode, _hostHwnd, Host.Handle, toolWindow, _log);
        Hosted.LogPlacementTo(_log);
        if (ProfileName == "Pane") ProfileName = NativeMethods.Title(hwnd);
        State = PaneState.Running;
        Place();
        return Hosted;
    }

    public void Place()
    {
        if (Hosted is null || Host.Visibility != Visibility.Visible) return;
        if (!Hosted.Place(Host.ScreenRect())) _log($"place {ProfileName} failed");
    }

    public void Release()
    {
        Hosted?.Release(_log);
        Hosted = null;
        _target = IntPtr.Zero;
        State = PaneState.Empty;
    }

    public void ClearHosted() { Hosted = null; if (State == PaneState.Running) State = PaneState.Empty; }

    /// <summary>Hide or show the hosted window without changing the pane's state (used while the host is minimised).</summary>
    public void SuspendWindow(bool hidden) => Hosted?.SetHidden(hidden);

    public void PopOut()
    {
        if (Hosted is null) return;
        Hosted.Release(_log);
        Hosted = null;
        State = PaneState.PoppedOut;
    }

    public void DockBack()
    {
        if (_target == IntPtr.Zero || !NativeMethods.IsWindow(_target)) { State = PaneState.Gone; return; }
        Hosted = HostedWindow.Attach(_target, _mode, _hostHwnd, Host.Handle, _toolWindow, _log);
        Hosted.LogPlacementTo(_log);
        State = PaneState.Running;
        Place();
    }

    /// <summary>Called by the shell's 1 s timer: hung detection and liveness; by the 3 s timer: memory.</summary>
    public void Sample(bool withMemory)
    {
        if (_target == IntPtr.Zero) return;
        if (!NativeMethods.IsWindow(_target))
        {
            if (State != PaneState.Gone) { _goneAt = DateTime.Now; Hosted = null; MemText = ""; State = PaneState.Gone; }
            return;
        }
        if (State is PaneState.Running or PaneState.NotResponding)
        {
            var hung = NativeMethods.IsHungAppWindow(_target);
            if (hung && State == PaneState.Running) State = PaneState.NotResponding;
            if (!hung && State == PaneState.NotResponding) State = PaneState.Running;
        }
        if (withMemory)
        {
            NativeMethods.GetWindowThreadProcessId(_target, out var pid);
            try { using var p = Process.GetProcessById((int)pid); MemText = $"· {p.PrivateMemorySize64 / 1048576.0 / 1024:0.0} GB"; }
            catch { MemText = ""; }
        }
    }

    // ----- header actions -----

    private void OnHeaderClick(object sender, MouseButtonEventArgs e)
    {
        IsActive = true;
        if (e.ClickCount == 2) Activated?.Invoke(this); // the shell treats a double-click as maximise toggle
    }
    private void OnPopOut(object sender, RoutedEventArgs e) { if (State == PaneState.PoppedOut) DockBack(); else PopOut(); }
    private void OnDockBack(object sender, RoutedEventArgs e) => DockBack();
    private void OnHide(object sender, RoutedEventArgs e) => HideRequested?.Invoke(this);
    private void OnMore(object sender, RoutedEventArgs e) { if (sender is Button b && b.ContextMenu is not null) { b.ContextMenu.PlacementTarget = b; b.ContextMenu.IsOpen = true; } }
    private void OnBringForward(object sender, RoutedEventArgs e) { if (_target != IntPtr.Zero) NativeMethods.SetForegroundWindow(_target); }
    private void OnRemove(object sender, RoutedEventArgs e) => RemoveRequested?.Invoke(this);
    private void OnKill(object sender, RoutedEventArgs e) { if (_target != IntPtr.Zero) KillRequested?.Invoke(this); }
    private void OnPrimaryAction(object sender, RoutedEventArgs e)
    {
        if (State == PaneState.PoppedOut) DockBack();
        else if (State == PaneState.Starting) MarkNotRunning();
        else if (State == PaneState.Empty && ProfileId is not null) StartRequested?.Invoke(this);
        else ChooseWindowRequested?.Invoke(this);
    }
    private void OnRename(object sender, RoutedEventArgs e)
    {
        var dlg = new PromptWindow("Rename " + ProfileName, "Its sign-in and data stay as they are.", ProfileName) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true && dlg.Value.Trim().Length > 0) { ProfileName = dlg.Value.Trim(); IdentityChanged?.Invoke(this); }
    }
    private void OnChangeColour(object sender, RoutedEventArgs e)
    {
        ColorIndex = (ColorIndex + 1) % 10;
        ColorBrush = (Brush)FindResource("P" + (ColorIndex + 1));
        IdentityChanged?.Invoke(this);
    }

    public static string MakeInitials(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return "--";
        var s = words.Length >= 2 ? $"{words[0][0]}{words[^1][0]}" : name.Length >= 2 ? name[..2] : name;
        return s.ToUpperInvariant();
    }

    private void Notify([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
