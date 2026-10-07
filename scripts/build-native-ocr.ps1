param([string]$CMake)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ocr-components.json') -Raw | ConvertFrom-Json
$recipe = (Get-FileHash -LiteralPath $PSCommandPath).Hash
$root = Join-Path $repoRoot 'artifacts/native-ocr'
$output = Join-Path $root 'current'
. (Join-Path $PSScriptRoot 'native-ocr-policy.ps1')
try { $cached = Test-SnippyNativeBuild $output; Write-Output 'Pinned codec-free native OCR build already verified.'; return } catch { }
if (-not $CMake) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) { $CMake = @(& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe' | Select-Object -First 1)[0] }
    if (-not $CMake) { $CMake = (Get-Command cmake -ErrorAction SilentlyContinue).Source }
}
if (-not $CMake) { throw 'Native OCR requires installed Visual Studio C++ x64 tools and CMake. See docs/NATIVE-OCR.md; no binary fallback is allowed.' }
$buildRoot = Join-Path $root $recipe.Substring(0, 16)
New-Item -ItemType Directory -Force $buildRoot | Out-Null
# Serialize concurrent builds from separate MSBuild invocations.
$lock = $null
for ($attempt = 0; $attempt -lt 600 -and -not $lock; $attempt++) {
    try { $lock = [IO.File]::Open((Join-Path $root 'build.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
    catch [IO.IOException] { Start-Sleep -Milliseconds 500 }
}
if (-not $lock) { throw 'Another native build is still active; retry after it finishes.' }
try {
    try { $cached = Test-SnippyNativeBuild $output; Write-Output 'Concurrent native build verified.'; return } catch { }
    function Run-CMake([string[]]$Arguments) {
        & $CMake @Arguments
        if ($LASTEXITCODE -ne 0) { throw 'Pinned native CMake build failed.' }
    }
    foreach ($component in @('Leptonica', 'Tesseract')) {
        $source = Join-Path $buildRoot $component.ToLowerInvariant()
        if (-not (Test-Path -LiteralPath $source)) {
            New-Item -ItemType Directory -Path $source | Out-Null
            & git -C $source init --quiet
            if ($LASTEXITCODE -ne 0) { throw 'Source initialization failed.' }
            # Preserve upstream LF bytes regardless of the build machine's Git defaults.
            & git -C $source config core.autocrlf false
            if ($LASTEXITCODE -ne 0) { throw 'Native source line-ending configuration failed.' }
            $url = if ($component -eq 'Leptonica') { 'https://github.com/DanBloomberg/leptonica.git' } else { 'https://github.com/tesseract-ocr/tesseract.git' }
            & git -C $source fetch --depth 1 $url $manifest.sources.$component
            if ($LASTEXITCODE -ne 0) { throw 'Pinned source fetch failed.' }
            & git -C $source checkout --detach FETCH_HEAD
            if ($LASTEXITCODE -ne 0) { throw 'Pinned source checkout failed.' }
            # Only change output names to match the pinned managed wrapper's ABI lookup.
            $target = if ($component -eq 'Leptonica') { 'leptonica' } else { 'libtesseract' }
            $name = if ($component -eq 'Leptonica') { 'leptonica-1.82.0' } else { 'tesseract50' }
            $cmakeFile = if ($component -eq 'Leptonica') { Join-Path $source 'src/CMakeLists.txt' } else { Join-Path $source 'CMakeLists.txt' }
            Add-Content -LiteralPath $cmakeFile -Value ("`nset_target_properties($target PROPERTIES OUTPUT_NAME `"$name`")")
        }
        $revision = & git -C $source rev-parse HEAD
        if ($LASTEXITCODE -ne 0 -or $revision -ne $manifest.sources.$component) { throw 'Native source commit differs from the pinned recipe.' }
        $changed = @(& git -C $source diff --name-only)
        $expectedChange = if ($component -eq 'Leptonica') { 'src/CMakeLists.txt' } else { 'CMakeLists.txt' }
        if ($changed.Count -ne 1 -or $changed[0] -ne $expectedChange -or @(& git -C $source ls-files --others --exclude-standard).Count) { throw 'Unexpected native source modifications.' }
        $actualFile = Get-Content -LiteralPath (Join-Path $source $expectedChange) -Raw
        $originalFile = (& git -C $source show "HEAD:$expectedChange") -join "`n"
        $target = if ($component -eq 'Leptonica') { 'leptonica' } else { 'libtesseract' }
        $name = if ($component -eq 'Leptonica') { 'leptonica-1.82.0' } else { 'tesseract50' }
        if ($actualFile.Replace("`r", '').TrimEnd() -ne ($originalFile.TrimEnd() + "`n`nset_target_properties($target PROPERTIES OUTPUT_NAME `"$name`")")) { throw 'Native naming patch differs from the reviewed recipe.' }
    }
    $install = Join-Path $buildRoot 'install'
    Run-CMake @('-S', (Join-Path $buildRoot 'leptonica'), '-B', (Join-Path $buildRoot 'leptonica-build'), '-G', 'Visual Studio 17 2022', '-A', 'x64', '-DBUILD_SHARED_LIBS=ON', '-DBUILD_PROG=OFF', '-DSW_BUILD=OFF', '-DENABLE_ZLIB=OFF', '-DENABLE_PNG=OFF', '-DENABLE_GIF=OFF', '-DENABLE_JPEG=OFF', '-DENABLE_TIFF=OFF', '-DENABLE_WEBP=OFF', '-DENABLE_OPENJPEG=OFF', "-DCMAKE_INSTALL_PREFIX=$install")
    Run-CMake @('--build', (Join-Path $buildRoot 'leptonica-build'), '--config', 'Release', '--parallel', '4', '--target', 'install')
    # MSVC ignores this optional OpenMP SIMD reduction with OpenMP disabled.
    # Suppress only C4849, which otherwise surfaces through MSBuild Exec as an error.
    Run-CMake @('-S', (Join-Path $buildRoot 'tesseract'), '-B', (Join-Path $buildRoot 'tesseract-build'), '-G', 'Visual Studio 17 2022', '-A', 'x64', '-DBUILD_SHARED_LIBS=ON', '-DBUILD_TRAINING_TOOLS=OFF', '-DBUILD_TESTS=OFF', '-DGRAPHICS_DISABLED=ON', '-DDISABLED_LEGACY_ENGINE=OFF', '-DDISABLE_TIFF=ON', '-DDISABLE_ARCHIVE=ON', '-DDISABLE_CURL=ON', '-DOPENMP_BUILD=OFF', '-DENABLE_NATIVE=OFF', '-DCMAKE_CXX_FLAGS=/wd4849', "-DCMAKE_PREFIX_PATH=$install")
    Run-CMake @('--build', (Join-Path $buildRoot 'tesseract-build'), '--config', 'Release', '--parallel', '4', '--target', 'libtesseract')
    New-Item -ItemType Directory -Force (Join-Path $output 'x64') | Out-Null
    Copy-Item -LiteralPath (Join-Path $install 'bin/leptonica-1.82.0.dll') -Destination (Join-Path $output 'x64') -Force
    Copy-Item -LiteralPath (Join-Path $buildRoot 'tesseract-build/bin/Release/tesseract50.dll') -Destination (Join-Path $output 'x64') -Force
    $files = [ordered]@{}
    foreach ($name in @('x64/leptonica-1.82.0.dll', 'x64/tesseract50.dll')) { $files[$name] = (Get-FileHash -LiteralPath (Join-Path $output $name)).Hash }
    [ordered]@{ Schema = 1; RecipeSha256 = $recipe; TesseractCommit = $manifest.sources.Tesseract; LeptonicaCommit = $manifest.sources.Leptonica; ExternalCodecs = 'disabled'; Generator = 'Visual Studio 17 2022 x64'; Files = $files } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'NATIVE-OCR-PROVENANCE.json') -Encoding utf8
    $null = Test-SnippyNativeBuild $output
} finally { $lock.Dispose() }
