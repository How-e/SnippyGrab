param([string]$CMake)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'webp-policy.ps1')
$root = Join-Path $repo 'artifacts/native-webp'
$output = Join-Path $root 'current'
try { $null = Test-SnippyWebpBuild $output; Write-Output 'Pinned native WebP encoder already verified.'; return } catch { }
if (-not $CMake) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) { $CMake = @(& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe' | Select-Object -First 1)[0] }
    if (-not $CMake) { $CMake = (Get-Command cmake -ErrorAction SilentlyContinue).Source }
}
if (-not $CMake) { throw 'WebP build requires Visual Studio C++ x64 tools and CMake. No downloaded binary fallback is allowed.' }
New-Item -ItemType Directory -Force $root | Out-Null
$lock = $null
for ($attempt = 0; $attempt -lt 600 -and -not $lock; $attempt++) {
    try { $lock = [IO.File]::Open((Join-Path $root 'build.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
    catch [IO.IOException] { Start-Sleep -Milliseconds 500 }
}
if (-not $lock) { throw 'WebP build busy; retry after the other build completes.' }
try {
    try { $null = Test-SnippyWebpBuild $output; return } catch { }
    $manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'webp-component.json') -Raw | ConvertFrom-Json
    $source = Join-Path $root 'source'
    if (-not (Test-Path -LiteralPath (Join-Path $source '.git'))) {
        New-Item -ItemType Directory -Force $source | Out-Null
        & git -C $source init --quiet
        if ($LASTEXITCODE) { throw 'WebP source initialization failed.' }
        & git -C $source config core.autocrlf false
        & git -C $source fetch --depth 1 $manifest.Repository $manifest.Commit
        if ($LASTEXITCODE) { throw 'Pinned WebP source fetch failed.' }
        & git -C $source checkout --detach FETCH_HEAD
        if ($LASTEXITCODE) { throw 'Pinned WebP source checkout failed.' }
    }
    $head = & git -C $source rev-parse HEAD
    if ($LASTEXITCODE -or $head -ne $manifest.Commit -or (& git -C $source status --porcelain)) { throw 'WebP source differs from the immutable pin.' }
    $build = Join-Path $root ('build-' + (Get-SnippyWebpRecipe).Substring(0, 16))
    & $CMake -S (Join-Path $repo 'native/webp') -B $build -G 'Visual Studio 17 2022' -A x64 "-DWEBP_SOURCE=$source"
    if ($LASTEXITCODE) { throw 'WebP configure failed.' }
    & $CMake --build $build --config Release --parallel 4 --target snippywebp
    if ($LASTEXITCODE) { throw 'WebP build failed.' }
    New-Item -ItemType Directory -Force (Join-Path $output 'x64') | Out-Null
    Copy-Item -LiteralPath (Join-Path $build 'Release/snippywebp.dll') -Destination (Join-Path $output 'x64/snippywebp.dll') -Force
    [ordered]@{ Schema = 1; Version = $manifest.Version; Commit = $manifest.Commit; RecipeSha256 = (Get-SnippyWebpRecipe); BinarySha256 = (Get-FileHash (Join-Path $output 'x64/snippywebp.dll') -Algorithm SHA256).Hash } | ConvertTo-Json | Set-Content (Join-Path $output 'WEBP-PROVENANCE.json') -Encoding utf8
    $null = Test-SnippyWebpBuild $output
} finally { $lock.Dispose() }
