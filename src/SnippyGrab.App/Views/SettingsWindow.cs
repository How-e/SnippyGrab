using System.Reflection;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Interop;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal sealed class SettingsWindow : Window
{
    private readonly AppController controller;
    private Settings draft;
    private readonly Dictionary<string, Func<object?>> values = [];
    private readonly StackPanel body = new();
    private readonly TextBlock status;
    public SettingsWindow(AppController controller, bool welcome = false)
    {
        Ui.StyleWindow(this);
        this.controller = controller;
        draft = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(controller.Settings))!;
        Title = welcome ? "Welcome to SnippyGrab" : "SnippyGrab · Settings"; Width = 610; Height = 730; MinWidth = 480; MinHeight = 480; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new DockPanel { Margin = new Thickness(22) }; Content = root;
        var header = new StackPanel();
        header.Children.Add(Ui.Text(welcome ? "Capture. Paste. Keep moving." : "Settings", 24));
        header.Children.Add(Ui.Text(welcome ? "Print Screen → drag a region → release. Paste immediately with Ctrl+V, drag from the shelf to attach, or click to annotate. Everything stays local. Choose startup below." : "A quiet shelf, configured for your workflow.", 13, true));
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var footer = new StackPanel();
        status = Ui.Text(string.Join("\n", controller.Hotkeys.Warnings), 12, true); status.TextWrapping = TextWrapping.Wrap; footer.Children.Add(status);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Ui.Button("Reset", "Reset settings to defaults", () => { draft = new Settings { FirstRunComplete = true }; Populate(); }));
        buttons.Children.Add(Ui.Button("Cancel", "Close without applying", Close));
        buttons.Children.Add(Ui.Button(welcome ? "Start snipping" : "Apply", "Apply settings", Save));
        footer.Children.Add(buttons); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Populate();
    }
    private void Populate()
    {
        values.Clear(); body.Children.Clear();
        Section("Capture");
        Add(nameof(Settings.PrimaryHotkey), "Primary hotkey"); Add(nameof(Settings.DefaultCaptureMode), "Primary capture mode");
        Add(nameof(Settings.DesktopHotkey), "Entire desktop hotkey"); Add(nameof(Settings.WindowHotkey), "Window picker hotkey"); Add(nameof(Settings.ActiveWindowHotkey), "Active window hotkey"); Add(nameof(Settings.FallbackHotkey), "Fallback region hotkey");
        body.Children.Add(Ui.Text("Click a hotkey field, then press the desired combination. Escape disables it. Windows may intercept Print Screen: Settings → Accessibility → Keyboard → ‘Use the Print Screen key to open screen capture’. Disable it and restart if necessary.", 12, true));
        Add(nameof(Settings.IncludeCursor), "Include cursor"); Add(nameof(Settings.Animate), "Fade new captures into the shelf");
        body.Children.Add(Ui.Text("Animation affects arrival only, respects Windows reduced motion/high contrast, and does not animate selection or shelf expansion.", 12, true));
        Section("Screenshot shelf");
        AddMonitor(); Add(nameof(Settings.Corner), "Screen position"); Add(nameof(Settings.Orientation), "Corner orientation (edges expand inward)");
        Add(nameof(Settings.ThumbnailSize), "Thumbnail width (120–400 DIP)"); Add(nameof(Settings.ExpandedItems), "Maximum expanded items (1–5)"); Add(nameof(Settings.DockOpacity), "Opacity (0.25–1)");
        Add(nameof(Settings.PreviewQuality), "Preview quality");
        body.Children.Add(Ui.Text("Balanced uses less memory; Sharp improves detail; Original decodes every pixel and uses more memory. Small previews still shrink text to fit. Open the editor to inspect at 100%. Captures, copies, exports and OCR always use the full-resolution lossless PNG.", 12, true));
        Add(nameof(Settings.AlwaysOnTop), "Always on top"); Add(nameof(Settings.AutoCollapse), "Collapse when pointer leaves"); Add(nameof(Settings.AutoHideSeconds), "Auto-hide seconds (0 = off)"); Add(nameof(Settings.DockLifetimeMinutes), "Shelf lifetime minutes (0 = indefinitely)");
        Section("Clipboard"); Add(nameof(Settings.AutoCopy), "Copy automatically on capture"); Add(nameof(Settings.ClipboardPng), "Include PNG representation with image");
        Section("Files and history"); Add(nameof(Settings.CachePath), "Dedicated cache path (empty = LocalAppData)");
        body.Children.Add(Ui.Text("Cache path changes apply on next launch. Use a dedicated local directory. Existing captures stay in their original cache.", 12, true));
        AddRetention(); Add(nameof(Settings.CleanupMinutes), "Cleanup interval minutes"); Add(nameof(Settings.SessionOnly), "Clear unpinned session captures on exit"); Add(nameof(Settings.SaveDirectory), "Default export directory"); Add(nameof(Settings.HistoryEnabled), "Remember unpinned capture history");
        body.Children.Add(Ui.Text("Pins are retained. Active editors and transfers are protected; file transfers have a 24-hour grace period, even when clearing.", 12, true));
        body.Children.Add(Ui.Button("Clear temporary captures", "Clear unpinned captures, excluding transfers and editors", controller.ClearTemporary));
        Section("Editor"); Add(nameof(Settings.AnnotationColor), "Default color (#RRGGBB)"); Add(nameof(Settings.StrokeSize), "Stroke thickness"); Add(nameof(Settings.TextSize), "Text size");
        Section("Text recognition"); Add(nameof(Settings.OcrEnhanceSmallText), "Enhance small text for OCR"); Add(nameof(Settings.OcrLayout), "OCR text layout");
        body.Children.Add(Ui.Text("Auto keeps paragraph layout and retries scattered text when confidence is low. SparseText suits dialogs or mixed screenshots; SingleBlock suits one paragraph. Enhancement enlarges smaller inputs without changing the saved image. For best accuracy, use OCR selected area in the editor around the text. Recognition is local and English only.", 12, true));
        Section("Application"); Add(nameof(Settings.LaunchOnStartup), "Launch at Windows login"); Add(nameof(Settings.StartMinimized), "Start silently in tray after setup"); Add(nameof(Settings.Theme), "Theme");
        body.Children.Add(Ui.Text("Updates are manual through release downloads. No automatic network requests, accounts or analytics. SnippyGrab " + BuildVersion.Display + " · MIT", 12, true));
        foreach (var text in body.Children.OfType<TextBlock>()) text.TextWrapping = TextWrapping.Wrap;
    }
    private void Section(string title) => body.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 20, 0, 8) });
    private sealed record RetentionChoice(int Hours, string Label) { public override string ToString() => Label; }
    private void AddRetention()
    {
        body.Children.Add(Ui.Text("Temporary capture retention"));
        var choices = new List<RetentionChoice> { new(1, "1 hour"), new(24, "24 hours"), new(168, "7 days"), new(-1, "Never automatically delete") };
        if (!choices.Any(c => c.Hours == draft.RetentionHours)) choices.Add(new(draft.RetentionHours, $"Custom · {draft.RetentionHours} hours"));
        var combo = new ComboBox { ItemsSource = choices, SelectedItem = choices.Single(c => c.Hours == draft.RetentionHours) };
        AutomationProperties.SetName(combo, "Temporary capture retention"); body.Children.Add(combo);
        values[nameof(Settings.RetentionHours)] = () => ((RetentionChoice)combo.SelectedItem).Hours;
    }
    private sealed record MonitorChoice(int Index, string Identity, string Label) { public override string ToString() => Label; }
    private void AddMonitor()
    {
        body.Children.Add(Ui.Text("Monitor (saved by device identity)"));
        var choices = new List<MonitorChoice> { new(-1, "", "Follow pointer") };
        choices.AddRange(MonitorService.All().Select(m => new MonitorChoice(m.Index, m.Identity, $"Display {m.Index + 1} · {m.Bounds.Width} × {m.Bounds.Height}" + (m.Primary ? " · primary" : ""))));
        var selected = draft.DockMonitor < 0 ? choices[0] : choices.FirstOrDefault(c => draft.DockMonitorIdentity.Length > 0 ? c.Identity == draft.DockMonitorIdentity : c.Index == draft.DockMonitor);
        if (selected is null)
        {
            selected = new(draft.DockMonitor, draft.DockMonitorIdentity, "Saved display disconnected · use primary until it returns");
            choices.Add(selected);
        }
        var combo = new ComboBox { ItemsSource = choices, SelectedItem = selected };
        AutomationProperties.SetName(combo, "Screenshot shelf monitor"); body.Children.Add(combo);
        values[nameof(Settings.DockMonitor)] = () => ((MonitorChoice)combo.SelectedItem).Index;
        values[nameof(Settings.DockMonitorIdentity)] = () => ((MonitorChoice)combo.SelectedItem).Identity;
    }
    private void Add(string name, string label)
    {
        var property = typeof(Settings).GetProperty(name)!; var value = property.GetValue(draft);
        if (property.PropertyType == typeof(bool))
        {
            var check = new CheckBox { Content = label, IsChecked = (bool)value!, IsEnabled = !(controller.Diagnostic && name == nameof(Settings.LaunchOnStartup)) }; body.Children.Add(check); values[name] = () => check.IsChecked == true; return;
        }
        body.Children.Add(Ui.Text(label));
        FrameworkElement input;
        if (property.PropertyType.IsEnum)
        {
            var combo = new ComboBox { ItemsSource = Enum.GetValues(property.PropertyType), SelectedItem = value }; input = combo; values[name] = () => combo.SelectedItem;
        }
        else if (property.PropertyType == typeof(Hotkey))
        {
            var key = (Hotkey)value!;
            var field = new TextBox { Text = key.ToString(), IsReadOnly = true, ToolTip = "Press your hotkey combination" }; input = field;
            field.PreviewKeyDown += (_, e) =>
            {
                var pressed = e.Key == Key.System ? e.SystemKey : e.Key;
                if (pressed is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
                uint modifiers = 0; if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) modifiers |= 1; if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) modifiers |= 2; if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) modifiers |= 4; if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) modifiers |= 8;
                key = pressed == Key.Escape ? new(0, 0) : new((uint)KeyInterop.VirtualKeyFromKey(pressed), modifiers); field.Text = key.ToString(); e.Handled = true;
            };
            values[name] = () => key;
        }
        else
        {
            var field = new TextBox { Text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "" }; input = field;
            values[name] = () => property.PropertyType == typeof(string) ? field.Text : Convert.ChangeType(field.Text, property.PropertyType, System.Globalization.CultureInfo.InvariantCulture);
        }
        AutomationProperties.SetName(input, label); body.Children.Add(input);
        if (controller.Diagnostic && name == nameof(Settings.CachePath)) input.IsEnabled = false;
    }
    private void Save()
    {
        try
        {
            foreach (var (name, read) in values) typeof(Settings).GetProperty(name)!.SetValue(draft, read());
            draft.FirstRunComplete = true; draft.Validate();
            _ = (Color)ColorConverter.ConvertFromString(draft.AnnotationColor);
            if (draft.CachePath.Length > 0)
            {
                if (!Path.IsPathFullyQualified(draft.CachePath) || draft.CachePath.StartsWith(@"\\", StringComparison.Ordinal)) throw new InvalidDataException("Cache path must be absolute and local.");
                if (Path.GetFullPath(draft.CachePath) == Path.GetPathRoot(draft.CachePath)) throw new InvalidDataException("Choose a dedicated cache directory.");
            }
            if (draft.CachePath.Length > 0) new CaptureRepository(draft.CachePath).ProbeWritable();
            controller.ApplySettings(draft); controller.FinishSetup(); Close();
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or ArgumentException or IOException or System.Security.SecurityException or TargetInvocationException or UnauthorizedAccessException or AggregateException)
        { status.Text = "Settings could not be applied. " + OperationFailure.From(ex).Message; }
    }
}
