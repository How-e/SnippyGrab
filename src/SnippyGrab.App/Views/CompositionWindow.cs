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
        var root = new StackPanel { Margin = new Thickness(24, 20, 24, 8) };
        var shell = new DockPanel(); Content = shell;
        var footer = new WrapPanel { Margin = new Thickness(24, 8, 24, 16) }; DockPanel.SetDock(footer, Dock.Bottom); shell.Children.Add(footer); shell.Children.Add(Ui.Scroll(root));
        root.Children.Add(Ui.Heading("Combine screenshots"));
        root.Children.Add(Ui.Text($"Arrange {inputs.Count} screenshots into one image. Choose a layout, check the preview, then open the result in the editor.", 14, true));
        root.Children.Add(Ui.Heading("1  Choose a layout", 18));
        var mode = new ComboBox { ItemsSource = new[] { "Vertical strip · top to bottom", "Horizontal strip · side by side", "Grid · rows and columns" }, SelectedIndex = 0 }; AutomationProperties.SetName(mode, "Composition layout"); root.Children.Add(mode);
        var layoutHelp = Ui.Text("Stack screenshots top to bottom in the order below.", 12, true); root.Children.Add(layoutHelp);
        root.Children.Add(Ui.Heading("2  Set the order", 18));
        root.Children.Add(Ui.Text("Select a screenshot below, then move it earlier or later. Grid order runs left to right, then down.", 12, true));
        var order = new ListBox { Height = 120 }; AutomationProperties.SetName(order, "Composition order"); root.Children.Add(order);
        void RefreshOrder() => order.ItemsSource = inputs.Select((i, n) =>
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new Image { Source = ImageService.Load(i.Path, 64, 40), Width = 64, Height = 40, Stretch = Stretch.Uniform, Margin = new Thickness(0, 2, 12, 2) });
            row.Children.Add(Ui.Text($"{n + 1} · Screenshot {selected.ToList().IndexOf(i) + 1} · {i.Width} × {i.Height}", 12));
            return row;
        }).ToArray();
        RefreshOrder(); order.SelectedIndex = 0;
        var actions = new WrapPanel(); root.Children.Add(actions);
        root.Children.Add(Ui.Heading("3  Review the result", 18));
        var options = new StackPanel();
        var advanced = new Expander { Header = Ui.Text("Spacing, alignment & background"), Content = options, Margin = new Thickness(0, 4, 0, 4) }; root.Children.Add(advanced);
        var spacing = new TextBox { Text = "0" }; AutomationProperties.SetName(spacing, "Spacing in pixels"); options.Children.Add(Ui.Text("Spacing in pixels (0–256)")); options.Children.Add(spacing);
        var columns = new TextBox { Text = "2" }; AutomationProperties.SetName(columns, "Grid columns"); var columnsLabel = Ui.Text("Screenshots per row"); options.Children.Add(columnsLabel); options.Children.Add(columns);
        options.Children.Add(Ui.Text("Alignment of different-sized screenshots"));
        var alignment = new ComboBox { ItemsSource = new[] { "Start · left / top", "Center", "End · right / bottom" }, SelectedIndex = 0 }; AutomationProperties.SetName(alignment, "Image alignment"); options.Children.Add(alignment);
        options.Children.Add(Ui.Text("Background between screenshots"));
        var background = new ComboBox { ItemsSource = new[] { "White", "Black", "Transparent" }, SelectedIndex = 0 }; AutomationProperties.SetName(background, "Composition background"); options.Children.Add(background);
        var preview = new Image { Height = 200, Stretch = Stretch.Uniform, Margin = new Thickness(0, 10, 0, 10) }; root.Children.Add(preview);
        var status = Ui.Text("", 12, true); root.Children.Add(status);
        root.Children.Add(Ui.Text("Creates a new PNG at original resolution. Your source screenshots and clipboard stay unchanged. You can annotate or export the result from the editor.", 12, true));
        var confirm = Ui.Button("Create image & edit", "Create a new PNG and open the editor", () => { if (Layout is not null) DialogResult = true; }); confirm.Style = (Style)FindResource("PrimaryButton");
        // Thumbnails are bounded independently of full-resolution input sizes.
        var thumbs = selected.ToDictionary(i => i.Path, i => ImageService.Load(i.Path, 160, 160));
        void Update()
        {
            try
            {
                columns.Visibility = columnsLabel.Visibility = mode.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
                if (mode.SelectedIndex == 2) advanced.IsExpanded = true;
                layoutHelp.Text = mode.SelectedIndex switch { 1 => "Place screenshots side by side in the order below.", 2 => "Fill each row from left to right, then start the next row.", _ => "Stack screenshots top to bottom in the order below." };
                Layout = Composition.Plan(inputs.Select(i => (i.Width, i.Height)).ToArray(), new((CompositionMode)mode.SelectedIndex, int.Parse(spacing.Text), mode.SelectedIndex == 2 ? int.Parse(columns.Text) : 2, (CompositionAlignment)alignment.SelectedIndex));
                BackgroundColor = background.SelectedIndex == 1 ? Colors.Black : background.SelectedIndex == 2 ? Colors.Transparent : Colors.White;
                var scale = Math.Min(600d / Layout.Width, 200d / Layout.Height); var visual = new DrawingVisual();
                using (var drawing = visual.RenderOpen())
                {
                    drawing.DrawRectangle(new SolidColorBrush(BackgroundColor), null, new Rect(0, 0, Math.Ceiling(Layout.Width * scale), Math.Ceiling(Layout.Height * scale)));
                    for (var i = 0; i < inputs.Count; i++) { var r = Layout.Rectangles[i]; drawing.DrawImage(thumbs[inputs[i].Path], new Rect(r.X * scale, r.Y * scale, r.Width * scale, r.Height * scale)); }
                }
                var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(Layout.Width * scale)), Math.Max(1, (int)Math.Ceiling(Layout.Height * scale)), 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); preview.Source = bitmap;
                status.Text = $"{Layout.Width} × {Layout.Height} pixels · estimated image memory {Layout.EstimatedBytes / 1048576d:F1} MiB. No resizing.";
                confirm.IsEnabled = true;
            }
            catch (Exception ex) when (ex is InvalidDataException or FormatException or OverflowException) { Layout = null; confirm.IsEnabled = false; status.Text = ex is FormatException or OverflowException ? "Enter a whole number for spacing (0–256) and grid columns (1 or more)." : ex.Message; preview.Source = null; }
        }
        void Move(int delta) { var from = order.SelectedIndex; var to = from + delta; if (from < 0 || to < 0 || to >= inputs.Count) return; (inputs[from], inputs[to]) = (inputs[to], inputs[from]); RefreshOrder(); order.SelectedIndex = to; Update(); }
        actions.Children.Add(Ui.Button("Move up", "Move selected input earlier", () => Move(-1))); actions.Children.Add(Ui.Button("Move down", "Move selected input later", () => Move(1)));
        mode.SelectionChanged += (_, _) => Update(); spacing.TextChanged += (_, _) => Update(); columns.TextChanged += (_, _) => Update(); alignment.SelectionChanged += (_, _) => Update(); background.SelectionChanged += (_, _) => Update();
        footer.Children.Add(confirm);
        var cancel = Ui.Button("Cancel", "Leave originals unchanged", () => DialogResult = false); cancel.IsCancel = true; footer.Children.Add(cancel); Update();
    }
}
