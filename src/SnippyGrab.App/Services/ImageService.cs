using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using WpfPixelFormats = System.Windows.Media.PixelFormats;

namespace SnippyGrab.App.Services;

internal static class ImageService
{
    public const long MaxPixels = 80_000_000;
    internal static byte[] EncodeExport(string path, ExportFormat format, int quality, CancellationToken cancellation)
    {
        if (format is ExportFormat.WebpLossless or ExportFormat.WebpLossy)
        {
            // Inspect dimensions before decoding a potentially large composed PNG.
            using (var input = File.OpenRead(path))
            {
                var frame = BitmapDecoder.Create(input, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
                WebpService.ValidateDimensions(frame.PixelWidth, frame.PixelHeight);
            }
            return WebpService.Encode(Load(path), format == ExportFormat.WebpLossless, quality, cancellation);
        }
        cancellation.ThrowIfCancellationRequested();
        return Jpeg(Load(path), quality, cancellation);
    }
    internal static byte[] Jpeg(BitmapSource image, int quality, CancellationToken cancellation = default)
    {
        if (quality is < 1 or > 100 || (long)image.PixelWidth * image.PixelHeight > MaxPixels) throw new InvalidDataException("Invalid JPEG quality or dimensions.");
        cancellation.ThrowIfCancellationRequested();
        // Flatten one scanline at a time into a single opaque native buffer. A full
        // WPF render plus format conversion retained several copies of large images.
        var source = new FormatConvertedBitmap(image, WpfPixelFormats.Bgra32, null, 0); source.Freeze();
        using var opaque = new Bitmap(image.PixelWidth, image.PixelHeight, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        opaque.SetResolution(96, 96);
        var bits = opaque.LockBits(new System.Drawing.Rectangle(0, 0, opaque.Width, opaque.Height), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        try
        {
            var row = new byte[checked(image.PixelWidth * 4)];
            var flattened = new byte[checked(image.PixelWidth * 3)];
            for (var y = 0; y < image.PixelHeight; y++)
            {
                cancellation.ThrowIfCancellationRequested();
                source.CopyPixels(new Int32Rect(0, y, image.PixelWidth, 1), row, row.Length, 0);
                for (var x = 0; x < image.PixelWidth; x++)
                {
                    var alpha = row[x * 4 + 3];
                    for (var c = 0; c < 3; c++) flattened[x * 3 + c] = (byte)((row[x * 4 + c] * alpha + 255 * (255 - alpha) + 127) / 255);
                }
                Marshal.Copy(flattened, 0, IntPtr.Add(bits.Scan0, y * bits.Stride), flattened.Length);
            }
        }
        finally { opaque.UnlockBits(bits); }
        cancellation.ThrowIfCancellationRequested();
        using var options = new EncoderParameters(1);
        options.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
        using var stream = new MemoryStream();
        opaque.Save(stream, ImageCodecInfo.GetImageEncoders().Single(codec => codec.FormatID == ImageFormat.Jpeg.Guid), options);
        cancellation.ThrowIfCancellationRequested(); return stream.ToArray();
    }
    internal static int PreviewPixels(int pixels, PreviewQuality quality) => quality switch
    {
        PreviewQuality.Original => checked(pixels * 4),
        PreviewQuality.Sharp => checked(pixels * 2),
        _ => pixels
    };
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
                    var origin = CaptureSelection.CursorOrigin(info.Position.X, info.Position.Y, icon.HotX, icon.HotY, rect);
                    try { Native.DrawIconEx(dc, origin.X, origin.Y, info.Cursor, 0, 0, 0, 0, 3); }
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
    internal static BitmapSource LoadPreview(string path, int width, int height = 0) => Load(path, width, height, 4_000_000);
    public static BitmapSource Load(string path, int thumbnail = 0, int thumbnailHeight = 0, long maxDecodedPixels = MaxPixels)
    {
        using var stream = File.OpenRead(path);
        ValidateEncoded(stream);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        if (decoder is not (PngBitmapDecoder or JpegBitmapDecoder or BmpBitmapDecoder)) throw new InvalidDataException("Only PNG, JPEG and BMP image content is accepted.");
        var frame = decoder.Frames[0];
        if ((long)frame.PixelWidth * frame.PixelHeight > MaxPixels) throw new InvalidDataException("Image exceeds 80 megapixels.");
        if (maxDecodedPixels < 1) throw new ArgumentOutOfRangeException(nameof(maxDecodedPixels));
        stream.Position = 0;
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        var decodeScale = Math.Min(1, Math.Sqrt(maxDecodedPixels / ((double)frame.PixelWidth * frame.PixelHeight)));
        if (thumbnail > 0) decodeScale = Math.Min(decodeScale, thumbnail / (double)frame.PixelWidth);
        if (thumbnailHeight > 0) decodeScale = Math.Min(decodeScale, thumbnailHeight / (double)frame.PixelHeight);
        if (decodeScale < 1)
        {
            if (frame.PixelHeight > frame.PixelWidth) image.DecodePixelHeight = Math.Max(1, (int)Math.Floor(frame.PixelHeight * decodeScale));
            else image.DecodePixelWidth = Math.Max(1, (int)Math.Floor(frame.PixelWidth * decodeScale));
        }
        image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
    }
    internal static void ValidateEncoded(Stream stream)
    {
        if (stream.Length > 100 * 1024 * 1024) throw new InvalidDataException("Image file exceeds 100 MB.");
        Span<byte> header = stackalloc byte[24];
        if (stream.Length < 24) throw new InvalidDataException("Image is truncated.");
        stream.ReadExactly(header);
        if (header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            if (!header.Slice(12, 4).SequenceEqual("IHDR"u8)) throw new InvalidDataException("PNG header is invalid.");
            var width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.Slice(16, 4));
            var height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.Slice(20, 4));
            if (width == 0 || height == 0 || (ulong)width * height > MaxPixels) throw new InvalidDataException("Image exceeds 80 megapixels or has invalid dimensions.");
            stream.Position = stream.Length - 12;
            Span<byte> end = stackalloc byte[12]; stream.ReadExactly(end);
            if (!end.SequenceEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 })) throw new InvalidDataException("PNG is truncated or has an invalid end marker.");
        }
        else if (header[0] == 255 && header[1] == 216 && header[2] == 255)
        {
            stream.Position = stream.Length - 2;
            if (stream.ReadByte() != 255 || stream.ReadByte() != 217) throw new InvalidDataException("JPEG is truncated.");
        }
        else if (header[0] != 66 || header[1] != 77) throw new InvalidDataException("Only PNG, JPEG and BMP image content is accepted.");
        stream.Position = 0;
    }
    internal static byte[] ReadEncoded(string path)
    {
        using var stream = File.OpenRead(path);
        ValidateEncoded(stream); // Enforce size/header limits before allocating the clipboard/OCR payload.
        var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes); return bytes;
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
