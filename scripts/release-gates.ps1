function Assert-SnippyReleaseGates {
    param([Parameter(Mandatory)][string]$Version, [Parameter(Mandatory)][string]$QueueText)
    if ($Version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$') { throw 'Invalid release version.' }
    $entries = [regex]::Matches($QueueText, '(?m)^- \[( |x)\] \*\*(Q\d+) · P([012]) ·')
    $ids = @($entries | ForEach-Object { 'Q' + [int]$_.Groups[2].Value.Substring(1) })
    $expected = @(1..51 | ForEach-Object { "Q$_" })
    if ($ids.Count -ne 51 -or @($ids | Select-Object -Unique).Count -ne 51 -or @($expected | Where-Object { $_ -notin $ids }).Count) { throw 'Release queue is missing, duplicated or unsupported; refusing packaging.' }
    $open = @($entries | Where-Object { $_.Groups[1].Value -eq ' ' -and $_.Groups[3].Value -ne '2' } | ForEach-Object { $_.Groups[2].Value })
    if (-not $Version.Contains('-') -and $open.Count) { throw "Stable packaging is blocked by open P0/P1 gates: $($open -join ', '). See docs/MILESTONES.md." }
    Write-Output "Release gates: $($open.Count) required entries remain open; $Version admitted."
}
