param([switch]$Uninstall, [switch]$Startup)
$ErrorActionPreference = 'Stop'
# Per-user fixed paths; no user input is interpolated into shell commands.
$installRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs/SnippyGrab'))
$programRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs'))
if (-not $installRoot.StartsWith($programRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Leaf $installRoot) -ne 'SnippyGrab') { throw 'Unexpected install path.' }
for ($checkPath = $installRoot; $checkPath; $checkPath = Split-Path -Parent $checkPath) {
    if ((Test-Path -LiteralPath $checkPath) -and ((Get-Item -LiteralPath $checkPath).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Redirected installation directories are not supported.' }
}
$exePath = Join-Path $installRoot 'SnippyGrab.exe'
$running = Get-Process -Name SnippyGrab -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exePath }
if ($running) { throw 'Exit SnippyGrab from its tray menu before installing, updating or uninstalling.' }
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'SnippyGrab.lnk'
$uninstallKey = 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/SnippyGrab'
$startupKey = 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Run'
if ($Uninstall) {
    $startupEntry = Get-ItemProperty -LiteralPath $startupKey -ErrorAction SilentlyContinue
    $existing = $startupEntry.SnippyGrab
    if ($existing -eq ('"' + $exePath + '" --background')) { Remove-ItemProperty -LiteralPath $startupKey -Name SnippyGrab -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $shortcutPath) { Remove-Item -LiteralPath $shortcutPath }
    if (Test-Path -LiteralPath $uninstallKey) { Remove-Item -LiteralPath $uninstallKey }
    if (Test-Path -LiteralPath $installRoot) {
        if (Get-ChildItem -LiteralPath $installRoot -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Remove redirected application entries before uninstalling.' }
        Remove-Item -LiteralPath $installRoot -Recurse -Force
    }
    Write-Output 'Uninstalled. Captures/settings remain in LocalAppData/SnippyGrab for recovery; remove that directory separately if desired.'
    return
}
if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'SnippyGrab.exe'))) { throw 'Run install.ps1 from the extracted release bundle.' }
if ([IO.Path]::GetFullPath($PSScriptRoot).Equals($installRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Install from a separate extracted bundle.' }
. (Join-Path $PSScriptRoot 'install-files.ps1')
$previousRun = (Get-ItemProperty -LiteralPath $startupKey -ErrorAction SilentlyContinue).SnippyGrab
$previousUninstall = Get-ItemProperty -LiteralPath $uninstallKey -ErrorAction SilentlyContinue
$previousShortcut = if (Test-Path -LiteralPath $shortcutPath) { [IO.File]::ReadAllBytes($shortcutPath) } else { $null }
$transaction = Install-SnippyFiles -Source $PSScriptRoot -Target $installRoot
try {
$wsh = New-Object -ComObject WScript.Shell
$shortcut = $wsh.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath; $shortcut.WorkingDirectory = $installRoot; $shortcut.IconLocation = $exePath; $shortcut.Save()
New-Item -Path $uninstallKey -Force | Out-Null
New-ItemProperty -LiteralPath $uninstallKey -Name DisplayName -Value 'SnippyGrab' -PropertyType String -Force | Out-Null
New-ItemProperty -LiteralPath $uninstallKey -Name DisplayVersion -Value ([Diagnostics.FileVersionInfo]::GetVersionInfo($exePath).ProductVersion) -PropertyType String -Force | Out-Null
New-ItemProperty -LiteralPath $uninstallKey -Name InstallLocation -Value $installRoot -PropertyType String -Force | Out-Null
$uninstallScript = Join-Path $installRoot 'install.ps1'
$uninstallCommand = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + $uninstallScript + '" -Uninstall'
New-ItemProperty -LiteralPath $uninstallKey -Name UninstallString -Value $uninstallCommand -PropertyType String -Force | Out-Null
New-ItemProperty -LiteralPath $uninstallKey -Name DisplayIcon -Value $exePath -PropertyType String -Force | Out-Null
foreach ($name in @('NoModify', 'NoRepair')) { New-ItemProperty -LiteralPath $uninstallKey -Name $name -Value 1 -PropertyType DWord -Force | Out-Null }
if ($Startup -or $previousRun -eq ('"' + $exePath + '" --background')) { New-Item -Path $startupKey -Force | Out-Null; New-ItemProperty -LiteralPath $startupKey -Name SnippyGrab -Value ('"' + $exePath + '" --background') -PropertyType String -Force | Out-Null }
Write-Output "Installed: $exePath"
if ($transaction.Backup) { Write-Output "Previous application files preserved for rollback: $($transaction.Backup)" }
} catch {
    $failure = $_
    Undo-SnippyFiles $transaction
    if ($previousShortcut) { [IO.File]::WriteAllBytes($shortcutPath, $previousShortcut) } elseif (Test-Path -LiteralPath $shortcutPath) { Remove-Item -LiteralPath $shortcutPath }
    if (Test-Path -LiteralPath $uninstallKey) { Remove-Item -LiteralPath $uninstallKey }
    if ($previousUninstall) {
        New-Item -Path $uninstallKey -Force | Out-Null
        foreach ($property in $previousUninstall.PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' }) { New-ItemProperty -LiteralPath $uninstallKey -Name $property.Name -Value $property.Value -PropertyType $(if ($property.Value -is [int]) { 'DWord' } else { 'String' }) -Force | Out-Null }
    }
    if ($previousRun) { New-Item -Path $startupKey -Force | Out-Null; New-ItemProperty -LiteralPath $startupKey -Name SnippyGrab -Value $previousRun -PropertyType String -Force | Out-Null } else { Remove-ItemProperty -LiteralPath $startupKey -Name SnippyGrab -ErrorAction SilentlyContinue }
    throw $failure
}
