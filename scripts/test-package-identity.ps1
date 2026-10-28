$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'package-identity.ps1')
$revision = '0123456789abcdef0123456789abcdef01234567'
$provenance = [pscustomobject]@{ Version = '0.1.0-alpha.20261028.1'; Commit = $revision }
function New-OcrProbe {
    [pscustomobject]@{ Result = 'PASS'; Build = $provenance.Version + '+' + $revision; OcrIsolation = $true; NativeRecognition = $true; CopiesDuringBlockedOcr = 1; EngineCreations = 1 }
}
Assert-SnippyPackagedOcrIdentity (New-OcrProbe) $provenance
$fixtures = @(
    @{ Field = 'Build'; Value = $provenance.Version },
    @{ Field = 'Build'; Value = '0.1.0-alpha.20261017.1+' + $revision },
    @{ Field = 'Build'; Value = $provenance.Version + '+' + ('f' * 40) },
    @{ Field = 'Result'; Value = 'RUNNING' },
    @{ Field = 'OcrIsolation'; Value = $false },
    @{ Field = 'OcrIsolation'; Value = 'true' },
    @{ Field = 'NativeRecognition'; Value = $false },
    @{ Field = 'CopiesDuringBlockedOcr'; Value = 0 },
    @{ Field = 'EngineCreations'; Value = 2 }
)
foreach ($fixture in $fixtures) {
    $probe = New-OcrProbe; $probe.($fixture.Field) = $fixture.Value; $rejected = $false
    try { Assert-SnippyPackagedOcrIdentity $probe $provenance } catch { $rejected = $true }
    if (!$rejected) { throw "Package identity fixture was accepted: $($fixture.Field)" }
}
Write-Output 'PASS: packaged informational version/commit identity and OCR result fixtures.'
