using System.Text.Json;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal sealed class DesignSourceWindow : Window
{
    private readonly WebView2 browser = new();
    private readonly TextBox address = new() { Text = "http://localhost:5173", MinWidth = 220 };
    private readonly TextBox width = new() { Text = "1280", Width = 65 }, height = new() { Text = "800", Width = 65 };
    private readonly TextBox project = new() { Width = 170 }, revision = new() { Width = 140 };
    private readonly ComboBox mode = new() { ItemsSource = new[] { "Visible area", "Full loaded page", "Selected element" }, SelectedIndex = 0, Width = 170 };
    private readonly TextBlock status;
    private readonly CancellationTokenSource lifetime = new();
    private bool ready, pageReady, busy;
    internal DesignDocument? Result { get; private set; }
    internal DesignSourceWindow(string profile, DesignDocument? previous = null)
    {
        Ui.StyleWindow(this); Title = "SnippyGrab · Select source page"; Width = 1150; Height = 850; MinWidth = 700; MinHeight = 500;
        var root = new DockPanel(); Content = root; var top = new WrapPanel { Margin = new Thickness(12) };
        top.Children.Add(address); top.Children.Add(Ui.Button("Load", "Load source page", () => Run(Load))); top.Children.Add(Ui.Button("Open HTML…", "Open a local HTML source file", () =>
        {
            var dialog = new OpenFileDialog { Filter = "HTML files|*.html;*.htm", CheckFileExists = true }; if (dialog.ShowDialog(this) == true) { address.Text = new Uri(dialog.FileName).AbsoluteUri; Run(Load); }
        }));
        top.Children.Add(Ui.Text("Viewport", 12)); top.Children.Add(width); top.Children.Add(Ui.Text("×", 12)); top.Children.Add(height);
        top.Children.Add(Ui.Text("Project", 12)); top.Children.Add(project); top.Children.Add(Ui.Text("Revision", 12)); top.Children.Add(revision);
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        status = Ui.Text("Load a project page. Original page scripts run only in this isolated source browser.", 12, true);
        var bottom = new StackPanel { Margin = new Thickness(12) }; var actions = new WrapPanel(); actions.Children.Add(mode);
        actions.Children.Add(Ui.Button("Pick element", "Click an element in the source page to capture", () => Run(async () => { RequireReady(); await browser.CoreWebView2.ExecuteScriptAsync(await File.ReadAllTextAsync(Path.Combine(DesignBrowser.AssetsRoot, "capture.js"))); await browser.CoreWebView2.ExecuteScriptAsync("snippyPick()"); mode.SelectedIndex = 2; status.Text = "Move over the source page and click the element. Then choose Capture layers. Escape cancels the picker."; })));
        actions.Children.Add(Ui.Button("Capture layers", "Capture editable layers from this source", () => Run(Capture)));
        actions.Children.Add(Ui.Button("Cancel", "Cancel source import", Close)); bottom.Children.Add(actions); bottom.Children.Add(status); DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom); root.Children.Add(browser);
        foreach (var (control, label) in new (Control, string)[] { (address, "Source URL"), (width, "Viewport width"), (height, "Viewport height"), (project, "Project context"), (revision, "Source revision"), (mode, "Capture area") }) System.Windows.Automation.AutomationProperties.SetName(control, label);
        if (previous is not null) { address.Text = previous.Source.Url; width.Text = Math.Round(previous.Width).ToString(System.Globalization.CultureInfo.InvariantCulture); height.Text = Math.Round(previous.Height).ToString(System.Globalization.CultureInfo.InvariantCulture); project.Text = previous.Source.Project; revision.Text = previous.Source.Revision; }
        Loaded += (_, _) => Run(async () =>
        {
            await DesignBrowser.Initialize(browser, profile, false); ready = true;
            browser.CoreWebView2.NavigationStarting += (_, _) => pageReady = false;
            browser.CoreWebView2.NavigationCompleted += async (_, e) => { if (lifetime.IsCancellationRequested) return; if (!e.IsSuccess) { status.Text = "Page loading failed: " + e.WebErrorStatus + ". Check the address and retry."; return; } try { await Viewport(); pageReady = true; status.Text = "Page loaded. Check the visible state, then capture layers."; } catch (Exception ex) { status.Text = OperationFailure.From(ex).Message; } };
            await Load();
        });
        Closed += (_, _) => { lifetime.Cancel(); browser.Dispose(); lifetime.Dispose(); };
    }
    private void RequireReady() { if (!ready || lifetime.IsCancellationRequested) throw new InvalidOperationException("The source browser is not ready."); }
    private async Task Viewport()
    {
        if (!int.TryParse(width.Text, out var w) || !int.TryParse(height.Text, out var h) || w is < 100 or > 16384 || h is < 100 or > 16384 || (long)w * h > 64_000_000) throw new InvalidDataException("Enter a viewport of at least 100 pixels per axis, within 64 MP / 16384 per axis.");
        await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride", JsonSerializer.Serialize(new { width = w, height = h, deviceScaleFactor = 1, mobile = false }));
    }
    private Task Load()
    {
        RequireReady(); if (!Uri.TryCreate(address.Text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "file")) throw new InvalidDataException("Enter an HTTP(S) project URL or open an HTML file.");
        browser.CoreWebView2.Navigate(uri.AbsoluteUri); return Task.CompletedTask;
    }
    private async Task Capture()
    {
        RequireReady(); if (!pageReady) throw new InvalidOperationException("Wait for a source page to load successfully before capturing."); await Viewport(); status.Text = "Capturing source structure, resources and reference image…";
        var result = await DesignBrowser.Capture(browser, mode.SelectedIndex == 1 ? "full" : mode.SelectedIndex == 2 ? "element" : "visible", lifetime.Token);
        result.Source.Project = project.Text.Trim(); result.Source.Revision = revision.Text.Trim(); result.Validate();
        if (lifetime.IsCancellationRequested) return; Result = result; DialogResult = true;
    }
    private async void Run(Func<Task> action)
    {
        if (busy) return; busy = true;
        try { await action(); }
        catch (Exception ex) { if (!lifetime.IsCancellationRequested) status.Text = OperationFailure.From(ex).Message; }
        finally { busy = false; }
    }
}
