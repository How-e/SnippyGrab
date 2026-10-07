$ErrorActionPreference = 'Stop'
$exe = Join-Path (Split-Path -Parent $PSScriptRoot) 'src/SnippyGrab.Installer/bin/Release/net10.0-windows/SnippyGrab-Setup.exe'
$root = Join-Path ([IO.Path]::GetTempPath()) ('SnippyGrab-installer-lab-' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $root 'source'; New-Item -ItemType Directory -Path $source -Force | Out-Null
function Check([string]$Archive, [bool]$Accept) {
    $report = Join-Path $root 'result.txt'
    $process = Start-Process -FilePath $exe -ArgumentList @('--verify-archive', ('"' + $Archive + '"'), ('"' + $report + '"')) -WindowStyle Hidden -PassThru -Wait
    if (($process.ExitCode -eq 0) -ne $Accept) { throw "Installer validation mismatch: $(Get-Content $report)" }
}
try {
    foreach ($name in @('SnippyGrab.exe', 'install.ps1', 'install-files.ps1', 'LICENSE')) { Set-Content (Join-Path $source $name) 'synthetic' }
    Get-ChildItem -LiteralPath $source -File | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName).Hash, $_.Name } | Set-Content (Join-Path $source 'SHA256SUMS.txt')
    $archive = Join-Path $root 'valid.zip'; & (Join-Path $PSScriptRoot 'archive.ps1') -Source $source -Destination $archive; Check $archive $true
    foreach ($name in @('../escape.txt', 'SNIPPYGRAB.EXE', 'unlisted.txt')) {
        $malformed = Join-Path $root 'malformed.zip'; Copy-Item -LiteralPath $archive -Destination $malformed -Force
        $zip = [IO.Compression.ZipFile]::Open($malformed, [IO.Compression.ZipArchiveMode]::Update)
        try { $entry = $zip.CreateEntry($name); $stream = $entry.Open(); $stream.Dispose() } finally { $zip.Dispose() }
        Check $malformed $false
    }
    Set-Content (Join-Path $source 'SnippyGrab.exe') 'modified'; & (Join-Path $PSScriptRoot 'archive.ps1') -Source $source -Destination $archive; Check $archive $false
    Write-Output 'PASS: valid payload, traversal, duplicate identity, missing checksum membership and tampered hash fixtures.'
} finally {
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($root)) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -or -not [IO.Path]::GetFileName($root).StartsWith('SnippyGrab-installer-lab-')) { throw 'Unexpected fixture cleanup path.' }
    Remove-Item -LiteralPath $root -Recurse -Force
}
