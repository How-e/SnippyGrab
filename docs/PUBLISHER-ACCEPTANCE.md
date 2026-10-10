# Publisher release evidence procedure (F03)

2026-10-09: credential-free implementation and injected signing fixtures PASS in PowerShell 7 and Windows PowerShell 5.1. Real publisher signing, downloaded signed assets, their browser protections and clean-profile installation of signed artifacts are **NOT RUN**. A provisioned real publisher identity/protected signer and separately authorized publication are external prerequisites for this signing procedure. The owner later reported PASS for the separate unsigned alpha Windows 11 installation batch and authorized unsigned alpha publication. This signing procedure did not create, purchase or install a certificate or publish signed assets.

Owned signing coverage is `SnippyGrab.exe` and `SnippyGrab-<version>-Setup.exe`. Third-party managed/native libraries keep upstream/source-build provenance; they are not re-signed. The existing order remains app signing → inventory/archive → embedded setup payload → setup signing → final hashes. Signed packaging fails without an HTTPS timestamp server; signatures must have Valid status, the configured identity and an authenticated timestamp. Unsigned local packages remain available.

On the protected signing machine, use the existing `package.ps1 -SigningThumbprint <identity> -TimestampServer https://<trusted-service>` interface. It accepts no PFX/password. After an authorized publication, independently download ZIP, setup and hashes, extract the complete ZIP, and run:

```powershell
pwsh ./scripts/verify-package.ps1 -Bundle C:/isolated/download/SnippyGrab-<version>-win-x64 -Installer C:/isolated/download/SnippyGrab-<version>-Setup.exe -SigningThumbprint <40-hex-identity> -ExpectedPublisher 'CN=<exact certificate subject>'
```

This validates inventory, artifact hashes, embedded payload and app/setup Authenticode chain status, certificate identity, expected publisher and timestamp. It does not constitute a browser download or clean installation observation. Preserve the output privately with exact source/version, SHA-256, publisher subject, timestamp identity, download date and OS build.

Use a disposable Windows profile for portable launch, non-admin setup, OCR readiness, upgrade/rollback, real-login startup opt-in and Apps uninstall/data preservation. Download through a browser retaining Mark-of-the-Web; record browser/version, Windows build, SmartScreen/Smart App Control configuration and displayed publisher/behavior. Do not bypass protections to turn a blocked test into a pass. Windows 10 is NOT AVAILABLE until an appropriate environment exists. Q41/Q44 later closed for the explicitly accepted **unsigned alpha** scope from the owner's current Windows 11 batch PASS report; that closure does not complete this separate real-publisher signing/download procedure. No signed-artifact or zero-warning claim is made.

Microsoft documents that new signed binaries can still warn while reputation accumulates. Signature success establishes publisher authentication, not a zero-prompt promise. [SmartScreen guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation) reviewed 2026-10-09.
