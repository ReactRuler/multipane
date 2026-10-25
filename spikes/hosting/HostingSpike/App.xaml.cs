using System.Windows;

namespace HostingSpike;

public partial class App : Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        var themeAt = Array.IndexOf(e.Args, "--theme");
        Theme.Load(this, themeAt >= 0 && themeAt + 1 < e.Args.Length ? e.Args[themeAt + 1] : null);
        // --theme light|dark  force a theme (screenshots); otherwise Windows decides
        // --dummy <name>   a plain target window for self-tests and manual trials
        // --selftest       hosts two dummies with A and B, checks rectangles, styles, hide/show and release, writes selftest-*.txt
        var args = e.Args;
        if (args.Length >= 2 && args[0] == "--dummy")
        {
            MainWindow = new DummyWindow(args[1]);
            MainWindow.Show();
            return;
        }
        var win = new MainWindow();
        MainWindow = win;
        // An unexpected exception is logged and the app keeps running; exiting would leave every hosted window frameless.
        DispatcherUnhandledException += (_, ex) =>
        {
            try { win.LogLine("UNHANDLED: " + ex.Exception); } catch { /* logging itself failed */ }
            ex.Handled = true;
        };
        win.Show();
        if (args.Contains("--selftest")) _ = SelfTest.RunAsync(win);
        var demoAt = Array.IndexOf(args, "--demo");
        if (demoAt >= 0) _ = Demo.RunAsync(win, demoAt + 1 < args.Length ? args[demoAt + 1] : null);
        else if (!args.Contains("--selftest")) _ = win.RestoreSessionAsync();
    }
}
