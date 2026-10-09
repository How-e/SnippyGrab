# F11 — Recording and GIF scope evaluation

Decision date: 2026-10-09. Evaluation complete. **NO-GO for enabling recording in the current still-image product.** A separately approved, bounded prototype could establish feasibility; this ADR authorizes no prototype, runtime feature, helper download or release. The queue requests an evaluation, and README's recording exclusion remains in effect.

## Candidate and alternatives

The useful candidate is explicitly started, silent window/display recording to MP4 (H.264), with an optional fixed region cropped from one selected capture item. Start with SDR only. Audio, webcam, streaming, cloud, HDR fidelity, multi-monitor mosaics, editing timelines and automatic/background recording are excluded. GIF would be a short derived export after MP4 is proven, not a second acquisition pipeline.

[Windows.Graphics.Capture](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture) documents system selection, visible capture indication, support checks, frame ownership and HDR constraints. [Media Foundation's H.264 encoder](https://learn.microsoft.com/en-us/windows/win32/medfound/h-264-video-encoder) supplies a Windows encoding candidate. These establish API availability paths, not a runnable WPF recorder, acceptable frame pacing or guaranteed hardware acceleration. [Windows N media components](https://support.microsoft.com/en-us/windows/experience/platform-variants/media-feature-pack-for-windows-10-11-n-february-2023) need explicit prerequisite handling; never install them silently.

Prefer an owned x64 helper process using Windows APIs, a bounded local named-pipe control protocol, a process job lifetime and one recording at a time. The WPF process owns selection, indication, confirmation and output metadata; the helper owns GPU surfaces, timestamps and encoding. No frame payloads travel through CaptureRepository, clipboard or OLE transfer. A helper crash must not take down capture/history. The process split adds packaging/IPC work but is worth evaluating for device-loss and codec fault containment.

Keeping recording outside SnippyGrab is the lowest-risk alternative. An in-process recorder reduces packaging but shares crash and memory pressure with the shelf. A broad FFmpeg stack is deferred: it adds distribution/license/advisory scope without evidence that Windows APIs cannot meet the small prototype. No market-share or “largest market gap” claim is established by this evaluation.

## Proposed prototype contract (requires separate authorization)

| Concern | Initial limit / behavior |
|---|---|
| Output | Silent MP4, SDR, one selected window/display or fixed crop, explicitly named destination outside cache |
| Duration / resolution / FPS | At most 2 minutes, 1920×1080, target 30 FPS; larger selection visibly fits within 1080p with aspect ratio preserved and even encoder dimensions |
| Storage | At most 1 GiB owned staging/output; refuse start without 1.25 GiB free, monitor during recording, stop before quota |
| Queue / memory | At most 3 pending surfaces (about 25 MiB BGRA at 1080p), measured combined helper/main peak target ≤256 MiB additional private memory; allocations/GPU memory measured separately |
| Time / drops | Monotonic capture timestamps, bounded queue; drop frames with explicit counts, preserve elapsed duration; reject nonmonotonic samples |
| Encoder overload | No unbounded buffering; stop with reason if sustained overload exceeds 10% dropped frames over 5 seconds; thresholds need real measurements |
| Stop / privacy | Persistent elapsed-time indicator, Windows capture border retained, always-available registered stop shortcut, explicit Start after target selection; no auto-resume |
| Geometry / device | Stop on item closure, size/DPI/display change, lock/session end or GPU/device loss; require reselection; protected/black output never claimed as supported |
| Commit / cancel | Finalize MP4 into private partial path, independently validate, then atomic destination commit; cancellation deletes only owned partials; destination collision never silently replaces |
| Crash | Partial path/marker remains recoverable or safely removable; no claim that an unfinalized MP4 is playable; never treat partial output as success |
| GIF follow-on | At most 10 seconds, 10 FPS, 720-pixel longest axis, 100 frames, 50 MiB file; explicit palette/quantization and timing review, fail if quota exceeded |

The caps are engineering proposals, not performance evidence. Hardware encoding is preferred when available but must be enumerated and measured. Software fallback must meet the same budgets or decline recording. Capture frames must be disposed promptly and GPU surfaces retained only until encoding permits release. No claim that this preserves headroom for all applications.

GIF requires palette quantization, transparency/disposal semantics and frame timing. [WIC's frame metadata model](https://learn.microsoft.com/en-us/windows/win32/wic/-wic-codec-readingwritingmetadata) and [Microsoft's animated GIF sample](https://github.com/microsoft/Windows-classic-samples/tree/main/Samples/Win7Samples/multimedia/wic/wicanimatedgif) support investigation, not an assumption that the still-image encoder provides a complete animated export. A derived exporter needs its own pixel/time/size acceptance.

## Prototype evidence required before product GO

1. Numbered moving synthetic frames and an independent decoder demonstrate resolution, duration, frame order, timestamp monotonicity, reported drops and absence of audio. Play finalized files in an actual Windows viewer.
2. Measure 2-minute static and scrolling workloads at 1080p/30 FPS, including CPU, helper/main private memory, GPU memory/engine and disk growth. Repeat on supported Windows versions and representative integrated/discrete GPU machines; Windows 10 remains unavailable locally.
3. Inject cancellation during capture/finalization, locked/unwritable paths, disk-full, encoder/device loss, helper crash, app exit and invalid IPC. Confirm no success metadata or destination replacement on failure; retry must work.
4. Observe picker, indicator/border, registered stop shortcut, target closure/movement, minimize, session lock and clean-profile media prerequisites. Synthetic pixel tests cannot establish these interactions.
5. Verify helper identity/provenance, owned executable signing coverage, license inventory, package inclusion, isolated IPC and cleanup. Reopen relevant release gates before accepting broader behavior.

No runtime experiment or resource measurement was performed for recording in this evaluation. The current decision preserves the still-image scope. To reconsider, the owner must explicitly approve recording scope and a prototype against this contract; the ADR must then be updated with evidence and a product GO/NO-GO.
