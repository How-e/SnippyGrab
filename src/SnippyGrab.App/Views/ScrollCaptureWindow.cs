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
        var root = new StackPanel { Margin = new Thickness(24, 20, 24, 8) };
        var shell = new DockPanel(); Content = shell;
        var actions = new WrapPanel { Margin = new Thickness(24, 8, 24, 16) }; DockPanel.SetDock(actions, Dock.Bottom); shell.Children.Add(actions); shell.Children.Add(Ui.Scroll(root));
        root.Children.Add(Ui.Heading("Build a scrolling screenshot"));
        root.Children.Add(Ui.Text("Your first frame is ready. You scroll the page; SnippyGrab joins the overlapping frames into one image.", 14, true));
        var step = Ui.Heading("1  Scroll the page down", 20); root.Children.Add(step);
        var guidance = Ui.Text("Return to the selected application and scroll about half a screen. Leave some of the previous content visible, then return here and click Capture next frame."); root.Children.Add(guidance);
        var progress = Ui.Text("1 frame saved · no screenshot created yet", 12, true); root.Children.Add(progress);
        var tips = new Expander { Header = Ui.Text("How it works & supported content"), Margin = new Thickness(0, 8, 0, 8) };
        tips.Content = Ui.Text("1. Scroll down about half a screen, keeping an overlap.\n2. Capture the next frame and check the join preview.\n3. Keep the frame, then repeat. Create screenshot opens the result in the editor.\n\nUse static vertical pages or text at 100% zoom. Animated/live content and spreadsheets are outside this workflow. Keep the target window and display scaling unchanged. SnippyGrab never scrolls for you; the cursor is omitted. Cancel discards this session without changing your existing screenshots.", 12, true); root.Children.Add(tips);
        var trims = new StackPanel(); var trimOptions = new Expander { Header = Ui.Text("Optional: hide a fixed header or footer"), Content = trims, Margin = new Thickness(0, 0, 0, 8) }; root.Children.Add(trimOptions);
        trims.Children.Add(Ui.Text("If a toolbar stays visible while the page scrolls, exclude its height from every frame. Check the preview; these values lock after the next capture.", 12, true));
        var top = new TextBox { Text = "0" }; var bottom = new TextBox { Text = "0" };
        AutomationProperties.SetName(top, "Sticky header trim in pixels"); AutomationProperties.SetName(bottom, "Sticky footer trim in pixels");
        trims.Children.Add(Ui.Text("Exclude from top (pixels)")); trims.Children.Add(top); trims.Children.Add(Ui.Text("Exclude from bottom (pixels)")); trims.Children.Add(bottom);
        var previewTitle = Ui.Heading("Selected content", 16); root.Children.Add(previewTitle);
        var preview = new Image { Height = 250, Stretch = Stretch.Uniform }; root.Children.Add(preview);
        var status = Ui.Text("First frame captured. Scroll down, leaving some repeated content visible.", 14); root.Children.Add(status);
        var overlap = new Slider { Minimum = 0, Maximum = Math.Max(1, frameHeight - 1), IsSnapToTickEnabled = true, TickFrequency = 1, Value = 0 };
        var seamControls = new StackPanel { Visibility = Visibility.Collapsed };
        seamControls.Children.Add(Ui.Text("The join sits between the upper and lower preview. Look for repeated or missing lines. If needed, adjust how much repeated content is removed from the new frame.", 12, true));
        AutomationProperties.SetName(overlap, "Seam overlap in pixels"); seamControls.Children.Add(Ui.Text("Repeated content to remove (pixels)")); seamControls.Children.Add(overlap); root.Children.Add(seamControls);
        var overlapLabel = Ui.Text("0 pixels"); seamControls.Children.Add(overlapLabel);
        ScrollMatch? match = null; int trimTop = 0, trimBottom = 0;
        int FrameHeight() => frameHeight - trimTop - trimBottom;
        void ReadTrim()
        {
            if (!int.TryParse(top.Text, out var nextTop) || !int.TryParse(bottom.Text, out var nextBottom)) throw new InvalidDataException("Enter whole pixel counts for the top and bottom exclusions.");
            ScrollOverlap.TrimmedHeight(frameHeight, nextTop, nextBottom);
            trimTop = nextTop; trimBottom = nextBottom;
        }
        void Preview()
        {
            var previous = ImageService.Load(session.Frames[^1], 600, 240);
            var scale = previous.PixelHeight / (double)frameHeight;
            var start = Math.Min(previous.PixelHeight - 1, (int)Math.Round(trimTop * scale));
            var end = Math.Min(previous.PixelHeight, Math.Max(start + 1, previous.PixelHeight - (int)Math.Round(trimBottom * scale)));
            if (session.Pending is null)
            {
                preview.Source = ImageService.Crop(previous, new(0, start, previous.PixelWidth, end - start)); return;
            }
            var next = ImageService.Load(session.Pending, 600, 240);
            var skip = Math.Min(next.PixelHeight - 1, (int)Math.Round((trimTop + overlap.Value) * scale));
            var tail = Math.Min(120, end - start);
            var head = Math.Min(120, Math.Max(1, next.PixelHeight - skip - (int)Math.Round(trimBottom * scale)));
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawImage(ImageService.Crop(previous, new(0, end - tail, previous.PixelWidth, tail)), new Rect(0, 0, previous.PixelWidth, tail));
                drawing.DrawImage(ImageService.Crop(next, new(0, skip, next.PixelWidth, head)), new Rect(0, tail, next.PixelWidth, head));
            }
            var bitmap = new RenderTargetBitmap(previous.PixelWidth, tail + head, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); preview.Source = bitmap;
        }
        top.TextChanged += (_, _) => { if (!top.IsEnabled) return; try { ReadTrim(); Preview(); status.Text = "Exclusions previewed. Scroll down, leaving some repeated content visible."; } catch (Exception ex) when (ex is FormatException or OverflowException or InvalidDataException) { status.Text = ex.Message; } };
        bottom.TextChanged += (_, _) => { if (!bottom.IsEnabled) return; try { ReadTrim(); Preview(); status.Text = "Exclusions previewed. Scroll down, leaving some repeated content visible."; } catch (Exception ex) when (ex is FormatException or OverflowException or InvalidDataException) { status.Text = ex.Message; } };
        overlap.ValueChanged += (_, _) => { overlapLabel.Text = $"{(int)overlap.Value} pixels"; if (session.Pending is not null) Preview(); };
        void RefreshCommands()
        {
            var pending = session.Pending is not null;
            seamControls.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
            previewTitle.Text = pending ? "Check the join · previous frame above, new content below" : "Selected content";
            step.Text = pending ? "2  Check and keep this frame" : "1  Scroll the page down";
            guidance.Text = pending ? "Check that each line appears once. Keep this frame if the join looks correct, or discard it and capture again." : "Return to the selected application and scroll about half a screen. Leave some of the previous content visible, then return here and click Capture next frame.";
            progress.Text = $"{session.Frames.Count} frame{(session.Frames.Count == 1 ? "" : "s")} saved{(pending ? " · 1 awaiting review" : "")} · Create screenshot opens the editor";
            foreach (var b in commands) b.Visibility = (b.Content as string) switch
            {
                "Keep this frame" or "Discard this frame" => pending ? Visibility.Visible : Visibility.Collapsed,
                "Capture next frame" => pending ? Visibility.Collapsed : Visibility.Visible,
                "Undo last frame" => session.Frames.Count > 1 ? Visibility.Visible : Visibility.Collapsed,
                _ => Visibility.Visible
            };
            foreach (var b in commands) b.IsEnabled = !busy && !finished && ((b.Content as string) switch
            {
                "Capture next frame" => !pending,
                "Keep this frame" or "Discard this frame" => pending,
                "Undo last frame" => !pending && session.Frames.Count > 1,
                "Create screenshot" => !pending,
                _ => true
            });
        }
        void Command(string label, Func<Task> action, bool primary = false)
        {
            var button = Ui.Button(label, label, () => Run(action)); commands.Add(button); actions.Children.Add(button);
            if (primary) button.Style = (Style)FindResource("PrimaryButton");
        }
        async void Run(Func<Task> action)
        {
            if (busy || finished) return; busy = true; foreach (var b in commands) b.IsEnabled = false;
            try { cancellation.Token.ThrowIfCancellationRequested(); await action(); }
            catch (OperationCanceledException) { finished = true; }
            catch (Exception ex) { status.Text = ex is InvalidOperationException or InvalidDataException ? ex.Message : OperationFailure.From(ex).Message; }
            finally
            {
                busy = false; RefreshCommands();
                if (finished || cancellation.IsCancellationRequested) { finished = true; Close(); }
            }
        }
        Command("Capture next frame", async () =>
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
            if (match.Duplicate) { session.Reject(); status.Text = "The page has not moved: identical frame discarded. Scroll farther or choose Create screenshot."; Preview(); return; }
            overlap.Maximum = FrameHeight() - 1; overlap.Value = match.Overlap ?? FrameHeight() / 2;
            status.Text = match.Overlap is null ? "Could not find a reliable join. Adjust the repeated-content slider until lines match, or discard this frame and scroll a shorter distance." : $"Matched {match.Overlap} pixels of repeated content. Check the preview, then keep this frame."; Preview();
        }, true);
        Command("Keep this frame", () =>
        {
            if (session.Pending is null) throw new InvalidOperationException("Capture the next frame first.");
            var chosen = (int)overlap.Value;
            if ((match?.Overlap is null || chosen != match.Overlap) && !DialogWindow.Confirm("Keep this adjusted frame?", "Check the join preview carefully. Keep this frame only if each line appears once, with nothing missing.", "Keep frame", this)) return Task.CompletedTask;
            session.Accept(chosen, FrameHeight()); match = null; status.Text = $"{session.Frames.Count} frames saved. Scroll down again or choose Create screenshot."; Preview(); return Task.CompletedTask;
        }, true);
        Command("Discard this frame", () => { session.Reject(); match = null; status.Text = "Frame discarded. Scroll back or adjust the page before capturing again."; Preview(); return Task.CompletedTask; });
        Command("Undo last frame", () => { session.Undo(); match = null; status.Text = "Last frame removed. Scroll back to the previous position before recapturing."; Preview(); return Task.CompletedTask; });
        Command("Create screenshot", async () =>
        {
            ReadTrim(); if (!DialogWindow.Confirm("Create scrolling screenshot?", "Your saved frames will become one new image and open in the editor. Your existing screenshots and clipboard stay unchanged.", "Create screenshot", this)) return;
            var image = await Task.Run(() => session.Build(trimTop, trimBottom, cancellation.Token), cancellation.Token);
            var png = await Task.Run(() => ImageService.Png(image), cancellation.Token); cancellation.Token.ThrowIfCancellationRequested(); commit(image, png); finished = true;
        });
        var cancel = Ui.Button("Cancel session", "Discard all staged frames", () => { cancellation.Cancel(); if (!busy) { finished = true; Close(); } }); actions.Children.Add(cancel);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { cancellation.Cancel(); if (!busy) { finished = true; Close(); } e.Handled = true; } };
        Closing += (_, e) => { cancellation.Cancel(); if (busy) e.Cancel = true; };
        Closed += (_, _) => session.Dispose(); Preview(); RefreshCommands();
    }
    internal void CancelSession() { cancellation.Cancel(); if (!busy) Close(); }
}
