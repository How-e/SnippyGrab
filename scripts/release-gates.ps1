function Read-SnippyReleaseManifest {
    param([Parameter(Mandatory)][string]$ManifestJson)
    $manifest = ConvertFrom-Json -InputObject $ManifestJson -ErrorAction Stop
    if ($manifest.schemaVersion -isnot [long] -and $manifest.schemaVersion -isnot [int]) { throw 'Invalid release manifest schema.' }
    if ($manifest.schemaVersion -ne 1) { throw 'Unsupported release manifest schema.' }
    $gates = @($manifest.gates)
    $expected = @(1..51 | ForEach-Object { "Q$_" })
    $ids = @($gates | ForEach-Object { $_.id })
    if ($gates.Count -ne 51 -or @($ids | Select-Object -Unique).Count -ne 51 -or @($expected | Where-Object { $_ -cnotin $ids }).Count) { throw 'Release manifest is missing, duplicated or unsupported; refusing packaging.' }
    foreach ($gate in $gates) {
        if ($gate.priority -isnot [long] -and $gate.priority -isnot [int]) { throw 'Invalid gate priority.' }
        if ($gate.priority -notin @(0,1,2) -or $gate.closed -isnot [bool] -or $gate.owner -cnotin @('Agent','Shared','User') -or [string]::IsNullOrWhiteSpace($gate.title)) { throw 'Invalid gate state or ownership.' }
        if (($gate.milestone -isnot [int] -and $gate.milestone -isnot [long]) -or $gate.milestone -notin @(0..10)) { throw 'Gate lacks a supported milestone.' }
    }
    $milestones = @($manifest.milestones)
    if ($milestones.Count -ne 11 -or @($milestones.id | Sort-Object -Unique).Count -ne 11 -or @(0..10 | Where-Object { $_ -notin $milestones.id }).Count) { throw 'Expected each milestone exactly once.' }
    foreach ($milestone in $milestones) {
        if ([string]::IsNullOrWhiteSpace($milestone.name) -or @($milestone.tests).Count -eq 0) { throw 'Milestone lacks regression coverage.' }
        foreach ($test in $milestone.tests) {
            if ($test -isnot [string] -or $test -notmatch '^(tests|scripts)/[a-zA-Z0-9_.\-/]+\.(cs|ps1)$' -or $test.Split('/') -contains '..') { throw 'Invalid regression path.' }
        }
    }
    return $manifest
}
function Assert-SnippyReleaseGates {
    param([Parameter(Mandatory)][string]$Version, [Parameter(Mandatory)][string]$ManifestJson)
    if ($Version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$') { throw 'Invalid release version.' }
    $manifest = Read-SnippyReleaseManifest -ManifestJson $ManifestJson
    $open = @($manifest.gates | Where-Object { -not $_.closed -and $_.priority -lt 2 })
    if (-not $Version.Contains('-') -and $open.Count) { throw "Stable packaging is blocked by open P0/P1 gates: $($open.id -join ', '). See scripts/release-gates.json." }
    Write-Output "Release gates: $($open.Count) required entries remain open; $Version admitted."
}
