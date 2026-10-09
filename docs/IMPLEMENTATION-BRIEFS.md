# Improvement research and implementation briefs

Research date: 2026-10-09. Source baseline: `df652d6`, current local checkout. This document prepares later implementation; proposed names, thresholds and interfaces are design recommendations, not existing features or measured results. [Task order](../TASK_QUEUE.md).

## Current source findings

| Area | Observed implementation | Consequence |
|---|---|---|
| Capture | `CaptureService.CaptureAsync` uses `Native.Desktop` for Desktop; Region freezes it and crops; ActiveWindow intersects window bounds with it | Dedicated display capture is missing; reuse physical monitor bounds rather than a new capture backend |
| Monitors | `MonitorService.All()` returns physical `Bounds`, `Work`, identity, DPI and primary status; settings persist a dock monitor identity | Reuse enumeration/identity but keep capture target separate from shelf placement |
| Image pipeline | `ImageService` captures SDR GDI pixels, freezes WPF images, encodes PNG; 80,000,000 pixels and 100 MiB encoded input limits | Preserve admission bounds; composites and long images also need transient-memory budgets |
| Storage/export | `CaptureRepository.Add/Replace` manages PNG revisions and leases; `CaptureExport.Write` copies PNG bytes atomically outside cache and updates save metadata | Keep internal PNG files; introduce format conversion only for exports |
| Sharing/order | `ClipboardService` uses `TransferPayload.Ordered`; file transfers use shelf order and durable grace | Batch and composition need the same explicit order, even in time-sorted history |
| OCR | Source-built native DLLs and pinned English model; lazy engine initialization; generic runtime error in `AppController.OcrText` | A load failure does not identify which prerequisite failed; diagnostics must distinguish supported failure categories |
| Installer/signing | Transactional installer and checksum inventory; `package.ps1` and `sign-artifact.ps1` already offer optional certificate-thumbprint signing before hashes/embedding | Extend the existing hooks and validate real release artifacts; do not build another signing system |
| Evidence | `release-gates.json` has 51 gates, 39 closed and 12 open; `ACCEPTANCE.md` explicitly defines a matrix rather than passed results | Reconcile the source record before claiming complete acceptance |

Open gates at this baseline: Q1, Q9, Q16, Q19, Q20, Q25, Q26, Q27, Q29, Q32, Q41, Q44. All are P1. Q34 (startup/capture latency), Q35 (resources) and Q48 (optional signing/languages) are closed. Q48 closure establishes neither a signed published build nor SmartScreen acceptance. README still calls startup latency unverified, describes synthetic resources, and leaves Explorer restart/sleep/resume/all-day behavior unverified. Some wording may correctly describe narrower evidence; **closed does not establish exhaustive compatibility**.

Older acceptance summaries reported 51/51 closure for a particular accepted build/scope. That historical statement does not override this checkout's reopened/open gate values. A future reconciliation must recover the applicable build and evidence before explaining any discrepancy. Detailed audit reports remain outside Git according to the existing project convention.

## Common integration constraints

- Preserve native WPF, local operation, no runtime downloads/account/telemetry and no continual screen polling. New capture loops run only in an explicitly started session.
- Preserve region/desktop/window shortcuts and behavior. Additive settings must load old settings safely; append enum values rather than shifting persisted numeric values.
- Retain PNG managed revisions, exact-byte PNG export where available, immutable issued transfer revisions, leases, atomic writes, cache exclusion and redirect checks. Never convert a cache file in place for a new export format.
- Expensive work uses bounded workers and frozen inputs; UI updates remain on the dispatcher. Cancel/dispose paths must release images, leases, staging files and native handles.
- Use the shared theme, accessibility and text-scale controls. No new layout system. Keep still-image OCR, editing and transfers working independently of optional additions.
- Do not log pixels, OCR text, window titles, document names or URLs. Describe runtime failures by category; diagnostic export must not introduce private content.

## F01 — Capability and evidence reconciliation

**Problem/value:** users cannot readily distinguish an implemented feature from observed acceptance or a scope waiver. Resolve this before expanding marketing claims.

**Recommended deliverable:** a short `docs/CAPABILITIES.md` with rows for capture modes, physical DPI/HDR limits, shelf/editor, OCR, file transfers, lifecycle, installation, accessibility and performance. Each row separates implementation, automated evidence, observed environment/build, scope limitations and remaining acceptance. Include a dated app/source version. Distinguish NOT RUN, NOT AVAILABLE and scope accepted; avoid an undifferentiated “supported” column.

**Integration:** README Installation/Known limitations and capture contracts; `docs/ACCEPTANCE.md`, `USER-TESTING.md`, `RESOURCE-TESTING.md`, `RELEASING.md`; `scripts/release-gates.json`; existing startup/pipeline/resource probes and external accepted reports. Reconcile Q34/Q35 against their actual workload and timestamps, and open Q25/Q27/Q41/Q44 against any older closure record. Gate counts should come from the JSON, not memory or an old checklist.

**Steps:** inventory every acceptance/performance claim; build a claim-to-evidence table; locate the corresponding external report where available; record unavailable reports explicitly; shorten README and link to the capability record. Leave gate status alone unless the relevant review establishes a reason and evidence for changing it. Describe the latest UI build separately from a previous accepted build.

**Done when:** no contradictory startup/lifecycle/install statements remain; performance numbers have units, workload and build; hardware/receiver claims identify observed scope; planned features are visibly absent. An agent can validate links/counts and cross-check claims. Owner input is needed only for evidence that cannot be recovered. Do not rerun prolonged workloads just to repair prose.

## F02 — OCR readiness and prerequisite handling

**Problem/value:** a complete ZIP and a working .NET app can still leave OCR unusable. The current generic error conflates missing model, native sidecar and runtime/load problems.

**Recommended design:** an explicit, on-demand “Check OCR readiness” action in Editor & OCR settings, with a quick file/model admission check and a bounded worker-based engine smoke check. Report Ready, missing/modified English model, missing native library, architecture/load failure or unsupported/inconclusive dependency failure. Show reinstall-complete-package guidance separately from a Microsoft runtime link. Do not claim every DLL load error proves VC++ is absent. Keep OCR lazy at ordinary startup.

**Integration:** `OcrService`, `AppController.OcrText`, `SettingsWindow`, installer `Program.cs`, `build-native-ocr.ps1`, native provenance/policy, `verify-package.ps1`, `NATIVE-OCR.md`. Reuse model hashing and hardened DLL search; do not broaden DLL discovery to arbitrary PATH directories. Installer can inspect package completeness without running recognition or blocking screenshot installation on OCR readiness.

**Runtime deployment decision:** inspect actual produced DLL imports using the installed build tools (e.g. `dumpbin /dependents`), then verify on a disposable profile with no deliberately added VC runtime. The build recipe has no explicit `CMAKE_MSVC_RUNTIME_LIBRARY` setting, so do not infer exact deployment needs from that omission. Prefer accurate prerequisites first. Evaluate app-local redistribution or static runtime as a separate measured change only if licensing, servicing, provenance, wrapper compatibility and install footprint justify it. Do not install a redistributable silently or introduce a runtime download. Microsoft describes central and app-local deployment and their servicing tradeoffs in [VC++ redistribution guidance](https://learn.microsoft.com/en-us/cpp/windows/redistributing-visual-cpp-files?view=msvc-170).

**Verification:** isolated missing/modified model and native-library cases; no compiler paths exposed to end users; capture/export work with OCR unavailable; retries after repairing installation; UI remains responsive and cancellation is honest about noninterruptible native recognition. Clean-profile observations record exact OS, runtime state, ZIP/setup and app version. No changes to the live user's sidecars or runtime installation.

## F03 — Publisher signing and installation confidence

**Problem/value:** integrity hashes do not identify a publisher. This is a high-value adoption improvement with external identity prerequisites.

**Recommended path:** reuse existing thumbprint hook for a protected Windows signing machine with a real publisher certificate; require a trusted timestamp for release signing and verify it on downloaded output. Evaluate a managed signing service only if owner eligibility, budget and credentials make it preferable; adaptation of the signer interface should be separately reviewed. No secrets in repository, PR jobs or logs.

**Integration:** `sign-artifact.ps1`, `package.ps1`, tag release workflow, `test-signing.ps1`, `verify-package.ps1`, installer and `RELEASING.md`. Existing flow signs app before bundle checksums/archive/setup embedding, then setup before its final checksum; preserve that order. Determine coverage for owned executable artifacts, preserve third-party dependency provenance and do not casually re-sign native dependencies.

**Acceptance plan:** verify signature chain, expected publisher, digest and timestamp on app and setup after downloading the authorized published assets; verify inventory and embedded payload; use a disposable clean Windows profile to test portable launch, non-admin install, OCR, upgrade/rollback, startup opt-in on real login and Apps uninstall with data preservation. Include a browser download retaining Mark-of-the-Web and record SmartScreen/Smart App Control behavior and OS settings. Do not bypass protections or treat command-line download alone as the browser-install experience. Windows 10 coverage remains NOT AVAILABLE until that environment exists.

**Owner prerequisites:** publisher identity/certificate or service enrollment, protected signer access and release authorization. Do all credential-free code and fixture work first when later prompted. Microsoft's [SmartScreen guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation) distinguishes signing from reputation: even a valid new signed build can warn. Success means authenticated publisher and documented behavior, not a promise of zero prompts. Q41 remains a relevant independent gate.

## F04 — Dedicated monitor capture

**Recommended UX:** add “Capture monitor…” to the tray with enumerated display choices and a keyboard-accessible picker. Offer an optional configured monitor-capture shortcut, disabled by default. The command captures the chosen display's complete physical bounds, including taskbar. Entire desktop continues to mean virtual desktop. Capture selection never borrows `DockMonitorIdentity`.

**Integration:** append Monitor to `CaptureMode` if routing through the mode enum; carry an explicit target/options object into `CaptureService` rather than overloading shelf settings. Use `MonitorService.All()`/`MonitorInfo.Bounds`, `MonitorIdentity`, `ImageService.Capture` and existing hide/foreground/clipboard delivery. Update `HotkeyService`, settings draft/validation, tray/icons/help and diagnostic capture checks. Use physical bounds, not work area or DIP dimensions.

**Policy:** resolve the identity immediately before capture; if a picked display disappears, cancel with a message instead of capturing an unintended monitor. A future automatic pointer-monitor option can be added separately; first release uses an explicit target. Store an optional last target independently, and re-resolve it after reconnect. Monitor identity remains topology-dependent rather than permanent hardware identity.

**Verification:** negative origins, mixed DPI, portrait bounds, primary/nonprimary displays, cursor clipping, hot-plug between choice and capture, cancellation, no unwanted focus/shelf pixels, 80 MP limit, old settings and all current modes. Geometry fixtures establish coordinate logic; owner observation establishes exact physical pixels and available display scope. No GDI-to-Windows.Graphics.Capture migration is necessary for this still-image addition.

## F05 — JPEG export and shared export boundary

**Recommended design:** keep PNG as default. Add PNG/JPEG choices to one shared export dialog reached from shelf, history and editor. JPEG quality starts at a proposed 90 with an explicit quality control; flatten alpha onto a visible default white background. Show output format/quality without pretending JPEG preserves exact text pixels. Editor export commits pending edits as today and leaves clipboard unchanged.

**Integration:** `CaptureExport`, `ImageService`, `AppController.Save`, `EditorWindow.ExportPng`, `DockWindow`/`HistoryWindow` actions, save-path metadata and settings draft. Move reusable path validation into a format-independent boundary in Core; keep WPF encoding in App. Preserve PNG's byte-copy path and atomic file replacement. Match actual encoded format to extension. A prior JPEG destination must not silently influence an explicit PNG selection.

**Safety:** snapshot the selected revision and acquire its lease before worker encoding. Recheck destination policy at commit, release on cancellation, and mark Saved/ExportPath only for the file actually written. Preserve the existing distinction between a successful file write and failed metadata persistence. Strip incidental metadata; maintain opaque redaction and explicit alpha conversion. Do not add JPEG managed cache revisions.

**Verification:** PNG bytes/ancillary metadata remain identical; JPEG header, dimensions, alpha background and decoded quality are correct; overwrites/cancel/unwritable destinations/cache redirects behave safely; stale revisions and metadata persistence failure are handled; all three UI entry points share semantics. Compare photographic and terminal fixtures for size/readability without universal compression claims. [WIC native formats](https://learn.microsoft.com/en-us/windows/win32/wic/-wic-codec-native-pixel-formats) document JPEG and PNG pixel constraints.

## F06 — Ordered batch export

**Recommended UX:** “Export selected…” for multiple shelf/history items; choose destination folder and format once. Preview number/order and deterministic collision-safe filenames such as `001-SnippyGrab-<time>.png`. Order matches `TransferPayload.Ordered`, not click order or history sorting. Keep single-file Save behavior.

**Integration:** shared F05 export boundary, shelf/history selection, repository leases and metadata. Snapshot ordered IDs/revisions before starting; export serially or with a small measured bound instead of decoding every image together. Offer progress/cancel. Use per-file atomic writes, a preflight naming plan, explicit skip/replace policy and a results summary.

**Failure contract:** batch export is not a folder-wide transaction. Cancellation leaves completed exports; failures are reported per file; completed metadata remains accurate. Never delete pre-existing files as rollback. Default collisions get unique names, including duplicate timestamps and case-insensitive Windows collisions. Clear/retention/edit/exit cannot invalidate leased inputs. UI should identify successful, skipped, failed and unprocessed counts.

**Verification:** 1/2/20 items, history pages/sort differences, shelf reorder, naming collisions, locked/unwritable files, cancel mid-batch, disk-full injection and metadata failure. Check original cached PNGs and transfer paths are unchanged. Report responsiveness and bounded memory; do not equate batch saving with multi-file receiver acceptance.

## F07 — Combine captures into strip/grid

**Recommended UX:** “Combine selected…” from shelf/history when at least two items are selected. Preview vertical strip (default), horizontal strip or grid; explicit order/reorder, alignment, spacing and background. Keep native pixels by default; an optional fit-to-width choice visibly warns of resizing. Create a new managed PNG capture on confirmation and open the normal editor. Original captures, edits and transfer files remain intact.

**Integration:** Core computes pure checked layout rectangles and total dimensions; App renders frozen images with the existing WPF image pipeline on a worker. Use `TransferPayload.Ordered` for initial order and lease snapshotted input revisions. Add through `CaptureRepository.Add` and the existing capture preservation path, with cancellation-safe commit. Do not put composite pixels into an existing source record or stamp misleading monitor geometry onto the new item.

**Limits:** preflight output dimensions, codec-specific dimensions, 80 MP and estimated simultaneous decoded/output/encoding memory. An 80 MP BGRA surface alone is about 320 MB before other allocations, so the pixel limit is not a complete memory budget. Load/render sources sequentially where possible; propose an initial selection limit of 20, subject to measured resource behavior. Reject oversize layouts before allocation and offer smaller dimensions/fewer inputs. Preview should decode thumbnails, not full-resolution sources.

**Verification:** heterogeneous sizes, alpha/padding, odd grid rows, no unrequested scaling, shelf/history ordering, cross-view revisions, cancel/clear/exit, checked overflow and oversize rejection. Decoded composite pixels establish seam/layout accuracy. Confirm OCR, redaction/editor, pin, copy, drag and export work on the new capture. Inputs retain byte-identical PNG files.

## F08 — Scrolling feasibility and scope decision

**Product goal:** one readable long still image. A generic screenshot tool cannot assume browsers, terminal logs, virtualized chats and spreadsheets expose the same scrolling or hidden content. Spreadsheets add frozen headers and horizontal scrolling; chats/logs may update continuously.

**Recommended first scope:** user-assisted vertical capture of a selected viewport on static content. The user scrolls; the app accepts explicit next-frame captures, estimates overlap, previews seams and lets the user reject a frame or adjust a join. Automatic wheel injection, page DOM access, browser extensions, horizontal spreadsheets and “capture every app” are deferred. Do not automatically restore scroll position when application state cannot be tracked reliably.

**Design deliverable before implementation:** an ADR documenting capture-session state, targets, overlap scoring, ambiguity threshold, seam correction UX, sticky-content handling, cursor policy, budgets and failure behavior. Build a synthetic fixture specification with unique lines, repeated rows, flat regions, fractional shifts, sticky headers, animation and virtualized content. Select at least one actual available browser/static document and one log-like target for later acceptance, recording exact versions. Mark unavailable apps/targets explicitly.

**Algorithm assessment:** compare deterministic pixel/luminance overlap matching with bounded coarse-to-fine search and edge/text structure; do not use OCR as the join authority. Repeated/blank content must return uncertain rather than inventing alignment. Offer manual overlap adjustment. Sticky header/footer removal must be explicit previewed trimming, not destructive automatic deduplication. Capture frames after content settles, with user confirmation if it continues changing.

**Go/no-go:** proceed with F09 only if the design supports bounded resource use, honest rejection of ambiguous joins, reversible seam correction and a defensible first target matrix. Otherwise ship manual combination and keep scrolling deferred. A later implementation agent may prototype only when specifically authorized; this research pass includes no prototype or runtime experiment.

## F09 — Bounded assisted scrolling capture

**Integration after F08 approval:** a session coordinator in App separate from one-shot `CaptureService`, a pure overlap/layout component in Core, a small preview/control view and existing ImageService/PNG repository commit. F07 supplies layout/budget conventions; scrolling needs its own alignment logic. Keep frames private to the session until confirmed final output; do not auto-copy each frame.

**Session contract:** select viewport → first frame → user scrolls → capture next → preview/accept join → repeat → finish/cancel. Escape cancels reliably; final save asks before accepting uncertain seams. Hide controls/pins during each frame, keep geometry fixed in physical coordinates, and cancel/reselect on target disappearance or display/DPI/viewport change. Cursor is omitted by default for joins. Never scroll the wrong foreground application or continue collecting frames after cancel/exit.

**Budgets:** proposed cap 30 frames with output constrained to existing 80 MP plus a lower measured memory budget; maximum elapsed session duration and temporary-disk quota must be chosen in the ADR. Stage lossless frames in an owned session directory with leases/cleanup, bound decoded working frames, and dispose previews. An 80 MP cap must remain end-to-end compatible with editor/export/OCR. End-of-content duplicates stop progression; uncertain matches require correction. No background capture service.

**Verification:** numbered synthetic document proves no missing/duplicate rows; ambiguous/repeated/blank frames fail safely; sticky regions, content animation, scale changes, premature finish, crash/temp cleanup, cancellation and oversize sessions are covered. Desktop acceptance records target/version and limitations for long pages, logs and chats separately. Spreadsheets remain unsupported unless independently demonstrated within the declared scope. A static corpus pass is not universal app compatibility.

## F10 — WebP export codec admission

**Recommended design:** a follow-on export adapter supporting lossless (default for UI/text) and explicit lossy quality. Keep PNG internal and do not expand image import or OCR encoded-input admission as a side effect. JPEG already delivers the first smaller-file option.

**Decision required:** WPF/WIC's documented native formats do not establish a universally available WebP encoder. Do not depend on user-installed codec extensions or assume a .NET wrapper supplies safe self-contained packaging. Evaluate a narrowly scoped pinned libwebp encoder with a maintained binding versus a suitable existing reviewed package. Use [Google's WebP API documentation](https://developers.google.com/speed/webp/docs/api) as the format/API authority and refresh security/version/license facts when implementation starts.

**Admission:** document immutable version/hash, license notices, advisories, native/runtime architecture requirements, publish/CI layout, alpha/pixel conversion and maximum dimensions. WebP format dimensions can be stricter than the 80 MP limit; reject oversized strips before calling the codec. Do not enable the OCR build's disabled WebP dependency to obtain an export encoder. Introduce no runtime downloads.

**Verification:** independent decode and dimensions/header validation, lossless pixel/alpha round trip, lossy quality behavior, codec-specific size rejection, cancellation/resource bounds, package inclusion on a clean profile and failure without corruption. Reuse F05/F06 path/metadata/atomicity checks. Codec admission is a prerequisite, not an implementation detail to postpone until packaging.

## F11 — Video/GIF scope evaluation (deferred)

**Current decision:** recording remains out of scope in README. Document a possible future product phase; do not route video frames through `CaptureRepository` or enable recording while implementing still-image items. The “major market gap” is a user hypothesis, not a researched market-share finding.

**Future ADR:** choose silent region/window/display recording, explicit duration/FPS/resolution/storage budgets, visible recording indicator and stop shortcut; consider MP4 first and GIF as a short-clip derived export. Defer audio, webcams, streaming and cloud. Evaluate Windows.Graphics.Capture/Direct3D frame acquisition with Media Foundation encoding; review whether a separate helper/process provides crash and resource isolation. Avoid packaging a broad external media stack without a demonstrated need and license/security review.

**Why separate:** timed frames, dropped-frame handling, timestamps, GPU/device-loss behavior, video encoding, recovery and media retention require different contracts from still PNG revisions/OLE bitmap transfer. GIF palette quantization and duration/size constraints do not follow from single-frame WIC GIF encoding. Recording may also change the privacy/no-polling statement, user expectations and protected-content behavior.

**Research sources and limits:** Microsoft documents display/window frame acquisition, system picker/indicators, device support checks, frame disposal and HDR considerations in [Windows screen capture](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture). This supports an architecture candidate; it does not prove a packaged WPF recorder, HDR fidelity or low resource use.

**Entry gate:** explicit owner approval to broaden scope, then a written ADR and separately authorized prototype plan. Future evidence must include timestamps/dropped frames, multi-minute bounded resources, disk-full/cancel/device-loss/crash recovery, actual playback of final media, privacy indicators and clean-profile packaging. No recording implementation is authorized by this backlog.

## Handoff evidence record

For each later F item, record source/build, implemented subset, changed files, automated commands/results, desktop observations, unsupported targets, affected Q gates and remaining owner prerequisites. Keep private/detailed reports outside Git and concise reproducible instructions in the repository. Never carry a passing result forward across changed behavior without evaluating whether it still applies.
