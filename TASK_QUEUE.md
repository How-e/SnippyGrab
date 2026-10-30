# Planned improvements task queue

Researched 2026-10-09 against `df652d6`. Implementation of the remaining backlog authorized 2026-10-09; local commits only. Implementation status does not close acceptance gates. Details, source links, integration points and acceptance criteria are in [IMPLEMENTATION-BRIEFS.md](docs/IMPLEMENTATION-BRIEFS.md).

New work uses F identifiers to avoid confusing it with Q1–Q51 in `scripts/release-gates.json`. That file remains the packaging authority. Existing release gates are not superseded by this backlog. Priority reflects user benefit, adoption risk, implementation cost and dependencies; size is relative complexity, not a time estimate.

## Current cycle — UI Design, 2026-10-29

Owner direction: independent editable design layers for a project UI, with tracked changes and an implementation-agent handoff. Assisted scrolling is removed. The replacement is implemented for a local unsigned alpha under Tray → UI Design; acceptance limitations remain explicit. [Feature design and acceptance](docs/UI-DESIGN.md) · [ADR-003](docs/ADR-003-UI-DESIGN-LAYERS.md).

| Order | ID | Priority | Task | Size | Dependencies / owner | Status |
|---|---|---|---|---|---|---|
| 1 | F12 | P1 | Remove assisted scrolling and design the layer-edit-to-agent workflow | M | Owner direction; Agent | Implemented and verified; assisted scrolling retired |
| 2 | F13 | P1 | Build versioned layer documents, commands, persistence and net-change tracking | L | F12; Agent | Implemented; model/storage/undo/diff regressions pass |
| 3 | F14 | P1 | Prove browser import, layer conversion and renderer fidelity | XL | F13; dependency/runtime admission; Agent | Implemented; real WebView2 fixture/render and bounded 1k/10k measurements pass; broad fidelity pending |
| 4 | F15 | P1 | Build the native layer workspace and accessible editing controls | XL | F14; Agent + desktop acceptance | Implemented; production editor bridge/save/copy fixture passes; physical accessibility/gestures pending |
| 5 | F16 | P1 | Build reviewed changes and portable agent handoff | L | F15; Agent + independent implementation exercise | Implemented; verified review/ZIP/visual inventory; independent external-agent exercise pending |
| 6 | F17 | P2 | Add existing-tab import and implementation recapture comparison | L | F16; browser/native-host admission + desktop acceptance | Implemented; bounded protocol/comparison fixtures pass; live signed-in extension acceptance pending |

F13–F17 implementation was authorized together with local commits and a local-only release. Implemented functionality and automated evidence do not close owner/physical acceptance. Do not push commits/tags or publish these artifacts to GitHub until separately requested. Existing signing and release prerequisites remain applicable to publication.

## Earlier cycle — historical status

| Order | ID | Priority | Task | Size | Dependencies / owner | Status |
|---|---|---|---|---|---|---|
| 1 | F01 | P1 | Reconcile capability, compatibility and acceptance documentation | S | Agent; owner evidence if unavailable | Implemented; unavailable build/receiver evidence explicit |
| 2 | F02 | P1 | Make OCR prerequisites and package failures actionable | M | F01; Agent + clean-profile acceptance | Implemented; owner-reported Windows 11 clean-profile PASS on acceptance .2 |
| 3 | F03 | P1 | Establish real publisher signing and downloaded-install evidence | M | F02; owner signing identity/access + Agent | Tooling implemented; real signing/install blocked by external prerequisites |
| 4 | F04 | P1 | Add dedicated monitor capture | S–M | Agent + physical monitor acceptance | Implemented; owner-reported full hardware batch PASS on acceptance .2 |
| 5 | F05 | P1 | Add JPEG export with shared safe export boundary | M | Agent | Implemented; agent PNG dialog and owner export batch PASS on acceptance .2 |
| 6 | F06 | P1 | Add ordered batch export from shelf and history | M | F05; Agent + receiver/file workflow acceptance | Implemented; owner-reported desktop workflow PASS on acceptance .2 |
| 7 | F07 | P1 | Combine selected captures into strip/grid | M | Agent + editor/shelf acceptance | Implemented; owner-reported native gesture PASS on acceptance .2 |
| 8 | F08 | P1 | Specify scrolling capture feasibility and supported first-release targets | M | Agent research; F07 informs composition | Retired 2026-10-29; superseded by ADR-003 |
| 9 | F09 | P1 | Implement bounded, assisted scrolling capture after feasibility decision | L | F08 go decision; F07 composition conventions; Shared | Removed 2026-10-29 under F12 |
| 10 | F10 | P2 | Add WebP export after explicit codec admission | M | F05; dependency/security review | Implemented; pinned encoder admitted; owner-reported Windows 11 clean-profile PASS on acceptance .2 |
| 11 | F11 | P3 | Evaluate video/GIF as a separately approved product scope | XL | Owner scope decision; Agent research | Evaluation complete; ADR-002 NO-GO for current product; recording deferred |

P1 means the next improvement cycle, not an emergency defect. P2 is useful follow-on work. P3 is outside the current still-image scope. The earlier cycle established bounded capture/export/composition behavior; the current cycle adds a separately stored design document and agent handoff. Documented evidence and installation confidence remain release prerequisites.

F03 may wait for publisher credentials. F08/F09 are retired and must not be restored from their historical briefs. F11 evaluation is complete in [ADR-002](docs/ADR-002-RECORDING-SCOPE.md); recording remains deferred until the owner explicitly changes the exclusion.

## Execution and completion rules

1. Prompt an agent with the selected F ID, its brief and the common constraints. Implement one coherent item per scoped commit; do not automatically implement the whole backlog.
2. Recheck current source and gate status. This research is a dated snapshot. Keep implemented behavior, automated verification, observed desktop acceptance and owner-accepted scope separately recorded.
3. Use synthetic content and isolated data. Run focused regressions and the repository's required build/test/format/release checks appropriate to the change. Reopen affected Q gates through the existing review process; never mark them closed from an F checkbox alone.
4. Record PASS, FAIL, NOT RUN or NOT AVAILABLE with app/build, environment and evidence scope. Real receivers, physical monitors, login, SmartScreen and clean-profile installation require observation beyond unit tests.
5. Do not buy credentials, provision paid services, push, tag or publish from this backlog alone. Signing prerequisites can remain outstanding while local implementation and documentation become reviewable.

## Suggested later agent prompt

> Implement Fxx from TASK_QUEUE.md using its section in docs/IMPLEMENTATION-BRIEFS.md. Revalidate the source assumptions first. Keep the work limited to this item and its stated dependencies; preserve the common constraints. Complete agent-available verification and document any remaining desktop or owner acceptance separately. Prepare one scoped commit if requested. Do not publish a release or begin another backlog item.
