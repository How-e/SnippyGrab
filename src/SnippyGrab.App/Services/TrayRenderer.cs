using System.Drawing;
using System.Windows.Forms;
using SnippyGrab.App.Views;
using DrawingColor = System.Drawing.Color;

namespace SnippyGrab.App.Services;

internal sealed class TrayRenderer : ToolStripProfessionalRenderer
{
    internal static readonly System.Drawing.Bitmap Placeholder = new(18, 18);
    private static DrawingColor Color(string key)
    {
        var color = ((SolidColorBrush)Ui.Brush(key)).Color; return DrawingColor.FromArgb(color.A, color.R, color.G, color.B);
    }
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { using var brush = new SolidBrush(Color("Raised")); e.Graphics.FillRectangle(brush, e.AffectedBounds); }
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { using var brush = new SolidBrush(Color("Raised")); e.Graphics.FillRectangle(brush, e.AffectedBounds); }
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { using var pen = new System.Drawing.Pen(Color("Border")); e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1); }
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        using var brush = new SolidBrush(Color("Selected")); e.Graphics.FillRectangle(brush, new System.Drawing.Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2));
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = Color(e.Item.Enabled ? e.Item.Selected ? "SelectedInk" : "Ink" : "DisabledInk"); base.OnRenderItemText(e); }
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = Color(e.Item?.Enabled == true ? e.Item.Selected ? "SelectedInk" : "Muted" : "DisabledInk"); base.OnRenderArrow(e); }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) { using var pen = new System.Drawing.Pen(Color("Border")); e.Graphics.DrawLine(pen, 10, e.Item.Height / 2, e.Item.Width - 10, e.Item.Height / 2); }
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        using var pen = new System.Drawing.Pen(Color(e.Item.Selected ? "SelectedInk" : "Accent"), 2); var r = e.ImageRectangle;
        e.Graphics.DrawLines(pen, [new System.Drawing.Point(r.Left + 2, r.Top + 7), new System.Drawing.Point(r.Left + 6, r.Top + 11), new System.Drawing.Point(r.Left + 14, r.Top + 3)]);
    }
    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Item is ToolStripMenuItem { Checked: true }) return;
        if (e.Item.Tag is not string icon) { base.OnRenderItemImage(e); return; }
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) { dc.PushTransform(new ScaleTransform(18.0 / 24, 18.0 / 24)); dc.DrawGeometry(null, new System.Windows.Media.Pen(Ui.Brush(e.Item.Enabled ? e.Item.Selected ? "SelectedInk" : "Ink" : "DisabledInk"), 1.6), Geometry.Parse(Icons.Data(icon))); }
        var bitmap = new RenderTargetBitmap(18, 18, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var bytes = new MemoryStream(); encoder.Save(bytes); bytes.Position = 0;
        using var glyph = System.Drawing.Image.FromStream(bytes); e.Graphics.DrawImage(glyph, e.ImageRectangle);
    }
}
