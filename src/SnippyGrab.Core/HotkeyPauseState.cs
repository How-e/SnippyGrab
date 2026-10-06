namespace SnippyGrab.Core;

public sealed class HotkeyPauseState
{
    private int windows;
    private bool desired;
    public bool Enter(bool currentlyPaused) { if (windows++ == 0) desired = currentlyPaused; return true; }
    public bool Exit() { if (windows > 0) windows--; return windows > 0 || desired; }
    public bool Toggle(bool currentlyPaused) { desired = windows == 0 ? !currentlyPaused : !desired; return windows > 0 || desired; }
}
