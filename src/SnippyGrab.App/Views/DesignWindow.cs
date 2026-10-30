using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal sealed class DesignWindow : Window
{
    private readonly WebView2 editor = new();
    private readonly string root;
    private readonly Func<byte[], int, int, string, Task> shelf;
    private readonly Func<string, Task<bool>> clipboard;
    private readonly CancellationTokenSource lifetime = new();
    private DesignSession? session;
    private long savedRevision = -1;
    private string? path;
    private string? fingerprint;
    private bool busy, ready, allowClose;
    private int queuedMessages;
    private readonly bool diagnostic;
    internal bool Ready => ready;
    internal bool Busy => busy || queuedMessages > 0;
    internal DesignSession? Session => session;
    internal WebView2 Editor => editor;
    internal bool Matches(DesignDocument document, string? savedPath) => session?.Document.Id == document.Id ||
        (path is not null && savedPath is not null && string.Equals(Path.GetFullPath(path), Path.GetFullPath(savedPath), StringComparison.OrdinalIgnoreCase));
    internal DesignWindow(string dataDirectory, Func<byte[], int, int, string, Task> addToShelf, Func<string, Task<bool>> copyBrief, DesignDocument? initial = null, string? savedPath = null, bool diagnostic = false, MonitorInfo? initialMonitor = null)
    {
        Ui.StyleWindow(this, initialMonitor: initialMonitor); Title = "SnippyGrab · UI Design"; Width = 1320; Height = 900; MinWidth = 800; MinHeight = 550;
        root = Path.Combine(dataDirectory, "designs"); ManagedPath.RejectRedirects(root); Directory.CreateDirectory(root); shelf = addToShelf; clipboard = copyBrief; this.diagnostic = diagnostic; Content = editor;
        if (initial is not null) { session = new(initial); path = savedPath; if (savedPath is not null) { savedRevision = initial.Revision; fingerprint = initial.LoadedFingerprint ?? DesignStore.Fingerprint(savedPath); } }
        Loaded += async (_, _) =>
        {
            try
            {
                await DesignBrowser.Initialize(editor, Path.Combine(dataDirectory, "design-editor-profile"), true);
                if (lifetime.IsCancellationRequested) return;
                editor.CoreWebView2.WebMessageReceived += Message;
                editor.CoreWebView2.ProcessFailed += (_, _) => Status("The editor browser stopped. Close and reopen UI Design; saved designs remain on disk.", true);
                editor.CoreWebView2.Navigate(DesignBrowser.EditorUrl);
            }
            catch (Exception ex) { if (!lifetime.IsCancellationRequested) Content = Ui.Scroll(Ui.Text(OperationFailure.From(ex).Message)); }
        };
        Closing += ClosingWindow;
        Closed += (_, _) => { lifetime.Cancel(); Ui.ThemeChanged -= Theme; editor.Dispose(); lifetime.Dispose(); };
        Ui.ThemeChanged += Theme;
    }
    private void Theme() { if (ready) Publish(); }
    internal void SetDocument(DesignDocument document, string? savedPath = null)
    {
        session = new(document); path = savedPath; fingerprint = savedPath is null ? null : document.LoadedFingerprint ?? DesignStore.Fingerprint(savedPath); savedRevision = savedPath is null ? -1 : document.Revision; Publish(true);
    }
    internal void Publish(bool assets = false, string status = "Ready")
    {
        if (!ready || session is null) return; var d = session.Document;
        object document = new { d.SchemaVersion, d.Id, d.Revision, d.Width, d.Height, d.DevicePixelRatio, d.Source, d.Layers, d.Baseline, d.Original, d.Limitations, assets = assets ? d.Assets : null };
        var brush = Ui.Brush("Surface") as SolidColorBrush; var c = brush?.Color ?? Colors.Black;
        editor.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { kind = "state", document, changes = session.NetChanges(), canUndo = session.CanUndo, canRedo = session.CanRedo, status, textScale = Ui.TextScale, theme = c.R + c.G + c.B < 384 ? "dark" : "light" }, DesignDocument.Json));
        Title = "SnippyGrab · UI Design · " + d.Source.Title + (savedRevision != d.Revision ? " *" : "");
    }
    private void Status(string text, bool error = false)
    {
        if (!ready || lifetime.IsCancellationRequested) return; editor.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { kind = "status", text, error }));
    }
    private void Idle() { if (ready && !lifetime.IsCancellationRequested) editor.CoreWebView2.PostWebMessageAsJson("{\"kind\":\"idle\"}"); }
    private async void Message(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (e.Source != DesignBrowser.EditorUrl || lifetime.IsCancellationRequested) return;
        var message = e.WebMessageAsJson;
        if (message.Length > 2_000_000) { Status("The edit request is too large. Choose fewer layers.", true); Idle(); return; }
        // Modal source/file dialogs must open after the WebView2 callback returns.
        // A nested message loop inside that callback stalls child WebView2 initialization.
        queuedMessages++;
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
        queuedMessages--;
        if (lifetime.IsCancellationRequested) return;
        try
        {
            using var json = JsonDocument.Parse(message); var data = json.RootElement; var command = data.GetProperty("command").GetString();
            if (command == "ready") { ready = true; Publish(true); return; }
            if (busy) { Status("An operation is running. Please retry when it finishes."); Idle(); return; }
            if (session is not null && data.GetProperty("revision").GetInt64() != session.Document.Revision) { Publish(false, "The design changed. Review the current state and retry."); Idle(); return; }
            busy = true;
            try { await Handle(command ?? "", data); }
            finally { busy = false; Idle(); }
        }
        catch (Exception ex) { Status(OperationFailure.From(ex).Message, true); Idle(); }
    }
    private static string[] Ids(JsonElement data, string property = "ids") => data.TryGetProperty(property, out var ids) ? ids.EnumerateArray().Select(x => x.GetString() ?? "").Take(10001).ToArray() : [];
    private async Task Handle(string command, JsonElement data)
    {
        if (command == "import")
        {
            if (!await ConfirmReplace()) return;
            var window = new DesignSourceWindow(Path.Combine(root, "source-profile")) { Owner = this };
            if (window.ShowDialog() == true && window.Result is { } document) SetDocument(document); return;
        }
        if (command == "open")
        {
            if (!await ConfirmReplace()) return;
            var dialog = new OpenFileDialog { Filter = "Designs and handoffs|*.snippy;*.zip|All files|*.*", InitialDirectory = root, CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return; var filename = dialog.FileName;
            var document = await Task.Run(() => filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? DesignStore.LoadHandoff(filename) : DesignStore.Load(filename));
            SetDocument(document); if (!filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) { path = filename; savedRevision = document.Revision; fingerprint = document.LoadedFingerprint ?? DesignStore.Fingerprint(filename); }
            Publish(); return;
        }
        if (command == "bridge") { DesignBrowserBridge.ShowSetup(this); return; }
        if (session is null) throw new InvalidOperationException("Import or open a design first.");
        var ids = Ids(data); var doc = session.Document;
        switch (command)
        {
            case "edit":
                var edits = data.GetProperty("edits").EnumerateArray().ToArray(); if (edits.Length > 20000) throw new InvalidDataException("Too many edits.");
                session.Apply("Edit layers", nodes =>
                {
                    foreach (var edit in edits)
                    {
                        var n = nodes.Single(n => n.Id == edit.GetProperty("id").GetString()); var property = edit.GetProperty("property").GetString()!; var value = edit.GetProperty("value");
                        if (n.Locked && property != "locked") throw new InvalidOperationException("Unlock the layer before editing.");
                        var allowed = new Dictionary<string, string> { ["name"] = "Name", ["text"] = "Text", ["x"] = "X", ["y"] = "Y", ["width"] = "Width", ["height"] = "Height", ["fill"] = "Fill", ["stroke"] = "Stroke", ["strokeWidth"] = "StrokeWidth", ["radius"] = "Radius", ["rotation"] = "Rotation", ["clip"] = "Clip", ["fontSize"] = "FontSize", ["fontFamily"] = "FontFamily", ["fontWeight"] = "FontWeight", ["lineHeight"] = "LineHeight", ["noWrap"] = "NoWrap", ["opacity"] = "Opacity", ["layout"] = "Layout", ["gap"] = "Gap", ["visible"] = "Visible", ["locked"] = "Locked", ["private"] = "Private", ["intent"] = "Intent", ["scope"] = "Scope" };
                        if (!allowed.TryGetValue(property, out var member)) throw new InvalidDataException("Unsupported edit command.");
                        var info = typeof(DesignLayer).GetProperty(member)!; info.SetValue(n, JsonSerializer.Deserialize(value.GetRawText(), info.PropertyType, DesignDocument.Json));
                        if (property == "fill") n.GradientStops.Clear();
                        if (property is "gap" or "layout" && n.Layout != "free")
                        {
                            double position = 0; foreach (var child in nodes.Where(child => child.Parent == n.Id)) { if (n.Layout == "vertical") { child.Y = position; position += child.Height + n.Gap; } else { child.X = position; position += child.Width + n.Gap; } }
                        }
                    }
                }); break;
            case "undo": session.Undo(); break;
            case "redo": session.Redo(); break;
            case "delete": session.Delete(ids); break;
            case "duplicate": session.Duplicate(ids); break;
            case "group": session.Group(ids); break;
            case "ungroup": session.Ungroup(ids.Single()); break;
            case "reparent": session.Reparent(ids.Single(), data.GetProperty("parent").GetString() is { Length: > 0 } parentId ? parentId : null); break;
            case "add-text":
            case "add-frame":
            case "add-ellipse":
                session.Apply("Add layer", nodes => nodes.Add(new() { Kind = command == "add-text" ? "text" : command == "add-ellipse" ? "ellipse" : "frame", Name = command == "add-text" ? "New text" : "New shape", Text = command == "add-text" ? "New text" : "", X = 40, Y = 40, Width = 200, Height = 60, Fill = "#555555", Source = new() { Status = "unmapped" } })); break;
            case "align-left":
            case "align-top":
            case "align-center":
                session.Apply("Align layers", nodes =>
                {
                    var selected = nodes.Where(n => ids.Contains(n.Id)).ToList(); if (selected.Count == 0 || selected.Any(n => n.Locked) || selected.Select(n => n.Parent).Distinct().Count() != 1) throw new InvalidOperationException("Select unlocked sibling layers to align.");
                    var left = selected.Count == 1 ? 0 : selected.Min(n => n.X); var top = selected.Count == 1 ? 0 : selected.Min(n => n.Y);
                    var width = selected[0].Parent is { } p ? nodes.Single(n => n.Id == p).Width : doc.Width;
                    foreach (var n in selected) { if (command == "align-left") n.X = left; else if (command == "align-top") n.Y = top; else n.X = (width - n.Width) / 2; }
                }); break;
            case "revert": foreach (var id in ids) session.Revert(id); break;
            case "up":
            case "down":
                session.Apply("Reorder layers", nodes =>
                {
                    foreach (var id in ids) { var n = nodes.Single(n => n.Id == id); if (n.Locked) throw new InvalidOperationException("Unlock the layer first."); var index = nodes.IndexOf(n); var sibling = command == "up" ? nodes.Skip(index + 1).FirstOrDefault(s => s.Parent == n.Parent) : nodes.Take(index).LastOrDefault(s => s.Parent == n.Parent); if (sibling is null) continue; var target = nodes.IndexOf(sibling); nodes.Remove(n); nodes.Insert(target, n); }
                }); break;
            case "replace-image":
                var selected = doc.Layers.Single(n => n.Id == ids.Single()); if (selected.Locked) throw new InvalidOperationException("Unlock the layer first.");
                var imageDialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp", CheckFileExists = true };
                if (imageDialog.ShowDialog(this) == true)
                {
                    var bytes = await ImageWorker.RunAsync(() => ImageService.Png(ImageService.Load(imageDialog.FileName)), lifetime.Token); var asset = doc.AddAsset(bytes);
                    session.Apply("Replace image", nodes => { var n = nodes.Single(n => n.Id == selected.Id); n.Asset = asset; n.Kind = "image"; n.Fallback = ""; }); Publish(true);
                }
                return;
            case "save": await Save(); return;
            case "save-as":
                var previousPath = path; var previousFingerprint = fingerprint; path = null; fingerprint = null;
                try { await Save(); } catch { path = previousPath; fingerprint = previousFingerprint; throw; }
                return;
            case "preview":
                var reviewed = await Reviewed(Ids(data, "excluded"));
                editor.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { kind = "review", document = reviewed, brief = DesignStore.Brief(reviewed) }, DesignDocument.Json)); return;
            case "copy":
                var copy = await Reviewed(Ids(data, "excluded")); if (new DesignSession(copy).NetChanges().Count == 0) throw new InvalidOperationException("There are no included implementation changes."); if (!await clipboard(DesignStore.Brief(copy))) throw new IOException("The clipboard was busy. Export the handoff or retry copying."); Status("Agent brief copied. Paste it into your chosen agent."); return;
            case "export":
                var export = await Reviewed(Ids(data, "excluded")); if (new DesignSession(export).NetChanges().Count == 0) throw new InvalidOperationException("There are no included implementation changes.");
                var save = new SaveFileDialog { Filter = "Agent handoff ZIP|*.zip", FileName = "ui-handoff-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip", OverwritePrompt = true };
                if (save.ShowDialog(this) != true) return;
                var rendered = await DesignHandoff.Render(editor.CoreWebView2, export); var visuals = await ImageWorker.RunAsync(() => DesignHandoff.Visuals(export, rendered), lifetime.Token);
                await Task.Run(() => DesignStore.Export(save.FileName, export, visuals)); Status("Handoff exported with brief, scene, changes and matching image evidence."); return;
            case "recapture":
                var source = new DesignSourceWindow(Path.Combine(root, "source-profile"), doc) { Owner = this };
                if (source.ShowDialog() != true || source.Result is null) return;
                var implementation = source.Result; var expected = await DesignHandoff.Render(editor.CoreWebView2, await Reviewed([]));
                var comparison = await ImageWorker.RunAsync(() => DesignComparison.Create(doc, implementation, expected), lifetime.Token);
                var comparisonSave = new SaveFileDialog { Filter = "Comparison report|*.json", FileName = "implementation-comparison.json" };
                if (comparisonSave.ShowDialog(this) == true) { var folder = Path.GetDirectoryName(comparisonSave.FileName)!; AtomicFile.Write(comparisonSave.FileName, JsonSerializer.SerializeToUtf8Bytes(comparison, DesignDocument.Json)); AtomicFile.Write(Path.Combine(folder, "requested.png"), expected); AtomicFile.Write(Path.Combine(folder, "implementation.png"), implementation.Assets[implementation.Original].Bytes()); }
                Status(comparison.Summary); return;
            default: throw new InvalidDataException("Unknown design command.");
        }
        // Each accepted transaction has an owned crash-recovery snapshot, separate from explicit Save.
        var recovery = DesignDocument.Copy(doc);
        var recoveryPath = Path.Combine(root, doc.Id.ToString("N") + ".recovery.snippy");
        try { await Task.Run(() => DesignStore.Save(recoveryPath, recovery)); if (string.Equals(path, recoveryPath, StringComparison.OrdinalIgnoreCase)) fingerprint = DesignDocument.Hash(JsonSerializer.SerializeToUtf8Bytes(recovery, DesignDocument.Json)); }
        catch (Exception ex) { Publish(false, "Edits are kept in memory, but the recovery snapshot failed. Save to a writable location. " + OperationFailure.From(ex).Message); return; }
        Publish();
    }
    private Task<DesignDocument> Reviewed(string[] excluded) => ImageWorker.RunAsync(() => DesignHandoff.Sanitize(DesignStore.Review(session!.Document, excluded)), lifetime.Token);
    internal async Task Save()
    {
        if (session is null) return; var doc = session.Document;
        if (path is null)
        {
            var dialog = new SaveFileDialog { Filter = "Editable UI design|*.snippy", InitialDirectory = root, FileName = doc.Id.ToString("N") + ".snippy", OverwritePrompt = true }; if (dialog.ShowDialog(this) != true) throw new OperationCanceledException("Save cancelled."); path = dialog.FileName; fingerprint = File.Exists(path) ? DesignStore.Fingerprint(path) : "";
        }
        var snapshot = DesignDocument.Copy(doc); var expected = fingerprint; await Task.Run(() => DesignStore.Save(path, snapshot, expected)); fingerprint = DesignDocument.Hash(JsonSerializer.SerializeToUtf8Bytes(snapshot, DesignDocument.Json)); savedRevision = snapshot.Revision;
        try
        {
            var png = await DesignHandoff.Render(editor.CoreWebView2, doc); await shelf(png, (int)Math.Ceiling(doc.Width), (int)Math.Ceiling(doc.Height), path);
            Publish(false, "Design saved. Original, layers, resources and undo history are preserved.");
        }
        catch (Exception ex) { Publish(false, "Design file saved; its shelf preview could not be updated. " + OperationFailure.From(ex).Message); }
    }
    private async Task<bool> ConfirmReplace()
    {
        if (session is null || savedRevision == session.Document.Revision) return true;
        var answer = MessageBox.Show(this, "Save the current editable design before continuing?", "SnippyGrab · UI Design", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Cancel) return false; if (answer == MessageBoxResult.Yes) await Save(); return true;
    }
    private async void ClosingWindow(object? sender, CancelEventArgs e)
    {
        if (allowClose) return; e.Cancel = true; if (Busy) { Status("Wait for the current operation to finish before closing."); return; }
        busy = true;
        try { if (await ConfirmReplace()) { allowClose = true; Close(); } }
        catch (Exception ex) { Status(OperationFailure.From(ex).Message, true); }
        finally { busy = false; }
    }
    internal async Task<bool> RequestCloseAsync()
    {
        if (Busy) { Status("Wait for the current operation to finish before exiting."); return false; }
        busy = true; try { if (!await ConfirmReplace()) return false; allowClose = true; Close(); return true; } finally { busy = false; }
    }
    internal void CloseForDiagnostics() { if (!diagnostic) throw new InvalidOperationException("Diagnostic isolation is required."); allowClose = true; Close(); }
}
