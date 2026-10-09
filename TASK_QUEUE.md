# Planned improvements task queue

Researched 2026-10-09 against `df652d6`. Implementation of the remaining backlog authorized 2026-10-09; local commits only. Implementation status does not close acceptance gates. Details, source links, integration points and acceptance criteria are in [IMPLEMENTATION-BRIEFS.md](docs/IMPLEMENTATION-BRIEFS.md).

The former task queue is absent from this checkout. New work uses F identifiers to avoid confusing it with Q1–Q51 in `scripts/release-gates.json`. That file remains the packaging authority. Existing release gates are not superseded by this backlog. Priority reflects user benefit, adoption risk, implementation cost and dependencies; size is relative complexity, not a time estimate.

| Order | ID | Priority | Task | Size | Dependencies / owner | Status |
|---|---|---|---|---|---|---|
| 1 | F01 | P1 | Reconcile capability, compatibility and acceptance documentation | S | Agent; owner evidence if unavailable | Implemented; unavailable build/receiver evidence explicit |
| 2 | F02 | P1 | Make OCR prerequisites and package failures actionable | M | F01; Agent + clean-profile acceptance | Implemented; clean-profile acceptance NOT RUN |
| 3 | F03 | P1 | Establish real publisher signing and downloaded-install evidence | M | F02; owner signing identity/access + Agent | Tooling implemented; real signing/install blocked by external prerequisites |
| 4 | F04 | P1 | Add dedicated monitor capture | S–M | Agent + physical monitor acceptance | Implemented; physical monitor acceptance NOT RUN |
| 5 | F05 | P1 | Add JPEG export with shared safe export boundary | M | Agent | Implemented; shared dialog desktop acceptance NOT RUN |
| 6 | F06 | P1 | Add ordered batch export from shelf and history | M | F05; Agent + receiver/file workflow acceptance | Implemented; desktop workflow acceptance NOT RUN |
| 7 | F07 | P1 | Combine selected captures into strip/grid | M | Agent + editor/shelf acceptance | Implemented; native gesture acceptance NOT RUN |
| 8 | F08 | P1 | Specify scrolling capture feasibility and supported first-release targets | M | Agent research; F07 informs composition | Complete; GO for bounded assisted static viewport scope |
| 9 | F09 | P1 | Implement bounded, assisted scrolling capture after feasibility decision | L | F08 go decision; F07 composition conventions; Shared | Implemented within ADR-001; physical target acceptance NOT RUN |
| 10 | F10 | P2 | Add WebP export after explicit codec admission | M | F05; dependency/security review | Implemented; pinned encoder admitted; clean-profile acceptance NOT RUN |
| 11 | F11 | P3 | Evaluate video/GIF as a separately approved product scope | XL | Owner scope decision; Agent research | Deferred; no recording implementation |

P1 means the next improvement cycle, not an emergency defect. P2 is useful follow-on work. P3 is outside the current still-image scope. Scrolling is the largest still-image gap; its two-stage sequence follows the simpler export and composition improvements because those deliver value sooner and establish bounded image processing. WebP follows scrolling because JPEG already addresses a broad file-size workflow without a new codec. Documented evidence and installation confidence take precedence over feature expansion.

F03 may wait for publisher credentials; that wait should not prevent F04 onward once separately authorized. F09 must not start unless F08 establishes an acceptable first-release scope. F11 remains deferred until the owner explicitly changes the recording exclusion.

## Execution and completion rules

1. Prompt an agent with the selected F ID, its brief and the common constraints. Implement one coherent item per scoped commit; do not automatically implement the whole backlog.
2. Recheck current source and gate status. This research is a dated snapshot. Keep implemented behavior, automated verification, observed desktop acceptance and owner-accepted scope separately recorded.
3. Use synthetic content and isolated data. Run focused regressions and the repository's required build/test/format/release checks appropriate to the change. Reopen affected Q gates through the existing review process; never mark them closed from an F checkbox alone.
4. Record PASS, FAIL, NOT RUN or NOT AVAILABLE with app/build, environment and evidence scope. Real receivers, physical monitors, login, SmartScreen and clean-profile installation require observation beyond unit tests.
5. Do not buy credentials, provision paid services, push, tag or publish from this backlog alone. Signing prerequisites can remain outstanding while local implementation and documentation become reviewable.

## Suggested later agent prompt

> Implement Fxx from TASK_QUEUE.md using its section in docs/IMPLEMENTATION-BRIEFS.md. Revalidate the source assumptions first. Keep the work limited to this item and its stated dependencies; preserve the common constraints. Complete agent-available verification and document any remaining desktop or owner acceptance separately. Prepare one scoped commit if requested. Do not publish a release or begin another backlog item.
