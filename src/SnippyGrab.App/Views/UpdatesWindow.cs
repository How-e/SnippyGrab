using System.Diagnostics;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal sealed class UpdatesWindow : Window
{
    private readonly UpdateService updates;
    private readonly AppController controller;
    private readonly TextBlock status = Ui.Text("", 14);
    private readonly TextBox notes = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly Button check;
    private readonly Button install;
    private readonly Button page;
    private readonly CancellationTokenSource lifetime = new();
    private bool downloading;
    internal UpdatesWindow(AppController controller, UpdateService updates)
    {
        this.controller = controller; this.updates = updates;
        Ui.StyleWindow(this); Title = "SnippyGrab · Updates"; Width = 680; Height = 570; MinWidth = 500; MinHeight = 380; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        header.Children.Add(Ui.Heading("Updates", 24)); header.Children.Add(Ui.Text("Installed: " + BuildVersion.Display, 13));
        status.TextWrapping = TextWrapping.Wrap; status.Margin = new Thickness(0, 12, 0, 8); header.Children.Add(status);
        header.Children.Add(Ui.Text("Quiet checks every 6 hours. Updates install only when you choose.\nPrerelease builds follow prereleases; stable builds follow stable releases.", 13));
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var actions = new WrapPanel { Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        check = Ui.Button("Check now", "Check GitHub for new releases", () => _ = updates.CheckAsync());
        page = Ui.Button("Release page", "Read the release notes on GitHub", () => { if (updates.Release is { } release) Process.Start(new ProcessStartInfo(release.Page.AbsoluteUri) { UseShellExecute = true }); });
        install = Ui.Button("Update & restart", "Download, verify, update and restart SnippyGrab", () => _ = InstallAsync()); install.Style = (Style)FindResource("PrimaryButton");
        actions.Children.Add(check); actions.Children.Add(page); actions.Children.Add(install); actions.Children.Add(Ui.Button("Close", "Close updates", Close));
        DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions); root.Children.Add(notes);
        System.Windows.Automation.AutomationProperties.SetName(notes, "Release changelog");
        updates.Changed += Refresh;
        Closed += (_, _) => { updates.Changed -= Refresh; lifetime.Cancel(); };
        Refresh();
    }
    private void Refresh()
    {
        if (downloading) return;
        status.Text = updates.Detail + (updates.CheckedAt is { } time ? "\nLast successful check: " + time.ToString("g") : "") + (!UpdateService.CanInstall ? "\nInstall updates from a packaged release. Source builds cannot update in place." : "");
        notes.Text = updates.Release is { } release ? release.Version + "\n\n" + release.Notes : "Release notes will appear after a successful check.";
        check.IsEnabled = updates.State != UpdateState.Checking;
        page.IsEnabled = updates.Release is not null;
        install.IsEnabled = UpdateService.CanInstall && updates.State == UpdateState.Available;
    }
    private async Task InstallAsync()
    {
        if (downloading || updates.State != UpdateState.Available || updates.Release is not { } release) return;
        if (!DialogWindow.Confirm("Update SnippyGrab", $"Install {release.Version} and restart?\n\nOpen editors will ask to save changes. Captures and settings are retained. Previous application files are kept beside the app for rollback.\n\nThis release is unsigned. Download checksums verify integrity, not publisher identity.", "Update & restart", this)) return;
        downloading = true; check.IsEnabled = install.IsEnabled = false;
        string? root = null;
        try
        {
            root = await updates.PrepareAsync(release, new Progress<string>(text => status.Text = text), lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            await controller.ExitForUpdateAsync(() => UpdateService.Launch(root));
            if (!controller.Exiting) status.Text = "Update cancelled because an editor stayed open. You can retry.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { status.Text = "Update failed. " + ex.Message; }
        finally
        {
            downloading = false;
            if (!controller.Exiting && root is not null)
            {
                try { Directory.Delete(root, true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status.Text += "\nTemporary download could not be removed: " + root; }
            }
            check.IsEnabled = true; install.IsEnabled = UpdateService.CanInstall && updates.State == UpdateState.Available;
        }
    }
}
