using System.Windows.Automation;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal sealed class MonitorCaptureWindow : Window
{
    internal CaptureTarget? Target { get; private set; }
    internal MonitorCaptureWindow()
    {
        Ui.StyleWindow(this); Title = "SnippyGrab · Capture monitor"; Width = 540; Height = 300; MinWidth = 420; MinHeight = 260;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new StackPanel { Margin = new Thickness(24) }; Content = Ui.Scroll(root);
        root.Children.Add(Ui.Heading("Capture monitor")); root.Children.Add(Ui.Text("Choose the complete display, including its taskbar.", 14, true));
        var displays = MonitorService.All();
        var picker = new ComboBox { ItemsSource = displays.Select(m => new DisplayChoice(m.Identity, $"Display {m.Index + 1} · {m.Bounds.Width} × {m.Bounds.Height}" + (m.Primary ? " · primary" : ""))).ToArray(), SelectedIndex = displays.Count > 0 ? 0 : -1, Margin = new Thickness(0, 18, 0, 18) };
        AutomationProperties.SetName(picker, "Display to capture"); root.Children.Add(picker);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = Ui.Button("Cancel", "Cancel monitor capture", () => DialogResult = false); cancel.IsCancel = true; actions.Children.Add(cancel);
        var capture = Ui.Button("Capture", "Capture the selected display", () => { if (picker.SelectedItem is DisplayChoice choice) { Target = new(choice.Identity); DialogResult = true; } }); capture.IsDefault = true; capture.IsEnabled = displays.Count > 0; actions.Children.Add(capture); root.Children.Add(actions);
        Loaded += (_, _) => picker.Focus();
    }
    private sealed record DisplayChoice(string Identity, string Label) { public override string ToString() => Label; }
}
