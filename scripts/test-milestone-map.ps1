param([string]$Repository = (Split-Path -Parent $PSScriptRoot), [string]$QueueText, [string]$LedgerText, [string]$ReviewText, [switch]$SelfTest)
$ErrorActionPreference = 'Stop'
$queue = if ($PSBoundParameters.ContainsKey('QueueText')) { $QueueText } else { Get-Content -LiteralPath (Join-Path $Repository 'TASK_QUEUE.md') -Raw }
$ledger = if ($PSBoundParameters.ContainsKey('LedgerText')) { $LedgerText } else { Get-Content -LiteralPath (Join-Path $Repository 'docs/MILESTONES.md') -Raw }
$review = if ($PSBoundParameters.ContainsKey('ReviewText')) { $ReviewText } else { Get-Content -LiteralPath (Join-Path $Repository 'docs/AGENT-GATE-REVIEW-20261007.md') -Raw }
$entries = [regex]::Matches($queue, '(?m)^- \[(?<done>[ x])\] \*\*Q(?<id>\d+) · P(?<priority>\d)')
if ($entries.Count -ne 51) { throw 'Expected exactly 51 queue entries.' }
$rows = [regex]::Matches($ledger, '(?m)^\| M(?<id>\d+) [^\r\n]+')
if ($rows.Count -ne 11 -or (@($rows | ForEach-Object { [int]$_.Groups['id'].Value } | Sort-Object -Unique).Count -ne 11)) { throw 'Every M0-M10 milestone must appear exactly once.' }
$mapped = [Collections.Generic.HashSet[int]]::new()
foreach ($row in $rows) {
    $columns = $row.Value.Split('|')
    if ($columns.Count -ne 6 -or [string]::IsNullOrWhiteSpace($columns[2]) -or [string]::IsNullOrWhiteSpace($columns[3]) -or [string]::IsNullOrWhiteSpace($columns[4])) { throw 'Milestone lacks implementation, evidence or ownership.' }
    foreach ($range in [regex]::Matches($columns[4], 'Q(?<first>\d+)[–-]Q(?<last>\d+)')) {
        foreach ($id in ([int]$range.Groups['first'].Value)..([int]$range.Groups['last'].Value)) { $mapped.Add($id) | Out-Null }
    }
    foreach ($id in [regex]::Matches($columns[4], 'Q(?<id>\d+)')) { $mapped.Add([int]$id.Groups['id'].Value) | Out-Null }
    foreach ($test in [regex]::Matches($columns[3], '\b(?<name>\w+Tests)\b')) {
        $files = [IO.Directory]::EnumerateFiles((Join-Path $Repository 'tests'), $test.Groups['name'].Value + '.cs', [IO.SearchOption]::AllDirectories)
        if (!@($files).Count) { throw "Mapped test file does not exist: $($test.Value)" }
    }
    foreach ($commit in [regex]::Matches($columns[2], '`(?<sha>[a-f0-9]{7,40})`')) {
        git -C $Repository cat-file -e ($commit.Groups['sha'].Value + '^{commit}')
        if ($LASTEXITCODE -ne 0) { throw "Mapped implementation commit does not exist: $($commit.Value)" }
    }
}
$open = @($entries | Where-Object { $_.Groups['done'].Value -eq ' ' -and [int]$_.Groups['priority'].Value -lt 2 })
foreach ($entry in $open) {
    if (!$mapped.Contains([int]$entry.Groups['id'].Value)) { throw "Open required gate has no milestone owner: Q$($entry.Groups['id'].Value)" }
}
$q01 = $entries | Where-Object { [int]$_.Groups['id'].Value -eq 1 }
if ($open.Count -gt 0 -and $q01.Groups['done'].Value -eq 'x') { throw 'Milestone acceptance closed while required gates remain open.' }
foreach ($id in @(2,3,49,18,15,21,7,14,28,4,5,6,13,22,1)) {
    $name = 'Q{0:D2}' -f $id
    if ([regex]::Matches($review, "(?m)^## $name\r?$").Count -ne 1) { throw "Missing or duplicate current closure record: $name" }
}
$q01Status = if ($q01.Groups['done'].Value -eq 'x') { 'CLOSED' } else { 'OPEN' }
Write-Output "PASS: 11 milestone rows, mapped test files/commits, $($open.Count) open required gates with owners, 15 current closure records; Q01 $q01Status."
if ($SelfTest) {
    $fixtures = @(
        @{ LedgerText = $ledger -replace '(?m)^\| M4 Native[^\r\n]+\r?\n', '' },
        @{ LedgerText = $ledger.Replace('ClipboardWriterTests', 'AbsentRegressionTests') },
        @{ LedgerText = $ledger.Replace('/Q49', ''); QueueText = $queue.Replace('- [x] **Q49 ·', '- [ ] **Q49 ·') },
        @{ QueueText = $queue.Replace('- [ ] **Q01 ·', '- [x] **Q01 ·').Replace('- [x] **Q02 ·', '- [ ] **Q02 ·') },
        @{ ReviewText = $review.Replace('## Q02', '## Unmapped') }
    )
    foreach ($fixture in $fixtures) {
        $rejected = $false
        try { & $PSCommandPath -Repository $Repository @fixture | Out-Null }
        catch { $rejected = $true }
        if (!$rejected) { throw 'Invalid milestone mapping fixture was admitted.' }
    }
    Write-Output 'PASS: five malformed mapping/ownership/closure fixtures rejected without changing workspace documents.'
}
