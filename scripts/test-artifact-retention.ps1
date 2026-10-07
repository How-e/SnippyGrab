$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'artifact-retention.ps1')
$root = Join-Path ([IO.Path]::GetTempPath()) ('SnippyGrab-artifact-lab-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
try {
    foreach ($number in 1..4) { $name = "SnippyGrab-0.0.$number-win-x64"; $path = Join-Path $root $name; New-Item -ItemType Directory -Path $path | Out-Null; Set-Content (Join-Path $path 'synthetic.txt') 'generated'; Register-SnippyArtifacts $root @($name); Start-Sleep -Milliseconds 50 }
    Set-Content (Join-Path $root 'unowned.txt') 'preserve'
    $plan = @(Remove-OldSnippyArtifacts $root 3 $false)
    if ($plan.Count -ne 1 -or -not (Test-Path (Join-Path $root 'SnippyGrab-0.0.1-win-x64'))) { throw 'Dry run changed files.' }
    Set-Content (Join-Path $root 'SnippyGrab-0.0.1-win-x64/synthetic.txt') 'modified'
    $plan = @(Remove-OldSnippyArtifacts $root 3 $true)
    if ($plan[0].Action -ne 'KeepModifiedOrRunning') { throw 'Modified artifact selected.' }
    Set-Content (Join-Path $root 'SnippyGrab-0.0.1-win-x64/synthetic.txt') 'generated'
    Remove-OldSnippyArtifacts $root 3 $true | Out-Null
    if ((Test-Path (Join-Path $root 'SnippyGrab-0.0.1-win-x64')) -or -not (Test-Path (Join-Path $root 'unowned.txt'))) { throw 'Retention containment failed.' }
    $rejected = $false; try { Assert-ArtifactChild $root '../escape' } catch { $rejected = $true }
    if (-not $rejected) { throw 'Escaped artifact accepted.' }
    Write-Output 'PASS: three-build retention, dry run, modified/unowned preservation and escaped-path rejection.'
} finally {
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($root)) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -or -not [IO.Path]::GetFileName($root).StartsWith('SnippyGrab-artifact-lab-')) { throw 'Unexpected fixture cleanup path.' }
    Remove-Item -LiteralPath $root -Recurse -Force
}
