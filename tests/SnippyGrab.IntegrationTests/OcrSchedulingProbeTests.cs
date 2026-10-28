using System.IO;
using System.Text.Json;
using SnippyGrab.App.Services;

namespace SnippyGrab.IntegrationTests;

[Collection("Application resources")]
public sealed class OcrSchedulingProbeTests
{
    [Fact]
    public async Task ProductionOcrIsolationProbeRunsRealRecognitionAndReusesEngine()
    {
        var report = Path.Combine(Path.GetTempPath(), "SnippyGrab-ocr-scheduling-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await OcrSchedulingChecks.Run(report);
            using var saved = JsonDocument.Parse(File.ReadAllText(report));
            Assert.Equal("PASS", saved.RootElement.GetProperty("Result").GetString());
            Assert.True(saved.RootElement.GetProperty("OcrIsolation").GetBoolean());
            Assert.True(saved.RootElement.GetProperty("NativeRecognition").GetBoolean());
            Assert.Equal(1, saved.RootElement.GetProperty("EngineCreations").GetInt32());
        }
        finally { if (File.Exists(report)) File.Delete(report); }
    }
}
