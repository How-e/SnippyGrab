param([int]$ParentId, [Parameter(Mandatory)][string]$Target)
$ErrorActionPreference = 'Stop'
# A helper launched from PowerShell 7 can inherit its incompatible module search path.
# Load the Windows PowerShell utility module explicitly for bundle hash verification.
$targetPath = [IO.Path]::GetFullPath($Target)
$source = Join-Path $PSScriptRoot 'bundle'
$exe = Join-Path $targetPath 'SnippyGrab.exe'
try {
    Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Utility') -Force
    $parentProcess = Get-Process -Id $ParentId -ErrorAction SilentlyContinue
    if ($parentProcess -and -not $parentProcess.WaitForExit(120000)) { throw 'SnippyGrab did not exit. The update was cancelled.' }
    # Use the installed validator before executing any script from the downloaded bundle.
    . (Join-Path $PSScriptRoot 'install-files.ps1')
    Assert-SnippyInstallPath $targetPath (Split-Path -Parent $targetPath)
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf) -or -not (Test-Path -LiteralPath (Join-Path $targetPath 'BUILD-PROVENANCE.json'))) { throw 'The original application directory is unavailable.' }
    $running = Get-Process -Name SnippyGrab -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }
    if ($running) { throw 'Another SnippyGrab process is running. Exit it before retrying.' }
    Test-SnippyBundle $source
    $installedPath = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs/SnippyGrab'))
    if ($targetPath.Equals($installedPath, [StringComparison]::OrdinalIgnoreCase)) {
        & (Join-Path $source 'install.ps1')
    } else {
        $transaction = Install-SnippyFiles -Source $source -Target $targetPath -Portable
    }
    Start-Process -FilePath $exe -ArgumentList '--background' -WorkingDirectory $targetPath -WindowStyle Hidden
} catch {
    $failure = $_.Exception.Message
    $failure | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'update-error.txt')
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show("Update failed: $failure`n`nYour previous application files and captures are preserved. Details: $PSScriptRoot", 'SnippyGrab update') | Out-Null
}
