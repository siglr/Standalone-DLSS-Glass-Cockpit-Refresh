> Source builds are labeled **source build** and have official automatic app updates disabled. Use `python -B scripts/dev_package.py` to build an installable local package with your own key. The automatic-update instructions below describe official WeSimGlide binary releases.

# Standalone-DLSS-Glass-Cockpit-Refresh v1.0.1

Based on Glass Cockpit DLSS Fix by IFreemz v1.0.0 (https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix), with the required ReShade D3D12 components built in. MSFS 2024 desktop and VR. No separate ReShade install, menu or hotkeys. Frame generation is not included; official OFXR Bridge can be installed and run separately.

## v1.0.1 compatibility fix

Fixes a reproduced startup crash introduced in the standalone v1.0.0 presentation hook when another graphics component copies a swapchain's method table. The fix keeps the original presentation function associated with its implementation, so a copied table does not lose it. This addresses the reproduced regression; confirmation on affected MSFS systems is still needed. No glass defaults, saved settings or OFXR settings change. Update the app, close MSFS, then click **Update**; no uninstall is needed.

## v1.0.0 graphics update

Modified/unofficial Standalone Glass Graphics engine, based on IFreemz v1.0.0. Includes repeated-upscaler and VR view recovery fixes, per-mesh brightness, stable screen edges, depth-based occlusion and updated glass filtering. Experimental settings are scrollable. New defaults: stable edges, full-size sampling and occlusion check ON; temporal anti-aliasing, soft-edged smoothing and calm glass OFF; edge trim 0. Line smoothing remains 35. Temporal anti-aliasing and soft smoothing are alternatives. Temporal anti-aliasing allocates additional VRAM at output resolution; it is optional and off by default.

Existing settings are preserved. Missing new settings use the author's defaults. Reset experimental restores those defaults. Close MSFS, let the app update, then click Update once to replace the simulator DLLs; uninstalling first is unnecessary. OFXR remains separate and unchanged.

The graphics engine is GPL-3.0-only with the upstream NOTICE terms. The separate app remains PolyForm Noncommercial. See ENGINE-LICENSE.md and NOTICE.md. Matching complete source is distributed alongside this release.

## v0.1.10 appearance

The compact control window now uses WeSimGlide's styling and embedded WSG logo and app icon. Choose Dark or Light at the top right; the app remembers the choice in Appearance.txt beside the EXE. All glass controls, author defaults, live changes and reset buttons are retained. No browser runtime or extra theme files are needed. Windows high-contrast mode uses system colours.

## Install and test

1. Close MSFS and its mod control windows.
2. For an older version, follow Updating below; no uninstall is needed. For a different glass-cockpit add-on, remove it first to avoid duplicate fixes.
3. Run Standalone-DLSS-Glass-Cockpit-Refresh.exe from this complete package. Check the detected MSFS 2024 game folder, then click Install. Use Browse if auto-detection cannot find your installation. Administrator rights are requested only if folder permissions require them. Existing mod-file replacement asks before backing up; Uninstall restores those backups.
4. For frame generation, install/start official OFXR Bridge separately using its own instructions. Keep your existing OFXR settings and use settings supported by your hardware. This package never reads or changes OFXR settings. AutoFPS detects official OFXR directly; this package includes no compatibility helper.
5. Start MSFS with your normal shortcut and existing working VR runtime. Enable DLSS or FSR upscaling in MSFS; the glass fix needs upscaler calls.
6. First compare the glass fix enabled/disabled while parked, checking both eyes, instrument digits, scrolling tapes and head movement. The checkbox applies during upscaling; resume may take one additional rendered frame. The window can be closed after setting it. Compare with official OFXR off and on using OFXR's own controls. Default aircraft first, then gliders.

The new package has no OpenXR layer registration, FG controls, FPS overlay or 2X/3X settings. Use official OFXR for those. Enable/disable changes correction; it does not unload hooks. Close MSFS before installing or uninstalling.

## Finding MSFS 2024 (Xbox and Steam)

Starting with v0.1.7, automatic detection checks Steam and its additional library folders, including other drives. It uses the MSFS 2024 Steam app manifest and requires FlightSimulator2024.exe. Xbox/Microsoft Store detection is retained. If several installations are found, choose the one you use with Browse; your choice is remembered.

For Xbox, this is normally XboxGames/Microsoft Flight Simulator 2024/Content. For Steam, use Steam > Library > Microsoft Flight Simulator 2024 > Manage > Browse local files, then select that game folder in this app. Steam does not need a folder named Content or the Xbox MicrosoftGame.Config file. Do not select Community, Official, or the streamed packages/cache folder.

This is an app-only detection update: existing simulator DLLs and your settings do not change. No uninstall/reinstall is needed.

## Sharing with testers

Send the complete **Standalone-DLSS-Glass-Cockpit-Refresh.zip** together with its matching **1.0.1-source.zip**, or provide access to that exact corresponding source alongside the binary download. Extract it into a writable folder and keep the contents together. Do not send just the EXE, copy DLLs manually, or include your own settings backups/logs. Official OFXR is a separate optional download for frame generation. See TESTING.md for the comparison and useful feedback.

## Files and recovery

Only dxgi.dll, StandaloneGlassGraphics.dll and StandaloneGlass.ini are installed beside MSFS. A .StandaloneGlass-install.json receipt and optional .StandaloneGlass-backup folder make removal/replacement recoverable. Settings changes are saved beside this external installer as StandaloneGlass.saved.ini; differing older copies are preserved using a content-hash suffix. Logs may remain in Content for diagnosis: StandaloneGlassGraphics.log and glass-cockpit-dlss-fix.log (plus its previous-run .old.log). No original MSFS binaries, Pimax files, OFXR settings or OpenXR registrations are changed.

Keep the whole extracted package together, including the signed inventory and licenses. The four legacy .ps1 filenames contain retirement notices only; the app never runs them.

## Hardware and VR runtimes

Requires Windows x64, MSFS 2024 D3D12, and the Microsoft Visual C++ x64 runtime used by the compiled DLLs. The small control EXE uses Windows' .NET Framework.

No Pimax model/runtime whitelist, resolution, refresh rate or OFXR preset is hard-coded in this standalone product. Its renderer targets MSFS 2024 D3D12 and DLSS/FSR upscaler calls, including stereo views. Other headsets/runtimes are candidates, not validated support claims. Offline tests run on an RTX4090; forced descriptor tracking exercises the alternate code path but does not validate an AMD/Intel GPU. SteamVR and other headsets have not been tested. Official OFXR has its own separate requirements.

## Scope and limitations

The source includes v0.8.1's exclusion of NVIDIA driver feature files from proxy hooking. It includes private-adapter passthrough and safe settings-backup recovery. Unsupported display textures are left to stock upscaling rather than recoloured. CPU cost reporting counts upscaler ticks, not headset FPS; GPU diagnostic readbacks remain disabled pending explicit completion tracking.

This is an experimental build. Offline graphics/installer validation does not establish actual headset delivery or guarantee ghost-free generated frames on every aircraft. A user-led test with official OFXR remains necessary. See VALIDATION.md.

## Upstream and licenses

Glass Cockpit DLSS Fix by IFreemz: https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix, v1.0.0, 9040e14ab26f6d8f48f53c05c7f4fb0ec7e805fa. GPL-3.0-only with the section 7 terms in licenses/glass/NOTICE.md. The full GPL is licenses/glass/LICENSE. This package contains the modified/unofficial Standalone Glass Graphics build.
ReShade: crosire, BSD-3-Clause (selected files BSD/MIT), D3D12 subset based on 6a7eaf8dbf5ed7050de1afd381ff058dfb51935e.
Original source pins, licenses and local adaptations are retained. This package does not contain OFXR code or binaries. Source: https://github.com/siglr/Standalone-DLSS-Glass-Cockpit-Refresh. See the root README and NOTICE.md for source licensing; upstream licenses remain separate.

Build: python scripts/build.py -S . -B build/standalone -G "Visual Studio 17 2022" -A x64 -DUVR_GLASS_STANDALONE=ON, then build Release there. Package with python standalone/package.py. See the repository README for the Windows toolchain requirements.

## Glass controls (v1.0.0)

Glass settings, Advanced and Experimental apply during upscaling without leaving VR. Values show requests, not confirmed runtime status. Hover over controls for explanations. No capture/report controls or keyboard shortcuts are included.

| Setting | Range | Author default |
|---|---|---|
| Line smoothing | 0-50, or 0-100 with Extra | 35 |
| Temporal anti-aliasing | Off/on; extra output-resolution VRAM | Off |
| Soft-edged line smoothing | Off/on; alternative to temporal anti-aliasing | Off |
| Stable screen edges | Off/on | On |
| Full-size texture sampling | Off/on | On |
| Calm glass at symbols | Off/on | Off |
| Occlusion check | Off/on | On |
| Edge trim | 0-400; 100 = one render pixel | 0 |
| Extra smoothing | Off/on; above 50 softens the whole display | Off |
| Green coverage tint | Off/on while this window is open | Off |
| Flip jitter direction | Off/on | Off |
| Glass layer strength | 0-100% | 100% |
| Dark screen fix | Off/on | Off |
| UV seam fix | Off/on | On |
| Detail clamp | 0-100% | 0 |
| Edge fade | 0-250, where 100 = one render pixel | 0 |
| Angle softening | 0-100% | 0 |
| Edge stretcher | 0-400, where 100 = one render pixel | 0 |

Each tab has its own reset button. Reset experimental restores UV seam fix, stable edges, full-size sampling and occlusion check ON; the other experimental options return to OFF/0. Dark screen fix can help mostly black displays in strong sun, but can interfere with other displays; enable it only for an affected aircraft. Switching Extra off clamps smoothing to 50; Reset glass restores 35 and Extra off. Green tint ends when the window closes. Other choices remain active without the window.

Updates preserve existing choices. New options missing from an older INI use the defaults above. Use the reset buttons if you want all glass settings restored to the author's defaults.

## Updating the app and installed mod

Starting with **v0.1.4**, the standalone app checks for a newer release each time you open it. It downloads the ZIP in the background, checks its SHA-256 checksum and contents, then briefly closes and reopens itself using a temporary updater. Keep the complete package in a writable folder outside MSFS Content. No separate updater installation or administrator prompt is needed for this app update.

If you have **v0.1.3 or earlier**, close the old control window and extract the entire v0.1.4-or-newer ZIP over your external mod folder once. Run the new EXE. This enables automatic updates for subsequent releases; no uninstall is required. Preserve your saved settings and SimulatorDirectory.txt when replacing package files.

Updating the external app does not replace files in a running simulator. When a release includes different simulator DLLs, the window says **Mod update available**. Close MSFS, then click **Update** and approve an administrator prompt if folder permissions require it. Start MSFS normally afterwards. An app-only release does not require this step when the installed DLLs already match. Install remains available for a fresh installation, and Uninstall removes the installed mod.

The MSFS Update action keeps the complete installed settings file and the original backups used by Uninstall. Your choices, settings backups, and selected simulator folder survive app updates. Official OFXR remains separate and its settings are untouched. New glass options use the author's defaults; existing choices are preserved.

If the update server is unavailable, you can keep using the current app. An invalid download is rejected. If installation is busy when a download finishes, reopen the app later to finish the app update. Interrupted app replacement is recovered on the next launch; if recovery needs attention, extract a fresh complete ZIP over the external mod folder with the control window closed. Changed package files or collisions with unrelated files stop automatic replacement rather than silently overwriting them.

Interrupted updates of the files inside MSFS are separate: keep MSFS closed and click Update or Uninstall to recover. Unexpectedly modified installed DLLs or missing files are preserved and reported. No uninstall/reinstall cycle is needed for a normal upgrade.

The update service uses these fixed HTTPS addresses (available once the publisher uploads the release):

- [Version file](https://wesimglide.org/otherdata/tools/Standalone-DLSS-Glass-Cockpit-Refresh.version.json)
- [Complete ZIP](https://wesimglide.org/otherdata/tools/Standalone-DLSS-Glass-Cockpit-Refresh.zip)

No capture/report feature, telemetry, or keyboard shortcuts are added by the updater.

## v0.1.5 update test release

v0.1.5 has the same glass fix, settings defaults and updater behavior as v0.1.4. This release increments the app version so existing v0.1.4 installations can test automatic updating. Upload the matching ZIP and version file, then close and reopen v0.1.4. The app should update and reopen showing v0.1.5. The simulator DLLs are unchanged, so this test needs no uninstall or MSFS Update action.

## v0.1.6: Update only when needed

The Update button is greyed out when the installed simulator DLLs match this package, with an "Installed - files are up to date" message. A newer app version alone does not enable it. Your glass settings are excluded from this comparison. Install remains available for a fresh installation, and Uninstall remains available for an existing installation.

Different, verified package DLLs enable Update. Close MSFS before clicking it. An interrupted update also enables recovery through Update. Missing or unexpectedly modified installed files, or unreadable metadata, are shown separately; the app does not offer an ordinary update that would overwrite unrecognized changes. The installer still checks files before replacing them.

## v0.1.8: About and troubleshooting

Click About at the top right of the app to open the mod website in your default browser. It includes installation instructions and FAQs for crash comparisons with the original ReShade add-on, DLSS modes/presets and MSFS 2024-only support. This is an app-only update; simulator DLLs and settings are unchanged, so no uninstall/reinstall is needed.

## v0.1.9: installation and update protection

The launcher now discovers the simulator and installs/uninstalls directly in C#, without starting PowerShell, encoded commands or execution-policy overrides. It tries with normal user permissions before offering administrator elevation for the selected setup operation. App self-updates run without elevation.

New releases and their file inventories use RSA-3072/SHA-256 signatures verified with a public key embedded in the launcher. This is **not Windows Authenticode signing**: the EXE still has no trusted publisher certificate, and antivirus/SmartScreen warnings may remain. See SECURITY.md for the exact file changes, network requests, protection limits and antivirus status.

Existing v0.1.4–v0.1.8 apps can update once using their original HTTPS/checksum updater; the added signature fields are compatible with it. Signature enforcement starts after v0.1.9 is installed. Alternatively, close the app and extract the complete official ZIP over its external folder, preserving settings backups and SimulatorDirectory.txt. No simulator uninstall is required. For v0.1.9, renderer DLLs and defaults were unchanged, so that release normally left the MSFS Update button disabled. Later source builds can differ; the button checks the actual installed DLL hashes.

Installation replacement is transactional and preserves original backups. Modified DLLs, invalid signatures, linked paths or damaged recovery data stop the operation. Settings changes are preserved during updates and saved during uninstall. If recovery cannot safely finish, keep its files and contact the maintainer rather than deleting backups.

The download page shows the release ZIP's SHA-256, version-specific file hashes and exact-hash VirusTotal lookups. Its optional file selector hashes the downloaded ZIP locally without uploading it. Matching a hash identifies a release; it does not prove safety. While antivirus review is ongoing, leave a detected file blocked and keep protection enabled.
