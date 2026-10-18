# Standalone validation

The dedicated D3D12 fixture does not call UvrSetSessionActive to activate the renderer. It exercises desktop and stereo correction, off/on via the real settings file without calling the enable API, rejected-fit recovery, FSR, proxy/nested/hotpatched upscalers and the new NVIDIA feature exclusion. Hardware: this gaming PC's RTX4090. Graphics debug errors and incorrect current images remain failures. Actual MSFS + official OFXR + Pimax integration is pending the user trial.

23/23 distinct D3D12 checks pass. The initial run passed 22/23 in 87.59 s; the bootstrap test still used an obsolete DLL filename. Its path now uses the actual target filename; the corrected probe passed in 0.34 s. Logs: build-standalone-tests.log and build-standalone-probe-tests.log. No runtime implementation change was needed for that test-path failure.

The standalone installer/window suite passes: roundtrip; both settings backups preserved during partial uninstall; other-product collision; explicit replacement/restoration; corrupted backup refusal; rollback; path rejection; independent OFXR/other settings unchanged; actual EXE discovery, live INI toggles and readable messages. Log: build-standalone-installer-tests.log. The preview was visually inspected; version 0.1.0 was in the title and executable metadata for that run.

DLL dependency inspection shows system/Visual C++ runtime imports only, with no OFXR or OpenXR dependency. CMake explicitly builds with UVR_BUILD_FRAMEGEN=OFF. The package contains three game payload files and no frame-generation helpers, fake GPU libraries, private settings backups or logs. No real game files or runtime settings were modified to test this standalone product.

Version 0.1.1 updates tester documentation and uses a generic conflicting-mod message in the window. The packaged installer/window suite passes again, including settings preservation and generic conflict handling. Rendering and installer behavior are unchanged. Official OFXR/headset interoperability still requires a real simulator test.

## v0.1.2 / Glass v0.8.2 validation

The final complete D3D12 suite passes **25/25** (107.79 s), including new dark-display/sun-glare recognition and live settings/clamping/reset-recovery checks. A first-accepted-frame glare flash was reproduced and fixed by preserving the original upscaled pixel until current glass residuals exist. The installer/window suite passes, including every glass option, group resets, temporary tint and unchanged unrelated settings. Screenshots of all tabs were visually checked. No log/report controls or hotkeys were added.

Five initial test processes exited without captured output; the operator reported possibly closing FlightSimulator2024 processes. Interruption is plausible but not confirmed. Direct runs, all five retries and the final complete suite pass with the same renderer. Fixture output now flushes immediately for future diagnosis. Test fixtures and their logs are not distributed. Real headset and official OFXR interoperability remain user acceptance checks.

## v0.1.3 / Glass v0.8.3 validation (2026-10-14)

Release build and complete offline suite: **28/28 pass** (120.56 s). Covers desktop/stereo upscaling, extra DLSS passes, discontinuous UV mesh in stereo, new live settings and limits, dark-screen opt-in, fit rejection/recovery, proxy recognition, toggles and native D3D12 adapter handling.

Installer/window tests and direct-update recovery tests pass: repeated upgrades preserve the entire settings file and original uninstall backups; modified files/corrupt backups are rejected; simulated failure, process termination and locked DLLs recover safely. Update and Uninstall can recover an interrupted replacement. Actual EXE controls/defaults/resets were exercised and previews visually checked. Official OFXR remains separate and its settings are untouched.

Defaults: line smoothing 35, Extra OFF, UV seam fix ON, dark screen fix OFF. No capture/report feature or shortcuts. The upstream frame-overlap fallback is desktop-only, not a VR flicker fix. Real headset/aircraft acceptance remains a user test.

## v0.1.4 / automatic app updates (2026-10-14)

The production updater passes **42 isolated offline fixtures**, including a real handoff from a parent process to a copy of the shipped EXE, waiting for parent exit, replacing the external package, launching the replacement EXE and removing staging files. Tests cover version ordering, unavailable servers, equal/older releases, busy-window deferral, checksum/size/metadata errors, malformed ZIPs, unsafe paths/case collisions/symlinks, unlisted files, executable/payload version mismatches, local modifications and unrelated-file collisions. No test downloads or executes server content.

Simulated replacement failures, locked files, and process termination before and after the inventory commit recover correctly. User changes made after an interruption are preserved and recovery stops. Settings backups, selected simulator folder, unrelated files and original OFXR files in fixtures remain unchanged. Junction staging paths are rejected without following or deleting them.

The standalone installer/window suite and both products' direct-installation-update recovery tests pass. The shipped window was exercised for live glass controls, matching-DLL versus changed-DLL update notices, and readable messages. The v0.1.4 preview was visually checked. Renderer DLLs are byte-identical to v0.1.3, so its 28/28 graphics result remains the relevant renderer validation; graphics tests were not repeated for this control/updater-only release.

Evidence: check-app-update.log, check-standalone014-installer.log and check-update-standalone014.log in the development build folder (not distributed). The external integrated package and actual installed glass/official OFXR settings were verified unchanged. No actual MSFS files were replaced by these tests. The publisher's HTTPS endpoint is not yet published; a live server-to-client release cycle remains to be checked after upload. Actual VR quality remains a user acceptance check.

## v0.1.5 / update test release (2026-10-14)

Only the app version and release documentation changed. The actual v0.1.4 package upgrades to the v0.1.5 ZIP with the unchanged production updater in an isolated fixture: executable and package version metadata, every managed file hash, settings preservation and staging cleanup pass. The standalone installer/window suite also passes. Both renderer DLLs and default INI are byte-identical to v0.1.4, so renderer checks were not repeated. The user's installed app remains v0.1.4 for a live server update test; actual MSFS/OFXR settings and external app folders were verified unchanged. Evidence: build/check-release015.log and build/check-standalone015-installer.log. Live download/relaunch acceptance is pending the user's upload and test.

## v0.1.6 / conditional Update button (2026-10-14)

The actual-window/installer suite passes. New control checks cover matching DLLs (Update disabled despite an older receipt version), different verified package DLLs (enabled), completed replacement (disabled again), changed installed DLLs, invalid metadata, missing files, settings-only changes, fresh Install and interrupted-update recovery. The preview was visually checked. File hashes are cached by size and modification time so the live window polls metadata rather than re-reading DLL contents each second; the installer still verifies hashes before replacement.

Renderer DLLs and default INI remain identical to v0.1.5; no graphics or OFXR code changed. Existing installed settings were verified unchanged. The standalone app version is 0.1.6, with a new matching ZIP/version-file pair for the launch updater. Development fixtures and preview files are removed after checking; no new runtime reports or shortcuts.

## v0.1.7 / Steam discovery (2026-10-14)

18 isolated discovery checks pass using Windows PowerShell 5.1: Steam main/custom libraries, escaped paths and apostrophes, modern and legacy library files, duplicate libraries, malformed/stale manifests, app ID mismatch, traversal rejection, multiple installations, protected WindowsApps exclusion, manual game selection, and Xbox regression cases. Read-only discovery on this PC still selects the confirmed Xbox Content folder. This PC has no installed Steam copy of MSFS 2024; real Steam detection remains a tester acceptance check.

The standalone installer/window suite passes, including actual EXE detection and control events without MicrosoftGame.Config. All 42 app-updater fixtures pass, including helper handoff, restart and rollback. No renderer, default settings or OFXR code changed; no actual simulator files or user settings were written. The new ZIP and version file are prepared for upload, not published. The separate HTML download page includes Steam folder guidance and directs existing ReShade users to the original add-on.

## v0.1.8 / About link and public FAQ (2026-10-14)

Release control-window build and standalone installer/window suite pass. The rendered window was visually checked with the new About link at the top right. It uses Windows shell URL opening for the fixed HTTPS mod page, with a readable error if opening fails; default-browser dispatch remains a manual acceptance check. No renderer, settings, installer or app-updater behavior changed. The three public FAQs distinguish standalone-only crashes from upstream reproduction, quality modes from DLSS model presets, and MSFS 2024 scope from MSFS 2020. The local page was rendered and its crash accordion checked. No capture/report feature or shortcuts added.

## v0.1.9 / native setup and authenticated releases (2026-10-14)

Release control-window build passes. Native installer suite passes **44 operations** and **18 isolated Steam/Xbox discovery checks**, including actual-window controls, signed payload rejection, locked DLL rollback, junction rejection, changed-file preservation, original backup restoration, repeated updates, whole-INI preservation, simulated failure and process interruption, and migration from the legacy PowerShell update journal. The rendered v0.1.9 window was inspected. Read-only native discovery on this PC selects the confirmed Xbox Content directory.

The production updater sources pass **50 isolated fixtures**: normal replacement, local modifications, filename/path/ZIP bounds, locked files, rollback, crash before/after commit, unavailable/equal/older releases, busy-window deferral, missing/forged signatures, substituted helper, and a forged recovery journal attempting to delete unrelated user data. The production-source EXE helper is compiled with a disposable test key and performs the actual parent-exit wait, replacement, relaunch and cleanup. No fake fixture is signed with the publisher key.

The exact published v0.1.8 ZIP (SHA-256 e1c9c0147f2c1acaaae856d5bc1756a92f624a0a469f4b0e2cd5b100832a862d) upgrades to the signed v0.1.9 package using the prior updater source in a maintainer-only isolated fixture. The new verifier accepts every resulting inventory hash and the signed feed. Settings/selected-folder/unrelated OFXR fixture data and staging cleanup pass. The old-updater transition necessarily uses its original HTTPS/checksum trust; signature enforcement begins in v0.1.9.

Both graphics DLLs and default INI are byte-identical to v0.1.8. No renderer tests were repeated because renderer code/binaries did not change; the existing 28/28 graphics result remains applicable to those binaries. No actual MSFS, headset or OFXR installation/settings were modified. UAC interaction, live server rollout and in-simulator acceptance remain manual checks. No antivirus clearance, independent penetration test or guarantee of safety is claimed.

The public page shows the maintainer's antivirus notice, a release checksum, exact-hash VirusTotal lookup links and local browser file verification with a PowerShell alternative. Browser checks confirm a matching current ZIP is accepted and the older ZIP is rejected. Files selected for hashing are not uploaded. There is no new capture/report feature or shortcut.

## Public source export (2026-10-15)

Fresh standalone-only source builds successfully using VS 2022/MSVC and Windows SDK 10.0.26100.0, without access to a parent repository or OFXR sources. All **28/28 D3D12 graphics checks pass** (124.84 seconds), including stereo/desktop upscalers, experimental options, toggles and DXGI bootstrap/private-adapter paths.

The exported native installer passes **44 operations and 18 discovery checks**. The updater passes **50 isolated fixtures**, including production-source helper handoff. A separately signed development ZIP passes a real payload install/uninstall round trip in a dummy game directory while preserving an unrelated OFXR settings marker. The source-build update entry points are verified disabled. These checks used a separate local development key; no maintainer private key or real simulator installation was used.

The public export has fresh history, includes the selected upstream sources/licenses and build instructions, and excludes binaries, release private keys and OFXR implementation. Original code is under PolyForm Noncommercial 1.0.0; upstream licenses remain intact. This is a source-build validation, not a claim of byte-identical reproduction of the official v0.1.9 download or antivirus clearance. Official distributed binaries remain unchanged.

## v0.1.10 / compact WeSimGlide styling (2026-10-15)

The native control window offers the WSG Client dark teal/mint and light palettes, embedded WSG logo/app icon, dark Windows title bar where supported, and three compact settings-page buttons. The client size remains 660 x 610. Original defaults, settings writes, transient tint, reset controls, discovery and update/install logic are retained. System high-contrast colours are respected. No browser runtime, remote theme assets or shortcuts were added.

Release and source-build control windows compile. The native installer/window suite passes **44 operations plus 18 discovery checks**, including all settings, reset buttons and installed/update/recovery states. The Dark/Light buttons, saved appearance on a new window, and unchanged glass INI were checked. The updater fixtures preserve Appearance.txt along with existing settings. All three settings pages were visually inspected; the live window and dark title bar were also checked. The updater suite passes **50 fixtures**, including the production-source helper with a disposable key and embedded branding.

The build from this repository passes **28/28 graphics tests** in 123.84 seconds. Renderer source and defaults were not changed by this UI update; compiled artifacts can differ from the historical v0.1.9 download. The existing official v0.1.9 ZIP successfully upgrades to the signed v0.1.10 package in an isolated fixture, preserving saved settings and clearing staging. Signatures and inventories verify with the established production public key.

The signed package is prepared locally; no server upload or new GitHub release is implied. Real MSFS, Pimax and OFXR files/settings are not changed by these checks. High-DPI/multiple-monitor and high-contrast appearance outside the tested desktop remain manual acceptance checks.


## v1.0.0 / Glass v1.0.0 (2026-10-17)

Release build succeeds on this RTX 4090 PC. The expanded graphics suite passes **31/31** (184.68 seconds), including live application of all 17 values, missing-key author defaults, repeated desktop FG upscales, exhausted VR view counts, and motion regressions. The motion check covers 13 scenarios: temporal/Gaussian smoothing, occlusion and its disabled negative control, dim shared textures, power-on, texture replacement, blank displays and layered windows. D3D12 debug errors remain failures. Occluder error falls from 0.1655 with the check off to 0.0004 with it on; these are synthetic normalized image errors, not headset performance measurements.

A new exhausted-view test initially rejected the second eye under the inherited 135 ms expiry. The VR fallback now uses upstream's eight-present window and both eyes pass. A native-present test initially released swapchain GPU resources without waiting; an explicit completion fence fixed the fixture teardown. The native Present/Present1 probes verify exactly one frame count, exclusion of test presents and (in a final targeted 3/3 pass) maintenance polling without any upscaler calls, which is needed for late-loaded/displaced-hook recovery. The full suite preceded that final maintenance-only addition; the affected three probes were rebuilt and rerun afterward.

Native installer/UI suite: **44 operations and 18 discovery checks pass**, including preservation of independent OFXR settings and the entire installed INI, original proxy restoration, rollback and recovery. Settings/reset controls and exclusive temporal/soft smoothing were exercised; the experimental tab preview was inspected. Updater suite: **50 isolated fixtures pass**. Development-package verification confirms signed installation/removal and disabled official updates for source builds.

No real simulator or headset files were changed by these checks. Actual MSFS aircraft, Pimax VR and independent official OFXR interoperability remain user acceptance tests. No capture/report UI, capture shader/export or keyboard shortcuts are included. See ENGINE-LICENSE.md for the GPL engine/noncommercial app split and matching-source distribution.


The actual production-signed v0.1.10 ZIP upgrades to the v1.0.0 candidate through the production updater in an isolated folder. Its inventory and new GPL notices verify; a subsequent fake-game DLL update preserves the old INI and independent OFXR fixture settings, and Uninstall restores the original third-party proxy. Source and release signatures/checksums are verified during packaging. This is an offline archive-to-archive test, not a live server update or an MSFS flight.

The final v1.0.0 rebuild passes the complete graphics suite again: **31/31 in 189.70 seconds**, including the present-maintenance change. The signed v0.1.10-to-v1.0.0 package upgrade also passes. Release preparation caught stale generated executable version metadata; VERSION is now a CMake configure dependency so future version changes regenerate it automatically.

## v1.0.1 / copied swapchain table startup regression (2026-10-18)

Reports describe a black screen or crash before the first MSFS splash screen after installing standalone v1.0.0; one reporter says the original IFreemz v1.0.0 with full ReShade works on that same system. Their exact crash stacks are not available, so the reproduced defect below is not yet confirmed as the cause of every report.

An isolated startup fixture reproduced an access violation (0xc0000005) in the **actual published v1.0.0 graphics DLL**, SHA-256 `5d6441db0adb2e75a0dad8d478f6120a9c99b9efe7d04cf3ba256f8b33f8cba1`. Creating a D3D12 swapchain works, but copying its method table before Present(TEST) or Present1(TEST) makes the v1.0.0 hook lose the original function and call nullptr. Windows' native DXGI and the published v0.1.10 graphics DLL pass both copied-table cases and both unchanged-table controls. No cockpit shader or DLSS/FSR call is involved. The existing engine error immediately preceding the crash is `Unable to resolve hook`.

The v1.0.1 fix retains a separate function trampoline for each observed presentation implementation. It no longer rewrites swapchain table entries or resolves the original function by the object's current table address. Failed/unsupported extra attachments leave the original function untouched. New permanent regression checks exercise copied tables through the direct backend and the normal dxgi bootstrap, including real/test presents, Present1, frame counts, maintenance and GPU completion. The initial targeted graphics probes pass **8/8**.

The comparison project is jtsorlinis/msfs2024-glass-cockpit-dlss-fix-no-reshade, discussed in IFreemz PR #21. Its published v0.8.3 DLL was downloaded for static export/import inspection (SHA-256 `ff8e0a33d9a0ef6b640f55cdaa4c21ae7052bd83d05a167c872ef047325b9486`), not installed or executed. Its newer standalone-build source at `aeed7ca95ee292ebc69d29710f4d32d44b015357` is based on v1.0.0 and uses function-based presentation hooks. This release retains this project's ReShade event-based rendering integration rather than replacing it with that fork. The fork exports additional DXGI compatibility entry points; our narrower bootstrap surface predates v1.0.0 and is a separate follow-up consideration, not an established explanation for this regression. Import comparison found no newly required Visual C++ runtime symbol in our v1.0.0 DLL versus v0.1.10.

Investigation fixtures remain under ignored `build/startup-investigation`; no new runtime capture/report feature is added. Real MSFS, Pimax and OFXR installations/settings are not modified by these tests. An affected user's simulator retest remains necessary.

Final Release build: **33/33 graphics checks pass in 187.20 seconds**, including both new copied-table regressions and all 13 motion-quality scenarios. Native installer/window checks pass **44 operations and 18 discovery cases**; the updater passes **50 isolated fixtures**. The actual production-signed v1.0.0 ZIP upgrades to the signed v1.0.1 candidate in an isolated folder. Its signed inventory/feed verify; updating the dummy simulator preserves the complete INI, saved settings, appearance and independent OFXR data; uninstall restores the original unrelated proxy. Evidence: `build/check-101-graphics.txt`, `build/check-101-installer.txt`, `build/check-101-updater.txt` and `build/check-101-release.json`. The shared v1.0.0 source, glass shaders and author defaults are unchanged apart from the standalone engine version string.


## v1.0.2 / Glass v1.0.1 hotfixes and version transition (2026-10-18)

Imported the exact upstream changes from `v1.0.0` to `v1.0.1` (`fabb8cef17595e8c521595023cfe85ecef33e8c0`) in the engine C++ and overlay shader. The author's defaults and the standalone presentation trampoline fix are unchanged. The version string retains this modified/unofficial build's own identity. No OFXR, simulator or headset installation/settings were changed.

The full Release graphics suite passes **34/34 in 188.94 seconds**, including native/bootstrap startup, both copied-swapchain regressions, desktop/stereo DLSS/FSR, live author defaults, and all 13 motion/occlusion/history cases. The subsequently added protected-module scanner probe also passes, bringing the registered suite to **35 checks**. It compiles the actual production scanner with simulated module APIs and fails any attempt to read the protected late-loaded aircraft file. Replacing its extracted scanner with the previous version fails as expected. This proves the file-access regression is caught; it does not emulate FSLabs protection or constitute an actual aircraft-loading test.

The new shader probe compiles extracted production calculations and runs six synthetic gain/seam decisions on D3D12. Low-variance green/blue gains stay at 4 rather than 13000/9000, the continuous stretched texture is not classified as a seam, and both real discontinuity cases remain seams. The previous shader fails the colour and stretch cases. Existing full-pipeline rendering tests pass with the new shader. D3D12 errors remain failures.

The exact signed release ZIP's two DLLs also pass **four isolated startup checks**: direct backend and normal dxgi bootstrap, each with unchanged and copied swapchain tables. The probes check real/test Present and Present1 paths without launching MSFS. These tests directly address the prior standalone startup regression; compatibility with every aircraft, overlay and simulator installation still needs real-world confirmation.

Native installer/UI: **44 operations plus 18 discovery checks pass**. An initial overlapping run correctly refused installation because a graphics fixture uses the simulator process name; it passed after the graphics suite finished, with no product workaround. Updater: **50 isolated fixtures pass**, now using a three-part-to-four-part upgrade through signed staging, rollback and actual helper handoff. Invalid/malformed/oversized components are rejected; three-part versions normalize to revision zero and revisions compare numerically. Development ZIP signing/install/uninstall and disabled official source-build updates also pass.

The actual published v1.0.1 ZIP upgrades to signed v1.0.2 using the **historical v1.0.1 updater source**, so the transition does not rely on the new parser. The full INI, saved settings, appearance, independent OFXR marker and original proxy/uninstall backups survive. The final release uses the established production signing identity. Four-part channel rollout is a separate future publishing step; older clients that skipped this transition must still be handled as documented in PUBLISHING.md.

Evidence (ignored, not distributed): `build/check-102-graphics.txt`, `build/check-102-updater.txt`, `build/check-102-installer.txt`, `build/check-102-development.txt`, `build/check-102-release.txt` and `build/check-102-packaged-startup.txt`. No capture/report feature or keyboard shortcuts were added.
