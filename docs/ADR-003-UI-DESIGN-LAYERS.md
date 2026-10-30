# ADR 003 — editable UI layers and agent handoff

Date: 2026-10-29. Status: product direction selected; implementation design, not a shipped feature.

The owner selected independent design layers, with their own SnippyGrab interaction design, for editing a project UI and handing the requested changes to an implementation agent. This supersedes the HTML-first recommendation and retires [ADR-001](ADR-001-ASSISTED-SCROLLING.md). Assisted scrolling is removed in this change. The replacement has no production entry point until its import, persistence, editor and export contracts work together.

## Decisions

1. An imported UI becomes a scene graph of frames, text, shapes, paths and images. HTML/CSS and the browser screenshot are capture evidence and source context; the editable layer document is authoritative. Unsupported regions become explicitly marked image layers.
2. Keep an immutable import baseline, stable layer IDs and transactional edit commands. Generate the agent brief from the net difference between baseline and reviewed state. Pointer motion, selection, pan and zoom are not implementation requests.
3. Export a local handoff package with a human brief, machine-readable changes, baseline/edited visuals, changed-region crops and source evidence. The user reviews and copies/exports it; delivery to an agent requires an explicit action. No agent account integration or automatic code mutation is needed for the initial workflow.
4. Preserve observed source evidence separately from user-confirmed mappings and inferred hints. A CSS selector identifies captured markup; it does not prove a repository file or React component. Optional local development instrumentation can supply explicit file/component/token references in a later adapter.
5. Retain the native WPF application, existing screenshot editor and PNG repository. Use a separate versioned design-document store linked to a PNG shelf preview. Do not broaden the capture repository's safe filename rules to accept arbitrary project contents.
6. Proposed renderer: a locally bundled SVG scene surface hosted in WebView2, with a text editing overlay and WPF surrounding controls. Its own layer model controls selection, grouping, transforms, clipping and undo. Start with one captured artboard and explicit variants; defer an infinite canvas, component library and custom WebGL renderer. Verify SVG text/effects/performance before admitting this design as implementation-complete.
7. Use a separate unprivileged browser surface for source navigation. The trusted editor loads only bundled code and sanitized layer data. No captured page scripts run in the editor. WebView2 admission includes runtime detection, clean-profile installation behavior, notices, update compatibility, host isolation and measured resource use.
8. First acquisition adapter targets URL/localhost and an element picker. A browser extension/native host adds existing signed-in tabs. Both produce the same import representation. These adapters and their package impact are new implementation work.

## Consequences

Independent layers allow free visual manipulation and precise before/after evidence. Reconstructing browser appearance requires conversion and a documented support matrix. A design move is a requested visual outcome; it does not establish that absolute CSS positioning is the correct source implementation. Responsive intent, parent layout relationships and repeated-component scope accompany the geometry.

No source round-trip guarantee is made. HTML export, if added, is generated reference markup. The initial product succeeds when an agent can locate affected UI, understand reviewed changes and verify an implementation against the edited design.

Old assisted-scrolling PNGs remain ordinary supported captures. No migration or deletion of user screenshots is required. Old private staging folders are left intact; a future cleanup tool must use the previous ownership checks and explicit scope.

## Implementation and acceptance

The [feature design](UI-DESIGN.md) specifies interaction, document/change contracts, resource targets and independently reviewable F13–F17 milestones. All targets are proposed until measured. No existing Q gate is closed by this ADR, the interaction concept, or removal of obsolete tests. Q1 and Q20 remain open for renewed implementation/tool acceptance.

## Reference behavior

- [pen.dev import/export](https://docs.pen.dev/core-concepts/import-and-export): webpage/element import as layers and separate screenshot capture; imported layers omit source JavaScript interactions. Used as capability reference, not a UI to reproduce.
- [WebView2 WPF](https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/wpf) and [host security](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security): candidate hosting boundary. No dependency is added by this design change.
- [Chrome native messaging](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging): candidate extension-to-desktop transport; large assets need bounded chunking or a negotiated transfer mechanism.
