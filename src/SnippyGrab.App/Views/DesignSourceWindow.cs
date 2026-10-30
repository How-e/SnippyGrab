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
    private readonly ComboBox mode = new() { ItemsSource = new[] { "Visible area", "Full loaded page", "Selected element" }, SelectedIndex = 1, Width = 170 };
    private readonly TextBlock status;
    private readonly CancellationTokenSource lifetime = new();
    private bool ready, pageReady, busy;
    private ulong navigation;
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
        status = Ui.Text("Load a project page and sign in here if needed. This browser keeps its own sign-in session; use the extension for a tab already signed in.", 12, true);
        var bottom = new StackPanel { Margin = new Thickness(12) }; var actions = new WrapPanel(); actions.Children.Add(mode);
        actions.Children.Add(Ui.Button("Pick element", "Click an element in the source page to capture", () => Run(async () => { RequireReady(); await browser.CoreWebView2.ExecuteScriptAsync(await File.ReadAllTextAsync(Path.Combine(DesignBrowser.AssetsRoot, "capture.js"))); await browser.CoreWebView2.ExecuteScriptAsync("snippyPick()"); mode.SelectedIndex = 2; status.Text = "Move over the source page and click the element. Then choose Capture layers. Escape cancels the picker."; })));
        actions.Children.Add(Ui.Button("Capture layers", "Capture editable layers from this source", () => Run(Capture)));
        actions.Children.Add(Ui.Button("Cancel", "Cancel source import", Close)); bottom.Children.Add(actions); bottom.Children.Add(status); DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom); root.Children.Add(browser);
        foreach (var (control, label) in new (Control, string)[] { (address, "Source URL"), (width, "Viewport width"), (height, "Viewport height"), (project, "Project context"), (revision, "Source revision"), (mode, "Capture area") }) System.Windows.Automation.AutomationProperties.SetName(control, label);
        if (previous is not null) { address.Text = previous.Source.Url; width.Text = Math.Round(previous.Source.ViewportWidth > 0 ? previous.Source.ViewportWidth : previous.Width).ToString(System.Globalization.CultureInfo.InvariantCulture); height.Text = Math.Round(previous.Source.ViewportHeight > 0 ? previous.Source.ViewportHeight : previous.Height).ToString(System.Globalization.CultureInfo.InvariantCulture); project.Text = previous.Source.Project; revision.Text = previous.Source.Revision; mode.SelectedIndex = previous.Source.CaptureMode == "visible" ? 0 : previous.Source.CaptureMode == "element" ? 2 : 1; }
        Loaded += (_, _) => Run(async () =>
        {
            await DesignBrowser.Initialize(browser, profile, false, Authenticate); ready = true;
            browser.CoreWebView2.NavigationStarting += (_, e) => { navigation = e.NavigationId; pageReady = false; };
            browser.CoreWebView2.NavigationCompleted += async (_, e) =>
            {
                if (lifetime.IsCancellationRequested || e.NavigationId != navigation) return;
                if (!e.IsSuccess) { status.Text = e.WebErrorStatus == Microsoft.Web.WebView2.Core.CoreWebView2WebErrorStatus.ValidAuthenticationCredentialsRequired ? "This page requires sign-in. Enter credentials when prompted, or reload to try again." : "Page loading failed: " + e.WebErrorStatus + ". Check the address and retry."; return; }
                try { await Viewport(); if (lifetime.IsCancellationRequested || e.NavigationId != navigation) return; pageReady = true; status.Text = "Page loaded. Sign in if needed, then capture the full loaded page or pick an element."; }
                catch (Exception ex) { if (!lifetime.IsCancellationRequested) status.Text = OperationFailure.From(ex).Message; }
            };
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
    private async Task Load()
    {
        RequireReady(); var uri = DesignBrowser.ParseAddress(address.Text); address.Text = uri.AbsoluteUri;
        await Viewport(); pageReady = false; browser.CoreWebView2.Navigate(uri.AbsoluteUri);
    }
    private Task<(string UserName, string Password)?> Authenticate(string uri, string challenge)
    {
        if (lifetime.IsCancellationRequested) return Task.FromResult<(string, string)?>(null);
        var dialog = new Window { Title = "SnippyGrab · Sign in to source page", Width = 480, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }; Ui.StyleWindow(dialog);
        var panel = new StackPanel { Margin = new Thickness(22) }; dialog.Content = panel;
        panel.Children.Add(Ui.Heading("Sign in", 23));
        panel.Children.Add(Ui.Text(new Uri(uri).GetLeftPart(UriPartial.Authority), 14)); panel.Children.Add(Ui.Text(challenge, 12, true));
        var user = new TextBox(); var password = new PasswordBox();
        System.Windows.Automation.AutomationProperties.SetName(user, "Username"); System.Windows.Automation.AutomationProperties.SetName(password, "Password");
        panel.Children.Add(Ui.Text("Username")); panel.Children.Add(user); panel.Children.Add(Ui.Text("Password")); panel.Children.Add(password);
        panel.Children.Add(Ui.Text("Credentials are passed to this source browser and are not stored in the design or handoff.", 12, true));
        panel.Children.Add(Ui.Button("Sign in", "Sign in to this source page", () => dialog.DialogResult = true));
        panel.Children.Add(Ui.Button("Cancel", "Cancel sign in", () => dialog.DialogResult = false));
        dialog.Loaded += (_, _) => user.Focus();
        (string, string)? result = dialog.ShowDialog() == true ? (user.Text, password.Password) : null;
        password.Clear(); return Task.FromResult(result);
    }
    private async Task Capture()
    {
        RequireReady(); if (!pageReady) throw new InvalidOperationException("Wait for a source page to load successfully before capturing."); var capturedNavigation = navigation; await Viewport(); status.Text = "Capturing source structure, resources and reference image…";
        var result = await DesignBrowser.Capture(browser, mode.SelectedIndex == 1 ? "full" : mode.SelectedIndex == 2 ? "element" : "visible", lifetime.Token);
        if (capturedNavigation != navigation || !pageReady) throw new InvalidOperationException("The page navigated during capture. Wait for it to load and capture again.");
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
