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
}
