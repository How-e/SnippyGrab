using System.Windows.Automation;

namespace SnippyGrab.App.Views;

internal sealed class DialogWindow : Window
{
    internal DialogWindow(string title, string message, string action = "Close", bool confirmation = false, bool danger = false, AppController? controller = null, Window? owner = null)
    {
        Ui.StyleWindow(this); Title = "SnippyGrab · " + title; Width = 550; Height = confirmation ? 330 : 440; MinWidth = 420; MinHeight = 280;
        WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner; if (owner is not null) Owner = owner;
        var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
        var heading = Ui.IconLabel(danger ? "trash" : "info", title); heading.Children.RemoveAt(1); var titleText = Ui.Heading(title, 20); titleText.Margin = new Thickness(10, 0, 0, 0); titleText.VerticalAlignment = VerticalAlignment.Center; heading.Children.Add(titleText); heading.Margin = new Thickness(0, 0, 0, 18); DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        if (!confirmation && controller is not null) actions.Children.Add(Ui.ActionButton("Copy details", "copy", "Copy these operation details", () => controller.Run(() => controller.CopyText(message))));
        if (confirmation) { var cancel = Ui.Button("Cancel", "Cancel without changing anything", () => DialogResult = false); cancel.IsCancel = true; actions.Children.Add(cancel); }
        var accept = Ui.Button(action, action, () => { if (confirmation) DialogResult = true; else Close(); }); accept.Style = (Style)FindResource(danger ? "DangerButton" : "PrimaryButton"); accept.IsDefault = true; actions.Children.Add(accept);
        DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
        var text = new TextBox { Text = message, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0), Background = Brushes.Transparent, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top };
        AutomationProperties.SetName(text, title + " details"); root.Children.Add(text);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { if (confirmation) DialogResult = false; else Close(); e.Handled = true; } };
    }
    internal static bool Confirm(string title, string message, string action, Window? owner = null, bool danger = false) => new DialogWindow(title, message, action, true, danger, owner: owner).ShowDialog() == true;
    internal static void Information(string title, string message, AppController controller, Window? owner = null) => new DialogWindow(title, message, controller: controller, owner: owner).ShowDialog();
}
