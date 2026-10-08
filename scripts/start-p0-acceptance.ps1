param([string]$Root, [string]$ReportName = ('native-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')))
$ErrorActionPreference = 'Stop'
if ($ReportName -notmatch '^[a-zA-Z0-9-]+$') { throw 'ReportName must contain only letters, numbers and hyphens.' }
$repository = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $repository 'src/SnippyGrab.App/bin/Release/net10.0-windows/SnippyGrab.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build the Release solution before launching P0 acceptance.' }
$reports = Join-Path $repository 'artifacts/p0-acceptance'
[IO.Directory]::CreateDirectory($reports) | Out-Null
$report = Join-Path $reports ($ReportName + '.json')
if (Test-Path -LiteralPath $report) { throw 'Choose a new ReportName; acceptance reports must not be overwritten.' }
$arguments = @('--p0-acceptance', ('"' + $report + '"'))
if ($Root) {
    $fixtureRoot = [IO.Path]::GetFullPath($Root)
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $fixtureRoot.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($fixtureRoot).StartsWith('SnippyGrab-p0-', [StringComparison]::Ordinal)) { throw 'Root must be an isolated P0 temporary directory.' }
    if (-not (Test-Path -LiteralPath (Join-Path $fixtureRoot 'p0-fixture.json'))) { throw 'The P0 fixture marker is missing.' }
    $arguments += '"' + $fixtureRoot + '"'
}
$provenance = [ordered]@{
    Source = (git -C $repository rev-parse HEAD)
    Dirty = [bool](git -C $repository status --porcelain)
    ExecutableSha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
    ManagedAssemblySha256 = (Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $executable) 'SnippyGrab.dll') -Algorithm SHA256).Hash
    Scope = 'Synthetic isolated P0 acceptance; real OS clipboard and fixture tray icon; no normal user cache/hotkeys/startup'
}
$provenance | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reports ($ReportName + '-launch.json'))
$process = Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "P0 fixture PID $($process.Id). Report: $report"
Write-Output 'Use only the tray icon labeled SnippyGrab P0 acceptance. Apply/copy replaces the OS clipboard with synthetic pixels.'
