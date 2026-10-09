namespace SnippyGrab.Core;

public enum CompositionMode { Vertical, Horizontal, Grid }
public enum CompositionAlignment { Start, Center, End }
public sealed record CompositionOptions(CompositionMode Mode = CompositionMode.Vertical, int Spacing = 0, int Columns = 2, CompositionAlignment Alignment = CompositionAlignment.Start);
public sealed record CompositionLayout(int Width, int Height, IReadOnlyList<PixelRect> Rectangles, long EstimatedBytes);
public static class Composition
{
    public const long MemoryBudget = 256L * 1024 * 1024;
    public static CompositionLayout Plan(IReadOnlyList<(int Width, int Height)> images, CompositionOptions options)
    {
        if (images.Count is < 1 or > 30 || images.Any(i => i.Width <= 0 || i.Height <= 0) || options.Spacing is < 0 or > 256 || options.Columns < 1 || !Enum.IsDefined(options.Mode) || !Enum.IsDefined(options.Alignment)) throw new InvalidDataException("Invalid image composition settings.");
        try
        {
            checked
            {
                int maxWidth = images.Max(i => i.Width), maxHeight = images.Max(i => i.Height);
                int columns = Math.Min(options.Columns, images.Count), rows = (images.Count + columns - 1) / columns;
                int width = options.Mode == CompositionMode.Horizontal ? images.Sum(i => i.Width) + options.Spacing * (images.Count - 1) : options.Mode == CompositionMode.Grid ? maxWidth * columns + options.Spacing * (columns - 1) : maxWidth;
                int height = options.Mode == CompositionMode.Vertical ? images.Sum(i => i.Height) + options.Spacing * (images.Count - 1) : options.Mode == CompositionMode.Grid ? maxHeight * rows + options.Spacing * (rows - 1) : maxHeight;
                var pixels = (long)width * height; var estimated = (pixels * 3 + images.Max(i => (long)i.Width * i.Height) * 2) * 4;
                if (width > 32767 || height > 32767 || pixels > 80_000_000 || estimated > MemoryBudget) throw new InvalidDataException("Composition exceeds the dimension, 80 MP or 256 MiB working-image budget. Choose fewer/smaller images.");
                var rectangles = new List<PixelRect>(); int position = 0;
                int Align(int available) => options.Alignment == CompositionAlignment.Center ? available / 2 : options.Alignment == CompositionAlignment.End ? available : 0;
                for (var index = 0; index < images.Count; index++)
                {
                    var image = images[index]; int x, y;
                    if (options.Mode == CompositionMode.Vertical) { x = Align(width - image.Width); y = position; position += image.Height + options.Spacing; }
                    else if (options.Mode == CompositionMode.Horizontal) { x = position; y = Align(height - image.Height); position += image.Width + options.Spacing; }
                    else { x = (index % columns) * (maxWidth + options.Spacing) + Align(maxWidth - image.Width); y = (index / columns) * (maxHeight + options.Spacing) + Align(maxHeight - image.Height); }
                    rectangles.Add(new(x, y, image.Width, image.Height));
                }
                return new(width, height, rectangles, estimated);
            }
        }
        catch (OverflowException ex) { throw new InvalidDataException("Composition dimensions overflow. Choose smaller images.", ex); }
    }
}
