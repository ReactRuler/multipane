using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace HostingSpike;

/// <summary>Confirmation in the design's dialog shape. Focus starts on Cancel and Enter cancels, so a reflex Enter never destroys anything.
/// With <paramref name="typeToConfirm"/> the danger button stays disabled until that text is typed.</summary>
public sealed class ConfirmWindow : Window
{
    public ConfirmWindow(string title, string body, string action, bool danger, string? typeToConfirm = null)
    {
        Title = title; Width = 448; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        Background = (Brush)FindResource("Surface"); Foreground = (Brush)FindResource("Text");
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        var bodyPanel = new StackPanel { Margin = new Thickness(24) };
        bodyPanel.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12), TextWrapping = TextWrapping.Wrap });
        bodyPanel.Children.Add(new TextBlock { Text = body, Foreground = (Brush)FindResource("Text2"), TextWrapping = TextWrapping.Wrap });
        var ok = new Button { Content = action, Style = (Style)FindResource("BtnPrimary"), Margin = new Thickness(0, 0, 4, 0) };
        if (danger) { ok.Background = (Brush)FindResource("Err"); ok.Foreground = Brushes.White; }
        if (typeToConfirm is not null)
        {
            bodyPanel.Children.Add(new TextBlock { Text = "Type the profile name to confirm", FontSize = 12, Foreground = (Brush)FindResource("Text2"), Margin = new Thickness(0, 12, 0, 4) });
            var box = new TextBox();
            box.TextChanged += (_, _) => ok.IsEnabled = string.Equals(box.Text.Trim(), typeToConfirm, StringComparison.Ordinal);
            ok.IsEnabled = false;
            bodyPanel.Children.Add(box);
        }
        var cancel = new Button { Content = "Cancel", Style = (Style)FindResource("Btn"), Margin = new Thickness(4, 0, 0, 0), IsCancel = true, IsDefault = true };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        var foot = new UniformGrid { Columns = 2, Margin = new Thickness(24) };
        foot.Children.Add(ok); foot.Children.Add(cancel);
        var footBorder = new Border { Background = (Brush)FindResource("Surface2"), BorderBrush = (Brush)FindResource("Stroke"), BorderThickness = new Thickness(0, 1, 0, 0), Child = foot };
        var root = new DockPanel();
        DockPanel.SetDock(footBorder, Dock.Bottom);
        root.Children.Add(footBorder);
        root.Children.Add(bodyPanel);
        Content = root;
        Loaded += (_, _) => cancel.Focus();
    }
}
