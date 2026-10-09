using System.Windows.Data;
using System.Windows.Shapes;

namespace SnippyGrab.App.Views;

// A single 24-unit line vocabulary keeps every tool, menu and window consistent.
internal static class Icons
{
    internal static string Data(string name) => name switch
    {
        "appearance" => "M 12,3 C 5,3 2,7 2,12 C 2,18 6,21 10,21 C 13,21 13,18 11,17 C 10,16 11,14 14,14 L 17,14 C 23,14 23,3 12,3 M 7,8 L 7.1,8 M 12,6 L 12.1,6 M 17,8 L 17.1,8 M 6,13 L 6.1,13",
        "keyboard" => "M 3,5 L 21,5 L 21,19 L 3,19 Z M 6,9 L 7,9 M 10,9 L 11,9 M 14,9 L 15,9 M 18,9 L 18.1,9 M 6,12 L 7,12 M 10,12 L 11,12 M 14,12 L 15,12 M 18,12 L 18.1,12 M 7,16 L 17,16",
        "image" => "M 3,3 L 21,3 L 21,21 L 3,21 Z M 3,16 L 9,10 L 14,15 L 17,12 L 21,16 M 16,7 A 1.5,1.5 0 1 0 19,7 A 1.5,1.5 0 1 0 16,7",
        "copy" => "M 9,4 L 21,4 L 21,20 L 9,20 Z M 6,17 L 3,17 L 3,1 L 15,1",
        "clipboard" => "M 9,4 L 4,4 L 4,22 L 20,22 L 20,4 L 15,4 M 9,2 L 15,2 L 15,6 L 9,6 Z",
        "folder" => "M 2,6 L 2,21 L 22,21 L 22,8 L 12,8 L 9,4 L 2,4 Z",
        "settings" => "M 9,3 L 15,3 L 16,6 L 19,7 L 22,9 L 21,14 L 18,15 L 17,19 L 13,22 L 9,20 L 8,17 L 4,16 L 2,12 L 4,8 L 8,7 Z M 8,12 A 4,4 0 1 0 16,12 A 4,4 0 1 0 8,12",
        "capture" => "M 9,3 L 3,3 L 3,9 M 15,3 L 21,3 L 21,9 M 3,15 L 3,21 L 9,21 M 21,15 L 21,21 L 15,21",
        "window" => "M 3,4 L 21,4 L 21,20 L 3,20 Z M 3,8 L 21,8",
        "desktop" => "M 2,3 L 22,3 L 22,17 L 2,17 Z M 12,17 L 12,22 M 7,22 L 17,22",
        "edit" or "pen" => "M 3,17 L 17,3 L 21,7 L 7,21 L 3,21 Z M 14,6 L 18,10",
        "export" => "M 12,16 L 12,2 M 7,7 L 12,2 L 17,7 M 4,12 L 4,21 L 20,21 L 20,12",
        "pin" => "M 9,2 L 19,6 L 16,10 L 18,15 L 13,16 L 8,11 L 6,7 Z M 9,14 L 3,22",
        "detach" => "M 9,2 L 19,6 L 16,10 L 18,15 L 13,16 L 8,11 L 6,7 Z M 9,14 L 3,22 M 2,3 L 22,21",
        "history" => "M 3,12 A 9,9 0 1 0 12,3 A 9,9 0 0 0 3,12 M 12,7 L 12,12 L 16,14",
        "ocr" => "M 8,3 L 3,3 L 3,8 M 16,3 L 21,3 L 21,8 M 3,16 L 3,21 L 8,21 M 21,16 L 21,21 L 16,21 M 8,8 L 16,8 M 12,8 L 12,17",
        "arrow" => "M 3,3 L 20,11 L 13,13 L 11,20 Z",
        "rectangle" or "redact" => "M 3,5 L 21,5 L 21,19 L 3,19 Z",
        "ellipse" => "M 3,12 A 9,9 0 1 0 21,12 A 9,9 0 1 0 3,12",
        "line" => "M 3,21 L 21,3",
        "freehand" => "M 2,17 C 4,1 8,2 8,13 C 8,24 12,21 14,9 C 16,-1 20,3 21,11",
        "text" => "M 4,4 L 20,4 M 12,4 L 12,21 M 8,21 L 16,21",
        "highlight" => "M 3,17 L 16,4 L 21,9 L 8,22 Z M 4,15 L 10,21 M 2,22 L 12,22",
        "number" => "M 3,12 A 9,9 0 1 0 21,12 A 9,9 0 1 0 3,12 M 9,9 L 12,7 L 12,17 M 9,17 L 15,17",
        "blur" => "M 12,2 C 10,7 4,11 4,16 A 8,7 0 0 0 20,16 C 20,11 14,7 12,2 Z",
        "pixelate" => "M 3,3 L 9,3 L 9,9 L 3,9 Z M 15,3 L 21,3 L 21,9 L 15,9 Z M 9,9 L 15,9 L 15,15 L 9,15 Z M 3,15 L 9,15 L 9,21 L 3,21 Z M 15,15 L 21,15 L 21,21 L 15,21 Z",
        "spotlight" => "M 3,12 A 9,9 0 1 0 21,12 A 9,9 0 1 0 3,12 M 9,12 A 3,3 0 1 0 15,12 A 3,3 0 1 0 9,12",
        "crop" => "M 6,2 L 6,18 L 22,18 M 2,6 L 18,6 L 18,22",
        "color" => "M 4,17 L 16,5 L 20,9 L 8,21 L 3,22 Z M 14,3 L 22,11",
        "zoom" => "M 3,10 A 7,7 0 1 0 17,10 A 7,7 0 1 0 3,10 M 15,15 L 22,22",
        "undo" => "M 8,3 L 3,8 L 8,13 M 3,8 L 15,8 A 6,6 0 0 1 15,20",
        "redo" => "M 16,3 L 21,8 L 16,13 M 21,8 L 9,8 A 6,6 0 0 0 9,20",
        "close" => "M 5,5 L 19,19 M 19,5 L 5,19",
        "trash" => "M 3,6 L 21,6 M 6,6 L 7,22 L 17,22 L 18,6 M 9,6 L 9,2 L 15,2 L 15,6 M 10,10 L 10,18 M 14,10 L 14,18",
        "more" => "M 4,12 L 4.1,12 M 12,12 L 12.1,12 M 20,12 L 20.1,12",
        "pause" => "M 7,3 L 7,21 M 17,3 L 17,21",
        "info" => "M 3,12 A 9,9 0 1 0 21,12 A 9,9 0 1 0 3,12 M 12,10 L 12,17 M 12,7 L 12.1,7",
        "check" => "M 4,12 L 10,18 L 21,5",
        "left" => "M 14,4 L 6,12 L 14,20 M 6,12 L 22,12",
        "right" => "M 10,4 L 18,12 L 10,20 M 18,12 L 2,12",
        "fit" => "M 8,2 L 2,2 L 2,8 M 16,2 L 22,2 L 22,8 M 2,16 L 2,22 L 8,22 M 22,16 L 22,22 L 16,22",
        "startup" => "M 7,3 L 7,21 L 21,12 Z",
        _ => "M 3,5 L 21,5 L 21,19 L 3,19 Z"
    };
    public static FrameworkElement Make(string name, double size = 20)
    {
        var path = new System.Windows.Shapes.Path { Data = Geometry.Parse(Data(name)), StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Stretch = Stretch.None, IsHitTestVisible = false };
        path.SetBinding(Shape.StrokeProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1), FallbackValue = Ui.Brush("Ink") });
        var canvas = new Canvas { Width = 24, Height = 24 }; canvas.Children.Add(path);
        return new Viewbox { Child = canvas, Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
    }
}
