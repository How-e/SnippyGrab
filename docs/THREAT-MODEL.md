# Initial threat model

Assets: screenshot pixels, clipboard, OCR output, cache paths and user settings. Trust boundary: Windows user session, imported images, drag receivers, native codecs, dependencies, release supply chain.

- Cache names are generated UUIDs; persisted metadata is validated against a canonical cache root and filename grammar. Cleanup rejects reparse points and never recursively traverses directories. Pin/transfer/editor leases take precedence over retention.
- Atomic metadata/image replacement avoids corrupt partial captures. Recovery reconciles orphan owned PNGs after crashes. Corrupt settings/history are quarantined locally without logging their contents.
- Windows user-profile ACLs are inherited. Cache is local plaintext, not an encrypted vault; other processes running as the same user and administrators can read it. Clipboard history/sync is controlled by Windows and may retain images. Redaction cannot retract prior clipboard/file copies.
- Import accepts PNG/JPEG/BMP only with size/pixel limits, fully decodes before admitting a capture and stores a newly encoded PNG. No arbitrary commands, scripts, SVG or external resource loading. No external drop import on the dock in the initial version.
- Startup uses HKCU Run with a quoted verified executable path. Installation/removal uses PowerShell literal paths and per-user directories. No privileged service or IPC/network endpoint.
- Native DLL search excludes the current working directory. OCR is lazy, model hash is pinned, package versions and lock files are committed. Dependabot and CodeQL cover supply-chain/static checks. Codec bugs remain an OS/dependency risk; keep Windows patched.
- Logs include error categories and timing only, never pixels, OCR text or source titles. Protected/DRM/secure desktop cannot be captured. HDR GDI capture is SDR and can have color differences.

Residual risks: receivers may retain files indefinitely; 24-hour transfer grace is a practical contract, not an acknowledgment protocol. Screen capture is not secure erase. Hardware/DPI/foreground and receiver compatibility require manual acceptance.
