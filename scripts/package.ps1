param([string]$Version = '0.1.0-alpha')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$') { throw 'Invalid release version.' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts'
$bundlePath = [IO.Path]::GetFullPath((Join-Path $artifactRoot "SnippyGrab-$Version-win-x64"))
if (-not $bundlePath.StartsWith([IO.Path]::GetFullPath($artifactRoot) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected output path.' }
& (Join-Path $PSScriptRoot 'provision-ocr.ps1')
# A fresh staging directory prevents stale files entering a release.
$stagingPath = Join-Path $artifactRoot ('staging-' + [Guid]::NewGuid().ToString('N'))
dotnet publish (Join-Path $repoRoot 'src/SnippyGrab.App') -c Release -r win-x64 --self-contained true -p:ReleasePackaging=true -p:RestoreLockedMode=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true -p:Version=$Version -o $stagingPath
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
# Keep the release x64-only, including only the sidecar dependencies actually used.
$unusedPath = [IO.Path]::GetFullPath((Join-Path $stagingPath 'x86'))
if ($unusedPath.StartsWith([IO.Path]::GetFullPath($stagingPath) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $unusedPath)) { Remove-Item -LiteralPath $unusedPath -Recurse -Force }
Get-ChildItem -LiteralPath $stagingPath -Filter '*.pdb' | Remove-Item -Force
& (Join-Path $PSScriptRoot 'audit-dependencies.ps1') -ComponentDirectory $stagingPath
foreach ($name in @('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md')) { Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination $stagingPath }
Copy-Item -LiteralPath (Join-Path $repoRoot 'licenses') -Destination $stagingPath -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install.ps1') -Destination $stagingPath
if (Test-Path -LiteralPath $bundlePath) {
    $oldPath = $bundlePath + '-previous-' + [Guid]::NewGuid().ToString('N')
    Move-Item -LiteralPath $bundlePath -Destination $oldPath
}
Move-Item -LiteralPath $stagingPath -Destination $bundlePath
$checks = Get-ChildItem -LiteralPath $bundlePath -Recurse -File | Sort-Object FullName | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetRelativePath($bundlePath, $_.FullName)
}
$checks | Set-Content -LiteralPath (Join-Path $bundlePath 'SHA256SUMS.txt') -Encoding utf8
$zipPath = "$bundlePath.zip"
Compress-Archive -Path "$bundlePath/*" -DestinationPath $zipPath -Force
'{0}  {1}' -f (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetFileName($zipPath) | Set-Content -LiteralPath "$zipPath.sha256" -Encoding utf8
Write-Output $bundlePath
