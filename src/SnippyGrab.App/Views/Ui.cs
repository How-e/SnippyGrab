using System.Windows.Automation;
using Microsoft.Win32;

namespace SnippyGrab.App.Views;

internal static class Ui
{
    public static Button Button(string label, string hint, Action action)
    {
        var button = new Button { Content = label, ToolTip = hint };
        AutomationProperties.SetName(button, hint);
        button.Click += (_, e) => { e.Handled = true; action(); };
        return button;
    }
    public static void StyleWindow(Window window) => window.Style = (Style)Application.Current.FindResource(typeof(Window));
    public static TextBlock Text(string text, double size = 13, bool muted = false)
    {
        var block = new TextBlock { Text = text, FontSize = size, Margin = new Thickness(0, 3, 0, 3) };
        block.SetResourceReference(TextBlock.ForegroundProperty, muted ? "Muted" : "Ink"); return block;
    }
    public static void Theme(AppTheme theme)
    {
        bool light = theme == AppTheme.Light;
        if (theme == AppTheme.System)
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            light = Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 0)) == 1;
        }
        if (SystemParameters.HighContrast)
        {
            Application.Current.Resources["Surface"] = SystemColors.WindowBrush;
            Application.Current.Resources["Raised"] = SystemColors.ControlBrush;
            Application.Current.Resources["Ink"] = SystemColors.WindowTextBrush;
            Application.Current.Resources["Muted"] = SystemColors.WindowTextBrush;
            Application.Current.Resources["Accent"] = SystemColors.HighlightBrush;
            return;
        }
        var colors = light ? new[] { "#F6F7FA", "#E2E7EE", "#17212E", "#506075", "#2768B8" } : new[] { "#191D23", "#282E37", "#F2F4F8", "#9EA9B7", "#A6C9FF" };
        var keys = new[] { "Surface", "Raised", "Ink", "Muted", "Accent" };
        for (var i = 0; i < keys.Length; i++) Application.Current.Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
    }
}
