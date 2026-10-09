$ErrorActionPreference = 'Stop'
function Get-SnippyWebpRecipe {
    $repo = Split-Path -Parent $PSScriptRoot
    $parts = @('scripts/build-native-webp.ps1', 'scripts/webp-component.json', 'native/webp/CMakeLists.txt', 'native/webp/encoder.c') | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $repo $_) -Algorithm SHA256).Hash }
    $bytes = [Text.Encoding]::UTF8.GetBytes($parts -join ':')
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
}
function Test-SnippyWebpBuild([string]$Directory, [switch]$Packaged) {
    $manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'webp-component.json') -Raw | ConvertFrom-Json
    $provenance = Get-Content -LiteralPath (Join-Path $Directory 'WEBP-PROVENANCE.json') -Raw | ConvertFrom-Json
    if ($provenance.Schema -ne 1 -or $provenance.Commit -ne $manifest.Commit -or $provenance.Version -ne $manifest.Version -or $provenance.RecipeSha256 -ne (Get-SnippyWebpRecipe)) { throw 'WebP provenance differs from the reviewed source/recipe.' }
    $dll = Join-Path $Directory 'x64/snippywebp.dll'
    if ((Get-Item -LiteralPath $dll).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Redirected WebP binary rejected.' }
    if ((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne $provenance.BinarySha256) { throw 'WebP binary hash differs from provenance.' }
    $provenance
}
