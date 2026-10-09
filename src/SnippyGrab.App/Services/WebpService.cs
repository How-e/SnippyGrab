using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace SnippyGrab.App.Services;

internal static class WebpService
{
    internal const string Commit = "4fa21912338357f89e4fd51cf2368325b59e9bd9";
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CancelCheck();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EncodeFunction(byte[] pixels, int width, int height, int lossless, int quality, CancelCheck cancel, out IntPtr output, out nuint size);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FreeFunction(IntPtr output);
    private sealed record Codec(IntPtr Handle, EncodeFunction Encode, FreeFunction Free);
    private static readonly Lazy<Codec> Encoder = new(() => Load(AppContext.BaseDirectory));
    internal static void ValidateDimensions(int width, int height)
    {
        if (width is < 1 or > 16383 || height is < 1 or > 16383 || (long)width * height > 16_000_000)
            throw new InvalidDataException("WebP export supports up to 16 MP and 16383 pixels per axis. Export PNG or reduce the image first.");
    }
    internal static void ValidatePackage(string directory)
    {
        var dll = Path.Combine(directory, "x64", "snippywebp.dll"); var provenance = Path.Combine(directory, "WEBP-PROVENANCE.json");
        ManagedPath.RejectRedirects(dll); ManagedPath.RejectRedirects(provenance);
        if (!File.Exists(dll) || !File.Exists(provenance)) throw new InvalidDataException("WebP encoder is missing. Restore the complete SnippyGrab package or export PNG/JPEG.");
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(provenance)); var root = json.RootElement;
            if (root.GetProperty("Schema").GetInt32() != 1 || root.GetProperty("Version").GetString() != "1.6.0" || root.GetProperty("Commit").GetString() != Commit ||
                !Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))).Equals(root.GetProperty("BinarySha256").GetString(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("WebP encoder integrity check failed. Restore the complete package or export PNG/JPEG.");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        { throw new InvalidDataException("WebP encoder provenance is invalid. Restore the complete package or export PNG/JPEG.", ex); }
    }
    private static Codec Load(string directory)
    {
        ValidatePackage(directory);
        var handle = NativeLibrary.Load(Path.GetFullPath(Path.Combine(directory, "x64", "snippywebp.dll")));
        try { return new(handle, Marshal.GetDelegateForFunctionPointer<EncodeFunction>(NativeLibrary.GetExport(handle, "SnippyWebpEncode")), Marshal.GetDelegateForFunctionPointer<FreeFunction>(NativeLibrary.GetExport(handle, "SnippyWebpFree"))); }
        catch { NativeLibrary.Free(handle); throw; }
    }
    internal static byte[] Encode(BitmapSource image, bool lossless, int quality, CancellationToken cancellation = default)
    {
        ValidateDimensions(image.PixelWidth, image.PixelHeight);
        if (quality is < 1 or > 100) throw new InvalidDataException("WebP quality must be between 1 and 100.");
        cancellation.ThrowIfCancellationRequested(); var codec = Encoder.Value;
        var bgra = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0); bgra.Freeze();
        var pixels = new byte[checked(image.PixelWidth * image.PixelHeight * 4)]; bgra.CopyPixels(pixels, image.PixelWidth * 4, 0);
        CancelCheck cancel = () => cancellation.IsCancellationRequested ? 1 : 0;
        IntPtr output = IntPtr.Zero;
        try
        {
            var success = codec.Encode(pixels, image.PixelWidth, image.PixelHeight, lossless ? 1 : 0, lossless ? 90 : quality, cancel, out output, out var size);
            cancellation.ThrowIfCancellationRequested();
            if (success != 1 || output == IntPtr.Zero || size is 0 or > 100 * 1024 * 1024) throw new InvalidDataException("WebP encoding failed. Try PNG/JPEG or a smaller image.");
            var encoded = new byte[checked((int)size)]; Marshal.Copy(output, encoded, 0, encoded.Length); return encoded;
        }
        finally { if (output != IntPtr.Zero) codec.Free(output); GC.KeepAlive(cancel); }
    }
}
