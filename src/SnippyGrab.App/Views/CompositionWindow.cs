using System.Windows.Automation;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal sealed record CompositionInput(string Path, int Width, int Height);
internal sealed class CompositionWindow : Window
{
    internal IReadOnlyList<CompositionInput> Inputs => inputs;
    internal CompositionLayout? Layout { get; private set; }
    internal Color BackgroundColor { get; private set; } = Colors.White;
    private readonly List<CompositionInput> inputs;
    internal CompositionWindow(IReadOnlyList<CompositionInput> selected, Window? owner)
    {
        if (selected.Count is < 2 or > 20) throw new InvalidDataException("Select between 2 and 20 captures to combine.");
        inputs = selected.ToList(); Ui.StyleWindow(this); Title = "SnippyGrab · Combine selected"; Width = 720; Height = 700; MinWidth = 520; MinHeight = 450;
        if (owner is not null) Owner = owner;
        var root = new StackPanel { Margin = new Thickness(24) }; Content = Ui.Scroll(root); root.Children.Add(Ui.Heading("Combine selected"));
        var order = new ListBox { Height = 120 }; AutomationProperties.SetName(order, "Composition order"); root.Children.Add(order);
        void RefreshOrder() => order.ItemsSource = inputs.Select((i, n) => $"{n + 1} · {i.Width} × {i.Height}").ToArray();
        RefreshOrder(); order.SelectedIndex = 0;
        var actions = new WrapPanel(); root.Children.Add(actions);
        var mode = new ComboBox { ItemsSource = Enum.GetValues<CompositionMode>(), SelectedIndex = 0 }; AutomationProperties.SetName(mode, "Composition layout"); root.Children.Add(mode);
        var spacing = new TextBox { Text = "0" }; AutomationProperties.SetName(spacing, "Spacing in pixels"); root.Children.Add(Ui.Text("Spacing in pixels (0–256)")); root.Children.Add(spacing);
        var columns = new TextBox { Text = "2" }; AutomationProperties.SetName(columns, "Grid columns"); root.Children.Add(Ui.Text("Grid columns")); root.Children.Add(columns);
        var alignment = new ComboBox { ItemsSource = Enum.GetValues<CompositionAlignment>(), SelectedIndex = 0 }; AutomationProperties.SetName(alignment, "Image alignment"); root.Children.Add(alignment);
        var background = new ComboBox { ItemsSource = new[] { "White", "Black", "Transparent" }, SelectedIndex = 0 }; AutomationProperties.SetName(background, "Composition background"); root.Children.Add(background);
        var preview = new Image { Height = 200, Stretch = Stretch.Uniform, Margin = new Thickness(0, 10, 0, 10) }; root.Children.Add(preview);
        var status = Ui.Text("Native pixels are preserved. Originals remain unchanged.", 12, true); root.Children.Add(status);
        // Thumbnails are bounded independently of full-resolution input sizes.
        var thumbs = selected.ToDictionary(i => i.Path, i => ImageService.Load(i.Path, 160, 160));
        void Update()
        {
            try
            {
                Layout = Composition.Plan(inputs.Select(i => (i.Width, i.Height)).ToArray(), new((CompositionMode)mode.SelectedItem, int.Parse(spacing.Text), int.Parse(columns.Text), (CompositionAlignment)alignment.SelectedItem));
                BackgroundColor = background.SelectedIndex == 1 ? Colors.Black : background.SelectedIndex == 2 ? Colors.Transparent : Colors.White;
                var scale = Math.Min(600d / Layout.Width, 200d / Layout.Height); var visual = new DrawingVisual();
                using (var drawing = visual.RenderOpen())
                {
                    drawing.DrawRectangle(new SolidColorBrush(BackgroundColor), null, new Rect(0, 0, 600, 200));
                    for (var i = 0; i < inputs.Count; i++) { var r = Layout.Rectangles[i]; drawing.DrawImage(thumbs[inputs[i].Path], new Rect(r.X * scale, r.Y * scale, r.Width * scale, r.Height * scale)); }
                }
                var bitmap = new RenderTargetBitmap(600, 200, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); preview.Source = bitmap;
                status.Text = $"{Layout.Width} × {Layout.Height} pixels · estimated image memory {Layout.EstimatedBytes / 1048576d:F1} MiB. No resizing.";
            }
            catch (Exception ex) when (ex is InvalidDataException or FormatException or OverflowException) { Layout = null; status.Text = ex.Message; preview.Source = null; }
        }
        void Move(int delta) { var from = order.SelectedIndex; var to = from + delta; if (from < 0 || to < 0 || to >= inputs.Count) return; (inputs[from], inputs[to]) = (inputs[to], inputs[from]); RefreshOrder(); order.SelectedIndex = to; Update(); }
        actions.Children.Add(Ui.Button("Move up", "Move selected input earlier", () => Move(-1))); actions.Children.Add(Ui.Button("Move down", "Move selected input later", () => Move(1)));
        mode.SelectionChanged += (_, _) => Update(); spacing.TextChanged += (_, _) => Update(); columns.TextChanged += (_, _) => Update(); alignment.SelectionChanged += (_, _) => Update(); background.SelectionChanged += (_, _) => Update();
        var confirm = Ui.Button("Combine", "Create a new PNG and open the editor", () => { Update(); if (Layout is not null) DialogResult = true; }); root.Children.Add(confirm);
        var cancel = Ui.Button("Cancel", "Leave originals unchanged", () => DialogResult = false); cancel.IsCancel = true; root.Children.Add(cancel); Update();
    }
}
