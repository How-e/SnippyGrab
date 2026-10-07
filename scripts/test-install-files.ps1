$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'install-files.ps1')
$root = Join-Path ([IO.Path]::GetTempPath()) ('SnippyGrab-install-lab-' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $root 'source'; $target = Join-Path $root 'Programs/SnippyGrab'; $data = Join-Path $root 'UserData'
New-Item -ItemType Directory -Path $source, $data -Force | Out-Null
function Write-Fixture([string]$Version) {
    foreach ($name in @('SnippyGrab.exe', 'install.ps1', 'install-files.ps1')) { Set-Content -LiteralPath (Join-Path $source $name) -Value $Version }
    Get-ChildItem -LiteralPath $source -File | Where-Object Name -ne 'SHA256SUMS.txt' | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName).Hash, $_.Name } | Set-Content -LiteralPath (Join-Path $source 'SHA256SUMS.txt')
}
try {
    # Hidden ancestors mirror the real LocalAppData installation path, including Windows PowerShell 5.1.
    (Get-Item -LiteralPath $root -Force).Attributes = [IO.FileAttributes]::Directory -bor [IO.FileAttributes]::Hidden
    Assert-SnippyInstallPath $target (Split-Path -Parent $target)
    $installScript = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'install.ps1') -Raw
    $guard = $installScript.Substring($installScript.IndexOf('for ($checkPath'), $installScript.IndexOf('$exePath =') - $installScript.IndexOf('for ($checkPath'))
    # Execute the real entry script's ancestor guard against the isolated hidden fixture.
    & { $installRoot = $target; Invoke-Expression $guard }
    Set-Content -LiteralPath (Join-Path $data 'sentinel.txt') 'preserve'
    Write-Fixture 'old'; $first = Install-SnippyFiles $source $target
    if ($first.Backup) { throw 'Fresh install had backup.' }
    Set-Content -LiteralPath (Join-Path $target 'obsolete.dll') 'old-sidecar'
    Write-Fixture 'new'; $upgrade = Install-SnippyFiles $source $target
    if ((Test-Path -LiteralPath (Join-Path $target 'obsolete.dll')) -or -not (Test-Path -LiteralPath (Join-Path $upgrade.Backup 'obsolete.dll'))) { throw 'Upgrade did not separate obsolete files.' }
    Undo-SnippyFiles $upgrade
    if ((Get-Content -LiteralPath (Join-Path $target 'SnippyGrab.exe')) -ne 'old') { throw 'Rollback failed.' }
    Set-Content -LiteralPath (Join-Path $source 'SnippyGrab.exe') 'tampered'
    $rejected = $false; try { Install-SnippyFiles $source $target | Out-Null } catch { $rejected = $true }
    if (-not $rejected -or (Get-Content -LiteralPath (Join-Path $target 'SnippyGrab.exe')) -ne 'old') { throw 'Tampering changed installation.' }
    if ((Get-Content -LiteralPath (Join-Path $data 'sentinel.txt')) -ne 'preserve') { throw 'User data changed.' }
    $rejected = $false; try { Assert-SnippyInstallPath (Join-Path $root '../escape') $root } catch { $rejected = $true }
    if (-not $rejected) { throw 'Escaped target accepted.' }
    Write-Output 'PASS: hidden ancestor entry guard, fresh install, upgrade/obsolete isolation, rollback, checksum rejection, data preservation and path containment.'
} finally {
    $full = [IO.Path]::GetFullPath($root)
    if ([IO.Path]::GetDirectoryName($full) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -or -not [IO.Path]::GetFileName($full).StartsWith('SnippyGrab-install-lab-')) { throw 'Unexpected fixture cleanup path.' }
    Remove-Item -LiteralPath $full -Recurse -Force
}
