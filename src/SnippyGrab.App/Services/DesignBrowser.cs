using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace SnippyGrab.App.Services;

internal static class DesignBrowser
{
    internal const string EditorUrl = "https://snippy-editor.invalid/editor.html";
    internal static string AssetsRoot => Path.Combine(AppContext.BaseDirectory, "Design");
    internal static Uri ParseAddress(string address)
    {
        var value = address.Trim();
        if (!value.Contains("://", StringComparison.Ordinal) && Regex.IsMatch(value, @"^(?:localhost|[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?|\[[a-f0-9:]+\])(?::\d{1,5})?(?:[/?#].*)?$", RegexOptions.IgnoreCase)) value = "http://" + value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "file") || (uri.Scheme != "file" && uri.Host.Length == 0)) throw new InvalidDataException("Enter a project address such as 10.0.1.11:8085, an HTTP(S) URL, or open an HTML file.");
        return uri;
    }
    internal static async Task Initialize(WebView2 view, string profile, bool editor, Func<string, string, Task<(string UserName, string Password)?>>? authenticate = null)
    {
        try { _ = CoreWebView2Environment.GetAvailableBrowserVersionString(); }
        catch (WebView2RuntimeNotFoundException ex) { throw new InvalidOperationException("UI Design needs the Microsoft Edge WebView2 Evergreen Runtime. Install it from Microsoft's WebView2 download page; ordinary screenshot capture remains available.", ex); }
        ManagedPath.RejectRedirects(profile); Directory.CreateDirectory(profile);
        var environment = await CoreWebView2Environment.CreateAsync(null, profile);
        await view.EnsureCoreWebView2Async(environment);
        var core = view.CoreWebView2; core.Settings.AreHostObjectsAllowed = false; core.Settings.IsWebMessageEnabled = editor; core.Settings.AreDefaultScriptDialogsEnabled = false; core.Settings.IsStatusBarEnabled = false; core.Settings.AreDevToolsEnabled = false;
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.DownloadStarting += (_, e) => { e.Cancel = true; e.Handled = true; };
        if (editor)
        {
            core.SetVirtualHostNameToFolderMapping("snippy-editor.invalid", AssetsRoot, CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting += (_, e) => { if (e.Uri != EditorUrl) e.Cancel = true; };
            core.FrameNavigationStarting += (_, e) => e.Cancel = true;
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, e) =>
            {
                if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri) || uri.Host != "snippy-editor.invalid" || uri.Scheme != "https") e.Response = environment.CreateWebResourceResponse(new MemoryStream(), 403, "Blocked", "Content-Type: text/plain");
            };
        }
        else
        {
            core.NavigationStarting += (_, e) => { if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "file" or "about")) e.Cancel = true; };
            core.BasicAuthenticationRequested += async (_, e) =>
            {
                using var deferral = e.GetDeferral();
                try
                {
                    // Leave the WebView callback before opening a modal credential dialog.
                    await view.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                    var credentials = authenticate is null ? null : await authenticate(e.Uri, e.Challenge);
                    if (credentials is { } value) { e.Response.UserName = value.UserName; e.Response.Password = value.Password; }
                    else e.Cancel = true;
                }
                catch { e.Cancel = true; }
            };
        }
    }
    internal static async Task<DesignDocument> Capture(WebView2 browser, string mode, CancellationToken cancellation)
    {
        var core = browser.CoreWebView2 ?? throw new InvalidOperationException("Load a source page first.");
        var script = await File.ReadAllTextAsync(Path.Combine(AssetsRoot, "capture.js"), cancellation);
        await core.ExecuteScriptAsync(script);
        try
        {
            var expression = "(async()=>{globalThis.snippyPickCleanup?.();return await snippyExtract(" + JsonSerializer.Serialize(new { mode }) + ")})()";
            var response = await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate", JsonSerializer.Serialize(new { expression, awaitPromise = true, returnByValue = true }));
            using var json = JsonDocument.Parse(response);
            if (json.RootElement.TryGetProperty("exceptionDetails", out _)) throw new InvalidOperationException("Page extraction failed. Choose a smaller region or wait for the page to finish loading.");
            var data = json.RootElement.GetProperty("result").GetProperty("value");
            var region = data.GetProperty("region");
            var screenshot = await core.CallDevToolsProtocolMethodAsync("Page.captureScreenshot", JsonSerializer.Serialize(new { format = "png", captureBeyondViewport = true, clip = new { x = region.GetProperty("x").GetDouble(), y = region.GetProperty("y").GetDouble(), width = region.GetProperty("width").GetDouble(), height = region.GetProperty("height").GetDouble(), scale = 1 } }));
            using var captured = JsonDocument.Parse(screenshot); var png = Convert.FromBase64String(captured.RootElement.GetProperty("data").GetString()!);
            cancellation.ThrowIfCancellationRequested();
            var document = await ImageWorker.RunAsync(() => FromCapture(data.GetRawText(), png), cancellation);
            document.Validate(); return document;
        }
        finally { try { await core.ExecuteScriptAsync("globalThis.snippyFreezeCleanup?.()"); } catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or ObjectDisposedException or InvalidOperationException) { } }
    }
    internal static DesignDocument FromCapture(string json, byte[] png)
    {
        using var envelope = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 160 }); DesignDocument.ValidateEnvelope(envelope.RootElement);
        var document = envelope.RootElement.Deserialize<DesignDocument>(DesignDocument.Json) ?? throw new InvalidDataException("Capture is empty.");
        document.Source.Url = DesignDocument.SafeUrl(document.Source.Url);
        var original = Decode(png);
        if (document.Width < 1 || document.Height < 1 || document.Width > 16384 || document.Height > 16384 || document.Width * document.Height > 64_000_000) throw new InvalidDataException("Invalid captured artboard.");
        if (original.PixelWidth != (int)Math.Ceiling(document.Width) || original.PixelHeight != (int)Math.Ceiling(document.Height))
        {
            var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) dc.DrawImage(original, new Rect(0, 0, document.Width, document.Height));
            var resized = new RenderTargetBitmap((int)Math.Ceiling(document.Width), (int)Math.Ceiling(document.Height), 96, 96, PixelFormats.Pbgra32); resized.Render(visual); resized.Freeze(); original = resized; png = ImageService.Png(original);
        }
        document.Original = document.AddAsset(png);
        var scaleX = original.PixelWidth / document.Width; var scaleY = original.PixelHeight / document.Height;
        var positions = new Dictionary<string, Point>();
        foreach (var n in document.Layers)
        {
            var p = n.Parent is { } parent && positions.TryGetValue(parent, out var origin) ? origin : new Point();
            p = new Point(p.X + n.X, p.Y + n.Y); positions[n.Id] = p;
            if (n.Kind != "image") continue;
            var left = (int)Math.Floor(p.X * scaleX); var top = (int)Math.Floor(p.Y * scaleY);
            var area = new PixelRect(left, top, Math.Max(1, (int)Math.Ceiling((p.X + n.Width) * scaleX) - left), Math.Max(1, (int)Math.Ceiling((p.Y + n.Height) * scaleY) - top)).Intersect(new(0, 0, original.PixelWidth, original.PixelHeight));
            if (area.IsEmpty) { n.Visible = false; continue; }
            n.Asset = document.AddAsset(ImageService.Png(ImageService.Crop(original, area))); n.Fill = "#00000000";
            // A clipped capture represents the visible portion of an image region.
            n.X += area.X / scaleX - p.X; n.Y += area.Y / scaleY - p.Y; n.Width = area.Width / scaleX; n.Height = area.Height / scaleY;
        }
        if (envelope.RootElement.TryGetProperty("fontAssets", out var fonts))
        {
            if (fonts.ValueKind != JsonValueKind.Array || fonts.GetArrayLength() > 40) throw new InvalidDataException("Too many captured fonts.");
            long total = 0;
            foreach (var font in fonts.EnumerateArray())
            {
                var data = font.GetProperty("data").GetString() ?? "";
                if (data.Length > 12 * 1024 * 1024 || (total += data.Length) > 24 * 1024 * 1024) throw new InvalidDataException("Captured fonts exceed their admission limit.");
                var bytes = Convert.FromBase64String(data); var media = font.GetProperty("mediaType").GetString() ?? "";
                if (media is not ("font/woff" or "font/woff2")) throw new InvalidDataException("Unsupported captured font.");
                if (bytes.Length > 8 * 1024 * 1024 || (media == "font/woff" ? bytes.Length <= 44 || !bytes.AsSpan().StartsWith("wOFF"u8) : bytes.Length <= 48 || !bytes.AsSpan().StartsWith("wOF2"u8))) throw new InvalidDataException("Invalid captured font signature or size.");
                document.AddAsset(bytes, media, font.GetProperty("family").GetString() ?? "", font.TryGetProperty("weight", out var weight) ? weight.GetString() ?? "normal" : "normal", font.TryGetProperty("style", out var style) ? style.GetString() ?? "normal" : "normal");
            }
        }
        document.SealBaseline(); return document;
    }
    internal static BitmapSource Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes); ImageService.ValidateEncoded(stream);
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        if (frame.PixelWidth > 16384 || frame.PixelHeight > 16384 || (long)frame.PixelWidth * frame.PixelHeight > 64_000_000) throw new InvalidDataException("Design image exceeds 64 MP or 16384 per axis.");
        frame.Freeze(); return frame;
    }
}
