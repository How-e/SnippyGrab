param([Parameter(Mandatory)][string]$Report)
$ErrorActionPreference = 'Stop'

function Get-SnippyResourceReview {
    param([Parameter(Mandatory)]$Data)
    function Read-Number($Object, [string]$Name) {
        $value = $Object.$Name
        if ($null -eq $value -or $value -is [string] -or $value -is [bool]) { throw "Missing or nonnumeric $Name." }
        $number = [double]$value
        if (-not [double]::IsFinite($number) -or $number -lt 0) { throw "Invalid $Name." }
        return $number
    }
    if ($Data.Result -notin @('PASS', 'RUNNING', 'FAIL')) { throw 'Unknown resource report result.' }
    $requested = Read-Number $Data 'RequestedSeconds'
    $elapsed = Read-Number $Data 'ElapsedSeconds'
    $cycles = Read-Number $Data 'Cycles'
    $ocr = Read-Number $Data 'OcrRuns'
    $samples = @($Data.Samples)
    if ($samples.Count -lt 1) { throw 'Report has no resource samples.' }
    $metrics = @('Captures', 'WorkingBytes', 'PrivateBytes', 'ManagedBytes', 'Handles', 'GdiHandles', 'UserHandles', 'CpuMs')
    $previous = $null
    $rows = @(for ($i = 0; $i -lt $samples.Count; $i++) {
        $sample = $samples[$i]
        $seconds = Read-Number $sample 'Seconds'
        foreach ($metric in $metrics) { Read-Number $sample $metric | Out-Null }
        if ($seconds -gt $elapsed) { throw 'Sample time exceeds report elapsed time.' }
        if ($null -ne $previous -and ($seconds -le $previous.Seconds -or $sample.CpuMs -lt $previous.CpuMs)) { throw 'Sample time or cumulative CPU is not monotonic.' }
        $cpu = if ($null -ne $previous) { [math]::Round(($sample.CpuMs - $previous.CpuMs) / (($seconds - $previous.Seconds) * 10), 2) } else { $null }
        $phase = if ($i -eq 0) { 'initial' } elseif ($Data.Result -eq 'PASS' -and $i -eq $samples.Count - 1) { 'disposed' } elseif ($Data.Result -eq 'PASS' -and $i -eq $samples.Count - 2) { 'post-GC' } else { 'workload' }
        [pscustomobject]@{
            Phase = $phase; Seconds = [math]::Round($seconds, 2); Captures = $sample.Captures
            WorkingMiB = [math]::Round($sample.WorkingBytes / 1MB, 2)
            PrivateMiB = [math]::Round($sample.PrivateBytes / 1MB, 2)
            ManagedMiB = [math]::Round($sample.ManagedBytes / 1MB, 2)
            Handles = $sample.Handles; GdiHandles = $sample.GdiHandles; UserHandles = $sample.UserHandles
            CpuOneCorePercent = $cpu
        }
        $previous = $sample
    })
    $complete = $Data.Result -eq 'PASS' -and $requested -eq 7200 -and $elapsed -ge 7200 -and $cycles -ge 300 -and $ocr -ge 1 -and $samples.Count -ge 4
    $workload = @($rows | Where-Object Phase -eq 'workload')
    $trends = @(if ($workload.Count -ge 2) {
        $first = $workload[0]; $last = $workload[-1]
        foreach ($metric in @('WorkingMiB', 'PrivateMiB', 'ManagedMiB', 'Handles', 'GdiHandles', 'UserHandles')) {
            [pscustomobject]@{ Metric = $metric; FirstWorkload = $first.$metric; LastWorkload = $last.$metric; Change = [math]::Round($last.$metric - $first.$metric, 2) }
        }
    })
    [pscustomobject]@{
        Result = $Data.Result; TwoHourWorkloadComplete = $complete
        RequestedSeconds = $requested; ElapsedSeconds = $elapsed; Cycles = $cycles; OcrRuns = $ocr
        Samples = $rows; WorkloadChanges = $trends
        Acceptance = 'Trend/recovery review required. No automatic leak verdict or queue closure. Stored history grows intentionally; CPU is percent of one core over each sample interval.'
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    Get-SnippyResourceReview (Get-Content -LiteralPath $Report -Raw | ConvertFrom-Json) | ConvertTo-Json -Depth 6
}
