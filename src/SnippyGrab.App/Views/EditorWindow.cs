using System.Globalization;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal enum EditTool { Arrow, Rectangle, Ellipse, Pen, Line, Text, Highlight, Number, Blur, Pixelate, Redact, Crop, Spotlight, OcrArea, ColorPicker, Magnify }
internal sealed record Annotation(EditTool Tool, Point Start, Point End, Color Color, double Stroke, double TextSize, string Text, IReadOnlyList<Point> Points, BitmapSource? Patch = null);
internal sealed record EditorState(BitmapSource Base, Annotation[] Marks);
internal sealed class EditorWindow : Window
{
    private readonly AppController controller;
    private readonly CaptureRecord record;
    private readonly CaptureViewLease lease;
    private string expectedRevision;
    private readonly UndoJournal<EditorState> journal;
    private readonly EditorSurface surface;
    private readonly ScrollViewer viewport;
    private readonly TextBlock status;
    private readonly Button openExportFolder;
    private readonly ComboBox tools;
    private readonly TextBox color;
    private readonly TextBox stroke;
    private readonly TextBox textSize;
    private readonly TextBox caption;
    private readonly ScaleTransform scale = new();
    private Point start;
    private readonly List<Point> points = [];
    private readonly CancellationTokenSource ocrLifetime = new();
    private bool drawing;
    private bool dirty;
    private int number = 1;
    private bool discard;
    private bool closeApproved;
    private bool copyRetryNeeded;
    private Task? documentWork;
    private readonly EditorCommitCoordinator commits = new();
    private readonly EditorCommitCoordinator closes = new();
    public EditorWindow(AppController controller, CaptureRecord record)
    {
        Ui.StyleWindow(this);
        this.controller = controller; this.record = record;
        expectedRevision = record.FileName;
        journal = new(new EditorState(ImageService.Load(controller.Repository.PathFor(record)), []), 20, RetainedBytes, 256L * 1024 * 1024);
        Title = $"SnippyGrab · {record.Width} × {record.Height}"; Width = 1000; Height = 700; MinWidth = 660; MinHeight = 440; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new DockPanel { Margin = new Thickness(12) }; Content = root;
        var actions = new WrapPanel();
        actions.Children.Add(Ui.Button("Apply + copy", "Update the managed shelf image and copy it (Ctrl+C); this does not export a file", () => controller.Run(ApplyCopy)));
        actions.Children.Add(Ui.Button("Export PNG…", "Apply edits and export a PNG outside the cache (Ctrl+S); clipboard is unchanged", () => controller.Run(ExportPng)));
        openExportFolder = Ui.Button("Open export folder", "Open the last successful export directory", () => controller.OpenExportFolder(record.ExportPath));
        openExportFolder.IsEnabled = !string.IsNullOrWhiteSpace(record.ExportPath); actions.Children.Add(openExportFolder);
        openExportFolder.ToolTip = string.IsNullOrWhiteSpace(record.ExportPath) ? "Export a PNG first" : "Last PNG export: " + record.ExportPath;
        actions.Children.Add(Ui.Button("Undo", "Undo (Ctrl+Z)", Undo)); actions.Children.Add(Ui.Button("Redo", "Redo (Ctrl+Y)", Redo));
        actions.Children.Add(Ui.Button("−", "Zoom out", () => Zoom(scale.ScaleX / 1.2))); actions.Children.Add(Ui.Button("+", "Zoom in", () => Zoom(scale.ScaleX * 1.2)));
        actions.Children.Add(Ui.Button("100%", "Inspect the full-resolution image at 100% zoom", () => Zoom(1)));
        actions.Children.Add(Ui.Button("Fit", "Fit image", Fit));
        actions.Children.Add(Ui.Button("OCR", "Copy text from the edited screenshot", () => controller.Run(() => CopyDocumentOcr())));
        actions.Children.Add(Ui.Button("Discard", "Close without applying unsaved changes", () => { discard = true; Close(); }));
        DockPanel.SetDock(actions, Dock.Top); root.Children.Add(actions);
        var toolbar = new WrapPanel { Margin = new Thickness(0, 4, 0, 8) };
        tools = new ComboBox { MinWidth = 138, ItemsSource = Enum.GetValues<EditTool>().Select(t => new ToolChoice(t, ToolLabel(t))), DisplayMemberPath = nameof(ToolChoice.Label), SelectedValuePath = nameof(ToolChoice.Tool), SelectedValue = EditTool.Arrow, ToolTip = "Annotation tool" };
        toolbar.Children.Add(tools);
        color = new TextBox { Text = controller.Settings.AnnotationColor, Width = 100, ToolTip = "Color (#RRGGBB or #AARRGGBB)" }; toolbar.Children.Add(color);
        stroke = new TextBox { Text = controller.Settings.StrokeSize.ToString(CultureInfo.InvariantCulture), Width = 44, ToolTip = "Stroke thickness" }; toolbar.Children.Add(stroke);
        textSize = new TextBox { Text = controller.Settings.TextSize.ToString(CultureInfo.InvariantCulture), Width = 48, ToolTip = "Text size" }; toolbar.Children.Add(textSize);
        caption = new TextBox { Text = "Look here", MaxLength = 2000, Width = 220, ToolTip = "Text annotation content" }; toolbar.Children.Add(caption);
        foreach (var input in new FrameworkElement[] { tools, color, stroke, textSize, caption })
            System.Windows.Automation.AutomationProperties.SetName(input, (string)input.ToolTip);
        toolbar.Children.Add(Ui.Text("  Draw on the image · Ctrl+wheel to zoom", 12, true));
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        status = Ui.Text(string.IsNullOrWhiteSpace(record.ExportPath) ? "Apply + copy updates the managed shelf image. Export PNG writes a separate file. Closing applies edits; Discard leaves pending edits unapplied." : "Last PNG export: " + record.ExportPath, 12, true);
        status.TextWrapping = TextWrapping.Wrap;
        DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        surface = new EditorSurface(journal.Current) { LayoutTransform = scale, Cursor = Cursors.Cross, Focusable = true };
        viewport = new ScrollViewer { Content = surface, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = new SolidColorBrush(Color.FromRgb(12, 15, 19)) };
        viewport.SetResourceReference(BackgroundProperty, "Surface");
        root.Children.Add(viewport);
        surface.MouseLeftButtonDown += (_, e) =>
        {
            if (commits.Busy || documentWork is not null) return;
            var point = Clamp(e.GetPosition(surface));
            if (Tool == EditTool.ColorPicker) { controller.Run(() => PickColorAsync(point)); e.Handled = true; return; }
            if (Tool == EditTool.Magnify)
            {
                Zoom((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? scale.ScaleX / 2 : scale.ScaleX * 2);
                UpdateLayout();
                viewport.ScrollToHorizontalOffset(point.X * scale.ScaleX - viewport.ViewportWidth / 2);
                viewport.ScrollToVerticalOffset(point.Y * scale.ScaleY - viewport.ViewportHeight / 2);
                e.Handled = true; return;
            }
            if (journal.Current.Marks.Length >= 500) { status.Text = "500 annotations reached. Apply and reopen to continue."; return; }
            start = point; points.Clear(); points.Add(start); drawing = true; surface.CaptureMouse(); e.Handled = true;
        };
        surface.MouseMove += (_, e) =>
        {
            if (!drawing) return;
            var point = Clamp(e.GetPosition(surface)); if (Tool == EditTool.Pen && points.Count < 20000 && (point - points[^1]).Length >= 0.75) points.Add(point);
            surface.Preview = Make(point, preview: true); surface.InvalidateVisual();
        };
        surface.MouseLeftButtonUp += (_, e) =>
        {
            if (!drawing) return;
            drawing = false; surface.ReleaseMouseCapture(); surface.Preview = null;
            var end = Clamp(e.GetPosition(surface)); var annotation = Make(end);
            controller.Run(() => StartDocumentEdit(annotation)); e.Handled = true;
        };
        PreviewMouseWheel += (_, e) => { if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return; Zoom(scale.ScaleX * (e.Delta > 0 ? 1.15 : 1 / 1.15)); e.Handled = true; };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { if (drawing) { drawing = false; surface.ReleaseMouseCapture(); surface.Preview = null; surface.InvalidateVisual(); } else Close(); e.Handled = true; }
            if (commits.Busy || documentWork is not null) { e.Handled = true; return; }
            if (Keyboard.Modifiers != ModifierKeys.Control || Keyboard.FocusedElement is TextBox) return;
            if (e.Key == Key.Z) Undo(); else if (e.Key == Key.Y) Redo(); else if (e.Key == Key.C) controller.Run(ApplyCopy); else if (e.Key == Key.S) controller.Run(ExportPng); else return;
            e.Handled = true;
        };
        Loaded += (_, _) => Fit();
        Closing += (_, e) =>
        {
            if (closeApproved) return;
            e.Cancel = true;
            controller.Run(async () => { await RequestCloseAsync(); });
        };
        lease = new(controller.Repository, record);
        Closed += (_, _) => { ocrLifetime.Cancel(); ocrLifetime.Dispose(); lease.Dispose(); surface.Preview = null; surface.State = null; viewport.Content = null; points.Clear(); journal.Clear(); };
    }
    private sealed record ToolChoice(EditTool Tool, string Label) { public override string ToString() => Label; }
    internal static string ToolLabel(EditTool tool) => tool switch { EditTool.Pen => "Freehand", EditTool.Highlight => "Highlighter", EditTool.Number => "Numbered marker", EditTool.Redact => "Solid redaction", EditTool.OcrArea => "OCR selected area", EditTool.ColorPicker => "Pick image color", EditTool.Magnify => "Magnify (Shift: out)", _ => tool.ToString() };
    private async Task PickColorAsync(Point point)
    {
        var content = (UIElement)Content; content.IsEnabled = false;
        try { documentWork = PickCoreAsync(point); await documentWork; }
        finally { documentWork = null; content.IsEnabled = true; }
    }
    private async Task PickCoreAsync(Point point)
    {
        var image = await RenderAsync(journal.Current);
        var picked = SampleColor(image, point);
        color.Text = $"#{picked.R:X2}{picked.G:X2}{picked.B:X2}";
        status.Text = "Annotation color: " + color.Text + ". Sampled from the edited image; select a drawing tool to use it.";
    }
    internal static Color SampleColor(BitmapSource image, Point point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new ArgumentOutOfRangeException(nameof(point));
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var pixel = new byte[4];
        converted.CopyPixels(new Int32Rect(Math.Clamp((int)point.X, 0, image.PixelWidth - 1), Math.Clamp((int)point.Y, 0, image.PixelHeight - 1), 1, 1), pixel, 4, 0);
        return Color.FromRgb(pixel[2], pixel[1], pixel[0]);
    }
    private EditTool Tool => (EditTool)(tools.SelectedValue ?? EditTool.Arrow);
    private Point Clamp(Point p) => new(Math.Clamp(p.X, 0, journal.Current.Base.PixelWidth), Math.Clamp(p.Y, 0, journal.Current.Base.PixelHeight));
    private Annotation Make(Point end, bool preview = false)
    {
        Color c; try { c = (Color)ColorConverter.ConvertFromString(color.Text); } catch (FormatException) { c = Colors.OrangeRed; }
        c.A = 255; // Redaction and annotations never inherit accidental transparent alpha.
        var width = double.TryParse(stroke.Text, CultureInfo.InvariantCulture, out var w) && double.IsFinite(w) ? Math.Clamp(w, 1, 30) : 3;
        var size = double.TryParse(textSize.Text, CultureInfo.InvariantCulture, out var t) && double.IsFinite(t) ? Math.Clamp(t, 8, 120) : 24;
        return new(Tool, start, end, c, width, size, Tool == EditTool.Number ? number.ToString(CultureInfo.InvariantCulture) : caption.Text, Tool == EditTool.Pen ? preview ? points : points.ToArray() : []);
    }
    internal async Task StartDocumentEdit(Annotation mark)
    {
        var content = (UIElement)Content; content.IsEnabled = false;
        try { documentWork = CommitAsync(mark); await documentWork; }
        finally { documentWork = null; content.IsEnabled = true; }
    }
    private async Task CommitAsync(Annotation mark)
    {
        var area = PixelRect.Between((int)mark.Start.X, (int)mark.Start.Y, (int)mark.End.X, (int)mark.End.Y).Intersect(new(0, 0, journal.Current.Base.PixelWidth, journal.Current.Base.PixelHeight));
        if (mark.Tool == EditTool.OcrArea)
        {
            if (!area.IsEmpty) controller.Run(() => CopyDocumentOcr(area));
            surface.InvalidateVisual(); return;
        }
        if (mark.Tool == EditTool.Crop)
        {
            if (area.Width < 2 || area.Height < 2) return;
            journal.Push(await BuildStateAsync(journal.Current, mark)); Refresh(); Fit(); dirty = true; return;
        }
        if (mark.Tool is EditTool.Blur or EditTool.Pixelate)
        {
            if (area.Width < 2 || area.Height < 2) return;
        }
        if (mark.Tool == EditTool.Number) number++;
        journal.Push(await BuildStateAsync(journal.Current, mark)); dirty = true; Refresh();
    }
    internal static Task<EditorState> BuildStateAsync(EditorState state, Annotation mark)
    {
        if (mark.Tool is not (EditTool.Crop or EditTool.Blur or EditTool.Pixelate)) return Task.FromResult(state with { Marks = [.. state.Marks, mark] });
        var area = PixelRect.Between((int)mark.Start.X, (int)mark.Start.Y, (int)mark.End.X, (int)mark.End.Y).Intersect(new(0, 0, state.Base.PixelWidth, state.Base.PixelHeight));
        if (area.Width < 2 || area.Height < 2) return Task.FromResult(state);
        return Task.Run(() =>
        {
            // Create the drawing on this worker; all input bitmaps are frozen and cross-thread safe.
            var rendered = Render(state);
            var crop = ImageService.Crop(rendered, area);
            if (mark.Tool == EditTool.Crop) return new EditorState(crop, []);
            var patch = Effects.Apply(crop, mark.Tool == EditTool.Pixelate);
            return state with { Marks = [.. state.Marks, mark with { Start = new(area.X, area.Y), End = new(area.Right, area.Bottom), Patch = patch }] };
        });
    }
    private void Undo() { if (!journal.CanUndo) return; journal.Undo(); dirty = true; Refresh(); }
    private void Redo() { if (!journal.CanRedo) return; journal.Redo(); dirty = true; Refresh(); }
    private void Refresh() { surface.State = journal.Current; surface.Width = journal.Current.Base.PixelWidth; surface.Height = journal.Current.Base.PixelHeight; surface.InvalidateVisual(); }
    private void Fit() => Zoom(Math.Min(1, Math.Min(Math.Max(200, viewport.ActualWidth - 24) / journal.Current.Base.PixelWidth, Math.Max(200, viewport.ActualHeight - 24) / journal.Current.Base.PixelHeight)));
    private void Zoom(double value) { scale.ScaleX = scale.ScaleY = Math.Clamp(value, 0.03, 8); status.Text = $"{scale.ScaleX:P0} · {journal.Current.Base.PixelWidth} × {journal.Current.Base.PixelHeight} · Esc closes and applies" + (string.IsNullOrWhiteSpace(record.ExportPath) ? "" : " · Last PNG export: " + record.ExportPath); }
    private async Task<BitmapSource> ApplyAsync()
    {
        var image = await RenderAsync(journal.Current);
        var png = await Task.Run(() => ImageService.Png(image));
        controller.Repository.Replace(record, png, image.PixelWidth, image.PixelHeight, expectedRevision);
        expectedRevision = record.FileName;
        dirty = false; controller.Dock.Refresh(); return image;
    }
    private Task<bool> ApplyCopy() => commits.RunAsync(async () =>
    {
        var content = (UIElement)Content;
        content.IsEnabled = false;
        try
        {
            var image = dirty ? await ApplyAsync() : await Task.Run(() => ImageService.Load(controller.Repository.PathFor(record)));
            copyRetryNeeded = !await controller.CopyImage(image);
            status.Text = copyRetryNeeded ? "Edits are saved to the shelf. Clipboard is busy; retry Apply + copy or close again, or Discard to close." : "Applied to the managed shelf image and copied to clipboard. Use Export PNG for a separate file.";
            return !copyRetryNeeded;
        }
        catch
        {
            status.Text = "Could not apply edits. This editor stays open. Retry Copy or close again, or choose Discard to close without applying.";
            throw;
        }
        finally { content.IsEnabled = true; }
    });

    private Task<bool> ExportPng() => commits.RunAsync(async () =>
    {
        var content = (UIElement)Content; content.IsEnabled = false;
        try
        {
            if (dirty) await ApplyAsync();
            var outcome = controller.Save(record, this);
            status.Text = outcome.Status switch
            {
                ExportStatus.Exported => "PNG exported: " + outcome.Path + (outcome.Message is null ? "" : " · " + outcome.Message),
                ExportStatus.Cancelled => "Export cancelled. No file was written; applied edits remain in the managed shelf image.",
                _ => "Export failed: " + outcome.Message + ". Applied edits remain in the shelf; retry Export PNG."
            };
            openExportFolder.IsEnabled = !string.IsNullOrWhiteSpace(record.ExportPath);
            openExportFolder.ToolTip = string.IsNullOrWhiteSpace(record.ExportPath) ? "Export a PNG first" : "Last PNG export: " + record.ExportPath;
            await Task.CompletedTask;
            return outcome.Status == ExportStatus.Exported;
        }
        catch
        {
            status.Text = "Could not apply edits for export. This editor stays open; retry Export PNG or choose Discard.";
            throw;
        }
        finally { content.IsEnabled = true; }
    });

    internal Task<bool> RequestCloseAsync() => closes.RunAsync(CloseCoreAsync);
    private async Task<bool> CloseCoreAsync()
    {
        if (closeApproved) return true;
        try
        {
            if (documentWork is { } pending) await pending;
            if (!discard && (dirty || commits.Busy || copyRetryNeeded) && !await ApplyCopy()) return false;
            closeApproved = true;
            Close();
            return true;
        }
        catch (Exception ex)
        {
            controller.Notify("Editor remains open: " + ex.Message + " Retry close/Copy or choose Discard.");
            return false;
        }
    }
    private async Task CopyDocumentOcr(PixelRect? area = null)
    {
        var token = ocrLifetime.Token; var state = journal.Current;
        var image = await Task.Run(() => area is { } crop ? ImageService.Crop(Render(state), crop) : Render(state));
        if (!token.IsCancellationRequested) await CopyOcr(image);
    }
    private async Task CopyOcr(BitmapSource image)
    {
        status.Text = "Reading text locally…";
        var token = ocrLifetime.Token;
        var outcome = await controller.OcrText(image, token);
        if (!token.IsCancellationRequested) status.Text = outcome;
    }
    internal static long RetainedBytes(IReadOnlyList<EditorState> states)
    {
        var images = new HashSet<BitmapSource>(ReferenceEqualityComparer.Instance);
        var marks = new HashSet<Annotation>(ReferenceEqualityComparer.Instance);
        foreach (var state in states)
        {
            images.Add(state.Base);
            foreach (var mark in state.Marks) { marks.Add(mark); if (mark.Patch is not null) images.Add(mark.Patch); }
        }
        return images.Sum(image => (long)image.PixelWidth * image.PixelHeight * 4) + marks.Sum(mark => (long)mark.Points.Count * 16 + mark.Text.Length * 2L + 128);
    }
    internal static BitmapSource Render(EditorState state)
    {
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
        {
            dc.DrawImage(state.Base, new Rect(0, 0, state.Base.PixelWidth, state.Base.PixelHeight));
            foreach (var mark in state.Marks) Draw(dc, mark, state.Base.PixelWidth, state.Base.PixelHeight);
        }
        var bitmap = new RenderTargetBitmap(state.Base.PixelWidth, state.Base.PixelHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(drawing); bitmap.Freeze(); return bitmap;
    }
    internal static Task<BitmapSource> RenderAsync(EditorState state) => Task.Run(() => Render(state));
    internal static void Draw(DrawingContext dc, Annotation m, int width, int height)
    {
        var brush = new SolidColorBrush(m.Color); var pen = new Pen(brush, m.Stroke) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        var box = new Rect(m.Start, m.End);
        if (m.Patch is not null) { dc.DrawImage(m.Patch, box); return; }
        switch (m.Tool)
        {
            case EditTool.Rectangle: dc.DrawRectangle(null, pen, box); break;
            case EditTool.Ellipse: dc.DrawEllipse(null, pen, new Point(box.X + box.Width / 2, box.Y + box.Height / 2), box.Width / 2, box.Height / 2); break;
            case EditTool.Redact: dc.DrawRectangle(Brushes.Black, null, box); break;
            case EditTool.Highlight: dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(78, m.Color.R, m.Color.G, m.Color.B)), null, box); break;
            case EditTool.Line: dc.DrawLine(pen, m.Start, m.End); break;
            case EditTool.Arrow:
                dc.DrawLine(pen, m.Start, m.End);
                var angle = Math.Atan2(m.End.Y - m.Start.Y, m.End.X - m.Start.X); var head = 12 + m.Stroke * 2;
                dc.DrawLine(pen, m.End, new Point(m.End.X - head * Math.Cos(angle - 0.45), m.End.Y - head * Math.Sin(angle - 0.45)));
                dc.DrawLine(pen, m.End, new Point(m.End.X - head * Math.Cos(angle + 0.45), m.End.Y - head * Math.Sin(angle + 0.45))); break;
            case EditTool.Pen:
                if (m.Points.Count == 1) dc.DrawEllipse(brush, null, m.Start, m.Stroke / 2, m.Stroke / 2);
                else if (m.Points.Count > 1)
                {
                    var path = new StreamGeometry();
                    using (var context = path.Open())
                    {
                        context.BeginFigure(m.Points[0], false, false);
                        for (var i = 1; i < m.Points.Count; i++) context.LineTo(m.Points[i], true, false);
                    }
                    path.Freeze(); dc.DrawGeometry(null, pen, path);
                }
                break;
            case EditTool.Text:
                dc.DrawText(new FormattedText(m.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), m.TextSize, brush, 1), m.Start); break;
            case EditTool.Number:
                dc.DrawEllipse(brush, new Pen(Brushes.White, 2), m.Start, m.TextSize * 0.65, m.TextSize * 0.65);
                var text = new FormattedText(m.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), m.TextSize, Brushes.White, 1);
                dc.DrawText(text, new Point(m.Start.X - text.Width / 2, m.Start.Y - text.Height / 2)); break;
            case EditTool.Spotlight:
                var shade = Geometry.Combine(new RectangleGeometry(new Rect(0, 0, width, height)), new RectangleGeometry(box), GeometryCombineMode.Exclude, null);
                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(155, 0, 0, 0)), null, shade); break;
            default: dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(40, 150, 190, 255)), new Pen(Brushes.LightBlue, 1), box); break;
        }
    }
    private sealed class EditorSurface : FrameworkElement
    {
        public EditorState? State { get; set; }
        public Annotation? Preview { get; set; }
        public EditorSurface(EditorState state) { State = state; Width = state.Base.PixelWidth; Height = state.Base.PixelHeight; }
        protected override void OnRender(DrawingContext dc)
        {
            if (State is null) return;
            dc.DrawImage(State.Base, new Rect(0, 0, Width, Height));
            foreach (var mark in State.Marks) Draw(dc, mark, State.Base.PixelWidth, State.Base.PixelHeight);
            if (Preview is not null) Draw(dc, Preview, State.Base.PixelWidth, State.Base.PixelHeight);
        }
    }
}

internal static class Effects
{
    public static BitmapSource Apply(BitmapSource image, bool pixelate)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        int width = image.PixelWidth, height = image.PixelHeight, stride = width * 4;
        var pixels = new byte[stride * height]; converted.CopyPixels(pixels, stride, 0);
        if (pixelate)
        {
            const int size = 14;
            for (var y = 0; y < height; y += size) for (var x = 0; x < width; x += size)
                for (var c = 0; c < 3; c++)
                {
                    long total = 0; int count = 0;
                    for (var j = y; j < Math.Min(y + size, height); j++) for (var i = x; i < Math.Min(x + size, width); i++) { total += pixels[j * stride + i * 4 + c]; count++; }
                    for (var j = y; j < Math.Min(y + size, height); j++) for (var i = x; i < Math.Min(x + size, width); i++) pixels[j * stride + i * 4 + c] = (byte)(total / count);
                }
        }
        else
        {
            // Three separable box passes approximate a Gaussian blur without a GPU/third-party dependency.
            for (var pass = 0; pass < 3; pass++)
            {
                var temp = (byte[])pixels.Clone(); const int radius = 7;
                for (var y = 0; y < height; y++) for (var c = 0; c < 3; c++)
                {
                    int sum = 0, count = 0;
                    for (var k = 0; k <= Math.Min(radius, width - 1); k++) { sum += pixels[y * stride + k * 4 + c]; count++; }
                    for (var x = 0; x < width; x++)
                    {
                        temp[y * stride + x * 4 + c] = (byte)(sum / count);
                        if (x - radius >= 0) { sum -= pixels[y * stride + (x - radius) * 4 + c]; count--; }
                        if (x + radius + 1 < width) { sum += pixels[y * stride + (x + radius + 1) * 4 + c]; count++; }
                    }
                }
                for (var x = 0; x < width; x++) for (var c = 0; c < 3; c++)
                {
                    int sum = 0, count = 0;
                    for (var k = 0; k <= Math.Min(radius, height - 1); k++) { sum += temp[k * stride + x * 4 + c]; count++; }
                    for (var y = 0; y < height; y++)
                    {
                        pixels[y * stride + x * 4 + c] = (byte)(sum / count);
                        if (y - radius >= 0) { sum -= temp[(y - radius) * stride + x * 4 + c]; count--; }
                        if (y + radius + 1 < height) { sum += temp[(y + radius + 1) * stride + x * 4 + c]; count++; }
                    }
                }
            }
        }
        var output = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride); output.Freeze(); return output;
    }
}
