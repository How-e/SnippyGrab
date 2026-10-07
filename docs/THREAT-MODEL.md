# Threat model and evidence review

Reviewed 2026-10-06 against the current source and local Windows tests. This is a documented review with residual risks, not stable security approval. Dependency and native advisory evidence is in [DEPENDENCY-REVIEW.md](DEPENDENCY-REVIEW.md).

Assets: screenshot pixels, clipboard, OCR output, cache paths and user settings. Trust boundary: Windows user session, imported images, drag receivers, native codecs, dependencies, release supply chain.

- Cache names are generated UUIDs; persisted metadata is validated against a canonical cache root and filename grammar. Cleanup rejects reparse points and never recursively traverses directories. Pin/transfer/editor leases take precedence over retention.
  Managed paths are rechecked before reading, writing and cleanup, including replacement of an initially valid cache with a junction. The isolated junction test proves operations reject redirection and preserve an external sentinel. Checks do not provide handle-based race protection against another process with the same user's write permissions.
- Atomic metadata/image replacement avoids corrupt partial captures. Recovery reconciles orphan owned PNGs after crashes. Corrupt settings/history are quarantined locally without logging their contents.
- Windows user-profile ACLs are inherited. Cache is local plaintext, not an encrypted vault; other processes running as the same user and administrators can read it. Clipboard history/sync is controlled by Windows and may retain images. Redaction cannot retract prior clipboard/file copies.
- Import accepts PNG/JPEG/BMP only with size/pixel limits, fully decodes before admitting a capture and stores a newly encoded PNG. No arbitrary commands, scripts, SVG or external resource loading. No external drop import on the dock in the initial version.
  Tests exercise real encoders/decoders, disguised GIF, truncated PNG, forged oversized PNG dimensions and sparse files beyond 100 MB. PNG dimensions and end marker are checked before decoding; allowed decoder types are checked independently of extension. These bounds mitigate resource abuse; they are not a codec sandbox or exhaustive image fuzzing.
- Startup uses HKCU Run with a quoted verified executable path. Installation/removal uses PowerShell literal paths and per-user directories. No privileged service or IPC/network endpoint.
- Native DLL search excludes the current working directory. OCR is lazy, model hash is pinned, package versions and lock files are committed. Dependabot and CodeQL cover supply-chain/static checks. Codec bugs remain an OS/dependency risk; keep Windows patched.
  Runtime validates the English model digest before native model parsing. CI/package audit checks native/model digests and fails reported NuGet vulnerabilities; the source-pinned codec-free native upgrade, upstream advisory fix ancestry and packaged regressions close  locally; residual native/OS decoder/same-user risks remain documented. Hosted CodeQL has not been observed; only local compiler/analyzer checks have run.
- Logs include error categories and timing only, never pixels, OCR text or source titles. Protected/DRM/secure desktop cannot be captured. HDR GDI capture is SDR and can have color differences.

Residual risks: receivers may retain files indefinitely; 24-hour transfer grace is a practical contract, not an acknowledgment protocol. Screen capture is not secure erase. Hardware/DPI/foreground and receiver compatibility require manual acceptance.

## Inspection evidence

Source search found no runtime HTTP clients, web requests, socket listeners, update polling or model downloads. The production shell launch opens a validated existing export directory; it does not execute metadata as a command. Native input injection exists only in opt-in legacy diagnostics, which were not run in this pass. Computer-use interactions used the Windows computer-use API.

Tracked-file inspection found the intended app icon and three synthetic documentation PNGs, no private captures, local settings/history, binaries, traineddata, build outputs, logs or signing credentials. Packaging uses a fresh staging directory and verifies OCR sidecars; the package hash manifest covers executable dependencies and notices. This is a tracked-file/source inspection, not a forensic scan of all historical Git objects or a hosted secret-scanner result.

The isolated interactive lane is explicitly opt-in. Its report contains capture IDs/dimensions/state, monitor geometry and intercepted clipboard format names; it does not record OCR text or desktop pixels. Native test captures remain in an isolated temporary cache. Cleanup of the two development caches was rejected by automatic approval review with only "blocked by policy" as the reason; those caches remain under Windows Temp, including the native desktop captures. The packaged lane contains synthetic captures only and also retains its isolated cache. Exact roots are in the ignored local interaction reports. Reports and synthetic validation images remain in ignored `artifacts/`. The normal user's cache and settings are not used.
