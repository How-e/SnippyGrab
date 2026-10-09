namespace SnippyGrab.App.Services;

internal enum OcrReadinessStatus { Ready, MissingModel, ModifiedModel, MissingLibrary, ArchitectureOrLoadFailure, Inconclusive }
internal sealed record OcrReadiness(OcrReadinessStatus Status)
{
    public string Message => Status switch
    {
        OcrReadinessStatus.Ready => "Ready: the English model and local OCR engine loaded successfully.",
        OcrReadinessStatus.MissingModel => "English OCR model is missing. Reinstall or extract the complete package, including tessdata.",
        OcrReadinessStatus.ModifiedModel => "English OCR model is modified or redirected. Reinstall the complete trusted package.",
        OcrReadinessStatus.MissingLibrary => "An OCR library is missing. Reinstall or extract the complete x64 package, including Tesseract.dll and x64 libraries.",
        OcrReadinessStatus.ArchitectureOrLoadFailure => "OCR libraries could not load. Check the complete x64 package. A dependency or architecture problem is possible; this does not prove the Visual C++ runtime is absent.",
        _ => "OCR readiness is inconclusive. Check package permissions and dependencies, then retry. No runtime or model is downloaded automatically."
    };
    public const string RuntimeUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist";
    public static OcrReadiness From(Exception error) => new(OcrService.HasNativeLoaderFailure(error) ? OcrReadinessStatus.ArchitectureOrLoadFailure : OcrReadinessStatus.Inconclusive);
}
internal sealed class OcrUnavailableException(OcrReadiness readiness) : InvalidOperationException(readiness.Message)
{
    public OcrReadiness Readiness { get; } = readiness;
}
