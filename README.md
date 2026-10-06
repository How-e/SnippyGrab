# SnippyGrab

A local, native Windows screenshot shelf for AI and developer workflows.

Print Screen → select → release → paste or drag. Built with .NET 10 and WPF, without Electron.

Development alpha. See docs/ARCHITECTURE.md for design decisions and docs/VALIDATION.md for verification status.

## Build
Windows 10 22H2 / Windows 11 x64, .NET 10 SDK:

```powershell
pwsh ./scripts/provision-ocr.ps1
dotnet restore
dotnet build
dotnet test
dotnet run --project src/SnippyGrab.App
```

Full usage, portable/install instructions, release verification and limitations are added with the runnable implementation.

## Privacy
No accounts, cloud, analytics or application network traffic. Captures stay in your local user profile. Windows clipboard history/sync and receiving applications have their own policies.

MIT licensed. See CONTRIBUTING.md and SECURITY.md.
