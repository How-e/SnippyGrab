param([switch]$NativeChecks, [string]$ReportDirectory = (Join-Path $PSScriptRoot '../artifacts/agent-verification'))
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$runDirectory = Join-Path ([IO.Path]::GetFullPath($ReportDirectory)) (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
[IO.Directory]::CreateDirectory($runDirectory) | Out-Null
$steps = [Collections.Generic.List[object]]::new()
$revision = git -C $repoRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Git revision unavailable.' }
$dirty = [bool](git -C $repoRoot status --porcelain)
$result = 'RUNNING'
function Save-Verification {
    [ordered]@{ Result = $result; Commit = $revision; Dirty = $dirty; NativeChecks = [bool]$NativeChecks; Steps = $steps.ToArray(); Scope = 'Local build, tests, boundary policy and optional isolated native probes. Does not establish gestures, external receivers, hardware, clean-profile/login or stable release acceptance.' } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $runDirectory 'summary.json') -Encoding utf8
}
function Invoke-Step([string]$Name, [scriptblock]$Action, [switch]$Critical) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $step = [ordered]@{ Name = $Name; Result = 'RUNNING'; Seconds = 0; Log = "$Name.log" }
    $steps.Add($step); Save-Verification
    Write-Output "Checking $Name"
    try {
        & $Action *>&1 | Tee-Object -FilePath (Join-Path $runDirectory $step.Log)
        $step.Result = 'PASS'
    }
    catch { $step.Result = 'FAIL'; Write-Output "FAIL: $Name; $($_.Exception.Message)"; if ($Critical) { throw } }
    finally { $step.Seconds = $watch.Elapsed.TotalSeconds; Save-Verification }
}
function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
}
Push-Location $repoRoot
try {
    Invoke-Step 'restore' { Invoke-Dotnet @('restore', '--locked-mode') } -Critical
    Invoke-Step 'build' { Invoke-Dotnet @('build', '--no-restore', '-c', 'Release', '-warnaserror') } -Critical
    Invoke-Step 'tests' { Invoke-Dotnet @('test', '--no-build', '-c', 'Release', '--logger', 'trx', '--results-directory', (Join-Path $runDirectory 'tests')) }
    Invoke-Step 'format' { Invoke-Dotnet @('format', '--no-restore', '--verify-no-changes') }
    foreach ($script in @('test-native-ocr-policy', 'test-webp-policy', 'test-release-gates', 'test-resource-review', 'test-signing', 'test-installer', 'test-install-files', 'test-update-helper', 'test-artifact-retention')) {
        $scriptPath = Join-Path $PSScriptRoot ($script + '.ps1')
        Invoke-Step $script { & $scriptPath }
    }
    Invoke-Step 'milestone-map' { & (Join-Path $PSScriptRoot 'test-milestone-map.ps1') -SelfTest }
    Invoke-Step 'dependency-fixtures' { & (Join-Path $PSScriptRoot 'audit-dependencies.ps1') -SelfTest }
    Invoke-Step 'dependency-audit' { & (Join-Path $PSScriptRoot 'audit-dependencies.ps1') }
    Invoke-Step 'whitespace' { git diff --check; if ($LASTEXITCODE -ne 0) { throw 'Git whitespace verification failed.' } }
    if ($NativeChecks) {
        $executable = Join-Path $repoRoot 'src/SnippyGrab.App/bin/Release/net10.0-windows/SnippyGrab.exe'
        foreach ($check in @('reliability', 'overlay-layout', 'editor-layout', 'ocr-corpus', 'dock-layout', 'pipeline-latency', 'editor-performance')) {
            Invoke-Step $check {
                $report = Join-Path $runDirectory ($check + '.json')
                & (Join-Path $PSScriptRoot 'test-native-probe.ps1') -Check $check -Executable $executable -Report $report
            }
        }
        Invoke-Step 'transfer-crash' { & (Join-Path $PSScriptRoot 'test-transfer-crash.ps1') -Executable $executable -Report (Join-Path $runDirectory 'transfer-crash.json') }
        Invoke-Step 'startup-latency' { & (Join-Path $PSScriptRoot 'measure-startup.ps1') -Executable $executable -ReportDirectory (Join-Path $runDirectory 'startup') }
    }
    if (@($steps | Where-Object { $_.Result -ne 'PASS' }).Count) { throw 'Verification has failed checks; inspect summary.json and individual reports.' }
    $result = 'PASS'
}
catch { $result = 'FAIL'; throw }
finally { Save-Verification; Pop-Location; Write-Output "Verification report: $(Join-Path $runDirectory 'summary.json')" }
