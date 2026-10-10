param([Parameter(Mandatory)][string]$Bundle, [Parameter(Mandatory)][string]$Installer, [string]$SigningThumbprint, [string]$ExpectedPublisher)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'install-files.ps1')
. (Join-Path $PSScriptRoot 'sign-artifact.ps1')
if ($ExpectedPublisher -and -not $SigningThumbprint) { throw 'ExpectedPublisher requires SigningThumbprint.' }
Test-SnippyBundle $Bundle
foreach ($required in @('BUILD-PROVENANCE.json', 'update-helper.ps1', 'install-files.ps1')) {
    if (-not (Test-Path -LiteralPath (Join-Path $Bundle $required) -PathType Leaf)) { throw "Release bundle is missing $required." }
}
. (Join-Path $PSScriptRoot 'webp-policy.ps1')
$null = Test-SnippyWebpBuild $Bundle -Packaged
foreach ($path in @("$Bundle.zip", $Installer)) {
    $line = (Get-Content -LiteralPath "$path.sha256" -Raw).Trim()
    if ($line -notmatch '^([0-9a-fA-F]{64})  (.+)$' -or $Matches[2] -ne [IO.Path]::GetFileName($path) -or (Get-FileHash -LiteralPath $path).Hash -ne $Matches[1]) { throw 'Release artifact checksum failed.' }
}
$report = Join-Path (Split-Path -Parent $Installer) 'installer-payload-check.txt'
$process = Start-Process -FilePath $Installer -ArgumentList @('--verify-payload', ('"' + $report + '"')) -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Embedded installer verification failed: $(Get-Content $report)" }
if ($SigningThumbprint) {
    if (-not $ExpectedPublisher) { throw 'Signed release verification requires the expected publisher certificate subject.' }
    foreach ($owned in @((Join-Path $Bundle 'SnippyGrab.exe'), $Installer)) {
        Assert-SnippySignature -Path $owned -Thumbprint $SigningThumbprint -ExpectedPublisher $ExpectedPublisher
    }
    Write-Output 'PASS: owned app/setup Authenticode chain, publisher identity and timestamp. Browser/clean-profile acceptance remains separate.'
}
Write-Output 'PASS: complete bundle inventory, ZIP/setup digests and independently embedded setup payload.'
