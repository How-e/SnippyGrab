namespace SnippyGrab.Core;

public static class ImageLimits
{
    public const long MaxPixels = 80_000_000;
    public const int MaxEncodedBytes = 100 * 1024 * 1024;
    public const long MaxOcrPixels = 16_000_000;
    public const long WorkingBytes = 768L * 1024 * 1024;
    public static void Validate(long pixels, long bytesPerPixel = 0, string operation = "Image")
    {
        if (pixels < 1 || pixels > MaxPixels) throw new InvalidDataException("Image exceeds 80 megapixels or has invalid dimensions.");
        if (bytesPerPixel > 0 && pixels > (WorkingBytes - 32 * 1024 * 1024) / bytesPerPixel)
            throw new InvalidDataException(operation + " exceeds the working-image budget. Select a smaller area or fewer images.");
    }
    public static void ValidateEncoded(long bytes)
    {
        if (bytes > MaxEncodedBytes) throw new InvalidDataException("PNG exceeds 100 MiB. Select a smaller capture area or reduce the image before importing or applying edits.");
    }
}

// Limit encoder output before it allocates another oversized buffer or commits an
// image that the preview/editor/clipboard/OCR loaders cannot subsequently read.
public sealed class BoundedImageStream : MemoryStream
{
    public bool LimitExceeded { get; private set; }
    private void Admit(int count)
    {
        var needed = Math.Max(Length, Position + count);
        if (needed > ImageLimits.MaxEncodedBytes) LimitExceeded = true;
        ImageLimits.ValidateEncoded(needed);
        if (needed > Capacity) Capacity = (int)Math.Max(needed, Math.Min(ImageLimits.MaxEncodedBytes, Math.Max(256L, Capacity * 2L)));
    }
    public override void Write(byte[] buffer, int offset, int count) { Admit(count); base.Write(buffer, offset, count); }
    public override void Write(ReadOnlySpan<byte> buffer) { Admit(buffer.Length); base.Write(buffer); }
    public override void WriteByte(byte value) { Admit(1); base.WriteByte(value); }
    public override void SetLength(long value) { if (value > ImageLimits.MaxEncodedBytes) LimitExceeded = true; ImageLimits.ValidateEncoded(value); base.SetLength(value); }
}
