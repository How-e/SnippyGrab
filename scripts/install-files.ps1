$ErrorActionPreference = 'Stop'
function Assert-SnippyInstallPath([string]$Path, [string]$Parent) {
    $full = [IO.Path]::GetFullPath($Path); $base = [IO.Path]::GetFullPath($Parent).TrimEnd('\', '/')
    if (-not $full.StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetDirectoryName($full) -ne $base) { throw 'Installation path escaped its parent.' }
    for ($probe = $full; $probe; $probe = Split-Path -Parent $probe) {
        if ((Test-Path -LiteralPath $probe) -and ((Get-Item -LiteralPath $probe -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Redirected installation path.' }
    }
}
function Test-SnippyBundle([string]$Source) {
    $root = [IO.Path]::GetFullPath($Source)
    foreach ($item in @(Get-Item -LiteralPath $root -Force) + @(Get-ChildItem -LiteralPath $root -Recurse -Force)) { if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Redirected bundle input.' } }
    $manifest = Join-Path $root 'SHA256SUMS.txt'
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw 'Bundle checksums are required.' }
    if ((Get-Item -LiteralPath $manifest).Length -gt 128KB) { throw 'Bundle checksum manifest too large.' }
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($line in Get-Content -LiteralPath $manifest) {
        if ($line -notmatch '^([0-9a-fA-F]{64})  (.+)$') { throw 'Invalid bundle checksum record.' }
        $expected = $Matches[1]; $relative = $Matches[2]; $path = [IO.Path]::GetFullPath((Join-Path $root $relative))
        if (-not $path.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $relative.Contains(':') -or -not $names.Add($relative.Replace('\', '/'))) { throw 'Unsafe or duplicate bundle path.' }
        for ($probe = $path; $probe -and $probe.StartsWith($root, [StringComparison]::OrdinalIgnoreCase); $probe = Split-Path -Parent $probe) {
            if ((Get-Item -LiteralPath $probe -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Redirected bundle path.' }
        }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $expected) { throw 'Bundle checksum mismatch.' }
    }
    $files = @(Get-ChildItem -LiteralPath $root -Recurse -File -Force)
    if ($names.Count -ne $files.Count - 1 -or $names.Contains('SHA256SUMS.txt')) { throw 'Incomplete bundle checksum coverage.' }
    foreach ($required in @('SnippyGrab.exe', 'install.ps1', 'install-files.ps1')) { if (-not $names.Contains($required)) { throw 'Incomplete application bundle.' } }
}
function Install-SnippyFiles([string]$Source, [string]$Target, [switch]$Portable) {
    $targetPath = [IO.Path]::GetFullPath($Target); $parent = Split-Path -Parent $targetPath
    if (-not $Portable -and (Split-Path -Leaf $targetPath) -ne 'SnippyGrab') { throw 'Unexpected application directory.' }
    Assert-SnippyInstallPath $targetPath $parent; Test-SnippyBundle $Source
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    $stage = Join-Path $parent ('SnippyGrab-stage-' + [Guid]::NewGuid().ToString('N'))
    $backup = Join-Path $parent ('SnippyGrab-backup-' + [Guid]::NewGuid().ToString('N'))
    Assert-SnippyInstallPath $stage $parent; Assert-SnippyInstallPath $backup $parent
    New-Item -ItemType Directory -Path $stage | Out-Null
    $moved = $false
    try {
        Get-ChildItem -LiteralPath $Source -Force | Copy-Item -Destination $stage -Recurse -Force
        Test-SnippyBundle $stage
        Assert-SnippyInstallPath $targetPath $parent
        if (Test-Path -LiteralPath $targetPath) { Move-Item -LiteralPath $targetPath -Destination $backup; $moved = $true }
        Move-Item -LiteralPath $stage -Destination $targetPath
        return [pscustomobject]@{ Target = $targetPath; Parent = $parent; Backup = $(if ($moved) { $backup } else { $null }) }
    } catch {
        if ($moved -and -not (Test-Path -LiteralPath $targetPath)) { Move-Item -LiteralPath $backup -Destination $targetPath }
        throw
    }
}
function Undo-SnippyFiles($Transaction) {
    Assert-SnippyInstallPath $Transaction.Target $Transaction.Parent
    # Preserve failed/new files for inspection rather than deleting unknown content.
    $failed = Join-Path $Transaction.Parent ('SnippyGrab-failed-' + [Guid]::NewGuid().ToString('N'))
    Assert-SnippyInstallPath $failed $Transaction.Parent
    if (Test-Path -LiteralPath $Transaction.Target) { Move-Item -LiteralPath $Transaction.Target -Destination $failed }
    if ($Transaction.Backup) { Assert-SnippyInstallPath $Transaction.Backup $Transaction.Parent; Move-Item -LiteralPath $Transaction.Backup -Destination $Transaction.Target }
}
