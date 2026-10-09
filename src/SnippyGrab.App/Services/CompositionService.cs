namespace SnippyGrab.App.Services;

internal static class CompositionService
{
    internal static BitmapSource Render(IReadOnlyList<string> paths, CompositionLayout layout, Color background, CancellationToken cancellation = default)
    {
        if (paths.Count != layout.Rectangles.Count || layout.EstimatedBytes > Composition.MemoryBudget) throw new InvalidDataException("Invalid composition plan.");
        var stride = checked(layout.Width * 4); var output = new byte[checked(stride * layout.Height)];
        for (var offset = 0; offset < output.Length; offset += 4) { output[offset] = background.B; output[offset + 1] = background.G; output[offset + 2] = background.R; output[offset + 3] = background.A; }
        for (var index = 0; index < paths.Count; index++)
        {
            cancellation.ThrowIfCancellationRequested(); ManagedPath.RejectRedirects(paths[index]);
            var image = ImageService.Load(paths[index]); var bounds = layout.Rectangles[index];
            if (image.PixelWidth != bounds.Width || image.PixelHeight != bounds.Height) throw new InvalidDataException("Composition input dimensions changed.");
            var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0); var row = new byte[checked(bounds.Width * 4)];
            for (var y = 0; y < bounds.Height; y++)
            {
                cancellation.ThrowIfCancellationRequested(); converted.CopyPixels(new Int32Rect(0, y, bounds.Width, 1), row, row.Length, 0);
                for (var x = 0; x < bounds.Width; x++)
                {
                    int source = x * 4, target = (bounds.Y + y) * stride + (bounds.X + x) * 4;
                    int alpha = row[source + 3], retained = output[target + 3] * (255 - alpha) / 255, combined = alpha + retained;
                    for (var channel = 0; channel < 3; channel++) output[target + channel] = combined == 0 ? (byte)0 : (byte)((row[source + channel] * alpha + output[target + channel] * retained + combined / 2) / combined);
                    output[target + 3] = (byte)combined;
                }
            }
        }
        var result = BitmapSource.Create(layout.Width, layout.Height, 96, 96, PixelFormats.Bgra32, null, output, stride); result.Freeze(); return result;
    }
}
