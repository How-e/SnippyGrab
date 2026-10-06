using Tesseract;

namespace SnippyGrab.App.Services;

internal interface IOcrService { Task<string> ReadAsync(byte[] png, CancellationToken cancellation = default); }
internal sealed class OcrService(string? modelDirectory = null) : IOcrService
{
    private readonly SemaphoreSlim serial = new(1);
    public async Task<string> ReadAsync(byte[] png, CancellationToken cancellation = default)
    {
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
                using var page = engine.Process(pix, PageSegMode.Auto);
                var text = page.GetText();
                cancellation.ThrowIfCancellationRequested(); return text;
            }, cancellation);
        }
        catch (Exception ex) when (HasNativeLoaderFailure(ex))
        { throw new InvalidOperationException("Local OCR native libraries could not load. Reinstall the complete x64 package and its Visual C++ runtime; capture remains available.", ex); }
        finally { serial.Release(); }
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
