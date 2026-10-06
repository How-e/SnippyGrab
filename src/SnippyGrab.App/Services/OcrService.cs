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
                TesseractEnviornment.CustomSearchPath = AppContext.BaseDirectory;
                using var engine = new TesseractEngine(directory, "eng", EngineMode.LstmOnly);
                using var pix = Pix.LoadFromMemory(png);
                if ((long)pix.Width * pix.Height > ImageService.MaxPixels) throw new InvalidDataException("OCR image exceeds 80 megapixels.");
                using var page = engine.Process(pix, PageSegMode.Auto);
                var text = page.GetText();
                cancellation.ThrowIfCancellationRequested(); return text;
            }, cancellation);
        }
        finally { serial.Release(); }
    }
}
