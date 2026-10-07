$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'native-ocr-policy.ps1')
$root = Join-Path ([IO.Path]::GetTempPath()) ('SnippyGrab-native-policy-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $root 'x64') -Force | Out-Null
try {
    $manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ocr-components.json') -Raw | ConvertFrom-Json
    $files = [ordered]@{}
    foreach ($name in @('x64/leptonica-1.82.0.dll', 'x64/tesseract50.dll')) {
        [IO.File]::WriteAllBytes((Join-Path $root $name), [byte[]](1, 2, 3)); $files[$name] = (Get-FileHash -LiteralPath (Join-Path $root $name)).Hash
    }
    $valid = [ordered]@{ Schema = 1; RecipeSha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'build-native-ocr.ps1')).Hash; TesseractCommit = $manifest.sources.Tesseract; LeptonicaCommit = $manifest.sources.Leptonica; ExternalCodecs = 'disabled'; Files = $files }
    $path = Join-Path $root 'NATIVE-OCR-PROVENANCE.json'
    $valid | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $path
    $null = Test-SnippyNativeBuild $root
    foreach ($field in @('Schema', 'RecipeSha256', 'TesseractCommit', 'LeptonicaCommit', 'ExternalCodecs')) {
        $fixture = $valid | ConvertTo-Json -Depth 5 | ConvertFrom-Json; $fixture.$field = 'invalid'
        $fixture | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $path
        $rejected = $false; try { $null = Test-SnippyNativeBuild $root } catch { $rejected = $true }
        if (-not $rejected) { throw 'Invalid native provenance admitted.' }
    }
    $valid | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $path
    [IO.File]::WriteAllBytes((Join-Path $root 'x64/tesseract50.dll'), [byte[]](3, 2, 1))
    $rejected = $false; try { $null = Test-SnippyNativeBuild $root } catch { $rejected = $true }
    if (-not $rejected) { throw 'Tampered native file admitted.' }
    Remove-Item -LiteralPath $path
    $rejected = $false; try { $null = Test-SnippyNativeBuild $root } catch { $rejected = $true }
    if (-not $rejected) { throw 'Missing native provenance admitted.' }
    Write-Output 'Native build policy: 8 isolated fixtures passed.'
} finally { Remove-Item -LiteralPath $root -Recurse -Force }
