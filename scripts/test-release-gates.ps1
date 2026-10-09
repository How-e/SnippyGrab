$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'release-gates.ps1')
$actual = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'release-gates.json') -Raw
Assert-SnippyReleaseGates -Version '0.1.0-alpha' -ManifestJson $actual | Out-Null
function Fixture([scriptblock]$Change) {
    $fixture = ConvertFrom-Json $actual
    & $Change $fixture
    ConvertTo-Json $fixture -Depth 10
}
$closed = Fixture { param($m) foreach ($g in $m.gates) { $g.closed = $true } }
Assert-SnippyReleaseGates -Version '0.1.0' -ManifestJson $closed | Out-Null
$open = Fixture { param($m) $m.gates[1].closed = $false }
Assert-SnippyReleaseGates -Version '0.1.0-alpha' -ManifestJson $open | Out-Null
$optionalManifest = ConvertFrom-Json $closed
$optionalManifest.gates[1].closed = $false; $optionalManifest.gates[1].priority = 2
$optional = ConvertTo-Json $optionalManifest -Depth 10
Assert-SnippyReleaseGates -Version '0.1.0' -ManifestJson $optional | Out-Null
$invalid = @(
    $open, '', '{invalid',
    (Fixture { param($m) $m.gates = @($m.gates | Select-Object -First 50) }),
    (Fixture { param($m) $m.gates[50].id = 'Q1' }),
    (Fixture { param($m) $m.gates[50].id = 'Q52' }),
    (Fixture { param($m) $m.gates[0].closed = 'true' }),
    (Fixture { param($m) $m.gates[0].priority = 3 }),
    (Fixture { param($m) $m.gates[0].owner = '' }),
    (Fixture { param($m) $m.gates[0].milestone = 11 }),
    (Fixture { param($m) $m.milestones[0].tests = @() }),
    (Fixture { param($m) $m.milestones[0].tests = @('tests/../private.cs') }),
    (Fixture { param($m) $m.schemaVersion = 2 })
)
foreach ($fixture in $invalid) {
    $rejected = $false; try { Assert-SnippyReleaseGates -Version '0.1.0' -ManifestJson $fixture | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Malformed or incomplete stable gate fixture was admitted.' }
}
Write-Output 'PASS: release manifest policy admits valid scope and rejects malformed or incomplete stable gates.'
