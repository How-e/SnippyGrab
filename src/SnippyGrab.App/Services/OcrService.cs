using System.Runtime.InteropServices;
using Tesseract;

namespace SnippyGrab.App.Services;

internal interface IOcrService { Task<string> ReadAsync(byte[] png, CancellationToken cancellation = default); }
internal sealed class OcrService(string? modelDirectory = null, Func<Settings>? settingsProvider = null) : IOcrService
{
    private readonly SemaphoreSlim serial = new(1);
    public async Task<string> ReadAsync(byte[] png, CancellationToken cancellation = default)
    {
        var settings = settingsProvider?.Invoke();
        var enhance = settings?.OcrEnhanceSmallText ?? true;
        var layout = settings?.OcrLayout ?? OcrLayout.Auto;
        await serial.WaitAsync(cancellation);
        try
        {
            return await Task.Run(() =>
            {
                cancellation.ThrowIfCancellationRequested();
                var directory = modelDirectory ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
                if (png.Length > 100 * 1024 * 1024) throw new InvalidDataException("OCR image exceeds 100 MB.");
                if (!File.Exists(Path.Combine(directory, "eng.traineddata"))) throw new InvalidOperationException("English OCR model is missing. Run scripts/provision-ocr.ps1 and rebuild, or reinstall the complete package.");
                ValidateModel(Path.Combine(directory, "eng.traineddata"));
                using (var encoded = new MemoryStream(png, writable: false)) ImageService.ValidateEncoded(encoded);
                TesseractEnviornment.CustomSearchPath = AppContext.BaseDirectory;
                using var engine = new TesseractEngine(directory, "eng", EngineMode.LstmOnly);
                // Windows decodes admitted images; native OCR receives raw pixels, never codec input.
                using var pix = DecodePixels(png, cancellation);
                if ((long)pix.Width * pix.Height > ImageService.MaxPixels) throw new InvalidDataException("OCR image exceeds 80 megapixels.");
                var mode = layout switch { OcrLayout.SparseText => PageSegMode.SparseText, OcrLayout.SingleBlock => PageSegMode.SingleBlock, _ => PageSegMode.Auto };
                string text; float confidence; int lineHeight;
                using (var page = engine.Process(pix, mode))
                {
                    text = page.GetText(); confidence = page.GetMeanConfidence();
                    var heights = page.GetSegmentedRegions(PageIteratorLevel.TextLine).Select(r => r.Height).Order().ToArray();
                    lineHeight = heights.Length == 0 ? 0 : heights[heights.Length / 2];
                }
                // Enhance genuinely small text; enlarging already readable text can reduce accuracy.
                // Keep original pixels intact and bound the extra working image to eight megapixels.
                var scale = enhance && lineHeight < 24 ? EnhancementScale(pix.Width, pix.Height) : 1;
                cancellation.ThrowIfCancellationRequested();
                using var enlarged = scale > 1 ? pix.Scale(scale, scale) : null;
                var input = enlarged ?? pix;
                if (enlarged is not null)
                {
                    using var enhanced = engine.Process(input, mode);
                    var alternative = enhanced.GetText(); var score = enhanced.GetMeanConfidence();
                    if (!string.IsNullOrWhiteSpace(alternative) && (string.IsNullOrWhiteSpace(text) || score > confidence + 0.03f)) { text = alternative; confidence = score; }
                }
                cancellation.ThrowIfCancellationRequested();
                // Auto's document layout can miss labels on mixed screenshots. Do not concatenate passes.
                if (layout == OcrLayout.Auto && (string.IsNullOrWhiteSpace(text) || confidence < 0.85f))
                {
                    using var sparse = engine.Process(input, PageSegMode.SparseText);
                    var alternative = sparse.GetText();
                    if (!string.IsNullOrWhiteSpace(alternative) && (string.IsNullOrWhiteSpace(text) || sparse.GetMeanConfidence() > confidence + 0.03f)) text = alternative;
                }
                cancellation.ThrowIfCancellationRequested(); return text;
            }, cancellation);
        }
        catch (Exception ex) when (HasNativeLoaderFailure(ex))
        { throw new InvalidOperationException("Local OCR native libraries could not load. Reinstall the complete x64 package and its Visual C++ runtime; capture remains available.", ex); }
        finally { serial.Release(); }
    }
    internal static float EnhancementScale(int width, int height)
    {
        var scale = Math.Min(2, Math.Sqrt(8_000_000d / ((long)width * height)));
        return scale >= 1.25 ? (float)scale : 1;
    }
    internal static unsafe Pix DecodePixels(byte[] encoded, CancellationToken cancellation = default)
    {
        using var stream = new MemoryStream(encoded, writable: false);
        ImageService.ValidateEncoded(stream); stream.Position = 0;
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder is not (PngBitmapDecoder or JpegBitmapDecoder or BmpBitmapDecoder)) throw new InvalidDataException("Unsupported OCR image codec.");
        var image = decoder.Frames[0];
        if ((long)image.PixelWidth * image.PixelHeight > ImageService.MaxPixels) throw new InvalidDataException("OCR image exceeds 80 megapixels.");
        var pixels = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var pix = CreateRgba(pixels.PixelWidth, pixels.PixelHeight);
        try
        {
            pix.XRes = (int)Math.Clamp(Math.Round(image.DpiX), 1, 2400);
            pix.YRes = (int)Math.Clamp(Math.Round(image.DpiY), 1, 2400);
            var data = pix.GetData(); var row = new byte[checked(pixels.PixelWidth * 4)];
            for (var y = 0; y < pixels.PixelHeight; y++)
            {
                cancellation.ThrowIfCancellationRequested();
                pixels.CopyPixels(new Int32Rect(0, y, pixels.PixelWidth, 1), row, row.Length, 0);
                var destination = (uint*)data.Data + y * data.WordsPerLine;
                for (var x = 0; x < pixels.PixelWidth; x++)
                {
                    var offset = x * 4;
                    destination[x] = PixData.EncodeAsRGBA(row[offset + 2], row[offset + 1], row[offset], row[offset + 3]);
                }
            }
            return pix;
        }
        catch { pix.Dispose(); throw; }
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint CreatePix(int width, int height, int depth);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetSamples(nint pix, int samples);
    private static Pix CreateRgba(int width, int height)
    {
        var library = NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "x64", "leptonica-1.82.0.dll"));
        try
        {
            var handle = Marshal.GetDelegateForFunctionPointer<CreatePix>(NativeLibrary.GetExport(library, "pixCreate"))(width, height, 32);
            var pix = Pix.Create(handle);
            try
            {
                // pixCreate defaults to RGB. Preserve the PNG reader's RGBA semantics for OCR.
                if (Marshal.GetDelegateForFunctionPointer<SetSamples>(NativeLibrary.GetExport(library, "pixSetSpp"))(handle, 4) != 0) throw new InvalidOperationException("Could not initialize OCR pixels.");
                if (Marshal.GetDelegateForFunctionPointer<SetSamples>(NativeLibrary.GetExport(library, "pixSetInputFormat"))(handle, 3) != 0) throw new InvalidOperationException("Could not initialize OCR alpha handling."); // IFF_PNG
                return pix;
            }
            catch { pix.Dispose(); throw; }
        }
        finally { NativeLibrary.Free(library); }
    }
    internal static bool HasNativeLoaderFailure(Exception error) => error is DllNotFoundException or BadImageFormatException || error.InnerException is { } inner && HasNativeLoaderFailure(inner);
    internal static void ValidateModel(string path)
    {
        try { ManagedPath.RejectRedirects(path); }
        catch (InvalidDataException ex) { throw new InvalidOperationException("English OCR model location is redirected. Reinstall the complete package.", ex); }
        using var stream = File.OpenRead(path);
        if (stream.Length > 16 * 1024 * 1024 || Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)) != "7D4322BD2A7749724879683FC3912CB542F19906C83BCC1A52132556427170B2")
            throw new InvalidOperationException("English OCR model checksum failed. Reinstall the complete package; untrusted models are not supported.");
    }
}
