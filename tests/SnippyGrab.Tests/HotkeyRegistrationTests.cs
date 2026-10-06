using SnippyGrab.Core;
namespace SnippyGrab.Tests;
public sealed class HotkeyRegistrationTests
{
    [Fact] public void ConflictDoesNotPreventFallbackAndDisablesRepeat()
    {
        var warnings = new List<string>(); var keys = new List<Hotkey>();
        var active = HotkeyRegistration.Register(new(), (id, key) => { keys.Add(key); return id == 5; }, warnings);
        Assert.Equal(CaptureMode.Region, Assert.Single(active).Value); Assert.Equal(4, warnings.Count);
        Assert.All(keys, key => Assert.NotEqual(0u, key.Modifiers & 0x4000));
    }
    [Fact] public void AllConflictsHaveActionableTrayRecovery()
    {
        var warnings = new List<string>();
        Assert.Empty(HotkeyRegistration.Register(new(), (_, _) => false, warnings));
        Assert.All(warnings, warning => Assert.Contains("Tray capture", warning));
        Assert.Contains("Alt+F8", HotkeyRegistration.Guidance(new() { FallbackHotkey = new(119, 1) }));
    }
}
