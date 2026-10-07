param([string]$ComponentDirectory, [string]$ReportPath)
$ErrorActionPreference = 'Stop'
if (-not $ComponentDirectory) { $ComponentDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'src/SnippyGrab.App/bin/Release/net10.0-windows' }
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ocr-components.json') -Raw | ConvertFrom-Json
foreach ($entry in $manifest.files.PSObject.Properties) {
    $component = Join-Path $ComponentDirectory $entry.Name
    if ((Get-FileHash -LiteralPath $component -Algorithm SHA256).Hash -ne $entry.Value) { throw 'Untrusted native/model input; inventory refused.' }
}
if (-not ('SnippyNativeInventory' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class SnippyNativeInventory {
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr VersionFunction();
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void FreeFunction(IntPtr value);
 public static string Read(string library, string export, bool owned) {
  var handle = NativeLibrary.Load(library, typeof(SnippyNativeInventory).Assembly, DllImportSearchPath.SafeDirectories | DllImportSearchPath.UseDllDirectoryForDependencies);
  try { var value = Marshal.GetDelegateForFunctionPointer<VersionFunction>(NativeLibrary.GetExport(handle, export))();
   try { return Marshal.PtrToStringAnsi(value); } finally { if(owned) Marshal.GetDelegateForFunctionPointer<FreeFunction>(NativeLibrary.GetExport(handle, "lept_free"))(value); }
  } finally { NativeLibrary.Free(handle); }
 }
}
'@
}
$leptonica = Join-Path $ComponentDirectory 'x64/leptonica-1.82.0.dll'
$tesseract = Join-Path $ComponentDirectory 'x64/tesseract50.dll'
$inventory = [ordered]@{
    Tesseract = [SnippyNativeInventory]::Read($tesseract, 'TessVersion', $false)
    Leptonica = [SnippyNativeInventory]::Read($leptonica, 'getLeptonicaVersion', $true)
    Codecs = [SnippyNativeInventory]::Read($leptonica, 'getImagelibVersions', $true)
}
foreach ($name in @('Tesseract', 'Leptonica', 'Codecs')) {
    if ($inventory[$name] -ne $manifest.versions.$name) { throw "Native version inventory mismatch: $name" }
}
$json = $inventory | ConvertTo-Json
if ($ReportPath) { $json | Set-Content -LiteralPath $ReportPath -Encoding utf8 }
Write-Output $json
