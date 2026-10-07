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
                using var pix = Pix.LoadFromMemory(png);
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
