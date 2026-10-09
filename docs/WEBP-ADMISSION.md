# F10 — WebP export admission

Decision (2026-10-09): admit an owned encoder-only ABI over source-built libwebp 1.6.0, immutable commit `4fa21912338357f89e4fd51cf2368325b59e9bd9` (peeled v1.6.0 tag). No new managed package, runtime download, installed codec extension, decoder/import admission or OCR change. PNG remains the default and internal revision format.

## Source, license and security review

The [upstream release source](https://chromium.googlesource.com/webm/libwebp/+/4fa21912338357f89e4fd51cf2368325b59e9bd9/) and its NEWS were checked before enabling the export option. The BSD license, AUTHORS and additional patent grant ship under licenses/. NEWS records the 1.3.1 lossless encoder security fixes including CVE-2023-1999 and the 1.3.2 lossless decoder fix for CVE-2023-4863; this pin includes both. [CVE-2023-4863](https://nvd.nist.gov/vuln/detail/CVE-2023-4863) is a reason to keep encoded input out of this adapter, not a claim that native encoding is risk-free. Upstream/NVD searches on this date did not establish a newer applicable encoder advisory; search completeness is not a vulnerability guarantee. Recheck advisories and upstream fixes before each public release.

## Build and packaging

Windows x64 only; Visual Studio C++ tools/CMake at build time, static MSVC runtime and static libwebp. Tools, animation, mux and worker threads are disabled. The DLL exports only SnippyWebpEncode and SnippyWebpFree. The app loads an absolute private x64 path, checks its recorded SHA-256 and source identity, and never searches user codec locations. Missing/mismatched sidecars fail the export before any destination replacement. Build scripts reject modified source; provenance binds source pin, wrapper/build recipe and binary. Publish copies DLL/provenance as sidecars; package validation checks them alongside the bundle inventory. This hash record provides integrity consistency, not publisher authentication (F03 remains separate).

## Pixels and limits

BGRA32 straight alpha, width/height 1–16383, at most 16 MP. This stricter export cap bounds managed raw pixels to 64 MB and reduces codec working memory; it is not a measured universal process-memory ceiling. Large composed PNGs remain valid but must be reduced before WebP export. Lossless uses exact=1 to preserve invisible RGB; lossy preserves alpha but can soften text. Quality 1–100 applies only to lossy output in the UI; lossless uses a fixed effort/quality 90. [The API](https://developers.google.com/speed/webp/docs/api) defines the format dimension limit and advanced configuration. No original PNG metadata is carried into WebP. Output is capped at 100 MiB before managed copy. Cancellation polls the codec progress hook and is checked before atomic commit; a single codec allocation is not interruptible.

## Evidence and remaining acceptance

Automated fixtures cover headers, size/quality behavior, cancellation, bounds and export paths. An independent Pillow decoder round trip must compare straight RGBA bytes including transparent pixels and dimensions. Package checks establish inclusion, not a clean Windows profile launch. Physical UI and clean-profile acceptance remain separate; no claim of universally smaller output or universal viewer compatibility.
