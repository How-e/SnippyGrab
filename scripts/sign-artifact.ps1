# Opt-in Authenticode using a certificate provisioned outside the repository.
function Assert-SnippySignature {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Thumbprint,
        [string]$ExpectedPublisher,
        [scriptblock]$Verify = { param($file) Get-AuthenticodeSignature -LiteralPath $file }
    )
    $signature = & $Verify $Path
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $Thumbprint) { throw 'Authenticode verification failed or signer identity differs.' }
    if (-not $signature.TimeStamperCertificate) { throw 'A verified timestamp is required for publisher-signed artifacts.' }
    if ($ExpectedPublisher -and $signature.SignerCertificate.Subject -ne $ExpectedPublisher) { throw 'Authenticated publisher differs from the expected certificate subject.' }
}

function Invoke-SnippySigning {
    param(
        [Parameter(Mandatory)][string]$Path,
        [string]$Thumbprint,
        [string]$TimestampServer,
        [scriptblock]$Sign = {
            param($file, $identity, $timestamp)
            $certificate = Get-Item -LiteralPath "Cert:/CurrentUser/My/$identity" -ErrorAction Stop
            if (-not $certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date)) { throw 'Signing certificate is unavailable or expired.' }
            if (@($certificate.EnhancedKeyUsageList | Where-Object ObjectId -eq '1.3.6.1.5.5.7.3.3').Count -eq 0) { throw 'Certificate is not for code signing.' }
            $arguments = @{ FilePath = $file; Certificate = $certificate; HashAlgorithm = 'SHA256' }
            if ($timestamp) { $arguments.TimestampServer = $timestamp }
            Set-AuthenticodeSignature @arguments -ErrorAction Stop
        },
        [scriptblock]$Verify = { param($file) Get-AuthenticodeSignature -LiteralPath $file }
    )
    if (-not $Thumbprint) { return } # Default packaging stays unsigned and offline.
    if ($Thumbprint -notmatch '^[a-fA-F0-9]{40}$') { throw 'Provide a 40-character certificate thumbprint, never a password or private key.' }
    if (-not $TimestampServer) { throw 'Publisher signing requires a trusted HTTPS timestamp server.' }
    if ($TimestampServer) {
        $timestamp = $null
        if (-not [Uri]::TryCreate($TimestampServer, [UriKind]::Absolute, [ref]$timestamp) -or $timestamp.Scheme -ne 'https' -or $timestamp.UserInfo -or $timestamp.Fragment) { throw 'Timestamp server must be an HTTPS URL without credentials or fragment.' }
    }
    $file = Get-Item -LiteralPath $Path -ErrorAction Stop
    if ($file.PSIsContainer -or $file.Extension -ne '.exe' -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Signing target must be a regular executable.' }
    $result = & $Sign $file.FullName $Thumbprint $TimestampServer
    if ($result.Status -ne 'Valid') { throw 'Authenticode signing failed; package publication is stopped.' }
    Assert-SnippySignature -Path $file.FullName -Thumbprint $Thumbprint -Verify $Verify
}
