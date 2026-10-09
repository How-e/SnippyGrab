using System.Windows.Automation;
using Microsoft.Win32;

namespace SnippyGrab.App.Views;

internal sealed record ExportRequest(string Path, ExportFormat Format, int Quality, ExportCollision Collision = ExportCollision.Unique);
internal sealed class ExportWindow : Window
{
    internal ExportRequest? Request { get; private set; }
    internal ExportWindow(CaptureRecord record, string directory, string cache, Window? owner, IReadOnlyList<CaptureRecord>? batch = null)
    {
        Ui.StyleWindow(this); Title = "SnippyGrab · Export image"; Width = 560; Height = 360; MinWidth = 440; MinHeight = 320;
        if (owner is not null) Owner = owner;
        WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new Thickness(24) }; Content = Ui.Scroll(root);
        root.Children.Add(Ui.Heading("Export image"));
        if (batch is not null) root.Children.Add(Ui.Text($"{batch.Count} captures in shelf order:\n" + string.Join("\n", batch.Select((c, i) => $"{i + 1:D3} · {c.Width} × {c.Height}")), 12, true));
        var collision = new ComboBox { ItemsSource = new[] { "Keep both (unique filenames)", "Skip existing files", "Replace existing files" }, SelectedIndex = 0 };
        AutomationProperties.SetName(collision, "Existing file policy"); if (batch is not null) root.Children.Add(collision);
        var format = new ComboBox { ItemsSource = new[] { "PNG · lossless (default)", "JPEG · smaller, lossy" }, SelectedIndex = 0, Margin = new Thickness(0, 16, 0, 12) };
        AutomationProperties.SetName(format, "Export format"); root.Children.Add(format);
        var qualityLabel = Ui.Text("JPEG quality: 90", 14);
        var quality = new Slider { Minimum = 1, Maximum = 100, Value = 90, TickFrequency = 1, IsSnapToTickEnabled = true, IsEnabled = false };
        AutomationProperties.SetName(quality, "JPEG quality"); quality.ValueChanged += (_, _) => qualityLabel.Text = $"JPEG quality: {(int)quality.Value}";
        root.Children.Add(qualityLabel); root.Children.Add(quality);
        var note = Ui.Text("PNG preserves original bytes. JPEG flattens transparency onto white and can soften text.", 12, true); root.Children.Add(note);
        format.SelectionChanged += (_, _) => quality.IsEnabled = format.SelectedIndex == 1;
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = Ui.Button("Cancel", "Cancel export", () => DialogResult = false); cancel.IsCancel = true; actions.Children.Add(cancel);
        var choose = Ui.Button("Choose destination…", "Choose export filename and confirm overwrite", () =>
        {
            var selected = format.SelectedIndex == 0 ? ExportFormat.Png : ExportFormat.Jpeg;
            if (batch is not null)
            {
                var folder = new OpenFolderDialog { Title = "Export selected captures", InitialDirectory = directory };
                if (folder.ShowDialog(this) == true) { Request = new(folder.FolderName, selected, (int)quality.Value, (ExportCollision)collision.SelectedIndex); DialogResult = true; }
                return;
            }
            var extension = selected == ExportFormat.Png ? ".png" : ".jpg";
            var dialog = new SaveFileDialog { Title = "Export " + selected + " outside the managed cache", Filter = selected == ExportFormat.Png ? "PNG image|*.png" : "JPEG image|*.jpg;*.jpeg", DefaultExt = extension, FileName = SuggestedFileName(record, selected), InitialDirectory = directory, AddExtension = true, OverwritePrompt = true };
            dialog.FileOk += (_, e) => { try { CaptureExport.ValidateDestination(cache, dialog.FileName, selected); } catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException) { e.Cancel = true; note.Text = ex.Message; MessageBox.Show(this, ex.Message, "Choose an export location", MessageBoxButton.OK, MessageBoxImage.Information); } };
            if (dialog.ShowDialog(this) != true) return;
            Request = new(dialog.FileName, selected, (int)quality.Value); DialogResult = true;
        }); choose.IsDefault = true; actions.Children.Add(choose); root.Children.Add(actions);
    }
    internal static string SuggestedFileName(CaptureRecord record, ExportFormat format)
    {
        var name = string.IsNullOrWhiteSpace(record.ExportPath) ? $"SnippyGrab-{record.CreatedUtc.LocalDateTime:yyyyMMdd-HHmmss}" : Path.GetFileNameWithoutExtension(record.ExportPath);
        return name + (format == ExportFormat.Png ? ".png" : ".jpg");
    }
}
