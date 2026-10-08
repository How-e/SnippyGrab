param(
    [Parameter(Mandatory)][ValidateSet('SnippyGrab', 'SnippingTool')][string]$Receiver,
    [ValidateRange(2, 30)][int]$Samples = 10,
    [ValidateRange(30, 600)][int]$TimeoutSeconds = 300,
    [string]$ReportDirectory = (Join-Path $PSScriptRoot '../artifacts/region-clipboard'),
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'
# Passive observation only: no injected input, clipboard content reads, screenshots,
# preference changes, or process termination. Polling adds approximately 5 ms resolution.
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class RegionClipboardProbe {
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] public static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern uint RegisterClipboardFormat(string name);
    [DllImport("user32.dll")] public static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
}
'@
Add-Type -AssemblyName System.Windows.Forms
if ($ValidateOnly) {
    $point = [RegionClipboardProbe+Point]::new()
    if (![RegionClipboardProbe]::GetCursorPos([ref]$point)) { throw 'Cursor observation unavailable.' }
    if (![RegionClipboardProbe]::RegisterClipboardFormat('PNG')) { throw 'PNG format registration failed.' }
    [RegionClipboardProbe]::GetClipboardSequenceNumber() | Out-Null
    [RegionClipboardProbe]::GetAsyncKeyState(1) | Out-Null
    [RegionClipboardProbe]::IsClipboardFormatAvailable(8) | Out-Null
    $ownerId = [uint32]0
    [RegionClipboardProbe]::GetWindowThreadProcessId([RegionClipboardProbe]::GetClipboardOwner(), [ref]$ownerId) | Out-Null
    Write-Output 'PASS: passive Win32 observation APIs loaded; no clipboard content or input injection.'
    return
}
$runDirectory = Join-Path ([IO.Path]::GetFullPath($ReportDirectory)) (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
[IO.Directory]::CreateDirectory($runDirectory) | Out-Null
$pngFormat = [RegionClipboardProbe]::RegisterClipboardFormat('PNG')
$measurements = [Collections.Generic.List[object]]::new()
$watch = [Diagnostics.Stopwatch]::StartNew()
$deadlineMs = $TimeoutSeconds * 1000
$down = $false
$dragStart = $null
$pending = $null
$result = 'INCOMPLETE'
function Summarize($values) {
    $ordered = @($values | Sort-Object)
    [ordered]@{ Count = $ordered.Count; MedianMs = $ordered[[int][Math]::Ceiling($ordered.Count * .5) - 1]; P95Ms = $ordered[[int][Math]::Ceiling($ordered.Count * .95) - 1]; MinMs = $ordered[0]; MaxMs = $ordered[-1] }
}
Write-Output "Observe $Samples $Receiver region captures within $TimeoutSeconds seconds."
Write-Output 'Keep auto-copy enabled. Use synthetic content. Invoke the region shortcut, drag a region, wait for copy, then repeat. Avoid other clicks/copies during measurement.'
Write-Output 'The first left-button release after each drag is paired with the next image-format clipboard change. Foreign-owner changes and missing copies are rejected.'
try {
    while ($measurements.Count -lt $Samples -and $watch.ElapsedMilliseconds -lt $deadlineMs) {
        $pressed = ([RegionClipboardProbe]::GetAsyncKeyState(1) -band 0x8000) -ne 0
        if ($pressed -and !$down -and !$pending) {
            $point = [RegionClipboardProbe+Point]::new()
            if ([RegionClipboardProbe]::GetCursorPos([ref]$point)) {
                $dragStart = @{ Point = $point; Sequence = [RegionClipboardProbe]::GetClipboardSequenceNumber() }
            }
        }
        if (!$pressed -and $down -and $dragStart -and !$pending) {
            $point = [RegionClipboardProbe+Point]::new()
            if ([RegionClipboardProbe]::GetCursorPos([ref]$point)) {
                $width = [Math]::Abs($point.X - $dragStart.Point.X)
                $height = [Math]::Abs($point.Y - $dragStart.Point.Y)
                if ($width -gt 2 -and $height -gt 2) {
                    $pending = @{ ReleaseMs = $watch.Elapsed.TotalMilliseconds; Sequence = $dragStart.Sequence; Width = $width; Height = $height }
                }
            }
            $dragStart = $null
        }
        $down = $pressed
        if ($pending) {
            $sequence = [RegionClipboardProbe]::GetClipboardSequenceNumber()
            $imageAvailable = [RegionClipboardProbe]::IsClipboardFormatAvailable(8) -or [RegionClipboardProbe]::IsClipboardFormatAvailable(17) -or [RegionClipboardProbe]::IsClipboardFormatAvailable(2) -or [RegionClipboardProbe]::IsClipboardFormatAvailable($pngFormat)
            if ($sequence -ne $pending.Sequence -and $imageAvailable) {
                $ownerId = [uint32]0
                [RegionClipboardProbe]::GetWindowThreadProcessId([RegionClipboardProbe]::GetClipboardOwner(), [ref]$ownerId) | Out-Null
                $owner = if ($ownerId) { (Get-Process -Id $ownerId -ErrorAction SilentlyContinue).ProcessName } else { '' }
                $allowed = if ($Receiver -eq 'SnippyGrab') { @('SnippyGrab') } else { @('SnippingTool', 'ScreenClippingHost') }
                if ($owner -in $allowed) {
                    $elapsed = [Math]::Round($watch.Elapsed.TotalMilliseconds - $pending.ReleaseMs, 2)
                    $measurements.Add([ordered]@{ MouseReleaseToClipboardMs = $elapsed; DragWidth = $pending.Width; DragHeight = $pending.Height; ClipboardOwner = $owner; ClipboardOwnerPid = $ownerId })
                    Write-Output "Sample $($measurements.Count)/${Samples}: $elapsed ms"
                }
                else { Write-Output "Rejected clipboard owner '$owner'; repeat this capture." }
                $pending = $null
            }
            elseif ($watch.Elapsed.TotalMilliseconds - $pending.ReleaseMs -gt 30000) {
                Write-Output 'No fresh image clipboard within 30 seconds; repeat this capture.'
                $pending = $null
            }
        }
        Start-Sleep -Milliseconds 5
    }
    if ($measurements.Count -eq $Samples) { $result = 'COLLECTED' }
}
finally {
    $cpu = Get-CimInstance Win32_Processor | Select-Object -ExpandProperty Name
    $gpu = Get-CimInstance Win32_VideoController | Select-Object -ExpandProperty Name
    $report = [ordered]@{
        Result = $result; Receiver = $Receiver; RequestedSamples = $Samples; Samples = $measurements.ToArray()
        OS = [Environment]::OSVersion.VersionString; CPU = @($cpu); GPU = @($gpu)
        Displays = @([System.Windows.Forms.Screen]::AllScreens | ForEach-Object { @{ Width = $_.Bounds.Width; Height = $_.Bounds.Height; Primary = $_.Primary } })
        Scope = 'Passive polling of user gestures and clipboard sequence/image-format availability. No image contents or screenshots read. Drag dimensions are observed cursor units, not verified output PNG dimensions. Excludes hotkey-to-overlay, file/dock readiness, receiver decoding, cold boot and 4K/8K unless actually selected. COLLECTED is measurement completion, not acceptance PASS.'
    }
    if ($measurements.Count) { $report.Timing = Summarize @($measurements | ForEach-Object { $_.MouseReleaseToClipboardMs }) }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $runDirectory 'summary.json') -Encoding utf8
    Write-Output "$result report: $(Join-Path $runDirectory 'summary.json')"
}
