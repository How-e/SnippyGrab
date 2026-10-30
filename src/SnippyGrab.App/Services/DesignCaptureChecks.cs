using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Wpf;

namespace SnippyGrab.App.Services;

// Production capture exercised against a local server; no owner browser profile or credentials.
internal static class DesignCaptureChecks
{
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    internal static async Task<object> Run(WebView2 source, WebView2 editor, string root, string report)
    {
        var fixture = Path.Combine(root, "import-regressions.html");
        await File.WriteAllTextAsync(fixture, Fixture);
        await Navigate(source, new Uri(fixture).AbsoluteUri);
        await source.CoreWebView2.ExecuteScriptAsync("scrollTo(0,200);document.querySelector('#scroll-panel').scrollTop=65");
        var full = await DesignBrowser.Capture(source, "full", CancellationToken.None);
        Require(full.Height >= 1800, "Full-page import cut off content below the viewport.");
        var inline = full.Layers.Single(n => n.Kind == "text" && n.Text == "1 / 5");
        Require(inline.Width > 50 && inline.NoWrap, "Inline text still uses a zero-width client box or wraps again.");
        Require(full.Layers.Any(n => n.Kind == "text" && n.Text == "Bottom of the loaded page"), "Bottom-page text was omitted.");
        Require(full.Layers.Any(n => n.Kind == "text" && n.Text == "Display contents child"), "display:contents children were dropped.");
        Require(full.Layers.Any(n => n.Kind == "text" && n.Text.Trim() == "Boxless direct text"), "Direct display:contents text was dropped.");
        Require(full.Layers.Any(n => n.Kind == "text" && n.Text == "Boxless child"), "Nested display:contents child was dropped.");
        Require(full.Layers.Any(n => n.Kind == "text" && n.Text == "Overflow child"), "Zero-size positioned ancestor lost its visible child.");
        Require(full.Layers.Any(n => n.Name == "Left border" && n.Fill == "#a0b0c0"), "Asymmetric border was lost.");
        Require(full.Layers.Any(n => n.Kind == "image" && n.Source.Selector == "#pseudo"), "Empty generated icon was omitted.");
        Require(full.Layers.Any(n => n.Kind == "image" && n.Source.Selector == "#outline-icon"), "Complex stroked SVG was imported with incomplete vector semantics.");
        Require(full.Layers.Any(n => n.Kind == "image" && n.Source.Selector == "#disclosure"), "Disclosure marker was lost.");
        Require(full.Layers.Any(n => n.Source.Selector == "#modern-color" && n.Fill != "#00000000"), "Modern CSS color became transparent.");
        Require(full.Layers.Any(n => n.Kind == "text" && n.Text == "STYLED TEXT" && n.FontStyle == "italic" && n.LetterSpacing == 2 && n.TextDecoration == "underline"), "Computed text styling was lost.");
        Require(full.Layers.Any(n => n.Kind == "text" && n.Text == "Nested scroller bottom"), "Full import did not expand a loaded nested scroll panel.");
        using (var restored = JsonDocument.Parse(await source.CoreWebView2.ExecuteScriptAsync("({y:scrollY,panelScroll:document.querySelector('#scroll-panel').scrollTop,style:document.querySelector('#scroll-panel').getAttribute('style')})")))
        {
            Require(restored.RootElement.GetProperty("y").GetInt32() == 200, "Full capture did not restore page scroll position.");
            Require(restored.RootElement.GetProperty("style").ValueKind == JsonValueKind.Null, "Full capture left source styles changed.");
            Require(restored.RootElement.GetProperty("panelScroll").GetInt32() == 65, "Full capture changed the nested panel's reading position.");
        }
        var reconstructed = await DesignHandoff.Render(editor.CoreWebView2, full);
        await File.WriteAllBytesAsync(report + ".full-original.png", full.Assets[full.Original].Bytes());
        await File.WriteAllBytesAsync(report + ".full-reconstructed.png", reconstructed);
        var fidelity = await ImageWorker.RunAsync(() => DesignComparison.Create(full, full, reconstructed));
        Require(fidelity.MeanChannelDifference < 8, "Reconstructed regression fixture differs materially from its reference.");
        var visible = await DesignBrowser.Capture(source, "visible", CancellationToken.None);
        Require(visible.Width == 640 && visible.Height == 480, "Visible capture changed viewport dimensions.");
        Require(!visible.Layers.Any(n => n.Kind == "text" && n.Text == "Bottom of the loaded page"), "Visible capture included offscreen layers.");
        await source.CoreWebView2.ExecuteScriptAsync("globalThis.snippyTarget=document.querySelector('#below-fold')");
        var element = await DesignBrowser.Capture(source, "element", CancellationToken.None);
        Require(element.Layers.Any(n => n.Kind == "text" && n.Text == "Bottom of the loaded page"), "Offscreen selected-element import failed.");
        await source.CoreWebView2.ExecuteScriptAsync("document.body.style.height='17000px'");
        try { await DesignBrowser.Capture(source, "full", CancellationToken.None); throw new InvalidDataException("Oversized full-page import was accepted."); }
        catch (InvalidOperationException) { }
        await source.CoreWebView2.ExecuteScriptAsync("document.body.style.height=''");
        var auth = await Authentication(root);
        object? extensionAdmission = null;
        var extensionInput = report + ".extension-input.json";
        if (File.Exists(extensionInput))
        {
            Require(new FileInfo(extensionInput).Length <= 64 * 1024 * 1024, "Extension fixture exceeds native admission.");
            using var payload = JsonDocument.Parse(await File.ReadAllBytesAsync(extensionInput));
            var screenshot = payload.RootElement.GetProperty("screenshot").GetString()!;
            Require(screenshot.StartsWith("data:image/png;base64,", StringComparison.Ordinal), "Expected extension PNG fixture.");
            var imported = await ImageWorker.RunAsync(() => DesignBrowser.FromCapture(payload.RootElement.GetProperty("capture").GetRawText(), Convert.FromBase64String(screenshot[22..])));
            Require(imported.Assets.Values.Any(a => a.MediaType.StartsWith("font/", StringComparison.Ordinal) && a.Weight == "100 900"), "Extension font metadata failed native admission.");
            var importedPng = await DesignHandoff.Render(editor.CoreWebView2, imported);
            await File.WriteAllBytesAsync(report + ".extension-reconstructed.png", importedPng);
            var comparison = await ImageWorker.RunAsync(() => DesignComparison.Create(imported, imported, importedPng));
            Require(comparison.MeanChannelDifference < 8, "Native reconstruction of extension payload differs materially from its reference.");
            extensionAdmission = new { Width = imported.Width, Height = imported.Height, FontAssets = imported.Assets.Values.Count(a => a.MediaType.StartsWith("font/", StringComparison.Ordinal)), FidelityMeanDifference = comparison.MeanChannelDifference };
        }
        return new { FullWidth = full.Width, FullHeight = full.Height, ImportedLayers = full.Layers.Count, FidelityMeanDifference = fidelity.MeanChannelDifference, FidelityChangedPixels = fidelity.ChangedPixels, Authentication = auth, ExtensionAdmission = extensionAdmission };
    }
    private static async Task<object> Authentication(string root)
    {
        using var server = new FixtureServer();
        var authView = new WebView2();
        var host = new Window { Content = authView, Width = 650, Height = 500, Left = -30000, Top = -30000, ShowActivated = false }; host.Show();
        var prompts = 0; var cancel = false; var observedOrigin = ""; var observedChallenge = "";
        try
        {
            await DesignBrowser.Initialize(authView, Path.Combine(root, "authentication-profile"), false, (uri, challenge) =>
            {
                observedOrigin = uri; observedChallenge = challenge;
                prompts++;
                return Task.FromResult<(string, string)?>(cancel ? null : ("fixture", "synthetic-password"));
            });
            try { await Navigate(authView, server.Url + "basic"); }
            catch (IOException ex) { throw new IOException($"Basic authentication failed after {prompts} prompts; origin={observedOrigin}; challenge={observedChallenge}", ex); }
            Require(observedOrigin.StartsWith(server.Url, StringComparison.Ordinal) && observedChallenge.Contains("Fixture", StringComparison.Ordinal), "Authentication prompt has the wrong origin/challenge.");
            var captured = await DesignBrowser.Capture(authView, "full", CancellationToken.None);
            Require(prompts > 0 && captured.Layers.Any(n => n.Text == "Authenticated page"), "HTTP Basic authentication did not load the protected page.");
            Require(!JsonSerializer.Serialize(captured, DesignDocument.Json).Contains("synthetic-password", StringComparison.Ordinal), "Authentication credentials entered design data.");
            await Navigate(authView, server.Url + "form");
            await authView.CoreWebView2.ExecuteScriptAsync("document.querySelector('button').click()");
            await Wait(async () => (await authView.CoreWebView2.ExecuteScriptAsync("document.body.innerText")).Contains("Cookie-authenticated page", StringComparison.Ordinal));
            await Navigate(authView, server.Url + "form");
            var cookie = await DesignBrowser.Capture(authView, "full", CancellationToken.None);
            Require(cookie.Layers.Any(n => n.Text == "Cookie-authenticated page"), "Source browser did not retain the signed-in cookie session.");
            cancel = true;
            authView.CoreWebView2.Navigate(server.Url + "cancel");
            await Wait(() => Task.FromResult(prompts >= 2));
            return new { Basic = true, FormLogin = true, SessionReuse = true, Cancellation = true, CredentialsExcluded = true };
        }
        finally { authView.Dispose(); host.Close(); }
    }
    internal static async Task<object> SourceWindowWorkflow(string root)
    {
        using var server = new FixtureServer();
        var workspace = new Views.DesignWindow(Path.Combine(root, "source-window-workflow"), (_, _, _, _) => Task.CompletedTask, _ => Task.FromResult(true), diagnostic: true)
        { ShowActivated = false, Left = -30000, Top = -30000, WindowStartupLocation = WindowStartupLocation.Manual };
        workspace.Show(); Views.DesignSourceWindow? source = null;
        try
        {
            await Wait(() => Task.FromResult(workspace.Ready));
            var command = workspace.Editor.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-command=import]').click()");
            await Wait(() =>
            {
                source = Application.Current.Windows.OfType<Views.DesignSourceWindow>().SingleOrDefault(w => w.Owner == workspace);
                return Task.FromResult(source is { Ready: true, Busy: false });
            });
            await command.WaitAsync(TimeSpan.FromSeconds(10));
            Require(source!.LoadButton.IsEnabled, "Load stayed disabled after source initialization.");
            Require(!source.ShowInTaskbar && source.Owner == workspace, "Source import lost modal ownership or created a separate taskbar entry.");
            source.Address.Text = "javascript:invalid"; source.LoadButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            await Wait(() => Task.FromResult(!source.Busy));
            Require(source.LoadButton.IsEnabled, "Invalid-address failure prevented retry.");
            source.Address.Text = new Uri(server.Url).Authority + "/public";
            source.LoadButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            await Wait(() => Task.FromResult(source.PageReady && !source.Busy));
            Require((await source.Browser.CoreWebView2.ExecuteScriptAsync("document.body.innerText")).Contains("Public source fixture", StringComparison.Ordinal), "Load did not navigate from a scheme-free local address.");
            Require(source.CaptureButton.IsEnabled, "Capture did not become available after Load.");
            source.CaptureButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            await Wait(() => Task.FromResult(workspace.Session is not null && !workspace.Busy));
            Require(workspace.Session!.Document.Layers.Any(n => n.Kind == "text" && n.Text == "Public source fixture"), "Source capture did not return editable layers to its owner workspace.");
            return new { Entry = "Production editor Import page message", SourceInitialized = true, InvalidAddressRetry = true, SchemeFreeLocalLoad = true, CaptureReturnedToWorkspace = true };
        }
        finally { if (source is { IsVisible: true }) source.Close(); workspace.CloseForDiagnostics(); }
    }
    private static async Task Navigate(WebView2 browser, string url)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Completed(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e) { if (e.IsSuccess) done.TrySetResult(); else if (e.WebErrorStatus != Microsoft.Web.WebView2.Core.CoreWebView2WebErrorStatus.ValidAuthenticationCredentialsRequired) done.TrySetException(new IOException("Fixture navigation failed: " + e.WebErrorStatus)); }
        browser.CoreWebView2.NavigationCompleted += Completed;
        try { browser.CoreWebView2.Navigate(url); await done.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
        finally { browser.CoreWebView2.NavigationCompleted -= Completed; }
    }
    private static async Task Wait(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!await condition()) { if (DateTime.UtcNow >= deadline) throw new TimeoutException("Fixture sign-in timed out."); await Task.Delay(25); }
    }
    private sealed class FixtureServer : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource lifetime = new();
        internal string Url { get; }
        internal FixtureServer() { listener.Start(); Url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/"; _ = Serve(); }
        private async Task Serve()
        {
            try
            {
                while (!lifetime.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(lifetime.Token);
                    _ = Respond(client, lifetime.Token);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or IOException) { }
        }
        private static async Task Respond(TcpClient client, CancellationToken cancellation)
        {
            try
            {
                using (client)
                {
                    using var stream = client.GetStream(); using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                    var request = await reader.ReadLineAsync(cancellation) ?? "";
                    var headers = new List<string>(); string? line;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellation))) headers.Add(line);
                    var path = (request.Split(' ').ElementAtOrDefault(1) ?? "/").Split('?')[0];
                    var authenticated = headers.Any(h => h.Equals("Authorization: Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("fixture:synthetic-password")), StringComparison.OrdinalIgnoreCase));
                    var cookie = headers.Any(h => h.Contains("fixture-session=yes", StringComparison.Ordinal));
                    var status = "200 OK"; var extra = ""; var body = "<!doctype html><p>Authenticated page</p>";
                    if (path is "/basic" or "/cancel")
                    {
                        if (!authenticated || path == "/cancel") { status = "401 Unauthorized"; extra = "WWW-Authenticate: Basic realm=\"Fixture" + (path == "/cancel" ? " cancel" : "") + "\"\r\n"; body = "Authentication required"; }
                    }
                    else if (path == "/signin") { extra = "Set-Cookie: fixture-session=yes; HttpOnly; SameSite=Lax; Path=/\r\n"; body = "<!doctype html><p>Cookie-authenticated page</p>"; }
                    else if (path == "/form") body = cookie ? "<!doctype html><p>Cookie-authenticated page</p>" : "<!doctype html><form action='/signin'><input name='user'><input type='password' name='password'><button>Sign in</button></form>";
                    else if (path == "/public") body = "<!doctype html><html><title>Local source fixture</title><p>Public source fixture</p></html>";
                    var bytes = Encoding.UTF8.GetBytes(body); var response = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: text/html; charset=utf-8\r\nConnection: close\r\n" + extra + "Content-Length: " + bytes.Length + "\r\n\r\n");
                    await stream.WriteAsync(response, cancellation); await stream.WriteAsync(bytes, cancellation);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or IOException) { }
        }
        public void Dispose() { lifetime.Cancel(); listener.Stop(); lifetime.Dispose(); }
    }
    private const string Fixture = """
        <!doctype html><html lang="en"><meta charset="utf-8"><style>
        html,body{margin:0;background:#101113;color:#eee;font-family:'Segoe UI',sans-serif}
        main{padding:24px;min-height:1800px}h1{margin:0;font-size:36px}.stat{font-size:28px;font-weight:700}
        .separator{border-left:3px solid #a0b0c0;padding:12px;margin:12px 0}
        #pseudo{height:30px;padding-left:26px;position:relative}#pseudo:before{content:"";position:absolute;left:0;top:3px;width:16px;height:16px;background:#a0b0c0}
        #outline-icon{width:24px;height:24px}#styled{font-style:italic;letter-spacing:2px;text-decoration:underline;text-transform:uppercase}
        #scroll-panel{height:60px;overflow:auto;background:#202124}#scroll-panel>div{height:180px}#below-fold{margin-top:800px;padding:20px;background:#303236}
        </style><main><h1>Overview</h1><div><span class="stat">1 / 5</span> capture slots <span class="stat">178.9 GB</span> free</div>
        <div class="separator">Asymmetric separator</div><div id="pseudo">Generated icon</div>
        <svg id="outline-icon" viewBox="0 0 24 24" fill="none" stroke="#eee" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M3 3 L21 3 L12 21 Z"/></svg>
        <p id="styled">Styled text</p><div style="display:contents"><p>Display contents child</p></div>
        <div style="display:contents">Boxless direct text <b>Boxless child</b></div>
        <details><summary id="disclosure">Capture log storage</summary><p>Hidden details</p></details><div id="modern-color" style="width:40px;height:20px;background:oklch(60% .15 230)"></div>
        <div style="width:0;height:0;position:relative"><div style="position:absolute;left:260px;top:-50px;width:200px;height:30px">Overflow child</div></div>
        <div id="scroll-panel"><div>Nested scroller top</div><p>Nested scroller bottom</p></div>
        <div id="below-fold">Bottom of the loaded page</div></main></html>
        """;
}
