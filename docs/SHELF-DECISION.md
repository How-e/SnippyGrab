# Shelf model decision — Q47

2026-10-06: retain the compact primary preview plus two offset stack edges and bounded hover strip. Defer an overlapping-real-preview redesign until current reachability/reorder/selection acceptance passes. This is a documented P2 deferral, not completed comparative user testing.

Workflow evidence is the recorded U03 first-entry/top-capture/selected-collapse/reorder failures and later agent three-capture padding/top-card route pass in [USER-TESTING](USER-TESTING.md). U01/U02 already report successful capture/paste and correct single-image drag. A new visual model would need renewed focus/transfer acceptance. No evidence establishes that overlapping partial previews solve the reported interaction failures better than the implemented anchor/hover fixes.

| Model | 1 capture | 5 captures | 20 captures | Tradeoff |
|---|---|---|---|---|
| Current primary/edges/strip | One screenshot and focus badge | Same collapsed footprint as 20; up to two slim edges; configured 1–5 hover images | Same bounded expansion; wheel/keyboard traversal, numbered transfer order, paged history | Full readable primary; older pixels require deliberate traversal. |
| Overlapping real previews | Same primary | Partial older previews | Needs a hard visible-layer cap and traversal | Partial recognition may help; occlusion reduces text readability and introduces overlapping hit targets. Requires gesture/order validation. |
| Compact thumbnail grid | Same primary | More direct targets | Must page or occupy more area | Helps scanning; a large gallery conflicts with the compact shelf requirement. |

Production footprint bounds: 120–400 DIP thumbnail width, preview height clamped to 72–65% of width, default 224 DIP and three expanded items. The expansion budget is 48% of work-area height vertically or 55% of work-area width horizontally, admitting at least the primary. Collapsed edges cap at two regardless of history size. Warm hover reuses cards; caches cap at five cards/twelve thumbnails. Narrow widths use Edit/Copy plus the all-actions menu. These are code bounds, not a readability claim for every scale.

The 64 offscreen native layouts cover 1/3/5/20 screenshots, eight positions and two orientation settings. Anchor preservation, displayed-card reachability, raised enter/leave with selection, twenty-capture traversal/revisions and keyboard routing pass with bounded caches. Synthetic layouts do not establish real hover/jitter or comparative usability.

Required current-shelf acceptance remains Q16/Q19/Q32/Q49: composer focus, cold/warm entry, top-card route, selected leave/re-entry, Alt-reorder both directions and keyboard traversal. Reconsider an optional redesign after these pass, using the same 1/5/20 workflows and comparative footprint/readability/timing evidence. No gallery redesign is shipped by this decision.
