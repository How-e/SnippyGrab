param([string]$Repository = (Split-Path -Parent $PSScriptRoot), [string]$ManifestJson, [switch]$SelfTest)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'release-gates.ps1')
$json = if ($PSBoundParameters.ContainsKey('ManifestJson')) { $ManifestJson } else { Get-Content -LiteralPath (Join-Path $Repository 'scripts/release-gates.json') -Raw }
$manifest = Read-SnippyReleaseManifest -ManifestJson $json
foreach ($milestone in $manifest.milestones) {
    foreach ($test in $milestone.tests) {
        if (-not (Test-Path -LiteralPath (Join-Path $Repository $test) -PathType Leaf)) { throw "Mapped regression does not exist: $test" }
    }
}
$open = @($manifest.gates | Where-Object { -not $_.closed -and $_.priority -lt 2 -and $_.id -ne 'Q1' })
if ($open.Count -and ($manifest.gates | Where-Object id -eq 'Q1').closed) { throw 'Milestone acceptance is closed while required gates remain open.' }
Write-Output 'PASS: release gates have owners, supported milestones and executable regression coverage.'
if ($SelfTest) {
    $fixtures = @(
        { param($m) $m.milestones = @($m.milestones | Select-Object -First 10) },
        { param($m) $m.milestones[0].tests = @('tests/AbsentRegressionTests.cs') },
        { param($m) $m.gates[0].milestone = 11 },
        { param($m) $m.gates[0].closed = $true; $m.gates[1].closed = $false },
        { param($m) $m.gates[0].owner = '' }
    )
    foreach ($change in $fixtures) {
        $fixture = ConvertFrom-Json $json; & $change $fixture; $candidate = ConvertTo-Json $fixture -Depth 10
        $rejected = $false; try { & $PSCommandPath -Repository $Repository -ManifestJson $candidate | Out-Null } catch { $rejected = $true }
        if (-not $rejected) { throw 'Invalid coverage/ownership fixture was admitted.' }
    }
    Write-Output 'PASS: malformed coverage and ownership fixtures rejected.'
}
