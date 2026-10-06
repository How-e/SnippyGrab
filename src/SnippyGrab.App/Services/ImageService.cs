using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using WpfPixelFormats = System.Windows.Media.PixelFormats;

namespace SnippyGrab.App.Services;

internal static class ImageService
{
    public const long MaxPixels = 80_000_000;
    public static BitmapSource Capture(PixelRect rect, bool cursor)
    {
        if (rect.IsEmpty || (long)rect.Width * rect.Height > MaxPixels) throw new InvalidDataException("Capture area is too large (80 megapixel limit).");
        using var bitmap = new Bitmap(rect.Width, rect.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var screen = Native.GetDC(0); var destination = graphics.GetHdc();
            try
            {
                if (!Native.BitBlt(destination, 0, 0, rect.Width, rect.Height, screen, rect.X, rect.Y, 0x40CC0020))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Desktop capture failed.");
            }
            finally { graphics.ReleaseHdc(destination); Native.ReleaseDC(0, screen); }
            if (cursor)
            {
                var info = new Native.CURSORINFO { Size = Marshal.SizeOf<Native.CURSORINFO>() };
                if (Native.GetCursorInfo(ref info) && info.Flags == 1 && Native.GetIconInfo(info.Cursor, out var icon))
                {
                    var dc = graphics.GetHdc();
                    try { Native.DrawIconEx(dc, info.Position.X - rect.X - icon.HotX, info.Position.Y - rect.Y - icon.HotY, info.Cursor, 0, 0, 0, 0, 3); }
                    finally { graphics.ReleaseHdc(dc); Native.DeleteObject(icon.Mask); if (icon.Color != 0) Native.DeleteObject(icon.Color); }
                }
            }
        }
        var bits = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        try
        {
            var source = BitmapSource.Create(bitmap.Width, bitmap.Height, 96, 96, WpfPixelFormats.Bgr32, null, bits.Scan0, bits.Stride * bitmap.Height, bits.Stride);
            source.Freeze(); return source;
        }
        finally { bitmap.UnlockBits(bits); }
    }
    public static BitmapSource Load(string path, int thumbnail = 0, int thumbnailHeight = 0)
    {
        if (new FileInfo(path).Length > 100 * 1024 * 1024) throw new InvalidDataException("Image file exceeds 100 MB.");
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        var frame = decoder.Frames[0];
        if ((long)frame.PixelWidth * frame.PixelHeight > MaxPixels) throw new InvalidDataException("Image exceeds 80 megapixels.");
        stream.Position = 0;
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        if (thumbnail > 0)
        {
            var width = Math.Min(thumbnail, frame.PixelWidth);
            if (thumbnailHeight > 0 && width * (double)frame.PixelHeight / frame.PixelWidth > thumbnailHeight)
                image.DecodePixelHeight = Math.Min(thumbnailHeight, frame.PixelHeight);
            else image.DecodePixelWidth = width;
        }
        image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
    }
    public static byte[] Png(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
    }
    public static BitmapSource Crop(BitmapSource image, PixelRect rect)
    {
        rect = rect.Intersect(new(0, 0, image.PixelWidth, image.PixelHeight));
        if (rect.IsEmpty) throw new InvalidDataException("Select an image area first.");
        var crop = new CroppedBitmap(image, new Int32Rect(rect.X, rect.Y, rect.Width, rect.Height)); crop.Freeze();
        // Detach the selected pixels: CroppedBitmap otherwise retains the entire virtual desktop.
        var stride = (crop.PixelWidth * crop.Format.BitsPerPixel + 7) / 8;
        var pixels = new byte[stride * crop.PixelHeight]; crop.CopyPixels(pixels, stride, 0);
        var detached = BitmapSource.Create(crop.PixelWidth, crop.PixelHeight, 96, 96, crop.Format, crop.Palette, pixels, stride); detached.Freeze(); return detached;
    }
}
