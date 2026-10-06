using System.Globalization;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal enum EditTool { Arrow, Rectangle, Ellipse, Pen, Line, Text, Highlight, Number, Blur, Pixelate, Redact, Crop, Spotlight, OcrArea }
internal sealed record Annotation(EditTool Tool, Point Start, Point End, Color Color, double Stroke, double TextSize, string Text, Point[] Points, BitmapSource? Patch = null);
internal sealed record EditorState(BitmapSource Base, Annotation[] Marks);
internal sealed class EditorWindow : Window
{
    private readonly AppController controller;
    private readonly CaptureRecord record;
    private readonly List<IDisposable> leases = [];
    private readonly UndoJournal<EditorState> journal;
    private readonly EditorSurface surface;
    private readonly ScrollViewer viewport;
    private readonly TextBlock status;
    private readonly ComboBox tools;
    private readonly TextBox color;
    private readonly TextBox stroke;
    private readonly TextBox textSize;
    private readonly TextBox caption;
    private readonly ScaleTransform scale = new();
    private Point start;
    private readonly List<Point> points = [];
    private bool drawing;
    private bool dirty;
    private int number = 1;
    private bool discard;
    public EditorWindow(AppController controller, CaptureRecord record)
    {
        Ui.StyleWindow(this);
        this.controller = controller; this.record = record;
        leases.Add(controller.Repository.Lease([record]));
        journal = new(new EditorState(ImageService.Load(controller.Repository.PathFor(record)), []), 20);
        Title = $"SnippyGrab · {record.Width} × {record.Height}"; Width = 1000; Height = 700; MinWidth = 660; MinHeight = 440; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new DockPanel { Margin = new Thickness(12) }; Content = root;
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(Ui.Button("Copy", "Apply edits and copy image (Ctrl+C)", () => controller.Run(ApplyCopy)));
        actions.Children.Add(Ui.Button("Save", "Apply edits to the shelf (Ctrl+S)", () => controller.Run(ApplyCopy)));
        actions.Children.Add(Ui.Button("Save as…", "Apply and export a PNG", () => controller.Run(async () => { await ApplyCopy(); controller.Save(record); })));
        actions.Children.Add(Ui.Button("Undo", "Undo (Ctrl+Z)", Undo)); actions.Children.Add(Ui.Button("Redo", "Redo (Ctrl+Y)", Redo));
        actions.Children.Add(Ui.Button("−", "Zoom out", () => Zoom(scale.ScaleX / 1.2))); actions.Children.Add(Ui.Button("+", "Zoom in", () => Zoom(scale.ScaleX * 1.2)));
        actions.Children.Add(Ui.Button("Fit", "Fit image", Fit));
        actions.Children.Add(Ui.Button("OCR", "Copy text from the edited screenshot", () => controller.Run(async () => await CopyOcr(Render(journal.Current)))));
        actions.Children.Add(Ui.Button("Discard", "Close without applying unsaved changes", () => { discard = true; Close(); }));
        DockPanel.SetDock(actions, Dock.Top); root.Children.Add(actions);
        var toolbar = new WrapPanel { Margin = new Thickness(0, 4, 0, 8) };
        tools = new ComboBox { Width = 118, ItemsSource = Enum.GetValues<EditTool>(), SelectedItem = EditTool.Arrow, ToolTip = "Annotation tool" };
        toolbar.Children.Add(tools);
        color = new TextBox { Text = controller.Settings.AnnotationColor, Width = 100, ToolTip = "Color (#RRGGBB or #AARRGGBB)" }; toolbar.Children.Add(color);
        stroke = new TextBox { Text = controller.Settings.StrokeSize.ToString(CultureInfo.InvariantCulture), Width = 44, ToolTip = "Stroke thickness" }; toolbar.Children.Add(stroke);
        textSize = new TextBox { Text = controller.Settings.TextSize.ToString(CultureInfo.InvariantCulture), Width = 48, ToolTip = "Text size" }; toolbar.Children.Add(textSize);
        caption = new TextBox { Text = "Look here", MaxLength = 2000, Width = 220, ToolTip = "Text annotation content" }; toolbar.Children.Add(caption);
        toolbar.Children.Add(Ui.Text("  Draw on the image · Ctrl+wheel to zoom", 12, true));
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        status = Ui.Text("Changes stay local. Closing applies edits; Discard leaves the capture unchanged.", 12, true);
        DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        surface = new EditorSurface(journal.Current) { LayoutTransform = scale, Cursor = Cursors.Cross, Focusable = true };
        viewport = new ScrollViewer { Content = surface, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = new SolidColorBrush(Color.FromRgb(12, 15, 19)) };
        root.Children.Add(viewport);
        surface.MouseLeftButtonDown += (_, e) => { if (journal.Current.Marks.Length >= 500) { status.Text = "500 annotations reached. Apply and reopen to continue."; return; } start = Clamp(e.GetPosition(surface)); points.Clear(); points.Add(start); drawing = true; surface.CaptureMouse(); e.Handled = true; };
        surface.MouseMove += (_, e) =>
        {
            if (!drawing) return;
            var point = Clamp(e.GetPosition(surface)); if (Tool == EditTool.Pen && points.Count < 20000) points.Add(point);
            surface.Preview = Make(point); surface.InvalidateVisual();
        };
        surface.MouseLeftButtonUp += (_, e) =>
        {
            if (!drawing) return;
            drawing = false; surface.ReleaseMouseCapture(); surface.Preview = null;
            var end = Clamp(e.GetPosition(surface)); var annotation = Make(end);
            controller.Try(() => Commit(annotation)); e.Handled = true;
        };
        PreviewMouseWheel += (_, e) => { if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return; Zoom(scale.ScaleX * (e.Delta > 0 ? 1.15 : 1 / 1.15)); e.Handled = true; };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { if (drawing) { drawing = false; surface.ReleaseMouseCapture(); surface.Preview = null; surface.InvalidateVisual(); } else Close(); e.Handled = true; }
            if (Keyboard.Modifiers != ModifierKeys.Control || Keyboard.FocusedElement is TextBox) return;
            if (e.Key == Key.Z) Undo(); else if (e.Key == Key.Y) Redo(); else if (e.Key is Key.C or Key.S) controller.Run(ApplyCopy); else return;
            e.Handled = true;
        };
        Loaded += (_, _) => Fit();
        Closing += (_, _) =>
        {
            if (!discard && dirty && !controller.Exiting) controller.Try(() => { var image = Apply(); controller.Run(() => controller.CopyImage(image)); });
        };
        Closed += (_, _) => { foreach (var lease in leases) lease.Dispose(); };
    }
    private EditTool Tool => (EditTool)(tools.SelectedItem ?? EditTool.Arrow);
    private Point Clamp(Point p) => new(Math.Clamp(p.X, 0, journal.Current.Base.PixelWidth), Math.Clamp(p.Y, 0, journal.Current.Base.PixelHeight));
    private Annotation Make(Point end)
    {
        Color c; try { c = (Color)ColorConverter.ConvertFromString(color.Text); } catch (FormatException) { c = Colors.OrangeRed; }
        c.A = 255; // Redaction and annotations never inherit accidental transparent alpha.
        var width = double.TryParse(stroke.Text, CultureInfo.InvariantCulture, out var w) && double.IsFinite(w) ? Math.Clamp(w, 1, 30) : 3;
        var size = double.TryParse(textSize.Text, CultureInfo.InvariantCulture, out var t) && double.IsFinite(t) ? Math.Clamp(t, 8, 120) : 24;
        return new(Tool, start, end, c, width, size, Tool == EditTool.Number ? number.ToString(CultureInfo.InvariantCulture) : caption.Text, points.ToArray());
    }
    private void Commit(Annotation mark)
    {
        var area = PixelRect.Between((int)mark.Start.X, (int)mark.Start.Y, (int)mark.End.X, (int)mark.End.Y).Intersect(new(0, 0, journal.Current.Base.PixelWidth, journal.Current.Base.PixelHeight));
        if (mark.Tool == EditTool.OcrArea)
        {
            if (!area.IsEmpty) controller.Run(async () => await CopyOcr(ImageService.Crop(Render(journal.Current), area)));
            surface.InvalidateVisual(); return;
        }
        if (mark.Tool == EditTool.Crop)
        {
            if (area.Width < 2 || area.Height < 2) return;
            journal.Push(new(ImageService.Crop(Render(journal.Current), area), [])); Refresh(); Fit(); dirty = true; return;
        }
        if (mark.Tool is EditTool.Blur or EditTool.Pixelate)
        {
            if (area.Width < 2 || area.Height < 2) return;
            var patch = Effects.Apply(ImageService.Crop(Render(journal.Current), area), mark.Tool == EditTool.Pixelate);
            mark = mark with { Start = new(area.X, area.Y), End = new(area.Right, area.Bottom), Patch = patch };
        }
        if (mark.Tool == EditTool.Number) number++;
        journal.Push(journal.Current with { Marks = [.. journal.Current.Marks, mark] }); dirty = true; Refresh();
    }
    private void Undo() { journal.Undo(); dirty = true; Refresh(); }
    private void Redo() { journal.Redo(); dirty = true; Refresh(); }
    private void Refresh() { surface.State = journal.Current; surface.Width = journal.Current.Base.PixelWidth; surface.Height = journal.Current.Base.PixelHeight; surface.InvalidateVisual(); }
    private void Fit() => Zoom(Math.Min(1, Math.Min(Math.Max(200, viewport.ActualWidth - 24) / journal.Current.Base.PixelWidth, Math.Max(200, viewport.ActualHeight - 24) / journal.Current.Base.PixelHeight)));
    private void Zoom(double value) { scale.ScaleX = scale.ScaleY = Math.Clamp(value, 0.03, 8); status.Text = $"{scale.ScaleX:P0} · {journal.Current.Base.PixelWidth} × {journal.Current.Base.PixelHeight} · Esc closes and applies"; }
    private BitmapSource Apply()
    {
        var image = Render(journal.Current);
        controller.Repository.Replace(record, ImageService.Png(image), image.PixelWidth, image.PixelHeight);
        leases.Add(controller.Repository.Lease([record]));
        dirty = false; controller.Dock.Refresh(); return image;
    }
    private async Task ApplyCopy()
    {
        var image = dirty ? Apply() : ImageService.Load(controller.Repository.PathFor(record));
        await controller.CopyImage(image); status.Text = "Applied to shelf and copied to clipboard.";
    }
    private async Task CopyOcr(BitmapSource image)
    {
        status.Text = "Reading text locally…";
        var text = await controller.OcrService.ReadAsync(ImageService.Png(image));
        if (string.IsNullOrWhiteSpace(text)) { status.Text = "No text found."; return; }
        await controller.Clipboard.TextAsync(text); status.Text = "OCR text copied.";
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
                if (m.Points.Length == 1) dc.DrawEllipse(brush, null, m.Start, m.Stroke / 2, m.Stroke / 2);
                for (var i = 1; i < m.Points.Length; i++) dc.DrawLine(pen, m.Points[i - 1], m.Points[i]); break;
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
        public EditorState State { get; set; }
        public Annotation? Preview { get; set; }
        public EditorSurface(EditorState state) { State = state; Width = state.Base.PixelWidth; Height = state.Base.PixelHeight; }
        protected override void OnRender(DrawingContext dc)
        {
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
