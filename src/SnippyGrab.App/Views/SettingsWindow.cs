using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal sealed class SettingsWindow : Window
{
    private readonly AppController controller;
    private Settings draft;
    private readonly Dictionary<string, Func<object?>> values = [];
    private readonly Dictionary<string, (FrameworkElement Input, int Category, TextBlock Error)> fields = [];
    private readonly List<StackPanel> pages = [];
    private readonly List<Button> navigation = [];
    private readonly ContentControl pageHost = new();
    private readonly TextBlock status = Ui.Text("", 12, true);
    private readonly Grid layout = new();
    private readonly ComboBox compactNavigation = new() { DisplayMemberPath = "Title" };
    private readonly Border sidebar;
    private int category;
    private int buildingCategory;
    private StackPanel group = new();
    private readonly CancellationTokenSource readinessLifetime = new();
    internal static readonly (string Title, string Icon, string Description)[] Categories =
    [
        ("Appearance", "appearance", "Choose how SnippyGrab looks on your desktop."),
        ("Capture & shortcuts", "keyboard", "Choose how you capture and which keys you use."),
        ("Screenshot shelf", "image", "Keep captures close without getting in the way."),
        ("Clipboard", "clipboard", "Choose what is copied when you capture."),
        ("Files & history", "folder", "Control where captures live and how long they stay."),
        ("Editor & OCR", "edit", "Set annotation defaults and local text recognition."),
        ("Application", "settings", "Startup, shortcuts and information about SnippyGrab.")
    ];
    public SettingsWindow(AppController controller, bool welcome = false)
    {
        Ui.StyleWindow(this); this.controller = controller; draft = Clone(controller.Settings);
        Title = "SnippyGrab · Settings"; Width = 960; Height = 760; MinWidth = 520; MinHeight = 500; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = layout;
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        var nav = new StackPanel { Margin = new Thickness(12, 18, 12, 18) };
        for (var i = 0; i < Categories.Length; i++)
        {
            var index = i; var button = Ui.ActionButton(Categories[i].Title, Categories[i].Icon, Categories[i].Title, () => SelectCategory(index), "NavigationButton");
            navigation.Add(button); nav.Children.Add(button);
        }
        sidebar = new Border { Child = Ui.Scroll(nav), BorderThickness = new Thickness(0, 0, 1, 0) }; sidebar.SetResourceReference(Border.BorderBrushProperty, "Border"); layout.Children.Add(sidebar);
        var right = new DockPanel(); Grid.SetColumn(right, 1); layout.Children.Add(right);
        var footer = new StackPanel { Margin = new Thickness(20, 8, 20, 12) }; footer.Children.Add(status);
        var buttons = new DockPanel();
        var reset = Ui.Button("Reset defaults", "Reset the entire settings draft", () => { draft = new Settings { FirstRunComplete = true }; Populate(); SelectCategory(category); });
        DockPanel.SetDock(reset, Dock.Left); buttons.Children.Add(reset);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(Ui.Button("Cancel", "Close without applying", Close));
        var apply = Ui.Button(welcome ? "Start snipping" : "Apply", "Apply settings", Save); apply.Style = (Style)FindResource("PrimaryButton"); apply.MinWidth = 100; actions.Children.Add(apply);
        buttons.Children.Add(actions); footer.Children.Add(buttons);
        var footerFrame = new Border { Child = footer, BorderThickness = new Thickness(0, 1, 0, 0) }; footerFrame.SetResourceReference(Border.BorderBrushProperty, "Border"); DockPanel.SetDock(footerFrame, Dock.Bottom); right.Children.Add(footerFrame);
        compactNavigation.ItemsSource = Categories.Select(c => new CategoryChoice(c.Title)).ToArray(); compactNavigation.Margin = new Thickness(24, 14, 24, 0); compactNavigation.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(compactNavigation, "Settings category"); compactNavigation.SelectionChanged += (_, _) => { if (compactNavigation.SelectedIndex >= 0) SelectCategory(compactNavigation.SelectedIndex); };
        DockPanel.SetDock(compactNavigation, Dock.Top); right.Children.Add(compactNavigation);
        right.Children.Add(Ui.Scroll(pageHost));
        SizeChanged += (_, _) => Responsive(); Ui.ThemeChanged += Responsive; Closed += (_, _) => Ui.ThemeChanged -= Responsive;
        Populate(); SelectCategory(0);
        Closed += (_, _) => readinessLifetime.Cancel();
    }
    private sealed record CategoryChoice(string Title);
    private static Settings Clone(Settings settings) => JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!;
    private void Responsive()
    {
        var compact = ActualWidth < 820 || Ui.TextScale > 1.5;
        sidebar.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        layout.ColumnDefinitions[0].Width = new GridLength(compact ? 0 : 220);
        compactNavigation.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
    }
    internal void SelectCategory(int index)
    {
        category = Math.Clamp(index, 0, Categories.Length - 1);
        pageHost.Content = pages[category];
        for (var i = 0; i < navigation.Count; i++)
        {
            navigation[i].SetResourceReference(BackgroundProperty, i == category ? "Selected" : "Surface");
            navigation[i].SetResourceReference(BorderBrushProperty, i == category ? "Accent" : "Surface");
            navigation[i].SetResourceReference(ForegroundProperty, i == category ? "SelectedInk" : "Ink");
        }
        if (compactNavigation.SelectedIndex != category) compactNavigation.SelectedIndex = category;
        ((ScrollViewer)pageHost.Parent).ScrollToTop();
    }
    internal FrameworkElement InputFor(string name) => fields[name].Input;
    internal int ActiveCategory => category;
    internal IReadOnlyCollection<string> EditableSettings => values.Keys;
    private void Populate()
    {
        pageHost.Content = null; values.Clear(); fields.Clear(); pages.Clear(); status.Text = string.Join("\n", controller.Hotkeys.Warnings); status.Visibility = status.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        for (var i = 0; i < Categories.Length; i++)
        {
            buildingCategory = i;
            var page = new StackPanel { Margin = new Thickness(28, 22, 20, 12) }; pages.Add(page);
            page.Children.Add(Ui.Heading(Categories[i].Title));
            var description = Ui.Text(Categories[i].Description, 14, true); description.Margin = new Thickness(0, 4, 0, 20); page.Children.Add(description);
            switch (i)
            {
                case 0: Appearance(); break;
                case 1:
                    Group("Capture"); Add(nameof(Settings.DefaultCaptureMode), "Default capture mode"); Add(nameof(Settings.IncludeCursor), "Include cursor");
                    Group("Keyboard shortcuts"); Add(nameof(Settings.PrimaryHotkey), "Region"); Add(nameof(Settings.DesktopHotkey), "Entire desktop"); Add(nameof(Settings.WindowHotkey), "Window picker"); Add(nameof(Settings.ActiveWindowHotkey), "Active window"); Add(nameof(Settings.FallbackHotkey), "Fallback region");
                    Add(nameof(Settings.MonitorHotkey), "Monitor picker", "Disabled by default; choose a display on each capture.");
                    Note("Click a shortcut and press the keys. Tab moves between fields; Escape disables a shortcut.");
                    var info = new StackPanel(); info.Children.Add(Ui.Heading("Print Screen in Windows", 16)); info.Children.Add(Ui.Text("Turn off Windows screen capture if this shortcut is intercepted.", 12, true));
                    info.Children.Add(Ui.ActionButton("Open Keyboard settings", "keyboard", "Open Windows Print Screen settings", () => Process.Start(new ProcessStartInfo("ms-settings:easeofaccess-keyboard") { UseShellExecute = true }), "QuietButton")); page.Children.Add(Ui.Group(info)); break;
                case 2:
                    Group("Position & appearance"); AddMonitor(); Add(nameof(Settings.Corner), "Screen position"); Add(nameof(Settings.Orientation), "Corner orientation", "Edges expand inward.");
                    Add(nameof(Settings.ThumbnailSize), "Thumbnail width", "120–400 DIP"); Add(nameof(Settings.ExpandedItems), "Expanded captures", "1–5 captures"); Add(nameof(Settings.DockOpacity), "Opacity", "0.25–1"); Add(nameof(Settings.PreviewQuality), "Preview quality");
                    Note("Balanced uses less memory. Sharp improves detail. Original uses more memory. Copies, exports and OCR always use the lossless full-resolution image.");
                    Group("Behavior"); Add(nameof(Settings.AlwaysOnTop), "Always on top"); Add(nameof(Settings.AutoCollapse), "Collapse when pointer leaves"); Add(nameof(Settings.AutoHideSeconds), "Auto-hide", "Seconds · 0 keeps the shelf visible"); Add(nameof(Settings.DockLifetimeMinutes), "Shelf lifetime", "Minutes · 0 keeps captures indefinitely"); Add(nameof(Settings.Animate), "Fade new captures into the shelf", "Arrival only. Respects Windows reduced motion."); break;
                case 3:
                    Group("Copying"); Add(nameof(Settings.AutoCopy), "Copy automatically on capture", "Paste immediately with Ctrl+V."); Add(nameof(Settings.ClipboardPng), "Include PNG representation", "Keeps a lossless PNG alongside the Windows image format."); break;
                case 4:
                    Group("Storage"); Add(nameof(Settings.CachePath), "Dedicated cache folder", "Empty uses LocalAppData. Changes apply on next launch; existing captures stay in their original cache."); Add(nameof(Settings.SaveDirectory), "Export folder");
                    Group("Retention & history"); AddRetention(); Add(nameof(Settings.CleanupMinutes), "Cleanup interval", "Minutes"); Add(nameof(Settings.SessionOnly), "Clear session captures on exit", "Unpinned captures only."); Add(nameof(Settings.HistoryEnabled), "Remember unpinned capture history");
                    Note("Pins, active editors and transfers stay protected. File transfers have a 24-hour grace period, including when clearing.");
                    page.Children.Add(Ui.ActionButton("Clear temporary captures…", "trash", "Clear eligible unpinned captures", controller.ClearTemporary)); break;
                case 5:
                    Group("Annotations"); Add(nameof(Settings.AnnotationColor), "Default color", "#RRGGBB or #AARRGGBB"); Add(nameof(Settings.StrokeSize), "Stroke thickness", "1–30 px"); Add(nameof(Settings.TextSize), "Text size", "8–120 px");
                    Group("Text recognition"); Add(nameof(Settings.OcrEnhanceSmallText), "Enhance small text", "Improves smaller inputs without changing the saved image."); Add(nameof(Settings.OcrLayout), "Text layout"); Note("Recognition is local and English only. Auto retries scattered text when confidence is low. Use OCR selected area in the editor for best accuracy."); AddReadiness(); break;
                case 6:
                    Group("Startup"); Add(nameof(Settings.LaunchOnStartup), "Launch at Windows login"); Add(nameof(Settings.StartMinimized), "Start silently in tray", "After first-run setup.");
                    Group("About SnippyGrab"); Note("A native, local screenshot shelf. No uploads, accounts or analytics."); Note("SnippyGrab " + BuildVersion.Display + " · MIT"); Note("Quiet GitHub release checks every six hours. Tray → Help & about → Updates for changelogs and updating."); page.Children.Add(Ui.ActionButton("Hotkey help", "keyboard", "Review hotkeys and conflicts", controller.ShowHotkeyHelp)); break;
            }
        }
    }
    private void Group(string title)
    {
        group = new StackPanel(); group.Children.Add(Ui.Heading(title, 16));
        pages[buildingCategory].Children.Add(Ui.Group(group));
    }
    private void Note(string text) { var note = Ui.Text(text, 12, true); note.Margin = new Thickness(0, 10, 0, 6); group.Children.Add(note); }
    private void Row(string name, string label, FrameworkElement input, string? hint = null)
    {
        var row = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.95, GridUnitType.Star) });
        row.RowDefinitions.Add(new RowDefinition()); row.RowDefinitions.Add(new RowDefinition()); row.RowDefinitions.Add(new RowDefinition());
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) }; labels.Children.Add(Ui.Text(label)); if (hint is not null) labels.Children.Add(Ui.Text(hint, 12, true)); row.Children.Add(labels);
        input.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(input, 1); row.Children.Add(input); AutomationProperties.SetName(input, label); AutomationProperties.SetAutomationId(input, name);
        var error = Ui.Text("", 12); error.SetResourceReference(TextBlock.ForegroundProperty, "Danger"); error.Visibility = Visibility.Collapsed; Grid.SetRow(error, 2); Grid.SetColumnSpan(error, 2); row.Children.Add(error);
        row.SizeChanged += (_, _) => { var stacked = row.ActualWidth < 390 * Ui.TextScale; Grid.SetColumn(input, stacked ? 0 : 1); Grid.SetRow(input, stacked ? 1 : 0); Grid.SetColumnSpan(input, stacked ? 2 : 1); Grid.SetColumnSpan(labels, stacked ? 2 : 1); input.Margin = new Thickness(0, stacked ? 8 : 0, 0, 0); };
        fields[name] = (input, buildingCategory, error); group.Children.Add(row);
    }
    private sealed record Choice(object Value, string Label) { public override string ToString() => Label; }
    private void AddReadiness()
    {
        var result = Ui.Text("Checks run only on request. Native engine loading cannot be interrupted; closing ignores its result.", 12, true);
        AutomationProperties.SetName(result, "OCR readiness result");
        Button? check = null;
        check = Ui.ActionButton("Check OCR readiness", "ocr", "Check the local model and load the OCR engine", () => controller.Run(async () =>
        {
            check!.IsEnabled = false; result.Text = "Checking local OCR…";
            try { var readiness = await controller.OcrService.CheckReadinessAsync(readinessLifetime.Token); if (!readinessLifetime.IsCancellationRequested) result.Text = readiness.Message; }
            catch (OperationCanceledException) { }
            finally { if (!readinessLifetime.IsCancellationRequested) check.IsEnabled = true; }
        }));
        group.Children.Add(check); group.Children.Add(result);
        group.Children.Add(Ui.ActionButton("Microsoft runtime guidance", "info", "Open Microsoft Visual C++ runtime guidance in your browser", () => Process.Start(new ProcessStartInfo(OcrReadiness.RuntimeUrl) { UseShellExecute = true }), "QuietButton"));
    }
    private static string Label(object value) => value switch { CaptureMode.ActiveWindow => "Active window", PreviewQuality.Original => "Original (more memory)", OcrLayout.SparseText => "Scattered text", OcrLayout.SingleBlock => "Single paragraph", _ => System.Text.RegularExpressions.Regex.Replace(value.ToString()!, "([a-z])([A-Z])", "$1 $2") };
    private void Add(string name, string label, string? hint = null)
    {
        var property = typeof(Settings).GetProperty(name)!; var value = property.GetValue(draft); FrameworkElement input;
        if (property.PropertyType == typeof(bool))
        {
            var check = new CheckBox { IsChecked = (bool)value!, Style = (Style)FindResource("Switch"), HorizontalAlignment = HorizontalAlignment.Right, IsEnabled = !(controller.Diagnostic && name == nameof(Settings.LaunchOnStartup)) };
            input = check; values[name] = () => check.IsChecked == true;
        }
        else if (property.PropertyType.IsEnum)
        {
            var combo = new ComboBox { ItemsSource = Enum.GetValues(property.PropertyType).Cast<object>().Select(v => new Choice(v, Label(v))), SelectedValuePath = nameof(Choice.Value), SelectedValue = value };
            input = combo; values[name] = () => ((Choice)combo.SelectedItem).Value;
        }
        else if (property.PropertyType == typeof(Hotkey))
        {
            var key = (Hotkey)value!; var field = new TextBox { Text = key.ToString(), IsReadOnly = true, ToolTip = "Press a key combination. Escape disables it." }; input = field;
            field.PreviewKeyDown += (_, e) =>
            {
                var pressed = e.Key == Key.System ? e.SystemKey : e.Key;
                if (pressed == Key.Tab && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Shift)) return;
                if (pressed is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
                uint modifiers = 0; if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) modifiers |= 1; if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) modifiers |= 2; if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) modifiers |= 4; if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) modifiers |= 8;
                key = pressed == Key.Escape ? new(0, 0) : new((uint)KeyInterop.VirtualKeyFromKey(pressed), modifiers); field.Text = key.ToString(); e.Handled = true;
            }; values[name] = () => key;
        }
        else
        {
            var field = new TextBox { Text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" }; input = field;
            values[name] = () => property.PropertyType == typeof(string) ? field.Text.Trim() : Convert.ChangeType(field.Text, property.PropertyType, CultureInfo.InvariantCulture);
            if (name is nameof(Settings.CachePath) or nameof(Settings.SaveDirectory))
            {
                var container = new DockPanel(); var browse = Ui.Button("Browse…", "Choose " + label.ToLowerInvariant(), () => { var dialog = new OpenFolderDialog { Title = label, InitialDirectory = Directory.Exists(field.Text) ? field.Text : "" }; if (dialog.ShowDialog(this) == true) field.Text = dialog.FolderName; });
                DockPanel.SetDock(browse, Dock.Right); container.Children.Add(browse); container.Children.Add(field); input = container;
                if (controller.Diagnostic && name == nameof(Settings.CachePath)) container.IsEnabled = false;
            }
        }
        Row(name, label, input, hint);
    }
    private void AddRetention()
    {
        var choices = new List<Choice> { new(1, "1 hour"), new(24, "24 hours"), new(168, "7 days"), new(-1, "Never automatically delete") };
        if (!choices.Any(c => (int)c.Value == draft.RetentionHours)) choices.Add(new(draft.RetentionHours, $"Custom · {draft.RetentionHours} hours"));
        var combo = new ComboBox { ItemsSource = choices, SelectedValuePath = nameof(Choice.Value), SelectedValue = draft.RetentionHours };
        values[nameof(Settings.RetentionHours)] = () => (int)((Choice)combo.SelectedItem).Value; Row(nameof(Settings.RetentionHours), "Temporary capture retention", combo);
    }
    private sealed record MonitorChoice(int Index, string Identity, string Label) { public override string ToString() => Label; }
    private void AddMonitor()
    {
        var choices = new List<MonitorChoice> { new(-1, "", "Follow pointer") }; choices.AddRange(MonitorService.All().Select(m => new MonitorChoice(m.Index, m.Identity, $"Display {m.Index + 1} · {m.Bounds.Width} × {m.Bounds.Height}" + (m.Primary ? " · primary" : ""))));
        var selected = draft.DockMonitor < 0 ? choices[0] : choices.FirstOrDefault(c => draft.DockMonitorIdentity.Length > 0 ? c.Identity == draft.DockMonitorIdentity : c.Index == draft.DockMonitor);
        if (selected is null) { selected = new(draft.DockMonitor, draft.DockMonitorIdentity, "Saved display disconnected · primary until it returns"); choices.Add(selected); }
        var combo = new ComboBox { ItemsSource = choices, SelectedItem = selected }; values[nameof(Settings.DockMonitor)] = () => ((MonitorChoice)combo.SelectedItem).Index; values[nameof(Settings.DockMonitorIdentity)] = () => ((MonitorChoice)combo.SelectedItem).Identity;
        Row(nameof(Settings.DockMonitor), "Monitor", combo, "Saved by display identity."); fields[nameof(Settings.DockMonitorIdentity)] = fields[nameof(Settings.DockMonitor)];
    }
    private void Appearance()
    {
        Group("Theme"); Note("Changes the look of SnippyGrab. System follows your Windows app theme.");
        var choices = new UniformGrid { Columns = 3, Margin = new Thickness(0, 14, 0, 0) }; group.Children.Add(choices);
        var theme = draft.Theme; Action updatePreview = () => { }; var marks = new Dictionary<AppTheme, System.Windows.Shapes.Ellipse>();
        foreach (var value in new[] { AppTheme.System, AppTheme.Light, AppTheme.Dark })
        {
            var contents = new StackPanel(); contents.Children.Add(ThemeSample(value));
            var labelRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var radio = new Grid { Width = 18, Height = 18, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            var ring = new System.Windows.Shapes.Ellipse { StrokeThickness = 1.5 }; ring.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Muted"); radio.Children.Add(ring);
            var mark = new System.Windows.Shapes.Ellipse { Width = 10, Height = 10, Visibility = value == theme ? Visibility.Visible : Visibility.Collapsed }; mark.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Accent"); marks[value] = mark; radio.Children.Add(mark);
            labelRow.Children.Add(radio); labelRow.Children.Add(Ui.Text(value.ToString())); contents.Children.Add(labelRow);
            var button = Ui.Button(value.ToString(), "Use " + value + " theme", () => { theme = value; foreach (var (choice, mark) in marks) mark.Visibility = choice == theme ? Visibility.Visible : Visibility.Collapsed; foreach (Button b in choices.Children) { b.SetResourceReference(Button.BorderBrushProperty, Equals(b.Tag, theme) ? "Accent" : "Border"); AutomationProperties.SetItemStatus(b, Equals(b.Tag, theme) ? "Selected" : "Not selected"); } updatePreview(); });
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.Content = contents; button.Tag = value; button.Padding = new Thickness(10); button.SetResourceReference(Button.BorderBrushProperty, value == theme ? "Accent" : "Border"); AutomationProperties.SetItemStatus(button, value == theme ? "Selected" : "Not selected"); choices.Children.Add(button);
        }
        values[nameof(Settings.Theme)] = () => theme; fields[nameof(Settings.Theme)] = (choices, buildingCategory, Ui.Text(""));
        choices.SizeChanged += (_, _) => choices.Columns = choices.ActualWidth < 350 * Ui.TextScale ? 1 : 3;
        Group("Interface preview"); Note("A preview of the screenshot shelf using the selected theme.");
        var preview = new Grid(); preview.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(268) }); preview.ColumnDefinitions.Add(new ColumnDefinition());
        var sample = new StackPanel(); var code = Ui.Text("function captureScreen() {\n    const image = grab();\n    return image;\n}\n\nconsole.log('SnippyGrab');", 12); code.FontFamily = new FontFamily("Consolas"); code.Margin = new Thickness(10); sample.Children.Add(code);
        var strip = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var icon in new[] { "edit", "copy", "pin", "export", "trash", "more" }) { var b = Ui.IconButton(icon, icon + " preview", () => { }); b.Focusable = false; b.IsHitTestVisible = false; strip.Children.Add(b); }
        sample.Children.Add(strip);
        var previewFrame = Ui.Group(sample, new Thickness(0)); preview.Children.Add(previewFrame); var guidance = Ui.Text("System follows your Windows app theme.", 14, true); guidance.Margin = new Thickness(18, 18, 0, 0); Grid.SetColumn(guidance, 1); preview.Children.Add(guidance); group.Children.Add(preview);
        preview.SizeChanged += (_, _) => { var narrow = preview.ActualWidth < 400 * Ui.TextScale; guidance.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible; };
        updatePreview = () =>
        {
            var light = theme == AppTheme.Light || theme == AppTheme.System && Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0) is int value && value == 1;
            guidance.Text = theme == AppTheme.System ? "System follows your Windows app theme." : theme + " is used throughout SnippyGrab.";
            var keys = new[] { "Raised", "Ink", "Border", "Muted", "Hover" }; var palette = light ? new[] { "#FFFFFF", "#202124", "#D4D5D8", "#60646C", "#ECEDEF" } : new[] { "#252629", "#F1F2F4", "#44464D", "#B2B5BD", "#303238" };
            for (var i = 0; i < keys.Length; i++) previewFrame.Resources[keys[i]] = SystemParameters.HighContrast ? Ui.Brush(keys[i]) : new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette[i]));
        }; updatePreview();
        Group("Accessibility"); Note("Uses Windows text size, high contrast and reduced motion.");
    }
    private static Border ThemeSample(AppTheme theme)
    {
        var light = theme == AppTheme.Light || theme == AppTheme.System && Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0) is int value && value == 1; var bg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#F7F7F8" : "#1C1D20")); var fg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#D4D5D8" : "#44464D"));
        var view = new Grid { Height = 92 }; view.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.3, GridUnitType.Star) }); view.ColumnDefinitions.Add(new ColumnDefinition());
        var rail = new StackPanel { Margin = new Thickness(7, 18, 5, 5) }; for (var i = 0; i < 3; i++) rail.Children.Add(new Border { Height = 6, Background = fg, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 0, 9) }); view.Children.Add(rail);
        var page = new StackPanel { Margin = new Thickness(6, 18, 7, 6) }; page.Children.Add(new Border { Height = 5, Background = fg, Margin = new Thickness(0, 0, 12, 10) }); page.Children.Add(new Border { Height = 35, Background = fg, CornerRadius = new CornerRadius(3) }); Grid.SetColumn(page, 1); view.Children.Add(page);
        return new Border { Background = bg, Child = view, BorderBrush = fg, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5) };
    }
    internal Settings BuildDraft()
    {
        var candidate = Clone(draft);
        foreach (var field in fields.Values) { field.Error.Text = ""; field.Error.Visibility = Visibility.Collapsed; }
        foreach (var (name, read) in values)
        {
            try { typeof(Settings).GetProperty(name)!.SetValue(candidate, read()); }
            catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or TargetInvocationException)
            { FieldError(name, "Enter a valid value."); throw new InvalidDataException("Check the highlighted setting.", ex); }
        }
        try { if (ColorConverter.ConvertFromString(candidate.AnnotationColor) is not Color) throw new FormatException("A color is required."); }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { FieldError(nameof(Settings.AnnotationColor), "Use a color such as #EF675E."); throw new InvalidDataException("Check the annotation color.", ex); }
        if (candidate.CachePath.Length > 0 && (!Path.IsPathFullyQualified(candidate.CachePath) || candidate.CachePath.StartsWith(@"\\", StringComparison.Ordinal) || Path.GetFullPath(candidate.CachePath) == Path.GetPathRoot(candidate.CachePath)))
        { FieldError(nameof(Settings.CachePath), "Choose a dedicated, absolute local folder."); throw new InvalidDataException("Check the cache folder."); }
        foreach (var name in new[] { nameof(Settings.DockOpacity), nameof(Settings.StrokeSize), nameof(Settings.TextSize) })
            if (!double.IsFinite((double)typeof(Settings).GetProperty(name)!.GetValue(candidate)!))
            { FieldError(name, "Enter a finite number."); throw new InvalidDataException("Check the highlighted setting."); }
        if (candidate.SaveDirectory.Length > 0)
        {
            try
            {
                if (!Path.IsPathFullyQualified(candidate.SaveDirectory)) throw new ArgumentException("Choose an absolute export folder.");
                candidate.SaveDirectory = Path.GetFullPath(candidate.SaveDirectory);
                if (File.Exists(candidate.SaveDirectory)) throw new ArgumentException("Choose a folder, not a file.");
                foreach (var cache in new[] { controller.Repository.Root, candidate.CachePath }.Where(p => p.Length > 0))
                {
                    var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(cache));
                    if (candidate.SaveDirectory.Equals(root, StringComparison.OrdinalIgnoreCase) || candidate.SaveDirectory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("Choose an export folder outside the temporary cache.");
                }
            }
            catch (ArgumentException ex) { FieldError(nameof(Settings.SaveDirectory), ex.Message); throw new InvalidDataException("Check the export folder.", ex); }
        }
        candidate.FirstRunComplete = true; candidate.Validate(); return candidate;
    }
    private void FieldError(string name, string message)
    {
        var field = fields[name]; field.Error.Text = message; field.Error.Visibility = Visibility.Visible; SelectCategory(field.Category); field.Input.BringIntoView(); field.Input.Focus();
    }
    private void Save()
    {
        try
        {
            var candidate = BuildDraft(); if (candidate.CachePath.Length > 0) new CaptureRepository(candidate.CachePath).ProbeWritable();
            controller.ApplySettings(candidate); controller.FinishSetup(); Close();
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or InvalidDataException or ArgumentException or IOException or System.Security.SecurityException or TargetInvocationException or UnauthorizedAccessException or AggregateException)
        { status.Visibility = Visibility.Visible; status.Text = "Settings could not be applied. " + OperationFailure.From(ex).Message; }
    }
}
