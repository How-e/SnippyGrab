using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Wpf;

namespace SnippyGrab.App.Services;

internal static class DesignChecks
{
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    internal static async Task Run(string report)
    {
        var destination = Path.GetFullPath(report); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-design-check-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var source = new WebView2(); var editor = new WebView2(); var tabs = new Grid(); tabs.RowDefinitions.Add(new()); tabs.RowDefinitions.Add(new()); tabs.Children.Add(source); Grid.SetRow(editor, 1); tabs.Children.Add(editor);
        var window = new Window { Content = tabs, Width = 900, Height = 900, ShowActivated = false, Left = -30000, Top = -30000, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Show(); var measurements = new List<object>();
        try
        {
            var fixture = Path.Combine(root, "fixture.html");
            await File.WriteAllTextAsync(fixture, """
                <!doctype html><html lang="en"><style>html,body{margin:0;background:white;font-family:'Segoe UI',sans-serif}main{padding:30px;width:580px}h1{font-size:28px;margin:0 0 16px}button{font:16px 'Segoe UI';background:#303030;color:white;border:0;padding:12px 20px;border-radius:5px}section{display:flex;flex-direction:column;gap:20px}#effect{filter:blur(.1px);width:80px;height:30px;background:#4169e1}svg{width:24px;height:24px}</style><main><h1 id="heading">Project preferences</h1><section data-testid="actions"><p>Choose the defaults for this project.</p><button data-testid="save">Save changes</button></section><svg viewBox="0 0 24 24"><path d="M2 2 L22 2 L12 20 Z" fill="#222222"/></svg><div id="effect"></div><input type="password" value="never-export-this"></main></html>
                """);
            await DesignBrowser.Initialize(source, Path.Combine(root, "source-profile"), false); await Navigate(source, new Uri(fixture).AbsoluteUri);
            await source.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride", "{\"width\":640,\"height\":480,\"deviceScaleFactor\":1,\"mobile\":false}");
            var document = await DesignBrowser.Capture(source, "visible", CancellationToken.None);
            Assert(document.Layers.Any(n => n.Kind == "text" && n.Text == "Save changes"), "Real DOM text was not imported as an editable layer.");
            Assert(document.Layers.Any(n => n.Kind == "path"), "Simple SVG path import failed.");
            Assert(document.Layers.Any(n => n.Kind == "image" && n.Fallback.Length > 0 && n.Asset.Length > 0), "Unsupported effect was not explicitly rasterized.");
            Assert(!JsonSerializer.Serialize(document, DesignDocument.Json).Contains("never-export-this", StringComparison.Ordinal), "Password value leaked into document data.");
            await DesignBrowser.Initialize(editor, Path.Combine(root, "editor-profile"), true); await Navigate(editor, DesignBrowser.EditorUrl);
            var importRegressions = await DesignCaptureChecks.Run(source, editor, root, destination);
            var sourceWindowWorkflow = await DesignCaptureChecks.SourceWindowWorkflow(root);
            Assert(editor.CoreWebView2.Settings.AreHostObjectsAllowed == false && source.CoreWebView2.Settings.IsWebMessageEnabled == false, "Browser privilege boundary is incorrect.");
            using (var host = Process.Start(new ProcessStartInfo { FileName = Environment.ProcessPath!, Arguments = "chrome-extension://invalid/", UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true })!)
            {
                host.StandardInput.Close(); var response = await DesignBrowserBridge.ReadMessage(host.StandardOutput.BaseStream).WaitAsync(TimeSpan.FromSeconds(10));
                using var rejected = JsonDocument.Parse(response); Assert(!rejected.RootElement.GetProperty("ok").GetBoolean(), "Native host accepted an unregistered origin.");
                await host.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            var originalReconstruction = await DesignHandoff.Render(editor.CoreWebView2, document);
            var session = new DesignSession(document); var target = document.Layers.Single(n => n.Kind == "text" && n.Text == "Save changes");
            session.Apply("Edit source button label", nodes => nodes.Single(n => n.Id == target.Id).Text = "Save preferences");
            var file = Path.Combine(root, "roundtrip.snippy"); await Task.Run(() => DesignStore.Save(file, document)); var reopened = await Task.Run(() => DesignStore.Load(file)); Assert(new DesignSession(reopened).NetChanges().Any(c => c.Property == "text"), "Reopened design lost its edits.");
            var reviewed = await ImageWorker.RunAsync(() => DesignHandoff.Sanitize(DesignStore.Review(reopened, [])));
            var rendered = await DesignHandoff.Render(editor.CoreWebView2, reviewed); var visuals = await ImageWorker.RunAsync(() => DesignHandoff.Visuals(reviewed, rendered));
            var handoff = Path.Combine(root, "handoff.zip"); await Task.Run(() => DesignStore.Export(handoff, reviewed, visuals)); var loaded = await Task.Run(() => DesignStore.LoadHandoff(handoff));
            Assert(loaded.Layers.Single(n => n.Id == target.Id).Text == "Save preferences", "Portable handoff lost reviewed text.");
            Assert(DesignStore.Brief(loaded).Contains("data-testid", StringComparison.Ordinal), "Agent brief lacks a target hint.");
            var excluded = DesignStore.Review(reopened, [target.Id]); Assert(!new DesignSession(excluded).NetChanges().Any(), "Excluded edit remained in reviewed state.");
            await File.WriteAllBytesAsync(destination + ".original.png", document.Assets[document.Original].Bytes()); await File.WriteAllBytesAsync(destination + ".reconstructed.png", originalReconstruction); await File.WriteAllBytesAsync(destination + ".edited.png", rendered);
            var fidelity = await ImageWorker.RunAsync(() => DesignComparison.Create(document, document, originalReconstruction));
            string? copied = null; byte[]? shelfImage = null;
            var workspacePath = Path.Combine(root, "workspace.snippy"); DesignStore.Save(workspacePath, document);
            var workspace = new Views.DesignWindow(root, (png, _, _, _) => { shelfImage = png; return Task.CompletedTask; }, text => { copied = text; return Task.FromResult(true); }, DesignDocument.Copy(document), workspacePath, diagnostic: true) { ShowActivated = false, Left = -30000, Top = -30000, WindowStartupLocation = WindowStartupLocation.Manual };
            workspace.Show();
            try
            {
                await Wait(() => workspace.Ready);
                var interaction = "const row=[...document.querySelectorAll('.tree-row')].find(r=>r.getAttribute('aria-label')==='Save changes');row.click();const field=document.querySelector('[data-property=text]');field.value='Apply preferences';field.dispatchEvent(new Event('change',{bubbles:true}));";
                await workspace.Editor.CoreWebView2.ExecuteScriptAsync(interaction);
                await Wait(() => !workspace.Busy && workspace.Session!.Document.Layers.Single(n => n.Id == target.Id).Text == "Apply preferences");
                await workspace.Editor.CoreWebView2.ExecuteScriptAsync("document.getElementById('review').click()"); await Wait(() => !workspace.Busy);
                await workspace.Editor.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-command=copy]').click()"); await Wait(() => copied is not null);
                Assert(copied!.Contains("Apply preferences", StringComparison.Ordinal), "Production copy bridge did not publish the reviewed brief.");
                await workspace.Editor.CoreWebView2.ExecuteScriptAsync("document.getElementById('back').click();document.querySelector('[data-command=save]').click()"); await Wait(() => shelfImage is not null && !workspace.Busy);
                Assert(DesignStore.Load(workspacePath).Layers.Single(n => n.Id == target.Id).Text == "Apply preferences", "Production Save did not persist editor changes.");
                using var screenshot = JsonDocument.Parse(await workspace.Editor.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.captureScreenshot", "{\"format\":\"png\"}")); await File.WriteAllBytesAsync(destination + ".workspace.png", Convert.FromBase64String(screenshot.RootElement.GetProperty("data").GetString()!));
                await workspace.Editor.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-command=undo]').click()"); await Wait(() => !workspace.Busy && workspace.Session!.Document.Layers.Single(n => n.Id == target.Id).Text == "Save preferences");
            }
            finally { workspace.CloseForDiagnostics(); }
            foreach (var count in new[] { 1000, 10000 })
            {
                var large = new DesignDocument { Width = 1600, Height = 1600, Layers = Enumerable.Range(0, count).Select(i => new DesignLayer { Id = "n" + i, X = i % 100 * 15, Y = i / 100 * 15, Width = 12, Height = 12, Fill = "#555555" }).ToList() }; large.SealBaseline();
                var timer = Stopwatch.StartNew(); var scene = JsonSerializer.Serialize(large, DesignDocument.Json);
                await editor.CoreWebView2.ExecuteScriptAsync("document.getElementById('artboard').replaceChildren(SGScene.svg(" + scene + "))"); timer.Stop();
                var modelTimer = Stopwatch.StartNew(); var s = new DesignSession(large); s.Apply("Move", nodes => nodes[0].X += 1); _ = s.NetChanges(); modelTimer.Stop(); measurements.Add(new { Layers = count, BrowserRenderMilliseconds = timer.Elapsed.TotalMilliseconds, ModelCommandAndDiffMilliseconds = modelTimer.Elapsed.TotalMilliseconds });
            }
            await File.WriteAllTextAsync(destination, JsonSerializer.Serialize(new { Result = "PASS", Runtime = source.CoreWebView2.Environment.BrowserVersionString, ImportedLayers = document.Layers.Count, FidelityMeanDifference = fidelity.MeanChannelDifference, FidelityChangedPixels = fidelity.ChangedPixels, ImportRegressions = importRegressions, SourceWindowWorkflow = sourceWindowWorkflow, Measurements = measurements, Scope = "Isolated real WebView2 local HTML import, production editor-to-source-window Import/Load/capture path, full/visible/offscreen-element capture, HTTP Basic/form authentication and cancellation, editable text/vector/fallback, save/reopen, reviewed ZIP and exact package inventory, render/export and bounded scene timings. Does not establish universal page fidelity, external browser extension installation, independent agent implementation, physical gestures or clean-profile runtime installation." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { source.Dispose(); editor.Dispose(); window.Close(); }
    }
    private static async Task Navigate(WebView2 view, string url)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Completed(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e) { if (e.IsSuccess) done.TrySetResult(); else done.TrySetException(new IOException("Fixture navigation failed: " + e.WebErrorStatus)); }
        view.CoreWebView2.NavigationCompleted += Completed;
        try { view.CoreWebView2.Navigate(url); await done.Task.WaitAsync(TimeSpan.FromSeconds(30)); }
        finally { view.CoreWebView2.NavigationCompleted -= Completed; }
    }
    private static async Task Wait(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(30); while (!condition()) { if (DateTime.UtcNow > until) throw new TimeoutException("Design interaction did not complete."); await Task.Delay(25); }
    }
}
