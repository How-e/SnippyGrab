using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class StartupRegistrationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("\"C:\\Old\\SnippyGrab.exe\" --background")]
    [InlineData("C:\\Program Files\\SnippyGrab\\SnippyGrab.exe --background")]
    [InlineData("\"C:\\Program Files\\SnippyGrab\\SnippyGrab.exe\" --background & other.exe")]
    [InlineData("\"C:\\Program Files\\SnippyGrab\\SnippyGrab.exe\"")]
    public void StaleMalformedOrDifferentRegistrationsAreNotEnabled(string? command) =>
        Assert.False(StartupCommand.Matches(command, @"C:\Program Files\SnippyGrab\SnippyGrab.exe"));
    [Fact]
    public void ExactQuotedSpacedPathMatchesCaseInsensitively() => Assert.True(StartupCommand.Matches(
        "\"c:\\program files\\snippygrab\\SNIPPYGRAB.EXE\" --background", @"C:\Program Files\SnippyGrab\SnippyGrab.exe"));
    [Fact]
    public void DisablingStaleRegistrationRemovesItAndFailedSaveRestoresExactOldPath()
    {
        const string stale = "\"C:\\Older Version\\SnippyGrab.exe\" --background";
        string? value = stale;
        StartupSettingsTransaction.Apply(new Settings { LaunchOnStartup = false }, value, @"C:\New\SnippyGrab.exe", command => value = command, _ => { });
        Assert.Null(value);
        value = stale;
        Assert.Throws<IOException>(() => StartupSettingsTransaction.Apply(new Settings { LaunchOnStartup = true }, value, @"C:\New\SnippyGrab.exe", command => value = command, _ => throw new IOException()));
        Assert.Equal(stale, value);
    }
}
