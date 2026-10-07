param([string]$Bundle = (Join-Path $PSScriptRoot '../artifacts/SnippyGrab-0.1.0-alpha.acceptance.20261007.1-win-x64'), [switch]$RealApp)
$ErrorActionPreference = 'Stop'
$bundlePath = (Resolve-Path -LiteralPath $Bundle).Path
$provenance = Get-Content -LiteralPath (Join-Path $bundlePath 'BUILD-PROVENANCE.json') -Raw | ConvertFrom-Json
. (Join-Path $PSScriptRoot 'install-files.ps1')
Test-SnippyBundle $bundlePath
$currentRevision = git -C (Split-Path -Parent $PSScriptRoot) rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Git revision unavailable.' }
if ($provenance.Commit -ne $currentRevision) { throw 'Acceptance build is stale. Prepare a build from the current commit before testing.' }
Write-Output "Testing $($provenance.Version), source $($provenance.Commit), Dirty=$($provenance.Dirty)"
if ($provenance.Dirty) { throw 'Acceptance requires committed source.' }
$executable = Join-Path $bundlePath 'SnippyGrab.exe'
if ($RealApp) {
    $existing = @(Get-Process -Name SnippyGrab -ErrorAction SilentlyContinue)
    if ($existing.Count) { throw 'Exit every running SnippyGrab instance through its tray or fixture first, then rerun with -RealApp.' }
    Start-Process -FilePath $executable -WindowStyle Hidden | Out-Null
    Write-Output 'Real app started: normal capture history, global hotkeys and OS clipboard are active. Test only synthetic content in unsent drafts.'
}
else {
    $results = Join-Path (Split-Path -Parent $bundlePath) 'acceptance-results'
    [IO.Directory]::CreateDirectory($results) | Out-Null
    $report = Join-Path $results ('interactive-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.json')
    Start-Process -FilePath $executable -ArgumentList '--interactive-check', ('"' + $report + '"') -WindowStyle Hidden | Out-Null
    Write-Output "Isolated fixture opened: use CAPTURE 01-20. Clipboard is intercepted. Local report: $report"
}
