$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$target = Join-Path $repoRoot 'src/SnippyGrab.App/tessdata/eng.traineddata'
$expected = '7D4322BD2A7749724879683FC3912CB542F19906C83BCC1A52132556427170B2'
if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $expected) { return }
New-Item -ItemType Directory -Force (Split-Path -Parent $target) | Out-Null
$tempTarget = "$target.download"
try {
    Invoke-WebRequest 'https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/87416418657359cb625c412a48b6e1d6d41c29bd/eng.traineddata' -OutFile $tempTarget
    if ((Get-FileHash -LiteralPath $tempTarget -Algorithm SHA256).Hash -ne $expected) { throw 'OCR model SHA-256 mismatch.' }
    Move-Item -LiteralPath $tempTarget -Destination $target -Force
} finally { if (Test-Path -LiteralPath $tempTarget) { Remove-Item -LiteralPath $tempTarget } }
