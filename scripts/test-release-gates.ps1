$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'release-gates.ps1')
$queue = (1..51 | ForEach-Object { "- [x] **Q$_ · P1 · Agent — fixture.**" }) -join "`n"
Assert-SnippyReleaseGates -Version '0.1.0' -QueueText $queue | Out-Null
$open = $queue.Replace('[x] **Q2 ', '[ ] **Q2 ')
Assert-SnippyReleaseGates -Version '0.1.0-alpha' -QueueText $open | Out-Null
$optional = $open.Replace('Q2 · P1', 'Q2 · P2')
Assert-SnippyReleaseGates -Version '0.1.0' -QueueText $optional | Out-Null
foreach ($fixture in @($open, '', $queue.Replace('Q51 ', 'Q1 '), $queue.Replace('Q51 ', 'Q52 '), $queue.Replace('- [x] **Q51', '- [yes] **Q51'))) {
    $rejected = $false; try { Assert-SnippyReleaseGates -Version '0.1.0' -QueueText $fixture | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Incomplete stable gate fixture was admitted.' }
}
Write-Output 'Release gate policy: 8 fixtures passed.'
