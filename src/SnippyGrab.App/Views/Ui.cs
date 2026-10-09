using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Data;
using System.Windows.Interop;
using Microsoft.Win32;

namespace SnippyGrab.App.Views;

internal static class Ui
{
    private static readonly HashSet<double> fontSizes = [];
    public static Action<Exception>? FailureHandler { get; set; }
    public static event Action? ThemeChanged;
    internal static double TextScale { get; private set; } = 1;
    public static Brush Brush(string key)
    {
        if (Application.Current?.TryFindResource(key) is Brush brush) return brush;
        // Standalone overlay diagnostics have no Application resource owner.
        if (SystemParameters.HighContrast) return key is "Ink" or "Accent" ? SystemColors.WindowTextBrush : SystemColors.WindowBrush;
        var fallback = new SolidColorBrush((Color)ColorConverter.ConvertFromString(key switch { "Ink" => "#F1F2F4", "Accent" => "#90A8CE", "Raised" => "#252629", _ => "#1C1D20" }));
        fallback.Freeze(); return fallback;
    }
    public static Button Button(string label, string hint, Action action)
    {
        var button = new Button { Content = label, ToolTip = hint };
        AutomationProperties.SetName(button, hint);
        AutomationProperties.SetAutomationId(button, label);
        button.Click += (_, e) => { e.Handled = true; try { action(); } catch (Exception ex) { if (FailureHandler is null) throw; FailureHandler(ex); } };
        return button;
    }
    public static Button ActionButton(string label, string icon, string hint, Action action, string? style = null)
    {
        var button = Button(label, hint, action);
        button.Content = IconLabel(icon, label);
        if (style is not null) button.Style = (Style)Application.Current.FindResource(style);
        return button;
    }
    public static Button IconButton(string icon, string hint, Action action)
    {
        var button = Button("", hint, action); button.Content = Icons.Make(icon);
        button.Style = (Style)Application.Current.FindResource("QuietButton");
        button.Padding = new Thickness(7); button.ToolTip = hint;
        AutomationProperties.SetAutomationId(button, hint); return button;
    }
    public static StackPanel IconLabel(string icon, string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(Icons.Make(icon));
        var text = Text(label, 14); text.Margin = new Thickness(10, 0, 0, 0); text.VerticalAlignment = VerticalAlignment.Center;
        text.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1) });
        row.Children.Add(text); return row;
    }
    public static void StyleWindow(Window window)
    {
        window.Style = (Style)Application.Current.FindResource(typeof(Window));
        window.SourceInitialized += (_, _) => Chrome(window);
    }
    public static TextBlock Text(string text, double size = 14, bool muted = false)
    {
        fontSizes.Add(size);
        var key = "TextSize." + size.ToString(CultureInfo.InvariantCulture);
        Application.Current.Resources[key] = size * TextScale;
        var block = new TextBlock { Text = text, Margin = new Thickness(0, 3, 0, 3) };
        block.SetResourceReference(TextBlock.FontSizeProperty, key);
        block.SetResourceReference(TextBlock.ForegroundProperty, muted ? "Muted" : "Ink"); return block;
    }
    public static TextBlock Heading(string title, double size = 28)
    {
        var text = Text(title, size); text.FontWeight = FontWeights.SemiBold; return text;
    }
    public static Border Group(UIElement child, Thickness? padding = null)
    {
        var group = new Border { Child = child, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = padding ?? new Thickness(18), Margin = new Thickness(0, 0, 0, 16) };
        group.SetResourceReference(Border.BackgroundProperty, "Raised"); group.SetResourceReference(Border.BorderBrushProperty, "Border"); return group;
    }
    public static Border Rule() { var rule = new Border { Height = 1, Margin = new Thickness(0, 12, 0, 12) }; rule.SetResourceReference(Border.BackgroundProperty, "Border"); return rule; }
    public static MenuItem Menu(string label, string icon, Action action, string shortcut = "")
    {
        var item = new MenuItem { Header = label, Icon = Icons.Make(icon, 18), InputGestureText = shortcut };
        AutomationProperties.SetName(item, label); item.Click += (_, e) => { e.Handled = true; try { action(); } catch (Exception ex) { if (FailureHandler is null) throw; FailureHandler(ex); } }; return item;
    }
    public static ScrollViewer Scroll(UIElement content) => new() { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 8, 0) };
    public static void Theme(AppTheme theme, double? textScaleOverride = null, bool? highContrastOverride = null)
    {
        using (var accessibility = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Accessibility"))
            TextScale = Math.Clamp(Convert.ToDouble(accessibility?.GetValue("TextScaleFactor", 100)) / 100.0, 1, 2.25);
        if (textScaleOverride is { } requested) TextScale = Math.Clamp(requested, 1, 2.25);
        Application.Current.Resources["BodyTextSize"] = 14 * TextScale;
        foreach (var size in fontSizes) Application.Current.Resources["TextSize." + size.ToString(CultureInfo.InvariantCulture)] = size * TextScale;
        bool light = theme == AppTheme.Light;
        if (theme == AppTheme.System)
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            light = Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 0)) == 1;
        }
        var keys = new[] { "Surface", "Raised", "Hover", "Pressed", "Border", "Ink", "Muted", "DisabledInk", "Accent", "Selected", "Primary", "PrimaryInk", "Canvas", "Danger", "ScrollThumb" };
        var colors = light
            ? new[] { "#F7F7F8", "#FFFFFF", "#ECEDEF", "#E1E3E8", "#D4D5D8", "#202124", "#60646C", "#70747B", "#465F84", "#E7ECF3", "#465F84", "#FFFFFF", "#ECEDEF", "#A43C43", "#888D97" }
            : new[] { "#1C1D20", "#252629", "#303238", "#383B42", "#44464D", "#F1F2F4", "#B2B5BD", "#92969F", "#90A8CE", "#303B4D", "#465F84", "#FFFFFF", "#161719", "#B34B50", "#747880" };
        for (var i = 0; i < keys.Length; i++) Application.Current.Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
        if (highContrastOverride ?? SystemParameters.HighContrast)
        {
            foreach (var key in new[] { "Surface", "Canvas" }) Application.Current.Resources[key] = SystemColors.WindowBrush;
            foreach (var key in new[] { "Raised", "Hover", "Pressed" }) Application.Current.Resources[key] = SystemColors.ControlBrush;
            foreach (var key in new[] { "Ink", "Muted", "Border" }) Application.Current.Resources[key] = SystemColors.WindowTextBrush;
            Application.Current.Resources["DisabledInk"] = SystemColors.GrayTextBrush; Application.Current.Resources["ScrollThumb"] = SystemColors.GrayTextBrush;
            foreach (var key in new[] { "Accent", "Selected", "Primary", "Danger" }) Application.Current.Resources[key] = SystemColors.HighlightBrush;
            Application.Current.Resources["PrimaryInk"] = SystemColors.HighlightTextBrush;
        }
        Application.Current.Resources["SelectedInk"] = highContrastOverride ?? SystemParameters.HighContrast ? SystemColors.HighlightTextBrush : Brush("Ink");
        Application.Current.Resources["SelectedIcon"] = highContrastOverride ?? SystemParameters.HighContrast ? SystemColors.HighlightTextBrush : Brush("Accent");
        foreach (Window window in Application.Current.Windows) Chrome(window);
        ThemeChanged?.Invoke();
    }
    private static void Chrome(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle; if (hwnd == 0 || window.WindowStyle == WindowStyle.None) return;
        var c = ((SolidColorBrush)Brush("Surface")).Color;
        var dark = c.R < 128 && !SystemParameters.HighContrast ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
