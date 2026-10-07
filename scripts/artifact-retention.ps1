param([int]$Keep = 3, [switch]$Apply)
$ErrorActionPreference = 'Stop'
function Assert-ArtifactChild([string]$Root, [string]$Name) {
    if ($Name -notmatch '^(SnippyGrab-[0-9][a-zA-Z0-9.\-]*(?:\.exe|\.zip|\.sha256)?|(?:installer-)?staging-[a-f0-9]{32})$') { throw 'Unrecognized generated artifact name.' }
    $path = [IO.Path]::GetFullPath((Join-Path $Root $Name)); $base = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    if ([IO.Path]::GetDirectoryName($path) -ne $base) { throw 'Artifact escaped its root.' }
    for ($probe = $path; $probe; $probe = Split-Path -Parent $probe) {
        if ((Test-Path -LiteralPath $probe) -and ((Get-Item -LiteralPath $probe -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Redirected artifact path.' }
    }
    return $path
}
function Get-ArtifactInventory([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return @() }
    $item = Get-Item -LiteralPath $Path -Force
    $entries = if ($item.PSIsContainer) { @(Get-ChildItem -LiteralPath $Path -Recurse -Force) } else { @($item) }
    if ($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Redirected artifact content.' }
    return @($entries | Where-Object { -not $_.PSIsContainer } | Sort-Object FullName | ForEach-Object { [ordered]@{ Name = $(if ($item.PSIsContainer) { [IO.Path]::GetRelativePath($Path, $_.FullName) } else { $_.Name }); Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
}
function Register-SnippyArtifacts([string]$Root, [string[]]$Names) {
    $records = Join-Path $Root '.build-records'
    if ((Test-Path -LiteralPath $records) -and ((Get-Item -LiteralPath $records).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Redirected artifact registry.' }
    New-Item -ItemType Directory -Path $records -Force | Out-Null
    $paths = @($Names | ForEach-Object { $path = Assert-ArtifactChild $Root $_; [ordered]@{ Name = $_; Inventory = @(Get-ArtifactInventory $path) } })
    [ordered]@{ Schema = 1; CreatedUtc = [DateTimeOffset]::UtcNow.ToString('O'); Paths = $paths } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $records ([Guid]::NewGuid().ToString('N') + '.json'))
}
function Remove-OldSnippyArtifacts([string]$Root, [int]$Keep = 3, [bool]$Apply = $false) {
    if ($Keep -lt 1) { throw 'At least one generated build must be kept.' }
    $records = Join-Path $Root '.build-records'
    if (-not (Test-Path -LiteralPath $records)) { return }
    if ((Get-Item -LiteralPath $records).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Redirected artifact registry.' }
    $items = @(Get-ChildItem -LiteralPath $records -Filter '*.json' | Sort-Object LastWriteTimeUtc -Descending)
    foreach ($recordFile in $items | Select-Object -Skip $Keep) {
        if ($recordFile.Attributes -band [IO.FileAttributes]::ReparsePoint -or $recordFile.Length -gt 1MB) { throw 'Invalid artifact record.' }
        $record = Get-Content -LiteralPath $recordFile.FullName -Raw | ConvertFrom-Json
        if ($record.Schema -ne 1 -or @($record.Paths).Count -eq 0) { throw 'Invalid artifact record schema.' }
        $safe = $true; $targets = @()
        foreach ($entry in $record.Paths) {
            $path = Assert-ArtifactChild $Root $entry.Name
            if (-not (Test-Path -LiteralPath $path)) { continue }
            $inventory = @(Get-ArtifactInventory $path)
            if ((ConvertTo-Json -InputObject $inventory -Depth 5 -Compress) -ne (ConvertTo-Json -InputObject @($entry.Inventory) -Depth 5 -Compress)) { $safe = $false }
            $running = Get-Process -Name 'SnippyGrab*' -ErrorAction SilentlyContinue | Where-Object { $_.Path -and ($_.Path.Equals($path, [StringComparison]::OrdinalIgnoreCase) -or $_.Path.StartsWith($path + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) }
            if ($running) { $safe = $false }
            $targets += $path
        }
        [pscustomobject]@{ Action = $(if ($safe) { 'Remove' } else { 'KeepModifiedOrRunning' }); Paths = $targets; Applied = $Apply -and $safe }
        if ($Apply -and $safe) {
            foreach ($path in $targets) { Assert-ArtifactChild $Root ([IO.Path]::GetFileName($path)) | Out-Null; Remove-Item -LiteralPath $path -Recurse -Force }
            Remove-Item -LiteralPath $recordFile.FullName
        }
    }
}
if ($MyInvocation.InvocationName -ne '.') {
    $root = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts'
    Remove-OldSnippyArtifacts -Root $root -Keep $Keep -Apply $Apply.IsPresent
}
