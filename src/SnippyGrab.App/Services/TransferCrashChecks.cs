using System.Security.Cryptography;
using System.Text.Json;

namespace SnippyGrab.App.Services;

// Dedicated child-process fixture. It never initializes the production controller or OS integrations.
internal static class TransferCrashChecks
{
    private sealed record Ready(DateTimeOffset Started, string Original, string Current, string Pin, string Hash);
    private static string ValidateRoot(string directory)
    {
        var root = Path.GetFullPath(directory);
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        if (!string.Equals(Path.GetDirectoryName(root), temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(root).StartsWith("SnippyGrab-transfer-crash-", StringComparison.Ordinal))
            throw new InvalidDataException("Crash fixture requires a dedicated immediate temporary child directory.");
        ManagedPath.RejectRedirects(root);
        return root;
    }
    public static async Task Hold(string directory)
    {
        var root = ValidateRoot(directory);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()) throw new InvalidDataException("Crash fixture must start empty.");
        var started = DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var repository = new CaptureRepository(root, utcNow: () => started);
        var png = ImageService.Png(RuntimeChecks.SyntheticCode(320, 160));
        var record = repository.Add(png, 320, 160); var original = record.FileName;
        using var transfer = repository.Lease([record], transfer: true);
        repository.Replace(record, ImageService.Png(RuntimeChecks.SyntheticCode(640, 240)), 640, 240);
        var pin = repository.Add(png, 320, 160); repository.SetPinned(pin, true);
        AtomicFile.Write(Path.Combine(root, "ready.json"), JsonSerializer.SerializeToUtf8Bytes(new Ready(started, original, record.FileName, pin.FileName, Convert.ToHexString(SHA256.HashData(png)))));
        await Task.Delay(Timeout.Infinite); // The test terminates only this child; no release/normal-exit cleanup runs.
    }
    public static void Verify(string directory, string destination)
    {
        var root = ValidateRoot(directory);
        var ready = JsonSerializer.Deserialize<Ready>(File.ReadAllText(Path.Combine(root, "ready.json"))) ?? throw new InvalidDataException("Missing crash fixture state.");
        var now = ready.Started.AddHours(23);
        var repository = new CaptureRepository(root, utcNow: () => now); repository.Load();
        if (repository.CleanupBlocked || repository.Captures.Count != 2) throw new InvalidDataException("Crash restart lost the durable metadata state.");
        var original = repository.PathForName(ready.Original);
        if (repository.Cleanup(now, 1, true) != 1 || File.Exists(repository.PathForName(ready.Current))) throw new InvalidDataException("Unprotected revision did not follow retention after crash.");
        if (!File.Exists(original) || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original))) != ready.Hash) throw new InvalidDataException("Delayed read lost immutable transferred bytes during crash grace.");
        now = ready.Started.AddHours(24);
        if (repository.Cleanup(now, 1, true) != 1 || File.Exists(original) || !File.Exists(repository.PathForName(ready.Pin))) throw new InvalidDataException("Crash grace boundary or pin preservation failed.");
        AtomicFile.Write(destination, JsonSerializer.SerializeToUtf8Bytes(new { Result = "PASS", Restart = true, DelayedReadHours = 23, GraceExpiresHours = 24, ImmutableRevision = true, PinPreserved = true, Scope = "Dedicated synthetic child killed without lease release; restart and delayed file reads use a fake clock. No native drag, OS clipboard or external receiver." }));
    }
}
