param([string]$ReportPath, [string]$ComponentDirectory, [switch]$SelfTest)
$ErrorActionPreference = 'Stop'
function Assert-DependencyPolicy($report) {
    if ($report.version -ne 1 -or @($report.projects).Count -ne 5 -or @($report.sources).Count -eq 0) { throw 'Incomplete or unsupported dependency audit report.' }
    if (@($report.PSObject.Properties.Name) -contains 'errors' -and $report.errors) { throw 'Dependency audit reported errors.' }
    if (@($report.PSObject.Properties.Name) -contains 'logs' -and @($report.logs | Where-Object { $_.level -eq 'error' }).Count) { throw 'Dependency audit feed/query error.' }
    $expected = @('SnippyGrab.Core.csproj', 'SnippyGrab.App.csproj', 'SnippyGrab.Installer.csproj', 'SnippyGrab.Tests.csproj', 'SnippyGrab.IntegrationTests.csproj') | Sort-Object
    $actual = @($report.projects | ForEach-Object { [IO.Path]::GetFileName($_.path) }) | Sort-Object
    if (($actual -join '|') -ne ($expected -join '|')) { throw 'Dependency audit project membership is incomplete or duplicated.' }
    foreach ($project in $report.projects) {
        if (-not $project.path) { throw 'Missing project in dependency audit.' }
        foreach ($framework in $project.frameworks) {
            foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
                foreach ($finding in $package.vulnerabilities) {
                    # All reported severities fail. No permanent suppressions or implicit exceptions.
                    throw "Dependency policy rejected $($package.id): $($finding.severity) $($finding.advisoryurl)"
                }
            }
        }
    }
}
if ($SelfTest) {
    $clean = '{"version":1,"sources":["fixture"],"projects":[{"path":"SnippyGrab.Core.csproj"},{"path":"SnippyGrab.App.csproj"},{"path":"SnippyGrab.Tests.csproj"},{"path":"SnippyGrab.IntegrationTests.csproj"},{"path":"SnippyGrab.Installer.csproj"}]}'
    Assert-DependencyPolicy ($clean | ConvertFrom-Json)
    foreach ($kind in @('topLevelPackages', 'transitivePackages')) {
        foreach ($severity in @('Low', 'Moderate', 'High', 'Critical', 'Unknown')) {
            $fixture = $clean | ConvertFrom-Json
            $fixture.projects[0] | Add-Member frameworks @(@{ $kind = @(@{ id = 'fixture'; vulnerabilities = @(@{ severity = $severity; advisoryurl = 'fixture' }) }) })
            $rejected = $false
            try { Assert-DependencyPolicy $fixture } catch { $rejected = $true }
            if (-not $rejected) { throw 'Vulnerable fixture was accepted.' }
        }
    }
    foreach ($fixture in @('{}', '{"version":2,"projects":[]}', $clean.Replace('"version":1', '"errors":["query failed"],"version":1'), $clean.Replace('SnippyGrab.Installer.csproj', 'SnippyGrab.Core.csproj'), $clean.Replace('SnippyGrab.Installer.csproj', 'Unknown.csproj'))) {
        $rejected = $false
        try { Assert-DependencyPolicy ($fixture | ConvertFrom-Json) } catch { $rejected = $true }
        if (-not $rejected) { throw 'Invalid audit fixture was accepted.' }
    }
    Write-Output 'Dependency policy: 16 fixtures passed.'
    return
}
if ($ReportPath) { $json = Get-Content -LiteralPath $ReportPath -Raw }
else {
    $json = (& dotnet list (Join-Path (Split-Path -Parent $PSScriptRoot) 'SnippyGrab.sln') package --vulnerable --include-transitive --format json --output-version 1) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw 'Dependency audit command failed.' }
}
Assert-DependencyPolicy ($json | ConvertFrom-Json)
Write-Output 'Dependency policy passed: no reported vulnerabilities in five projects, including transitive packages.'
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ocr-components.json') -Raw | ConvertFrom-Json
if (-not $ComponentDirectory) { $ComponentDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'src/SnippyGrab.App/bin/Release/net10.0-windows' }
foreach ($entry in $manifest.files.PSObject.Properties) {
    $component = Join-Path $ComponentDirectory $entry.Name
    if (-not (Test-Path -LiteralPath $component -PathType Leaf) -or (Get-FileHash -LiteralPath $component -Algorithm SHA256).Hash -ne $entry.Value) { throw "OCR component integrity check failed: $($entry.Name)" }
}
Write-Output 'OCR component integrity passed. Native advisory review and residual risks are documented in docs/DEPENDENCY-REVIEW.md; this is not a native CVE scanner.'
& (Join-Path $PSScriptRoot 'inventory-native-ocr.ps1') -ComponentDirectory $ComponentDirectory
. (Join-Path $PSScriptRoot 'webp-policy.ps1')
$null = Test-SnippyWebpBuild $ComponentDirectory -Packaged
Write-Output 'WebP source/recipe/binary integrity passed; advisory review is documented in docs/WEBP-ADMISSION.md, not covered by the NuGet scanner.'
