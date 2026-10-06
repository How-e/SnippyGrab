# SnippyGrab

A native Windows screenshot shelf for AI and developer workflows.

**Print Screen → select → release → paste or drag.** Captures land on the clipboard and a small transparent shelf. Click to annotate, Ctrl-click several images to attach together, or copy terminal errors with local OCR.

.NET 10, WPF and Win32. No Electron, account, telemetry or cloud dependency.

**0.1.0 alpha:** runnable implementation with unit, integration and interactive synthetic checks. Real Codex/ChatGPT receiver acceptance and the full hardware matrix remain release gates. [Verification](docs/VALIDATION.md) · [Architecture](docs/ARCHITECTURE.md) · [Desktop acceptance](docs/ACCEPTANCE.md)

## Preview

Synthetic content rendered by the actual WPF interface; no private captures.

![Compact shelf with twenty captures](docs/images/dock.png)

![Quick editor](docs/images/editor.png)

![Minimal first-run setup](docs/images/welcome.png)

A capture-to-Codex GIF will be added after external receiver acceptance.

## Installation

Windows 10 22H2 / Windows 11 **x64**; Windows 11 is the primary tested OS.

Extract the complete release ZIP, then run `SnippyGrab.exe`. Keep `Tesseract.dll`, `x64/` and `tessdata/` beside it. .NET is self-contained. OCR can additionally require the [Microsoft VC++ 2015–2022 x64 redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist); capture still works if OCR cannot load.

For per-user installation without elevation, run `powershell -NoProfile -File .\install.ps1` from the extracted bundle; add `-Startup` to enable login startup. Local script execution must be permitted by your PowerShell policy. The installer adds a Start menu shortcut and Apps uninstall entry. Exit from the tray before upgrading/uninstalling. Uninstall removes its startup registration and application files; captures/settings remain in `%LOCALAPPDATA%\SnippyGrab` for recovery. Portable removal: disable login startup, exit, then delete the extracted directory. Remove user data separately when no longer needed.

Initial builds are unsigned; SmartScreen may prompt. Compare `Get-FileHash <download.zip> -Algorithm SHA256` with the release `.sha256`. Bundles include `SHA256SUMS.txt`. Hashes verify integrity, not publisher identity. Release automation is prepared; this local setup does not publish a remote.

## Usage

The app lives in the tray. Closing a window keeps it running; **Exit** stops it. Double-click the tray icon to capture. First run offers a brief introduction and optional login startup.

- Capture and immediately **Ctrl+V** into an application accepting clipboard images.
- Drag a thumbnail to attach its temporary PNG file.
- **Ctrl-click** several captures, then drag one selected image to export all of them. Multi-image Ctrl+C copies file-drop data; no universal multi-image bitmap paste format exists.
- Wheel to browse. Hover expands a bounded strip and reveals edit/copy/pin/save/OCR/dismiss. **Alt-drag** onto another shelf image to reorder.
- Click to edit: crop, arrow, rectangle, ellipse, pen, line, text, highlighter, numbered marker, blur, pixelate, solid redaction and spotlight. Ctrl+wheel zooms. Copy/Save updates shelf and clipboard; Save as exports a PNG. Closing applies pending edits; **Discard** leaves them unapplied.
- OCR runs locally. Choose **OcrArea** in the editor to read a selected region. Review extracted text for character errors.
- Pin indefinitely. Detach through the context menu for a resizable desktop pin; restore click-through interaction from the tray.
- Recent captures recovers hidden items; import accepts PNG/JPEG/BMP with size limits.

## Default shortcuts

| Shortcut | Action |
|---|---|
| Print Screen | Region |
| Ctrl+Print Screen | Entire virtual desktop |
| Alt+Print Screen | Active window |
| Ctrl+Alt+Print Screen | Window picker |
| Ctrl+Shift+S | Fallback region |
| Esc during selection | Cancel |
| Ctrl+C / Ctrl+S on shelf | Copy / Save as |
| Enter / Delete on shelf | Edit / Dismiss |
| Ctrl+Z / Ctrl+Y in editor | Undo / Redo |
| Esc in editor | Close and apply |

Configure global shortcuts in Settings; Escape in a hotkey field disables that shortcut. If Windows intercepts Print Screen, disable **Settings → Accessibility → Keyboard → Use the Print Screen key to open screen capture**, then restart. Conflicts are reported; Windows settings are never changed automatically. Hotkeys pause while configuring settings.

## Settings and cache

Settings cover capture/cursor/hotkeys, monitor/corner/orientation/sizing, opacity/topmost/collapse/hide/lifetime, clipboard image + PNG, retention/history, annotation defaults, startup and theme.

Cache: `%LOCALAPPDATA%\SnippyGrab\cache`. Captures are not dumped into Pictures. Retention supports 1/24/168 hours or `-1` for never. Pins survive cleanup. Editors/drags acquire leases; transfers and clipboard-file copies protect sources for **24 hours** after use. Clear/session-only cleanup respects that grace. Receivers must copy before it ends. Saved exports must be outside the cache. Custom cache paths apply on restart; old captures stay in the old cache.

Optional history stores time, dimensions, capture origin and pin/edit/save state, not titles/processes or OCR text. Disabling history preserves pins. Corrupt history blocks cleanup to protect possible pins and preserves original metadata. Cache files are local plaintext, not an encrypted vault.

## Privacy

No uploads, accounts, advertising, analytics, update polling, local server or runtime network requests. Build-time package/model downloads need network. English OCR is bundled; other languages are not configured in the initial UI. Logs contain timings/error categories, never pixels, OCR text or titles.

Windows clipboard history/sync and receiving applications have their own policies. Redaction cannot retract previous copies or erase previous cached revisions. Protected/DRM/secure-desktop content may be blank. [Threat model](docs/THREAT-MODEL.md) · [Security](SECURITY.md)

## Build and verify

Pinned .NET SDK 10.0.400 on Windows:

```powershell
pwsh ./scripts/provision-ocr.ps1
dotnet restore --locked-mode
dotnet build -c Release -warnaserror
dotnet test -c Release
dotnet format --no-restore --verify-no-changes
dotnet run --project src/SnippyGrab.App
```

OCR provisioning pins model commit and SHA-256; NuGet versions/hashes are locked. Downloaded binaries/models and personal runtime data are excluded from Git.

`pwsh ./scripts/package.ps1 -Version 0.1.0-alpha` creates the executable, dependencies, installer script, notices and checksums.

On an idle interactive desktop, run the executable with `--self-test C:/absolute/checks.json` or `--benchmark C:/absolute/benchmarks.json`. These temporarily show synthetic windows and move the pointer; ordinary tests need no interactive desktop.

CI builds/tests, verifies formatting and audits dependencies. CodeQL runs separately. Tags publish ZIP + SHA-256 once a remote exists. [Release guide](docs/RELEASING.md)

## Known limitations

- Alpha: actual Codex/ChatGPT/VS Code/browser drop and paste acceptance is pending; receiver support varies.
- GDI produces SDR; HDR colors can differ. Window capture uses visible pixels, without reconstructing occluded/minimized/protected windows.
- Physical selections passed at 100% and 150% on the connected mixed-DPI layout. 125/175/200%, vertical/HDR screens, Explorer restart, sleep/resume, text scaling and prolonged stress need acceptance.
- A short fresh-process sample measured about 128 MB tray working set. Long-term resources and startup latency need more benchmarking; .NET packaging is larger than a C++ utility.
- Undo is bounded to 20 states; large crop/effect histories can consume substantial memory. Annotation counts and imported sizes are bounded.
- Transfers protect sources for 24 hours. Session-only cleans on normal exit while preserving pins/transfers; crashes fall back to retention.
- User-profile ACLs protect normal cache access. Files are not encrypted or securely erased.

## Contributing

[CONTRIBUTING.md](CONTRIBUTING.md) covers checks and privacy-safe evidence. MIT licensed; OCR licenses are under `licenses/`. Recording, cloud sharing and accounts are out of scope.
