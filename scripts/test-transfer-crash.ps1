param([string]$Executable = (Join-Path $PSScriptRoot '../src/SnippyGrab.App/bin/Release/net10.0-windows/SnippyGrab.exe'), [string]$Report = (Join-Path $PSScriptRoot '../artifacts/gate-review-20261007/q13-crash.json'))
$ErrorActionPreference = 'Stop'
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('SnippyGrab-transfer-crash-' + [guid]::NewGuid().ToString('N'))
$resolvedRoot = [IO.Path]::GetFullPath($fixtureRoot)
if ([IO.Path]::GetDirectoryName($resolvedRoot) -ne [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath([IO.Path]::GetTempPath()))) { throw 'Unexpected crash fixture root.' }
$executablePath = (Resolve-Path -LiteralPath $Executable).Path
$reportPath = [IO.Path]::GetFullPath($Report)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($reportPath)) | Out-Null
$child = $null
try {
    $child = Start-Process -FilePath $executablePath -ArgumentList '--check-transfer-crash', 'hold', ('"' + $resolvedRoot + '"') -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while (!(Test-Path -LiteralPath (Join-Path $resolvedRoot 'ready.json'))) {
        if ($child.HasExited -or [DateTime]::UtcNow -gt $deadline) { throw 'Dedicated crash fixture did not become ready.' }
        Start-Sleep -Milliseconds 50
    }
    # Terminate only the process returned by this launch, after its durable lease/revision readiness signal.
    $child.Kill(); $child.WaitForExit()
    $verify = Start-Process -FilePath $executablePath -ArgumentList '--check-transfer-crash', 'verify', ('"' + $resolvedRoot + '"'), ('"' + $reportPath + '"') -WindowStyle Hidden -PassThru
    if (!$verify.WaitForExit(15000)) { $verify.Kill(); throw 'Crash restart verification timed out.' }
    $verify.Refresh()
    if ($verify.ExitCode -ne 0) { throw "Crash restart verification failed. See $reportPath" }
    $result = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if ($result.Result -ne 'PASS') { throw 'Crash result was not PASS.' }
    Write-Output 'PASS: dedicated child termination, immutable delayed read at 23h, cleanup at 24h, pin preserved.'
}
finally {
    if ($child -and !$child.HasExited) { $child.Kill(); $child.WaitForExit() }
    if (Test-Path -LiteralPath $resolvedRoot) { Remove-Item -LiteralPath $resolvedRoot -Recurse -Force }
}
