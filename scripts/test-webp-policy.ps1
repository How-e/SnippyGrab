$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'webp-policy.ps1')
$root = Join-Path ([IO.Path]::GetTempPath()) ('SnippyGrab-webp-policy-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $root 'x64') -Force | Out-Null
try {
    $manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'webp-component.json') -Raw | ConvertFrom-Json
    $dll = Join-Path $root 'x64/snippywebp.dll'; $path = Join-Path $root 'WEBP-PROVENANCE.json'
    [IO.File]::WriteAllBytes($dll, [byte[]](1, 2, 3))
    $valid = [ordered]@{ Schema = 1; Version = $manifest.Version; Commit = $manifest.Commit; RecipeSha256 = (Get-SnippyWebpRecipe); BinarySha256 = (Get-FileHash $dll -Algorithm SHA256).Hash }
    $valid | ConvertTo-Json | Set-Content $path
    $null = Test-SnippyWebpBuild $root
    foreach ($field in @('Schema', 'Version', 'Commit', 'RecipeSha256', 'BinarySha256')) {
        $fixture = $valid | ConvertTo-Json | ConvertFrom-Json; $fixture.$field = 'invalid'; $fixture | ConvertTo-Json | Set-Content $path
        $rejected = $false; try { $null = Test-SnippyWebpBuild $root } catch { $rejected = $true }
        if (-not $rejected) { throw 'Invalid WebP provenance admitted.' }
    }
    $valid | ConvertTo-Json | Set-Content $path
    [IO.File]::WriteAllBytes($dll, [byte[]](3, 2, 1))
    $rejected = $false; try { $null = Test-SnippyWebpBuild $root } catch { $rejected = $true }
    if (-not $rejected) { throw 'Changed WebP binary admitted.' }
    Remove-Item -LiteralPath $path
    $rejected = $false; try { $null = Test-SnippyWebpBuild $root } catch { $rejected = $true }
    if (-not $rejected) { throw 'Missing WebP provenance admitted.' }
    Write-Output 'WebP provenance policy: 8 fixtures passed.'
} finally {
    $absolute = [IO.Path]::GetFullPath($root)
    if (-not $absolute.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($absolute).StartsWith('SnippyGrab-webp-policy-')) { throw 'Unexpected fixture cleanup path.' }
    Remove-Item -LiteralPath $absolute -Recurse -Force
}
