# Tester quick start

This experimental package integrates Glass Cockpit DLSS Fix v0.8.3 with the ReShade components it requires. It contains no frame-generation engine. No separate ReShade install is needed.

1. Extract the entire ZIP into a writable folder. Keep all its files/subfolders together.
2. Close MSFS. For v0.1.3 or earlier, extract the complete new ZIP over your mod folder once with the control window closed. From v0.1.4 onward, the app checks for updates at launch and reopens itself after updating. If it says Mod update available, close MSFS and click Update; no uninstall is needed. Remove a different glass-cockpit add-on before a fresh installation to avoid duplicates.
3. Run Standalone-DLSS-Glass-Cockpit-Refresh.exe, check the auto-detected MSFS 2024 game directory and click Install. Browse is available if detection fails. The installer can back up existing conflicting mod files if you explicitly choose replacement; Uninstall restores them.
4. Start MSFS normally. Use DLSS or FSR upscaling. First test parked in a default glass-cockpit aircraft, then your usual aircraft. Desktop and stereo VR paths are included.
5. Compare the Enable glass-cockpit refresh fix checkbox on/off while looking at digits and scrolling instruments. Changes apply during upscaler calls; enabling can take an extra frame. You may close the window after choosing a setting.
6. For frame generation, install and activate official OFXR separately with settings appropriate for your hardware. Compare cockpit-only first, then cockpit plus official OFXR. Use OFXR's controls for its FPS counter and FG settings. This standalone installer never changes those settings.
7. To remove it, close MSFS, reopen the same window and click Uninstall. Mod settings backups stay beside the external installer; diagnostic logs may remain beside MSFS.

Other headsets/runtimes are not blocked by a Pimax-specific check, but have not been validated. The offline GPU tests were run on an RTX4090. Working official OFXR and headset support must be established separately. The fix may leave unsupported screens to the native upscaler, so zero ghosting on every display/generated frame is not guaranteed.

## Useful feedback

- GPU, driver, headset, active VR runtime, MSFS build and aircraft.
- Upscaler selected, render scale, desktop or VR, and official OFXR version/settings if used.
- Result with the glass checkbox off/on, first without FG, then with FG.
- Whether both eyes agree; note colour changes, flicker, unreadable text or black/frozen output.
- Exact reproduction steps and the glass/backend logs from MSFS Content after closing MSFS. See README for filenames. Do not send DLLs or the entire game folder.

Keep the same scene and graphics settings between comparisons. Actual headset results are needed; an offline test pass is not proof of headset delivery or frame-generation quality.

Updates preserve your glass settings and leave official OFXR settings untouched. New author defaults are UV seam fix ON, dark screen fix OFF, smoothing 35 and Extra OFF. The v0.8.3 frame-overlap flicker fix applies to monitors; its VR equivalent is pending upstream.

App updates leave your installed glass settings and official OFXR settings alone. The app remains usable if the update server is offline. App-only updates need no MSFS restart; new simulator DLLs require MSFS to be closed while you click Update. See README.md for recovery and manual update instructions.

From v0.1.6, Update is greyed out when simulator DLLs already match. Changing glass settings or receiving an app-only update should leave it greyed out. When package DLLs differ, Update becomes available; after updating with MSFS closed, it returns to disabled. An interrupted update still offers recovery.

Steam detection (v0.1.7): test a default Steam library and a library on another drive. The selected folder must contain FlightSimulator2024.exe; it need not contain MicrosoftGame.Config or be named Content. With multiple installations, choose the intended folder via Browse. Existing Xbox detection must still work. Offline Steam fixtures pass; a real Steam installation remains a tester check.

In v0.1.8, click About at the top right. It should open the public mod page in your default browser without changing the mod settings. The page must first be uploaded by its publisher.

In v0.1.9, setup runs natively without PowerShell; elevation is offered only if permissions require it. Automatic updates require signed releases and inventories. The first transition from an older app uses that older app's HTTPS/checksum verification. Never disable antivirus for this test: leave detected files blocked while vendor review is ongoing. The website can compare your ZIP's SHA-256 locally; a match is not an antivirus clearance.

For manual acceptance, confirm normal launch/auto-detection, install/update/uninstall with MSFS closed, existing settings and original backups preserved, and a normal flight with official OFXR unchanged. Offline tests cannot establish antivirus clearance or replace an actual headset test. See SECURITY.md.
