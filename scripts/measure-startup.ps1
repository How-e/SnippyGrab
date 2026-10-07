param([string]$Executable = (Join-Path $PSScriptRoot '../src/SnippyGrab.App/bin/Release/net10.0-windows/SnippyGrab.exe'), [string]$ReportDirectory = (Join-Path $PSScriptRoot '../artifacts/startup-latency'), [ValidateRange(2, 20)][int]$Samples = 10)
$ErrorActionPreference = 'Stop'
$executablePath = (Resolve-Path -LiteralPath $Executable).Path
$runDirectory = Join-Path ([IO.Path]::GetFullPath($ReportDirectory)) (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
[IO.Directory]::CreateDirectory($runDirectory) | Out-Null
$runs = [Collections.Generic.List[object]]::new()
function Summarize($values) {
    $ordered = @($values | Sort-Object)
    [ordered]@{ Count = $ordered.Count; MedianMs = $ordered[[int][Math]::Ceiling($ordered.Count * .5) - 1]; P95Ms = $ordered[[int][Math]::Ceiling($ordered.Count * .95) - 1]; MinMs = $ordered[0]; MaxMs = $ordered[-1] }
}
$result = 'RUNNING'
try {
    for ($index = 0; $index -lt $Samples; $index++) {
        $report = Join-Path $runDirectory ("process-$index.json")
        $child = Start-Process -FilePath $executablePath -ArgumentList '--check-startup-latency', ('"' + $report + '"') -WindowStyle Hidden -PassThru
        try {
            $deadline = [DateTime]::UtcNow.AddSeconds(45)
            while (!$child.WaitForExit(1000)) { if ([DateTime]::UtcNow -gt $deadline) { throw 'Startup/OCR probe timed out.' } }
            $child.Refresh()
            if ($child.ExitCode -ne 0) { throw "Startup/OCR probe failed: $report" }
            $sample = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
            if ($sample.Result -ne 'PASS') { throw 'Startup/OCR probe did not pass.' }
            foreach ($value in @($sample.ProcessToControllerReadyMs, $sample.FirstOcrMs) + @($sample.OcrSamplesMs)) {
                if ($value -isnot [ValueType] -or ![double]::IsFinite([double]$value) -or $value -lt 0) { throw 'Invalid startup/OCR timing.' }
            }
            $runs.Add($sample)
        }
        finally { if (!$child.HasExited) { $child.Kill(); $child.WaitForExit() }; $child.Dispose() }
    }
    $result = 'PASS'
}
finally {
    $summary = [ordered]@{ Result = $result; ExecutableSha256 = (Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash; RequestedProcesses = $Samples; CompletedProcesses = $runs.Count; Scope = 'Repeated fresh processes with uncontrolled OS/file caches; first observation is not certified cold boot. Isolated controller startup and native OCR only; real capture/clipboard and Snipping Tool comparison remain pending.'; Runs = $runs.ToArray() }
    if ($runs.Count) {
        $summary.FirstObservedProcessMs = $runs[0].ProcessToControllerReadyMs
        $summary.ProcessStartup = Summarize @($runs | ForEach-Object { $_.ProcessToControllerReadyMs })
        $summary.FirstOcrPerProcess = Summarize @($runs | ForEach-Object { $_.FirstOcrMs })
        $summary.RepeatedOcr = Summarize @($runs | ForEach-Object { $_.OcrSamplesMs | Select-Object -Skip 1 })
    }
    $summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $runDirectory 'summary.json') -Encoding utf8
    Write-Output "Startup/OCR result $result; report $(Join-Path $runDirectory 'summary.json')"
}
