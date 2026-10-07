param([Parameter(Mandatory)][string]$Bundle, [Parameter(Mandatory)][string]$Installer)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'install-files.ps1')
Test-SnippyBundle $Bundle
foreach ($path in @("$Bundle.zip", $Installer)) {
    $line = (Get-Content -LiteralPath "$path.sha256" -Raw).Trim()
    if ($line -notmatch '^([0-9a-fA-F]{64})  (.+)$' -or $Matches[2] -ne [IO.Path]::GetFileName($path) -or (Get-FileHash -LiteralPath $path).Hash -ne $Matches[1]) { throw 'Release artifact checksum failed.' }
}
$report = Join-Path (Split-Path -Parent $Installer) 'installer-payload-check.txt'
$process = Start-Process -FilePath $Installer -ArgumentList @('--verify-payload', ('"' + $report + '"')) -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Embedded installer verification failed: $(Get-Content $report)" }
Write-Output 'PASS: complete bundle inventory, ZIP/setup digests and independently embedded setup payload.'
