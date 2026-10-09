using System.Windows.Automation;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal sealed class ScrollCaptureWindow : Window
{
    private readonly ScrollSession session;
    private readonly CancellationTokenSource cancellation = new();
    private bool busy, finished;
    private readonly List<Button> commands = [];
    internal ScrollCaptureWindow(ScrollSession prepared, BitmapSource first, Func<Task<BitmapSource>> captureNext, Action<BitmapSource, byte[]> commit)
    {
        session = prepared;
        int frameWidth = first.PixelWidth, frameHeight = first.PixelHeight;
        Ui.StyleWindow(this); Title = "SnippyGrab · Assisted scrolling"; Width = 760; Height = 720; MinWidth = 520; MinHeight = 440;
        var root = new StackPanel { Margin = new Thickness(24) }; Content = Ui.Scroll(root); root.Children.Add(Ui.Heading("Assisted scrolling"));
        root.Children.Add(Ui.Text("Scroll the selected application yourself, then capture the next frame. Static vertical content only. Cursor is omitted. No automatic scrolling.", 14, true));
        var top = new TextBox { Text = "0" }; var bottom = new TextBox { Text = "0" };
        AutomationProperties.SetName(top, "Sticky header trim in pixels"); AutomationProperties.SetName(bottom, "Sticky footer trim in pixels");
        root.Children.Add(Ui.Text("Trim sticky header / footer (pixels). Locked after capturing the next frame.")); root.Children.Add(top); root.Children.Add(bottom);
        var preview = new Image { Height = 250, Stretch = Stretch.Uniform }; root.Children.Add(preview);
        var status = Ui.Text("First frame captured. Preview trims, scroll the target and choose Capture next.", 14); root.Children.Add(status);
        var overlap = new Slider { Minimum = 0, Maximum = Math.Max(1, frameHeight - 1), IsSnapToTickEnabled = true, TickFrequency = 1, Value = 0 };
        AutomationProperties.SetName(overlap, "Seam overlap in pixels"); root.Children.Add(Ui.Text("Overlap in pixels (adjust to correct the seam)")); root.Children.Add(overlap);
        var overlapLabel = Ui.Text("0 pixels"); root.Children.Add(overlapLabel);
        ScrollMatch? match = null; int trimTop = 0, trimBottom = 0;
        int FrameHeight() => frameHeight - trimTop - trimBottom;
        void ReadTrim()
        {
            trimTop = int.Parse(top.Text); trimBottom = int.Parse(bottom.Text);
            if (trimTop < 0 || trimBottom < 0 || FrameHeight() < 32) throw new InvalidDataException("Trims must leave at least 32 pixels of viewport height.");
        }
        void Preview()
        {
            var previous = ImageService.Load(session.Frames[^1], 600, 240);
            var next = session.Pending is null ? null : ImageService.Load(session.Pending, 600, 240);
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, 600, 240));
                if (next is null)
                {
                    var scale = previous.PixelHeight / (double)frameHeight;
                    var y = (int)Math.Round(trimTop * scale); var height = Math.Max(1, previous.PixelHeight - y - (int)Math.Round(trimBottom * scale));
                    drawing.DrawImage(ImageService.Crop(previous, new(0, y, previous.PixelWidth, height)), new Rect(0, 0, 600, 240));
                }
                else
                {
                    // Show the previous tail and the proposed new content at the join.
                    var scale = next.PixelHeight / (double)frameHeight; var skip = (int)Math.Round((trimTop + overlap.Value) * scale);
                    var tail = Math.Max(1, previous.PixelHeight / 3);
                    drawing.DrawImage(ImageService.Crop(previous, new(0, Math.Max(0, previous.PixelHeight - (int)Math.Round(trimBottom * scale) - tail), previous.PixelWidth, tail)), new Rect(0, 0, 600, 100));
                    var height = Math.Max(1, next.PixelHeight - skip - (int)Math.Round(trimBottom * scale));
                    drawing.DrawImage(ImageService.Crop(next, new(0, Math.Min(skip, next.PixelHeight - 1), next.PixelWidth, Math.Min(height, next.PixelHeight - Math.Min(skip, next.PixelHeight - 1)))), new Rect(0, 100, 600, 140));
                }
            }
            var bitmap = new RenderTargetBitmap(600, 240, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); preview.Source = bitmap;
        }
        top.TextChanged += (_, _) => { if (!top.IsEnabled) return; try { ReadTrim(); Preview(); } catch (Exception ex) when (ex is FormatException or OverflowException or InvalidDataException) { status.Text = ex.Message; } };
        bottom.TextChanged += (_, _) => { if (!bottom.IsEnabled) return; try { ReadTrim(); Preview(); } catch (Exception ex) when (ex is FormatException or OverflowException or InvalidDataException) { status.Text = ex.Message; } };
        overlap.ValueChanged += (_, _) => { overlapLabel.Text = $"{(int)overlap.Value} pixels"; if (session.Pending is not null) Preview(); };
        var actions = new WrapPanel(); root.Children.Add(actions);
        void Command(string label, Func<Task> action)
        {
            var button = Ui.Button(label, label, () => Run(action)); commands.Add(button); actions.Children.Add(button);
        }
        async void Run(Func<Task> action)
        {
            if (busy || finished) return; busy = true; foreach (var b in commands) b.IsEnabled = false;
            try { cancellation.Token.ThrowIfCancellationRequested(); await action(); }
            catch (OperationCanceledException) { finished = true; }
            catch (Exception ex) { status.Text = ex is InvalidOperationException or InvalidDataException ? ex.Message : OperationFailure.From(ex).Message; }
            finally
            {
                busy = false; foreach (var b in commands) b.IsEnabled = !finished;
                if (finished || cancellation.IsCancellationRequested) { finished = true; Close(); }
            }
        }
        Command("Capture next", async () =>
        {
            ReadTrim(); session.CheckBudget(); if (session.Pending is not null) throw new InvalidOperationException("Accept or reject the pending seam first.");
            top.IsEnabled = bottom.IsEnabled = false;
            BitmapSource image;
            Hide();
            try { image = await captureNext(); }
            catch { cancellation.Cancel(); throw; }
            finally { if (!cancellation.IsCancellationRequested) Show(); }
            cancellation.Token.ThrowIfCancellationRequested();
            await Task.Run(() => session.Stage(image), cancellation.Token);
            match = await Task.Run(() =>
            {
                byte[] Pixels(string path)
                {
                    var source = ImageService.Load(path); var cropped = ImageService.Crop(source, new(0, trimTop, source.PixelWidth, source.PixelHeight - trimTop - trimBottom));
                    var converted = new FormatConvertedBitmap(cropped, PixelFormats.Bgra32, null, 0); var pixels = new byte[checked(converted.PixelWidth * converted.PixelHeight * 4)]; converted.CopyPixels(pixels, converted.PixelWidth * 4, 0); return pixels;
                }
                return ScrollOverlap.Match(Pixels(session.Frames[^1]), Pixels(session.Pending!), frameWidth, FrameHeight(), cancellation.Token);
            }, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (match.Duplicate) { session.Reject(); status.Text = "No progression: exact duplicate frame rejected. Scroll further or Finish."; Preview(); return; }
            overlap.Maximum = FrameHeight() - 1; overlap.Value = match.Overlap ?? FrameHeight() / 2;
            status.Text = match.Overlap is null ? "Uncertain seam. Adjust overlap and review; accepting requires confirmation." : $"Suggested overlap: {match.Overlap} pixels. Review the seam before accepting."; Preview();
        });
        Command("Accept join", () =>
        {
            if (session.Pending is null) throw new InvalidOperationException("Capture the next frame first.");
            var chosen = (int)overlap.Value;
            if ((match?.Overlap is null || chosen != match.Overlap) && !DialogWindow.Confirm("Accept manual seam?", "Alignment is uncertain or manually adjusted. Confirm that the preview contains no missing or duplicate rows.", "Accept seam", this)) return Task.CompletedTask;
            session.Accept(chosen, FrameHeight()); match = null; status.Text = $"{session.Frames.Count} frames accepted. Scroll further or Finish."; Preview(); return Task.CompletedTask;
        });
        Command("Reject frame", () => { session.Reject(); match = null; status.Text = "Pending frame rejected. Scroll position is unchanged by SnippyGrab."; Preview(); return Task.CompletedTask; });
        Command("Remove last join", () => { session.Undo(); match = null; status.Text = "Last join removed. Restore the target's scroll position yourself before recapturing."; Preview(); return Task.CompletedTask; });
        Command("Finish", async () =>
        {
            ReadTrim(); if (!DialogWindow.Confirm("Finish scrolling capture?", "Review the accepted seams. The result is a new PNG; uncertain joins remain your explicit choices.", "Create PNG", this)) return;
            var image = await Task.Run(() => session.Build(trimTop, trimBottom, cancellation.Token), cancellation.Token);
            var png = await Task.Run(() => ImageService.Png(image), cancellation.Token); cancellation.Token.ThrowIfCancellationRequested(); commit(image, png); finished = true;
        });
        var cancel = Ui.Button("Cancel session", "Discard all staged frames", () => { cancellation.Cancel(); if (!busy) { finished = true; Close(); } }); actions.Children.Add(cancel);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { cancellation.Cancel(); if (!busy) { finished = true; Close(); } e.Handled = true; } };
        Closing += (_, e) => { cancellation.Cancel(); if (busy) e.Cancel = true; };
        Closed += (_, _) => session.Dispose(); Preview();
    }
    internal void CancelSession() { cancellation.Cancel(); if (!busy) Close(); }
}
