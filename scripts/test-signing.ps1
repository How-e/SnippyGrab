$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'sign-artifact.ps1')
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('SnippyGrab-signing-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
try {
    $file = Join-Path $fixtureRoot 'fixture.exe'; [IO.File]::WriteAllBytes($file, [byte[]](1, 2, 3))
    $identity = 'A' * 40
    $valid = { param($file, $identity) @{ Status = 'Valid'; SignerCertificate = @{ Thumbprint = $identity } } }
    $verify = { param($file) @{ Status = 'Valid'; SignerCertificate = @{ Thumbprint = 'A' * 40; Subject = 'CN=Fixture Publisher' }; TimeStamperCertificate = @{ Subject = 'CN=Fixture Timestamp' } } }
    Invoke-SnippySigning -Path $file -Sign { throw 'Unsigned default invoked signing.' }
    Invoke-SnippySigning -Path $file -Thumbprint $identity -TimestampServer 'https://timestamp.invalid' -Sign $valid -Verify $verify
    Assert-SnippySignature -Path $file -Thumbprint $identity -ExpectedPublisher 'CN=Fixture Publisher' -Verify $verify
    $cases = @(
        @{ Thumbprint = 'password'; Sign = $valid; Verify = $verify },
        @{ Thumbprint = $identity; TimestampServer = 'http://timestamp.invalid'; Sign = $valid; Verify = $verify },
        @{ Thumbprint = $identity; TimestampServer = 'https://user:secret@timestamp.invalid'; Sign = $valid; Verify = $verify },
        @{ Thumbprint = $identity; Sign = { @{ Status = 'UnknownError' } }; Verify = $verify },
        @{ Thumbprint = $identity; Sign = $valid; Verify = { @{ Status = 'HashMismatch' } } },
        @{ Thumbprint = $identity; Sign = $valid; Verify = { @{ Status = 'Valid'; SignerCertificate = @{ Thumbprint = 'B' * 40 } } } }
    )
    $cases += @{ Thumbprint = $identity; Sign = $valid; Verify = { @{ Status = 'Valid'; SignerCertificate = @{ Thumbprint = 'A' * 40 }; TimeStamperCertificate = $null } } }
    foreach ($case in $cases) {
        if (-not $case.ContainsKey('TimestampServer')) { $case.TimestampServer = 'https://timestamp.invalid' }
        $rejected = $false; try { Invoke-SnippySigning -Path $file @case } catch { $rejected = $true }
        if (-not $rejected) { throw 'Unsafe signing fixture accepted.' }
    }
    if ((Get-FileHash -LiteralPath $file).Hash -ne '039058C6F2C0CB492C533B0A4D14EF77CC0F78ABCCCED5287D84A1A2011CFB81') { throw 'Fixture was changed.' }
    $rejected = $false; try { Invoke-SnippySigning -Path $file -Thumbprint $identity -Sign $valid -Verify $verify } catch { $rejected = $true }
    if (-not $rejected) { throw 'Missing timestamp server accepted.' }
    $rejected = $false; try { Assert-SnippySignature -Path $file -Thumbprint $identity -ExpectedPublisher 'CN=Another Publisher' -Verify $verify } catch { $rejected = $true }
    if (-not $rejected) { throw 'Unexpected publisher accepted.' }
    Write-Output 'Signing fixtures passed: unsigned default, timestamp, identity, chain status and publisher rejection; no certificate access or network requests.'
} finally { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
