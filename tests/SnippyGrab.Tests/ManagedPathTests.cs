using System.Diagnostics;
using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class ManagedPathTests
{
    [Fact]
    public void RootReplacedByJunctionAfterStartupCannotRedirectWritesReadsOrCleanup()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "SnippyGrab-path-test-" + Guid.NewGuid().ToString("N"));
        var cache = Path.Combine(workspace, "cache"); var external = Path.Combine(workspace, "external");
        Directory.CreateDirectory(external);
        try
        {
            var repository = new CaptureRepository(cache); repository.Load();
            Directory.Delete(cache);
            var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("/c"); start.ArgumentList.Add("mklink"); start.ArgumentList.Add("/J"); start.ArgumentList.Add(cache); start.ArgumentList.Add(external);
            using var process = Process.Start(start)!; process.WaitForExit(); Assert.Equal(0, process.ExitCode);
            Assert.Throws<InvalidDataException>(() => new CaptureRepository(Path.Combine(cache, "new-cache")));
            Assert.False(Directory.Exists(Path.Combine(external, "new-cache")));
            var sentinel = Path.Combine(external, "capture-" + Guid.NewGuid().ToString("N") + ".png"); File.WriteAllText(sentinel, "fixture");
            var settings = Path.Combine(external, "settings.json"); File.WriteAllText(settings, "external fixture");
            Assert.Throws<InvalidDataException>(() => repository.Add([1], 1, 1));
            Assert.Throws<InvalidDataException>(() => repository.Persist());
            Assert.Throws<InvalidDataException>(() => repository.Load());
            Assert.Throws<InvalidDataException>(() => repository.Cleanup(DateTimeOffset.UtcNow, 1, true));
            Assert.Throws<InvalidDataException>(() => AtomicFile.Write(Path.Combine(cache, "settings.json"), [1]));
            Assert.Throws<InvalidDataException>(() => new SettingsService(Path.Combine(cache, "settings.json")).Load());
            Assert.Equal("external fixture", File.ReadAllText(settings));
            Assert.Equal("fixture", File.ReadAllText(sentinel)); Assert.Equal(2, Directory.GetFiles(external).Length);
        }
        finally
        {
            if (Directory.Exists(cache)) Directory.Delete(cache); // Unlink the junction itself before recursive fixture cleanup.
            if (Directory.Exists(workspace)) Directory.Delete(workspace, true);
        }
    }
}
