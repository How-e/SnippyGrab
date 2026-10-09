using System.IO;
using SnippyGrab.App.Services;

namespace SnippyGrab.IntegrationTests;

public sealed class OcrReadinessTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SnippyGrab-readiness-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task PackageFailuresAreActionableAndRepairCanRetry()
    {
        Directory.CreateDirectory(root);
        Assert.Equal(OcrReadinessStatus.MissingModel, OcrService.InspectPackage(root, root).Status);
        File.WriteAllBytes(Path.Combine(root, "eng.traineddata"), [1, 2]);
        Assert.Equal(OcrReadinessStatus.ModifiedModel, OcrService.InspectPackage(root, root).Status);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "tessdata", "eng.traineddata"), Path.Combine(root, "eng.traineddata"), true);
        Assert.Equal(OcrReadinessStatus.MissingLibrary, OcrService.InspectPackage(root, root).Status);
        Assert.Equal(OcrReadinessStatus.Ready, OcrService.InspectPackage(root, AppContext.BaseDirectory).Status);
        var service = new OcrService(root);
        Assert.Equal(OcrReadinessStatus.Ready, (await service.CheckReadinessAsync()).Status);
        File.Delete(Path.Combine(root, "eng.traineddata"));
        Assert.Equal(OcrReadinessStatus.MissingModel, (await service.CheckReadinessAsync()).Status);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "tessdata", "eng.traineddata"), Path.Combine(root, "eng.traineddata"));
        Assert.Equal(OcrReadinessStatus.Ready, (await service.CheckReadinessAsync()).Status);
    }
    [Fact]
    public async Task CancelledAdmissionDoesNotPreventRetry()
    {
        var service = new OcrService();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CheckReadinessAsync(cancellation.Token));
        Assert.Equal(OcrReadinessStatus.Ready, (await service.CheckReadinessAsync()).Status);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LoaderDiagnosisDoesNotClaimRuntimeIsAbsent(bool architecture)
    {
        Exception error = architecture ? new BadImageFormatException() : new DllNotFoundException();
        var result = OcrReadiness.From(new InvalidOperationException("private compiler path", error));
        Assert.Equal(OcrReadinessStatus.ArchitectureOrLoadFailure, result.Status);
        Assert.DoesNotContain("private compiler path", result.Message);
        Assert.Contains("does not prove", result.Message);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
