namespace SnippyGrab.App.Views;

internal sealed class WelcomeWindow : Window
{
    public WelcomeWindow(AppController controller)
    {
        Ui.StyleWindow(this); Title = "Welcome to SnippyGrab"; Width = 520; Height = 620; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var panel = new StackPanel { Margin = new Thickness(24) }; Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(Ui.Text("Capture. Paste. Keep moving.", 23));
        panel.Children.Add(Ui.Text("A small screenshot shelf for your desktop.", 13, true));
        foreach (var line in new[] { "Print Screen → select a region → release.", "Ctrl+V pastes the capture immediately.", "Drag from the shelf to attach a real PNG file.", "Click a screenshot to annotate. Ctrl-click selects several." })
            panel.Children.Add(new TextBlock { Text = "•  " + line, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 9, 0, 0) });
        panel.Children.Add(Ui.Text("Local only. No accounts, uploads or analytics.", 12, true));
        var startup = new CheckBox { Content = "Launch silently at Windows login", IsChecked = controller.Settings.LaunchOnStartup, Margin = new Thickness(0, 14, 0, 12) }; panel.Children.Add(startup);
        if (controller.Hotkeys.Warnings.Count > 0) panel.Children.Add(Ui.Text(string.Join("\n", controller.Hotkeys.Warnings), 12, true));
        panel.Children.Add(Ui.Text(HotkeyRegistration.Guidance(controller.Settings), 12, true));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        actions.Children.Add(Ui.Button("Settings", "Configure hotkeys and shelf", () => controller.ShowSettings()));
        actions.Children.Add(Ui.Button("Start snipping", "Finish setup and stay in tray", () => controller.Try(() => { var draft = System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(controller.Settings))!; draft.FirstRunComplete = true; draft.LaunchOnStartup = startup.IsChecked == true; controller.ApplySettings(draft); Close(); })));
        panel.Children.Add(actions);
    }
}
