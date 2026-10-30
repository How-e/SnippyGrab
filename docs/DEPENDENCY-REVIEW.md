# Dependency policy

## UI Design browser admission — 2026-10-29

`Microsoft.Web.WebView2` is pinned to stable SDK `1.0.4314.35` in build and publish lockfiles. Its NuGet package supplies the WPF/managed bindings and native loader; it has no package dependencies. Full SDK redistribution terms are retained in `licenses/WebView2-LICENSE.txt`. The independent Evergreen Runtime is detected when UI Design opens; missing-runtime guidance names the prerequisite and leaves ordinary screenshot capture usable. No runtime download or installation occurs automatically. [Microsoft distribution guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution).

The source browser has no host objects, web messaging, downloads, popups or automatically granted permissions. The bundled editor accepts only its fixed virtual HTTPS origin, blocks external navigation/subframes/network and renders validated scene data rather than captured HTML/scripts. Native messages use a typed command allowlist and revision checks. The opt-in extension requires activeTab/scripting/nativeMessaging; its host accepts only an explicitly registered extension origin, bounded length-prefixed JSON and captured data. No browser cookies/storage are exported. Fonts/image content remains untrusted input to browser/Windows decoders; limits and the serviced runtime mitigate but do not eliminate decoder risk. Existing-tab registration, browser-store distribution and clean-profile runtime installation need their own acceptance observations.

Real local WebView2 import, simple vectors, explicit raster fallback, visual rendering, saved/reopened documents and production editor/brief/shelf callback paths are covered by `--check-ui-design`. The source runtime version and bounded fidelity/timing results are recorded in each report. Optional `scripts/test-native-probe.ps1 -Check ui-design` requires the runtime. These results do not establish universal page fidelity, external-agent implementation or all-day resource behavior.

Restore promotes NuGet audit warnings to errors. `scripts/audit-dependencies.ps1` validates the JSON audit format, requires all projects and a feed, and rejects direct/transitive vulnerabilities of any severity, query failures and unsupported reports. No exceptions are configured. Run its fixture tests with dependency changes.

GitHub Actions references use immutable commits and Dependabot. Native OCR sources, recipe and English model are pinned in `scripts/ocr-components.json`; the build receipt ties binary hashes to these inputs. CI and packaging verify integrity and exported versions. Runtime admits only the pinned English model and never downloads models or native components.

Production OCR uses source-built Tesseract and Leptonica with external codecs disabled; WPF supplies bounded, validated raw pixels through the managed wrapper. DLL filenames preserve wrapper compatibility and do not identify the source version. See [NATIVE-OCR.md](NATIVE-OCR.md) for build instructions, pins, advisory mapping, receipt policy and license provenance.

WebP export separately uses pinned libwebp 1.6.0 through an owned pixel-only encoder ABI. Its source/recipe/binary provenance is checked in CI and packaging; this native component is not scanned by the NuGet audit. See [WEBP-ADMISSION.md](WEBP-ADMISSION.md) for the dated advisory/license review, limits and residual risks. OCR's disabled WebP codec stays disabled.

Hash verification identifies admitted bytes, not publisher identity or a guarantee that native code has no vulnerabilities. Review upstream advisories when upgrading sources; retain model/loader/cancellation/raw-pixel regressions and packaged OCR checks. Same-user replacement races, Windows image codecs and unknown future native issues remain within the [threat model](THREAT-MODEL.md).

Detailed audit findings, dated inventories, test transcripts and package result reports belong in external evidence storage or CI artifacts.
