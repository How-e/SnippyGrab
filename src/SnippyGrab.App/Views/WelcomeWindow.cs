namespace SnippyGrab.App.Views;

internal sealed class WelcomeWindow : Window
{
    public WelcomeWindow(AppController controller)
    {
        Ui.StyleWindow(this); Title = "Welcome to SnippyGrab"; Width = 560; Height = 720; MinWidth = 480; MinHeight = 480; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
        var panel = new StackPanel();
        panel.Children.Add(Ui.Text("Capture. Paste. Keep moving.", 23));
        panel.Children.Add(Ui.Text("A small screenshot shelf for your desktop.", 13, true));
        foreach (var line in new[] { "Print Screen → select a region → release.", "Ctrl+V pastes the capture immediately.", "Drag from the shelf to attach a real PNG file.", "Click a screenshot to annotate. Ctrl-click selects several." })
            panel.Children.Add(new TextBlock { Text = "•  " + line, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 9, 0, 0) });
        panel.Children.Add(Ui.Text("Local only. No accounts, uploads or analytics.", 12, true));
        panel.Children.Add(Ui.Text("Set up Print Screen", 17));
        panel.Children.Add(Ui.Text("Windows Snipping Tool can take over Print Screen. Turn OFF ‘Use the Print Screen key to open screen capture’ in Windows Settings → Accessibility → Keyboard, then restart SnippyGrab. You can use the fallback shortcut or tray capture until then.", 13));
        var interception = Ui.Text("", 12, true); panel.Children.Add(interception);
        void RefreshInterception() => interception.Text = Services.HotkeyService.WindowsPrintScreenCaptureEnabled
            ? "Windows Print Screen capture is currently ON. Turn it off to use SnippyGrab with Print Screen."
            : "SnippyGrab never changes this Windows setting for you.";
        RefreshInterception(); Activated += (_, _) => RefreshInterception();
        panel.Children.Add(Ui.Button("Open Windows Keyboard settings", "Turn off Windows Print Screen capture", () => controller.Try(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:easeofaccess-keyboard") { UseShellExecute = true }))));
        panel.Children.Add(Ui.Text("Pictures / PNG export folder", 17));
        panel.Children.Add(Ui.Text("Choose where Export PNG starts. Defaults to your Windows Pictures folder. Captures stay in the temporary cache until you export them.", 12, true));
        var folderRow = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var folder = new TextBox { Text = controller.Settings.SaveDirectory };
        System.Windows.Automation.AutomationProperties.SetName(folder, "Pictures / PNG export folder");
        var browse = Ui.Button("Browse…", "Choose your Pictures / PNG export folder", () => controller.Try(() =>
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose Pictures / PNG export folder", InitialDirectory = Directory.Exists(folder.Text) ? folder.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) };
            if (dialog.ShowDialog(this) == true) folder.Text = dialog.FolderName;
        }));
        DockPanel.SetDock(browse, Dock.Right); folderRow.Children.Add(browse); folderRow.Children.Add(folder); panel.Children.Add(folderRow);
        void SettingsChanged() => folder.Text = controller.Settings.SaveDirectory;
        controller.SettingsChanged += SettingsChanged;
        Closed += (_, _) => controller.SettingsChanged -= SettingsChanged;
        var startup = new CheckBox { Content = "Launch silently at Windows login", IsChecked = controller.Settings.LaunchOnStartup, Margin = new Thickness(0, 14, 0, 12) }; panel.Children.Add(startup);
        if (controller.Hotkeys.Warnings.Count > 0) panel.Children.Add(Ui.Text(string.Join("\n", controller.Hotkeys.Warnings), 12, true));
        panel.Children.Add(Ui.Text(HotkeyRegistration.Guidance(controller.Settings), 12, true));
        var footer = new StackPanel();
        var status = Ui.Text("", 12, true); status.TextWrapping = TextWrapping.Wrap; footer.Children.Add(status);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        actions.Children.Add(Ui.Button("Settings", "Configure hotkeys and shelf", () => controller.ShowSettings()));
        actions.Children.Add(Ui.Button("Start snipping", "Finish setup and stay in tray", () =>
        {
            try
            {
                var destination = folder.Text.Trim();
                if (destination.Length == 0) destination = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (!Path.IsPathFullyQualified(destination)) throw new InvalidDataException("Choose an absolute Pictures / PNG export folder.");
                destination = Path.GetFullPath(destination);
                var cache = Path.TrimEndingDirectorySeparator(controller.Repository.Root);
                if (destination.Equals(cache, StringComparison.OrdinalIgnoreCase) || destination.StartsWith(cache + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Choose an export folder outside the temporary cache.");
                if (File.Exists(destination)) throw new InvalidDataException("Choose a folder, not a file.");
                var draft = System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(controller.Settings))!;
                draft.SaveDirectory = destination; draft.FirstRunComplete = true; draft.LaunchOnStartup = startup.IsChecked == true;
                controller.ApplySettings(draft); Close();
            }
            catch (InvalidDataException ex) { status.Text = ex.Message; }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or System.Security.SecurityException or AggregateException)
            { status.Text = "Setup could not be saved. " + OperationFailure.From(ex).Message; }
        }));
        footer.Children.Add(actions); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        foreach (var text in panel.Children.OfType<TextBlock>()) text.TextWrapping = TextWrapping.Wrap;
    }
}
