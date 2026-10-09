using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal sealed class WelcomeWindow : Window
{
    public WelcomeWindow(AppController controller)
    {
        Ui.StyleWindow(this); Title = "Welcome to SnippyGrab"; Width = 840; Height = 820; MinWidth = 540; MinHeight = 520; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new DockPanel { Margin = new Thickness(28, 22, 28, 18) }; Content = root;
        var body = new StackPanel(); body.Children.Add(Ui.Heading("Capture. Paste. Keep moving.")); body.Children.Add(Ui.Text("A small screenshot shelf for your desktop.", 16, true));
        var steps = new UniformGrid { Columns = 3, Margin = new Thickness(0, 16, 0, 14) };
        foreach (var (title, icon, text) in new[] { ("1  Capture", "capture", "Print Screen, select a region, release."), ("2  Paste", "copy", "Ctrl+V pastes the image immediately."), ("3  Keep", "image", "Drag from the shelf to attach a PNG. Click to edit.") })
        {
            var step = new StackPanel { Margin = new Thickness(0, 0, 20, 6) }; step.Children.Add(Ui.IconLabel(icon, title)); var detail = Ui.Text(text, 12, true); detail.Margin = new Thickness(30, 10, 0, 0); step.Children.Add(detail); steps.Children.Add(step);
        }
        steps.SizeChanged += (_, _) => steps.Columns = steps.ActualWidth < 480 * Ui.TextScale ? 1 : 3; body.Children.Add(steps);
        var sample = new DockPanel(); var miniature = new StackPanel { Width = 200, Margin = new Thickness(0, 0, 22, 0) };
        var code = Ui.Text("function captureScreen() {\n  const image = await grab();\n  return image;\n}", 11); code.FontFamily = new FontFamily("Consolas"); miniature.Children.Add(code);
        var icons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) }; foreach (var name in new[] { "edit", "copy", "pin", "more" }) { var icon = Icons.Make(name, 16); icon.Margin = new Thickness(0, 0, 18, 0); icons.Children.Add(icon); }
        miniature.Children.Add(icons); DockPanel.SetDock(miniature, Dock.Left); sample.Children.Add(miniature);
        var explanation = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; explanation.Children.Add(Ui.Heading("Your captures stay close.", 16)); explanation.Children.Add(Ui.Text("Screens you capture appear in the shelf, ready to paste, attach or edit.", 12, true)); sample.Children.Add(explanation); body.Children.Add(Ui.Group(sample, new Thickness(14)));
        var shortcut = new StackPanel(); shortcut.Children.Add(Ui.Heading("Set up Print Screen", 16)); var interception = Ui.Text("", 14); shortcut.Children.Add(interception); var guidance = Ui.Text("", 12, true); shortcut.Children.Add(guidance);
        void RefreshInterception()
        {
            var intercepted = HotkeyService.WindowsPrintScreenCaptureEnabled;
            interception.Text = intercepted ? "Windows screen capture is currently on." : "Check the Windows shortcut setting.";
            guidance.Text = intercepted ? "Turn off ‘Use the Print Screen key to open screen capture’ in Windows Keyboard settings, then restart SnippyGrab. The fallback shortcut and tray capture remain available." : "If Windows intercepts Print Screen, turn off its screen capture shortcut. SnippyGrab never changes this setting for you.";
        }
        RefreshInterception(); Activated += (_, _) => RefreshInterception();
        shortcut.Children.Add(Ui.ActionButton("Open Keyboard settings", "keyboard", "Open Windows Print Screen settings", () => Process.Start(new ProcessStartInfo("ms-settings:easeofaccess-keyboard") { UseShellExecute = true }))); body.Children.Add(Ui.Group(shortcut, new Thickness(14)));
        var export = new StackPanel(); export.Children.Add(Ui.Heading("Export folder", 16));
        var folderRow = new DockPanel { Margin = new Thickness(0, 10, 0, 0) }; var folder = new TextBox { Text = controller.Settings.SaveDirectory }; AutomationProperties.SetName(folder, "Pictures / PNG export folder");
        var browse = Ui.Button("Browse…", "Choose your PNG export folder", () => { var dialog = new OpenFolderDialog { Title = "Choose PNG export folder", InitialDirectory = Directory.Exists(folder.Text) ? folder.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) }; if (dialog.ShowDialog(this) == true) folder.Text = dialog.FolderName; }); DockPanel.SetDock(browse, Dock.Right); folderRow.Children.Add(browse); folderRow.Children.Add(folder); export.Children.Add(folderRow); export.Children.Add(Ui.Text("Captures stay in the temporary cache until you export them.", 12, true)); body.Children.Add(Ui.Group(export, new Thickness(14)));
        void SettingsChanged() => folder.Text = controller.Settings.SaveDirectory; controller.SettingsChanged += SettingsChanged; Closed += (_, _) => controller.SettingsChanged -= SettingsChanged;
        var startup = new CheckBox { IsChecked = controller.Settings.LaunchOnStartup, Style = (Style)FindResource("Switch"), IsEnabled = !controller.Diagnostic };
        AutomationProperties.SetName(startup, "Launch at Windows login"); var startupRow = new DockPanel(); DockPanel.SetDock(startup, Dock.Right); startupRow.Children.Add(startup); startupRow.Children.Add(Ui.IconLabel("startup", "Launch at Windows login")); body.Children.Add(Ui.Group(startupRow, new Thickness(14)));
        if (controller.Hotkeys.Warnings.Count > 0) body.Children.Add(Ui.Text(string.Join("\n", controller.Hotkeys.Warnings), 12, true));
        var footer = new StackPanel(); var status = Ui.Text("", 12, true); status.Visibility = Visibility.Collapsed; footer.Children.Add(status);
        var actions = new DockPanel { Margin = new Thickness(0, 8, 0, 0) }; var settings = Ui.ActionButton("Settings", "settings", "Configure hotkeys and shelf", controller.ShowSettings, "QuietButton"); DockPanel.SetDock(settings, Dock.Left); actions.Children.Add(settings);
        var finish = Ui.Button("Start snipping", "Finish setup and stay in tray", () =>
        {
            try
            {
                var destination = folder.Text.Trim(); if (destination.Length == 0) destination = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (!Path.IsPathFullyQualified(destination)) throw new InvalidDataException("Choose an absolute PNG export folder."); destination = Path.GetFullPath(destination);
                var cache = Path.TrimEndingDirectorySeparator(controller.Repository.Root);
                if (destination.Equals(cache, StringComparison.OrdinalIgnoreCase) || destination.StartsWith(cache + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Choose an export folder outside the temporary cache.");
                if (File.Exists(destination)) throw new InvalidDataException("Choose a folder, not a file.");
                var draft = System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(controller.Settings))!; draft.SaveDirectory = destination; draft.FirstRunComplete = true; draft.LaunchOnStartup = startup.IsChecked == true;
                controller.ApplySettings(draft); Close();
            }
            catch (InvalidDataException ex) { status.Visibility = Visibility.Visible; status.Text = ex.Message; }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or System.Security.SecurityException or AggregateException) { status.Visibility = Visibility.Visible; status.Text = "Setup could not be saved. " + OperationFailure.From(ex).Message; }
        }); finish.Style = (Style)FindResource("PrimaryButton"); DockPanel.SetDock(finish, Dock.Right); actions.Children.Add(finish);
        var privacy = Ui.Text("Local only. No accounts, uploads or analytics.", 12, true); privacy.Margin = new Thickness(12, 5, 12, 0); privacy.VerticalAlignment = VerticalAlignment.Center; actions.Children.Add(privacy); footer.Children.Add(actions);
        void ResponsiveFooter() { var compact = ActualWidth < 700 * Ui.TextScale; privacy.Visibility = compact ? Visibility.Collapsed : Visibility.Visible; settings.Content = compact ? Icons.Make("settings") : Ui.IconLabel("settings", "Settings"); }
        SizeChanged += (_, _) => ResponsiveFooter(); Ui.ThemeChanged += ResponsiveFooter; Closed += (_, _) => Ui.ThemeChanged -= ResponsiveFooter;
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer); root.Children.Add(Ui.Scroll(body));
    }
}
