using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace HostingSpike;

/// <summary>A 448 px dialog in the design's shape: title 20 px, body in the secondary colour, one input, buttons sharing the footer.</summary>
public sealed class PromptWindow : Window
{
    private readonly TextBox _input;
    public string Value => _input.Text;

    public PromptWindow(string title, string body, string initial)
    {
        Title = title; Width = 448; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        Background = (Brush)FindResource("Surface"); Foreground = (Brush)FindResource("Text");
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        var bodyPanel = new StackPanel { Margin = new Thickness(24) };
        bodyPanel.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        bodyPanel.Children.Add(new TextBlock { Text = body, Foreground = (Brush)FindResource("Text2"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        _input = new TextBox { Text = initial };
        _input.SelectAll();
        bodyPanel.Children.Add(_input);
        var foot = new UniformGrid { Columns = 2, Margin = new Thickness(24), };
        var ok = new Button { Content = "Save", Style = (Style)FindResource("BtnPrimary"), Margin = new Thickness(0, 0, 4, 0), IsDefault = true };
        var cancel = new Button { Content = "Cancel", Style = (Style)FindResource("Btn"), Margin = new Thickness(4, 0, 0, 0), IsCancel = true };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        foot.Children.Add(ok); foot.Children.Add(cancel);
        var footBorder = new Border { Background = (Brush)FindResource("Surface2"), BorderBrush = (Brush)FindResource("Stroke"), BorderThickness = new Thickness(0, 1, 0, 0), Child = foot };
        var root = new DockPanel();
        DockPanel.SetDock(footBorder, Dock.Bottom);
        root.Children.Add(footBorder);
        root.Children.Add(bodyPanel);
        Content = root;
        Loaded += (_, _) => _input.Focus();
    }
}
