# Manual two-hour resource test — Q35

You will run this test manually. Q35 stays open until the completed report and resource trends have been reviewed. The agent's interrupted two-hour attempt is not a pass and will not be resumed automatically.

Use the complete portable **0.1.0-alpha.queue.20261006.3** folder, including its x64 native libraries and tessdata. No compiler is required. Allow at least two hours plus startup/cleanup, enough free disk space for thousands of synthetic captures, and keep Windows awake. Avoid running another heavy benchmark at the same time.

Run this in PowerShell from the repository root. For a moved portable folder, set `$bundle` to that complete folder instead; keep its native libraries and model beside the executable:

```powershell
$bundle = Resolve-Path '.\artifacts\SnippyGrab-0.1.0-alpha.queue.20261006.3-win-x64'
$exe = Join-Path $bundle 'SnippyGrab.exe'
$results = Join-Path $env:USERPROFILE 'Documents\SnippyGrab-test-results'
New-Item -ItemType Directory -Path $results -Force | Out-Null
$report = Join-Path $results ('resources-2h-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.json')
$testProcess = Start-Process -FilePath $exe -ArgumentList @('--check-resource-stress', ('"' + $report + '"'), '7200') -WindowStyle Hidden -PassThru
"Test process: $($testProcess.Id); report: $report"
Wait-Process -Id $testProcess.Id
$r = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
$r | Select-Object Result, RequestedSeconds, ElapsedSeconds, Cycles, OcrRuns
$r.Samples | Select-Object -Last 5 | Format-Table
```

The report updates every 25 cycles. You can read it in a second PowerShell window using the printed report path. The hidden test uses an isolated temporary cache/settings, synthetic 640×360 images, retention=never, dock rebuilds, worker blur/revisions, three offscreen pin windows every 25 cycles and real local OCR every 100 cycles. It does not move the pointer, capture your desktop or write the OS clipboard. It does not use your normal capture history. Normal completion removes its temporary cache.

Completion requires `Result = PASS`, `RequestedSeconds = 7200`, `ElapsedSeconds >= 7200`, at least 300 cycles and at least one OCR run. `RUNNING`, a missing report, a crash or an early stop is incomplete. `PASS` means the workload completed; it does not automatically certify freedom from leaks.

Review `WorkingBytes`, `PrivateBytes`, `ManagedBytes`, `Handles`, `GdiHandles`, `UserHandles` and cumulative `CpuMs` over time after warm-up. The penultimate sample follows forced GC/finalizers; the last follows controller/dock disposal. Look for handles that grow continually with pin/editor cycles or fail to recover, steadily worsening memory unrelated to retained history, excessive CPU between cycles, or stalls. Retention=never deliberately grows stored captures and repository metadata; working set alone is not a leak verdict. CPU time is cumulative, so compare changes between sample times rather than treating it as an instantaneous percentage. There is no invented universal MB cutoff.

If you must stop, use `Stop-Process -Id <printed test process ID>` for this test process only. An interrupted report remains `RUNNING` and the isolated temporary cache may remain; do not delete your normal app cache. Preserve the report for diagnosis.

Report the build, Windows version, duration/cycles/OCR count, completion status, early/middle/final and post-GC/disposal resource values, other heavy workloads, and any freeze/crash or rising trend. No screenshots or private captures are needed. A two-hour pass covers this workload on your environment; all-day use, real idle/startup and the broader Q34 latency comparison remain separate evidence.
