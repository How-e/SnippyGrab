param([Parameter(Mandatory)][ValidateSet('reliability', 'overlay-layout', 'editor-layout', 'ocr-corpus', 'dock-layout', 'pipeline-latency', 'editor-performance')][string]$Check, [string]$Executable = (Join-Path $PSScriptRoot '../src/SnippyGrab.App/bin/Release/net10.0-windows/SnippyGrab.exe'), [string]$Report)
$ErrorActionPreference = 'Stop'
$executablePath = (Resolve-Path -LiteralPath $Executable).Path
if (!$Report) { $Report = Join-Path $PSScriptRoot ("../artifacts/native-checks/$Check-" + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.json') }
$reportPath = [IO.Path]::GetFullPath($Report)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($reportPath)) | Out-Null
if (Test-Path -LiteralPath $reportPath) { throw 'Use a new report path so an old PASS cannot satisfy a new probe.' }
$process = Start-Process -FilePath $executablePath -ArgumentList ("--check-$Check"), ('"' + $reportPath + '"') -WindowStyle Hidden -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddMinutes(3)
    while (!$process.WaitForExit(1000)) { if ([DateTime]::UtcNow -gt $deadline) { throw "Native $Check timed out." } }
    $process.Refresh()
    if ($process.ExitCode -ne 0) {
        # Hosted runners discard this local report after the job. Expose the first
        # diagnostic line for isolated synthetic CI probes, without a stack dump.
        if ($env:GITHUB_ACTIONS -eq 'true' -and (Test-Path -LiteralPath $reportPath)) {
            $firstLine = Get-Content -LiteralPath $reportPath -TotalCount 1
            Write-Output "Synthetic native failure: $firstLine"
        }
        throw "Native $Check failed; inspect $reportPath"
    }
    $probe = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if ($probe.Result -ne 'PASS') { throw "Native $Check did not report PASS." }
    Write-Output "PASS: $Check; $($probe.Scope)"
}
finally { if (!$process.HasExited) { $process.Kill(); $process.WaitForExit() }; $process.Dispose() }
