param([Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
$sourceRoot = [IO.Path]::GetFullPath($Source)
$stream = [IO.File]::Open([IO.Path]::GetFullPath($Destination), [IO.FileMode]::Create)
try {
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        $files = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | Sort-Object { [IO.Path]::GetRelativePath($sourceRoot, $_.FullName) })
        foreach ($file in $files) {
            if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Redirected archive input.' }
            $name = [IO.Path]::GetRelativePath($sourceRoot, $file.FullName).Replace('\', '/')
            $entry = $zip.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            $output = $entry.Open(); $input = $file.OpenRead()
            try { $input.CopyTo($output) } finally { $input.Dispose(); $output.Dispose() }
        }
    } finally { $zip.Dispose() }
} finally { $stream.Dispose() }
