namespace SnippyGrab.App.Views;

internal sealed class OperationWindow : Window
{
    private readonly TextBlock status = Ui.Text("Preparing…", 14);
    private readonly CancellationTokenSource cancel = new();
    private bool completed;
    internal CancellationToken Cancellation => cancel.Token;
    internal OperationWindow(string title, Window? owner = null)
    {
        Ui.StyleWindow(this); Title = "SnippyGrab · " + title; Width = 560; Height = 300; MinWidth = 420; MinHeight = 260;
        if (owner is not null) Owner = owner;
        var root = new StackPanel { Margin = new Thickness(24) }; Content = Ui.Scroll(root);
        root.Children.Add(Ui.Heading(title)); root.Children.Add(status);
        root.Children.Add(Ui.Button("Cancel / Close", "Cancel remaining work or close completed results", () => { if (completed) Close(); else { cancel.Cancel(); Report("Cancelling after current operation…"); } }));
        Closing += (_, e) => { if (!completed) { cancel.Cancel(); e.Cancel = true; Report("Cancelling after current operation…"); } };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { if (completed) Close(); else cancel.Cancel(); e.Handled = true; } };
    }
    internal void Report(string text) { if (!completed) status.Text = text; }
    internal void Complete(string text) { completed = true; status.Text = text; }
}
