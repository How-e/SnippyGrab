function Test-SnippyNativeBuild {
    param([string]$Directory)
    $manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ocr-components.json') -Raw | ConvertFrom-Json
    $file = Join-Path $Directory 'NATIVE-OCR-PROVENANCE.json'
    if (-not (Test-Path -LiteralPath $file -PathType Leaf) -or (Get-Item -LiteralPath $file).Length -gt 16KB) { throw 'Native build provenance is missing or oversized.' }
    if ((Get-Item -LiteralPath $file -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Redirected native build provenance.' }
    $provenance = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    $recipeHash = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'build-native-ocr.ps1')).Hash
    if ($provenance.Schema -ne 1 -or $provenance.RecipeSha256 -ne $recipeHash -or $provenance.TesseractCommit -ne $manifest.sources.Tesseract -or $provenance.LeptonicaCommit -ne $manifest.sources.Leptonica -or $provenance.ExternalCodecs -ne 'disabled') { throw 'Native build provenance does not match the pinned source recipe.' }
    $names = @($provenance.Files.PSObject.Properties.Name | Sort-Object)
    if (($names -join '|') -ne 'x64/leptonica-1.82.0.dll|x64/tesseract50.dll') { throw 'Incomplete or unexpected native build inventory.' }
    foreach ($entry in $provenance.Files.PSObject.Properties) {
        $path = Join-Path $Directory $entry.Name
        for ($probe = [IO.Path]::GetFullPath($path); $probe; $probe = Split-Path -Parent $probe) {
            if ((Test-Path -LiteralPath $probe) -and ((Get-Item -LiteralPath $probe -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Redirected native build input.' }
        }
        if ((Get-FileHash -LiteralPath $path).Hash -ne $entry.Value) { throw 'Native build hash mismatch.' }
    }
    return $provenance
}
