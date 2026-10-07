$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'review-resource-report.ps1') -Report 'unused'
function New-Report {
    @{ Result = 'PASS'; RequestedSeconds = 7200; ElapsedSeconds = 7201; Cycles = 7200; OcrRuns = 72; Samples = @(0, 60, 3600, 7200, 7200.5, 7201 | ForEach-Object {
        @{ Seconds = $_; Captures = $_; WorkingBytes = 100MB; PrivateBytes = 80MB; ManagedBytes = 20MB; Handles = 200; GdiHandles = 10; UserHandles = 20; CpuMs = $_ * 250 }
    }) }
}
$fixtureReport = New-Report
$review = Get-SnippyResourceReview $fixtureReport
if (-not $review.TwoHourWorkloadComplete -or $review.Samples[-1].Phase -ne 'disposed' -or $review.Samples[-2].Phase -ne 'post-GC' -or $review.Samples[1].CpuOneCorePercent -ne 25) { throw 'Completed report or CPU/phase calculation failed.' }
foreach ($field in @('Result', 'RequestedSeconds', 'ElapsedSeconds', 'Cycles', 'OcrRuns')) {
    $fixtureReport = New-Report
    $fixtureReport[$field] = if ($field -eq 'Result') { 'RUNNING' } else { 0 }
    if ($field -eq 'ElapsedSeconds') { $fixtureReport.ElapsedSeconds = 7199; foreach ($sample in $fixtureReport.Samples) { $sample.Seconds /= 2 } }
    if ((Get-SnippyResourceReview $fixtureReport).TwoHourWorkloadComplete) { throw "Incomplete $field admitted." }
}
$fixtureReport = New-Report; $fixtureReport.Result = 'RUNNING'
if (@((Get-SnippyResourceReview $fixtureReport).Samples | Where-Object Phase -in @('post-GC', 'disposed')).Count) { throw 'Running samples labeled as recovery.' }
foreach ($fault in @('missing', 'negative', 'time', 'cpu', 'nonfinite', 'empty', 'future')) {
    $fixtureReport = New-Report
    switch ($fault) {
        missing { $fixtureReport.Samples[1].Remove('Handles') }
        negative { $fixtureReport.Samples[1].WorkingBytes = -1 }
        time { $fixtureReport.Samples[1].Seconds = 0 }
        cpu { $fixtureReport.Samples[2].CpuMs = 1 }
        nonfinite { $fixtureReport.Samples[1].PrivateBytes = [double]::NaN }
        empty { $fixtureReport.Samples = @() }
        future { $fixtureReport.Samples[-1].Seconds = 7202 }
    }
    $rejected = $false
    try { Get-SnippyResourceReview $fixtureReport | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw "Malformed $fault report admitted." }
}
$fixturePath = Join-Path ([IO.Path]::GetTempPath()) ('SnippyGrab resource-review-' + [guid]::NewGuid().ToString('N') + '.json')
try {
    New-Report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $fixturePath
    $before = (Get-FileHash -LiteralPath $fixturePath).Hash
    $output = & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'review-resource-report.ps1') -Report $fixturePath
    if ($LASTEXITCODE -ne 0 -or -not ($output | ConvertFrom-Json).TwoHourWorkloadComplete -or (Get-FileHash -LiteralPath $fixturePath).Hash -ne $before) { throw 'CLI review failed or changed its source report.' }
}
finally { Remove-Item -LiteralPath $fixturePath -ErrorAction SilentlyContinue }
Write-Output 'Resource report review: completed, incomplete, CPU/phase, malformed-report and read-only CLI fixtures passed.'
