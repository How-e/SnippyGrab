Final acceptance — 2026-10-08: **zero required gates open / all 51 checked** after receiver/performance scope acceptance, verified hosted publication and final reconciliation. [Final evidence](FINAL-FOUR-ACCEPTANCE-20261007.md). VM-specific observations and earlier counts below retain their historical scope.

# Fresh Windows VM acceptance and first-run setup

Current acceptance: **4 required gates OPEN / 47 checked**. Q01, Q12, Q34 and Q44 remain open. Stable packaging remains blocked.

The owner reports completing the entire install process and testing all features on the fresh Windows 11 26H2 VM, OS build 26300.9457, with no additional programs or extensions needed. The owner explicitly requests that all gates reliant on the VM be checked and cleared. This is owner-reported acceptance and a scope decision; no independent agent observation of the VM is claimed.

| Gate closed | Acceptance basis |
|---|---|
| Q07 | Owner accepts VM storage/settings/cache fallback and recovery requirements following the full runbook; existing injected persistence/rollback and native settings retry evidence remains complementary. |
| Q09 | Owner accepts first-run/hotkey VM workflows. Two setup improvements are implemented separately below; the acceptance report does not erase the need for clear Print Screen guidance. |
| Q13 | Owner accepts VM native transfer/cleanup/restart requirements; existing fake-clock/crash/revision protections remain complementary. No exact concurrent-drag/expiry or full 24-hour external read trace was supplied. |
| Q25 | Owner accepts VM tray/OS lifecycle scope. Exact guest sleep/physical reconnect traces were not separately supplied, so no independent hardware-event claim is made. |
| Q27 | Owner accepts fresh setup/install/login/upgrade/uninstall requirements after completing the VM runbook. Individual login traces were not separately supplied. |
| Q41 | Owner accepts the clean VM install/upgrade/remove scope and reports no extra prerequisites. This establishes reported Windows 11 success; Windows 10 22H2 remains untested and is accepted as a compatibility limitation rather than a remaining requirement. |

The candidate prescribed by the preceding runbook was `0.1.0-alpha.acceptance.20261007.6` (source `1e96e8c`); the owner did not separately restate the executed filename/version. Do not turn the prescribed identity into independently verified VM provenance. Existing successful receiver/hardware/accessibility/resource evidence remains unchanged.

## Requested first-run changes

- First-run setup prominently explains that Windows Snipping Tool can take over Print Screen and asks users to turn **Use the Print Screen key to open screen capture** OFF under Windows Settings → Accessibility → Keyboard, then restart SnippyGrab. It displays an enabled-state notice when the Windows preference is on and provides **Open Windows Keyboard settings**. The preference is never changed automatically; fallback/tray capture remains available.
- First-run setup exposes **Pictures / PNG export folder** with an editable path and native **Browse…** folder picker. New users start at Windows' Pictures known-folder location, including the user's normal relocated Pictures path. Existing configured export directories are preserved. Completing setup saves the choice; canceling the folder picker leaves it unchanged. Empty input uses Pictures; relative/file/cache destinations are rejected with visible feedback while setup stays open.
- This folder is the initial destination for explicit PNG export. Captures continue to use the managed temporary cache, and exports retain their filename/overwrite dialogs.
- Setup actions remain accessible below scrollable content when the window is resized. An isolated interaction fixture exposes **First-run setup** for native verification without loading the owner's settings, registering hotkeys or publishing the clipboard.

Q12 receiver/version/native-format coverage, Q34 full capture/OS clipboard timing distributions and Snipping Tool comparison, Q44 GIF/release validation, and Q01 final milestone reconciliation remain separate. No remote push, release or publication is implied by VM acceptance.

## Independent local verification of setup changes

Warnings-as-errors Release build: zero warnings/errors. Full suite: **170 core + 61 Windows tests PASS**, zero failures/skips. Formatting verification, release-policy fixtures and milestone-map admission/rejection checks PASS; the policy observes four remaining required gates.

Computer Use exercised only disposable interaction fixtures. The rendered first-run window exposes the prominent Print Screen guidance, Keyboard settings button and folder controls. Actual native folder-picker Escape preserved the existing field; Enter selected the Windows Pictures location. Native Start snipping rejected a relative destination with visible feedback while leaving setup open. Empty input persisted the Windows Pictures location and completed first run with startup disabled. Reopening setup preserved that preference; changing to a custom absolute directory and completing setup persisted the new choice in isolated settings. No OS preferences, startup registrations, normal user settings or OS clipboard were changed.

The host Print Screen preference was OFF, so enabled-state rendering and the Keyboard settings launch button were not separately clicked/tested in this lane. Source uses the existing Windows registry preference and shell Settings URI. Some stale geometry/index actions were rejected by the input helper and were not counted as passes. An initial rebuild was blocked by the running disposable executable; stopping only that owned fixture allowed the final clean build. Normal SnippyGrab instances were not terminated or replaced.

Local evidence is ignored under `artifacts/setup-acceptance-20261007/`. A new local self-contained candidate `0.1.0-alpha.acceptance.20261007.7` is prepared from committed source; ZIP/setup checksum and embedded-payload checks are run separately. No stable gate or remote publication is claimed.
