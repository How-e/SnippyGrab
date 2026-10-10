$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('SnippyGrab-update-lab-' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $root 'bundle'
$target = Join-Path $root 'portable app with spaces'
New-Item -ItemType Directory -Path $source, $target -Force | Out-Null
$helper = $null
try {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'update-helper.ps1') -Destination $root
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install-files.ps1') -Destination $root
    foreach ($name in @('install.ps1', 'install-files.ps1')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $source }
    # A harmless native executable exercises Process.Start without launching the real app.
    Copy-Item -LiteralPath (Join-Path $env:SystemRoot 'System32/hostname.exe') -Destination (Join-Path $source 'SnippyGrab.exe')
    Set-Content -LiteralPath (Join-Path $source 'BUILD-PROVENANCE.json') '{"Version":"fixture-new"}'
    Get-ChildItem -LiteralPath $source -File | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName).Hash, $_.Name } | Set-Content -LiteralPath (Join-Path $source 'SHA256SUMS.txt')
    Set-Content -LiteralPath (Join-Path $target 'BUILD-PROVENANCE.json') '{"Version":"fixture-old"}'
    Set-Content -LiteralPath (Join-Path $target 'SnippyGrab.exe') 'old-app'
    Set-Content -LiteralPath (Join-Path $target 'obsolete.dll') 'old-sidecar'
    $info = [Diagnostics.ProcessStartInfo]::new((Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'))
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    foreach ($arg in @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'update-helper.ps1'), '-ParentId', '2147483647', '-Target', $target)) { $info.ArgumentList.Add($arg) }
    $helper = [Diagnostics.Process]::Start($info)
    if (-not $helper.WaitForExit(30000)) { $helper.Kill(); throw 'Update helper timed out.' }
    if ($helper.ExitCode -ne 0 -or (Test-Path -LiteralPath (Join-Path $root 'update-error.txt'))) { throw 'Update helper failed.' }
    if ((Get-Content -LiteralPath (Join-Path $target 'BUILD-PROVENANCE.json') -Raw).Trim() -ne '{"Version":"fixture-new"}') { throw 'Helper did not install the update.' }
    if (Test-Path -LiteralPath (Join-Path $target 'obsolete.dll')) { throw 'Helper kept an obsolete sidecar in the new app.' }
    $backup = @(Get-ChildItem -LiteralPath $root -Directory -Filter 'SnippyGrab-backup-*')
    if ($backup.Count -ne 1 -or (Get-Content -LiteralPath (Join-Path $backup[0].FullName 'SnippyGrab.exe')) -ne 'old-app') { throw 'Helper did not preserve previous files.' }
    Write-Output 'PASS: Windows PowerShell helper, custom portable path, verified bundle transaction, backup and native restart launch.'
} finally {
    if ($helper) { if (-not $helper.HasExited) { $helper.Kill(); $helper.WaitForExit() }; $helper.Dispose() }
    $full = [IO.Path]::GetFullPath($root)
    if ([IO.Path]::GetDirectoryName($full) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -or -not [IO.Path]::GetFileName($full).StartsWith('SnippyGrab-update-lab-')) { throw 'Unexpected fixture cleanup path.' }
    Remove-Item -LiteralPath $full -Recurse -Force
}
