using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using SnippyGrab.App.Services;

namespace SnippyGrab.IntegrationTests;

public sealed class UpdateServiceTests
{
    private static object Release(string tag, bool prerelease = false, bool draft = false, string? digest = null, string host = "github.com") => new
    {
        tag_name = tag,
        prerelease,
        draft,
        html_url = $"https://{host}/How-e/SnippyGrab/releases/tag/{tag}",
        body = "Release notes",
        assets = new[] { new { name = $"SnippyGrab-{tag.TrimStart('v')}-win-x64.zip", state = "uploaded", digest = digest ?? "sha256:" + new string('a', 64), browser_download_url = $"https://{host}/How-e/SnippyGrab/releases/download/{tag}/app.zip" } }
    };
    [Theory]
    [InlineData("0.1.0-alpha.design.20261008.9", "0.1.0-alpha.design.20261008.10")]
    [InlineData("0.1.0-alpha.queue.20261006.3", "0.1.0-alpha.design.20261008.1")]
    [InlineData("0.1.0-alpha.design.20261008.1", "0.1.0-alpha.20261009.1")]
    [InlineData("0.1.0-alpha.9", "0.1.0-beta.1")]
    [InlineData("0.1.0-rc.9", "0.1.0")]
    [InlineData("0.9.9", "0.10.0")]
    [InlineData("0.1.0-alpha.99999999999999999999", "0.1.0-alpha.100000000000000000000")]
    public void VersionsCompareNumerically(string older, string newer) => Assert.True(ReleaseVersion.Parse(older)!.CompareTo(ReleaseVersion.Parse(newer)) < 0);
    [Fact]
    public void MetadataDoesNotChangePrecedence() => Assert.Equal(0, ReleaseVersion.Parse("v1.0.0+abcdef")!.CompareTo(ReleaseVersion.Parse("1.0.0")));
    [Fact]
    public void LegacyAlphaNamesSelectTheNewestDatedBuild()
    {
        var json = JsonSerializer.Serialize(new[] { Release("v0.1.0-alpha.queue.20261006.3", true), Release("v0.1.0-alpha.design.20261008.1", true), Release("v0.1.0-alpha.acceptance.20261007.8", true) });
        Assert.Equal("0.1.0-alpha.design.20261008.1", UpdateService.SelectRelease(json, "0.1.0-alpha")!.Version);
    }
    [Fact]
    public void StableSkipsPrereleasesAndDraftsAndIgnoresPublishOrder()
    {
        var json = JsonSerializer.Serialize(new[] { Release("v1.1.0"), Release("v3.0.0-beta", true), Release("v1.0.9"), Release("v9.0.0", draft: true) });
        Assert.Equal("1.1.0", UpdateService.SelectRelease(json, "1.0.0")!.Version);
        Assert.Equal("3.0.0-beta", UpdateService.SelectRelease(json, "1.0.0-alpha")!.Version);
    }
    [Theory]
    [InlineData("bad", "github.com")]
    [InlineData("sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "evil.example")]
    public void IncompleteOrUntrustedLatestCannotShowGreen(string digest, string host)
    {
        var json = JsonSerializer.Serialize(new[] { Release("v1.0.0"), Release("v1.1.0", digest: digest, host: host) });
        Assert.Throws<InvalidDataException>(() => UpdateService.SelectRelease(json, "1.0.0"));
    }
    [Fact]
    public void ModifiedDownloadIsRejected()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        Assert.Throws<InvalidDataException>(() => UpdateService.VerifyDigest(stream, new string('a', 64)));
        stream.Position = 0;
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)); stream.Position = 0;
        UpdateService.VerifyDigest(stream, hash); Assert.Equal(0, stream.Position);
    }
    [Fact]
    public async Task OfflineFailurePreservesKnownReleaseButDoesNotClaimCurrent()
    {
        var handler = new Handler(JsonSerializer.Serialize(new[] { Release("v1.1.0") }));
        using var service = new UpdateService("1.0.0", handler);
        await service.CheckAsync(); Assert.Equal(UpdateState.Available, service.State);
        handler.Fail = true; await service.CheckAsync();
        Assert.Equal(UpdateState.Failed, service.State); Assert.Equal("1.1.0", service.Release!.Version);
    }
    [Fact]
    public async Task NewerLocalBuildIsCurrentAndConcurrentChecksAreCoalesced()
    {
        var handler = new Handler(JsonSerializer.Serialize(new[] { Release("v1.0.0") }));
        using var service = new UpdateService("1.1.0", handler);
        var first = service.CheckAsync(); await service.CheckAsync(); await first;
        Assert.Equal(1, handler.Calls); Assert.Equal(UpdateState.Current, service.State);
    }
    private sealed class Handler(string json) : HttpMessageHandler
    {
        internal bool Fail; internal int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++; await Task.Delay(20, token);
            if (Fail) throw new HttpRequestException("Offline");
            return new(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }
}
