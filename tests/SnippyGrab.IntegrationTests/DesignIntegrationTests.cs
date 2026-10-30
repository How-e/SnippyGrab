using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using SnippyGrab.App.Services;
using SnippyGrab.Core;

namespace SnippyGrab.IntegrationTests;

public sealed class DesignIntegrationTests
{
    [Fact]
    public void CapturedFontsRejectUnsupportedTypesSignaturesAndStyleInjection()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var png = ImageService.Png(EditorDocumentTests.Fixture());
            string Capture(string media, string data, string weight = "normal") => JsonSerializer.Serialize(new { width = 160, height = 100, layers = Array.Empty<object>(), fontAssets = new[] { new { mediaType = media, data, family = "Fixture", weight, style = "normal" } } });
            Assert.Throws<InvalidDataException>(() => DesignBrowser.FromCapture(Capture("text/html", Convert.ToBase64String(png)), png));
            Assert.Throws<InvalidDataException>(() => DesignBrowser.FromCapture(Capture("font/woff", Convert.ToBase64String(png)), png));
            var header = new byte[48]; "wOFF"u8.CopyTo(header);
            Assert.Throws<InvalidDataException>(() => DesignBrowser.FromCapture(Capture("font/woff", Convert.ToBase64String(header), "normal;src:url(secret)"), png));
            var admitted = DesignBrowser.FromCapture(Capture("font/woff", Convert.ToBase64String(header), "100 900"), png);
            Assert.Contains(admitted.Assets.Values, asset => asset.MediaType == "font/woff" && asset.Weight == "100 900"); return true;
        });
    }
    [Fact]
    public void PartiallyClippedRasterLayersUseOnlyTheirIntersectionWithTheReference()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var png = ImageService.Png(EditorDocumentTests.Fixture());
            var d = DesignBrowser.FromCapture("""{"width":160,"height":100,"layers":[{"id":"partial","kind":"image","x":-20,"y":-10,"width":80,"height":40},{"id":"outside","kind":"image","x":-100,"y":0,"width":40,"height":20}]}""", png);
            Assert.Equal(0, d.Layers[0].X); Assert.Equal(0, d.Layers[0].Y); Assert.Equal(60, d.Layers[0].Width); Assert.Equal(30, d.Layers[0].Height); Assert.False(d.Layers[1].Visible);
            var crop = DesignBrowser.Decode(d.Assets[d.Layers[0].Asset].Bytes()); Assert.Equal(60, crop.PixelWidth); Assert.Equal(30, crop.PixelHeight); return true;
        });
    }
    internal static DesignDocument Fixture()
    {
        var png = ImageService.Png(EditorDocumentTests.Fixture());
        return DesignBrowser.FromCapture("""
            {"width":160,"height":100,"source":{"url":"http://localhost:5173/settings"},"layers":[
            {"id":"root","width":160,"height":100,"fill":"#ffffff"},
            {"id":"text","parent":"root","kind":"text","x":10,"y":10,"width":120,"height":25,"text":"Original label","fill":"#222222","source":{"selector":"#title"}},
            {"id":"secret","parent":"root","kind":"text","x":10,"y":50,"width":130,"height":20,"text":"private-value","fill":"#222222"}]}
            """, png);
    }
    [Fact]
    public void HandoffRoundTripVerifiesEveryEntryAndRejectsTampering()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var d = Fixture(); new DesignSession(d).Apply("text", n => n[1].Text = "Requested label"); var png = d.Assets[d.Original].Bytes();
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");
            try
            {
                DesignStore.Export(path, d, new Dictionary<string, byte[]> { ["visuals/original.png"] = png, ["visuals/edited.png"] = png });
                var loaded = DesignStore.LoadHandoff(path); Assert.Equal("Requested label", loaded.Layers[1].Text);
                using (var zip = ZipFile.Open(path, ZipArchiveMode.Update)) { zip.GetEntry("brief.md")!.Delete(); using var entry = zip.CreateEntry("brief.md").Open(); entry.Write(Encoding.UTF8.GetBytes("tampered")); }
                Assert.Throws<InvalidDataException>(() => DesignStore.LoadHandoff(path));
            }
            finally { File.Delete(path); }
            return true;
        });
    }
    [Fact]
    public void PrivacyRemovesOriginalPixelsSceneHistoryAndDeletedTargets()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var d = Fixture(); var s = new DesignSession(d); s.Apply("text", n => n[1].Text = "Requested label"); s.Apply("privacy", n => n[2].Private = true); s.Delete(["secret"]);
            var clean = DesignHandoff.Sanitize(d); var serialized = JsonSerializer.Serialize(clean, DesignDocument.Json);
            Assert.DoesNotContain("private-value", serialized); Assert.Empty(clean.History); Assert.NotEqual(d.Original, clean.Original); Assert.False(clean.Assets.ContainsKey(d.Original));
            Assert.Single(new DesignSession(clean).NetChanges()); clean.Validate(); Assert.Equal(clean.BaselineHash, DesignHandoff.Sanitize(d).BaselineHash); return true;
        });
    }
    [Fact]
    public void CaptureRasterFallbackAndComparisonHaveExplicitBounds()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var png = ImageService.Png(EditorDocumentTests.Fixture());
            var d = DesignBrowser.FromCapture("""{"width":160,"height":100,"layers":[{"id":"image","kind":"image","width":80,"height":40,"fallback":"canvas"}]}""", png);
            Assert.NotEmpty(d.Layers[0].Asset); Assert.Equal(2, d.Assets.Count);
            var report = DesignComparison.Create(d, d, png); Assert.Equal(0, report.MeanChannelDifference); Assert.Equal(0, report.ChangedPixels);
            var changed = DesignDocument.Copy(d); changed.Width = 150; var mismatch = DesignComparison.Create(d, changed, png); Assert.False(mismatch.SameViewport); Assert.Null(mismatch.ChangedPixels); return true;
        });
    }
    [Fact]
    public void FullPageComparisonRejectsDifferentSourceViewportsEvenWhenArtboardsMatch()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var expected = Fixture(); expected.Source.ViewportWidth = 160; expected.Source.ViewportHeight = 100;
            var actual = DesignDocument.Copy(expected); actual.Source.ViewportHeight = 200;
            var comparison = DesignComparison.Create(expected, actual, expected.Assets[expected.Original].Bytes());
            Assert.False(comparison.SameViewport); Assert.Null(comparison.MeanChannelDifference); return true;
        });
    }
    [Fact]
    public async Task NativeProtocolRejectsOversizeAndTruncatedPayloads()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => DesignBrowserBridge.ReadMessage(new MemoryStream(new byte[] { 0xff, 0xff, 0xff, 0x7f })));
        await Assert.ThrowsAsync<EndOfStreamException>(() => DesignBrowserBridge.ReadMessage(new MemoryStream(new byte[] { 10, 0, 0, 0, 1 })));
        var payload = await DesignBrowserBridge.ReadMessage(new MemoryStream(new byte[] { 2, 0, 0, 0, 123, 125 })); Assert.Equal("{}", Encoding.UTF8.GetString(payload));
    }
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("C:/outside.txt")]
    [InlineData("duplicate/../file")]
    public void HandoffPathsFailBeforeAnyExtraction(string entry)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");
        try { using (var zip = ZipFile.Open(path, ZipArchiveMode.Create)) { using var output = zip.CreateEntry(entry).Open(); output.WriteByte(1); } Assert.Throws<InvalidDataException>(() => DesignStore.LoadHandoff(path)); }
        finally { File.Delete(path); }
    }
}
