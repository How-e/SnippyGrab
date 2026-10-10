using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace SnippyGrab.App.Services;

internal enum UpdateState { Unknown, Checking, Current, Available, Failed }
internal sealed record ReleaseInfo(string Version, string Notes, Uri Page, Uri Archive, string Digest);

internal sealed class UpdateService : IDisposable
{
    private readonly HttpClient http;
    private readonly CancellationTokenSource lifetime = new();
    private readonly string current;
    internal UpdateState State { get; private set; }
    internal ReleaseInfo? Release { get; private set; }
    internal string Detail { get; private set; } = "Updates have not been checked yet.";
    internal DateTimeOffset? CheckedAt { get; private set; }
    internal event Action? Changed;
    internal UpdateService(string? version = null, HttpMessageHandler? handler = null)
    {
        current = version ?? BuildVersion.Display;
        http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromSeconds(30);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SnippyGrab-Updater/1.0");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }
    internal static ReleaseInfo? SelectRelease(string json, string current)
    {
        var installed = ReleaseVersion.Parse(current) ?? throw new InvalidDataException("This build has an unknown version.");
        using var doc = JsonDocument.Parse(json);
        ReleaseInfo? selected = null; ReleaseVersion? best = null;
        foreach (var release in doc.RootElement.EnumerateArray())
        {
            if (release.GetProperty("draft").GetBoolean() || (installed.Pre.Length == 0 && release.GetProperty("prerelease").GetBoolean())) continue;
            var tag = release.GetProperty("tag_name").GetString() ?? "";
            var version = ReleaseVersion.Parse(tag);
            if (version is null || (installed.Pre.Length == 0 && version.Pre.Length != 0) || (best is not null && version.CompareTo(best) <= 0)) continue;
            best = version; selected = null;
            var name = $"SnippyGrab-{tag.TrimStart('v')}-win-x64.zip";
            foreach (var asset in release.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() != name || asset.GetProperty("state").GetString() != "uploaded") continue;
                var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
                if (!System.Text.RegularExpressions.Regex.IsMatch(digest, "^sha256:[a-fA-F0-9]{64}$")) continue;
                var page = new Uri(release.GetProperty("html_url").GetString()!);
                var archive = new Uri(asset.GetProperty("browser_download_url").GetString()!);
                if (!TrustedUrl(page, "/releases/tag/") || !TrustedUrl(archive, "/releases/download/")) continue;
                selected = new(tag.TrimStart('v'), release.GetProperty("body").GetString() ?? "No release notes supplied.", page, archive, digest[7..]);
            }
        }
        if (best is not null && selected is null) throw new InvalidDataException("The latest release does not have a compatible download with a SHA-256 digest. Visit GitHub releases and retry later.");
        return selected;
    }
    private static bool TrustedUrl(Uri uri, string suffix) => uri.Scheme == "https" && uri.Host == "github.com" && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.AbsolutePath.StartsWith("/How-e/SnippyGrab" + suffix, StringComparison.Ordinal);
    internal async Task CheckAsync()
    {
        if (State == UpdateState.Checking) return;
        State = UpdateState.Checking; Detail = "Checking for updates…"; Changed?.Invoke();
        try
        {
            var releases = new List<JsonElement>();
            for (var page = 1; ; page++)
            {
                if (page > 10) throw new InvalidDataException("Release history exceeded the check limit.");
                using var response = JsonDocument.Parse(await http.GetStringAsync($"https://api.github.com/repos/How-e/SnippyGrab/releases?per_page=100&page={page}", lifetime.Token));
                releases.AddRange(response.RootElement.EnumerateArray().Select(r => r.Clone()));
                if (response.RootElement.GetArrayLength() < 100) break;
            }
            var json = JsonSerializer.Serialize(releases);
            var latest = SelectRelease(json, current) ?? throw new InvalidDataException("No compatible release with a verified download is available.");
            Release = latest; CheckedAt = DateTimeOffset.Now;
            State = ReleaseVersion.Parse(latest.Version)!.CompareTo(ReleaseVersion.Parse(current)) > 0 ? UpdateState.Available : UpdateState.Current;
            Detail = State == UpdateState.Available ? $"Update available: {latest.Version}" : "SnippyGrab is up to date";
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidDataException or UriFormatException or InvalidOperationException or KeyNotFoundException)
        { State = UpdateState.Failed; Detail = "Could not check for updates. " + ex.Message; }
        Changed?.Invoke();
    }
    internal async Task<string> PrepareAsync(ReleaseInfo release, IProgress<string> progress, CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            progress.Report("Downloading update…");
            var zip = Path.Combine(root, "release.zip");
            using (var response = await http.GetAsync(release.Archive, HttpCompletionOption.ResponseHeadersRead, linked.Token))
            {
                response.EnsureSuccessStatusCode();
                using var downloadTimeout = CancellationTokenSource.CreateLinkedTokenSource(linked.Token); downloadTimeout.CancelAfter(TimeSpan.FromMinutes(15));
                await using var input = await response.Content.ReadAsStreamAsync(downloadTimeout.Token);
                await using var output = File.Create(zip);
                var buffer = new byte[81920]; long total = 0; int count;
                while ((count = await input.ReadAsync(buffer, downloadTimeout.Token)) != 0)
                {
                    total += count; if (total > 1024L * 1024 * 1024) throw new InvalidDataException("Update archive is too large.");
                    await output.WriteAsync(buffer.AsMemory(0, count), downloadTimeout.Token);
                    progress.Report($"Downloading update… {total / (1024 * 1024)} MB");
                }
            }
            progress.Report("Verifying and preparing update…");
            await Task.Run(() =>
            {
                linked.Token.ThrowIfCancellationRequested();
                using var input = File.OpenRead(zip);
                VerifyDigest(input, release.Digest);
                using var archive = new ZipArchive(input, ZipArchiveMode.Read);
                if (archive.Entries.Sum(e => e.Length) > 2L * 1024 * 1024 * 1024 || archive.Entries.Count > 10000) throw new InvalidDataException("Update bundle is too large.");
                archive.ExtractToDirectory(Path.Combine(root, "bundle"));
                foreach (var required in new[] { "SnippyGrab.exe", "install.ps1", "install-files.ps1", "SHA256SUMS.txt" })
                    if (!File.Exists(Path.Combine(root, "bundle", required))) throw new InvalidDataException("Incomplete update bundle.");
            }, linked.Token);
            foreach (var helper in new[] { "update-helper.ps1", "install-files.ps1" })
                File.Copy(Path.Combine(AppContext.BaseDirectory, helper), Path.Combine(root, helper));
            return root;
        }
        catch { Directory.Delete(root, true); throw; }
    }
    internal static void VerifyDigest(Stream stream, string expected)
    {
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        if (!hash.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update checksum mismatch. Nothing was installed.");
        stream.Position = 0;
    }
    internal static bool CanInstall => new[] { "BUILD-PROVENANCE.json", "update-helper.ps1", "install-files.ps1" }.All(name => File.Exists(Path.Combine(AppContext.BaseDirectory, name)));
    internal static void Launch(string root)
    {
        var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = root };
        foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(root, "update-helper.ps1"), "-ParentId", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture), "-Target", AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) }) info.ArgumentList.Add(arg);
        _ = Process.Start(info) ?? throw new InvalidOperationException("Could not start the update helper.");
    }
    public void Dispose() { lifetime.Cancel(); http.Dispose(); lifetime.Dispose(); }
}
