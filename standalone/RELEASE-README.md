# Standalone DLSS Glass Cockpit Refresh v1.0.2.10

Independent installer/settings app by **siglr / WeSimGlide**, using the **unmodified official standalone DLL** of **Glass Cockpit DLSS Fix by IFreemz**:
https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix

Bundled engine: v1.0.1. Install/Update selects the latest official stable release and verifies the ZIP and DLL against the SHA-256 values in the author's release notes. Full ReShade is not required. OFXR remains separate and its settings are untouched.

## Install or update

1. Extract the complete ZIP into a writable folder outside MSFS. Keep all files together.
2. Close MSFS. Run the EXE and confirm the game folder containing FlightSimulator2024.exe. Xbox normally uses Content; Steam uses the game's installation folder, not Community or the streamed cache.
3. Click Install, or Update for an existing installation. Start MSFS normally and use DLSS or FSR upscaling.

Existing users can open the app as usual to update automatically. Older apps may close and reopen more than once for compatibility updates. No uninstall is required. The app updates its external files first; close MSFS and click Update to change the installed DLL. Existing glass settings and original uninstall backups are preserved, including migration from the earlier custom engine.

Already using ReShade, OptiShade or another loader for this fix? Remove the duplicate fix using its own instructions first. Detected ReShade/add-on conflicts and unknown DXGI proxies block installation. Never run both the standalone DLL and the ReShade add-on.

## Settings and versions

Use Enable and the Glass settings, Advanced and Experimental tabs. Reset restores author defaults. Changes apply during upscaling without leaving VR. You can close the app afterward. The fixed-size window remembers its position and Dark/Light appearance.

Click the settings-window shortcut to record a binding. Save confirms; Escape, Cancel or leaving the recorder discards it. Existing bindings are preserved by default. Use the shortcut while MSFS has focus. The official settings window contains green tint. Defaults: Ctrl+Shift+F6 for settings, Ctrl+Shift+F11 for tint. Official diagnostics remain available. This app does not simulate keys or add external tint control.

The DLL selector offers up to ten verifiable stable releases plus the bundled version. Install selected DLL allows rollback with MSFS closed. Older versions can have crashes (v1.0.0 with FSLabs aircraft). New versions may change advanced INI keys; unverified settings controls are disabled in favor of the original window. Listing a version does not mean it passed these offline tests.

**Known official v1.0.1 limitation:** optional Dark screen fix ON reproduces incorrect glare on a newly recognized dark screen in an offline test. Author default remains OFF. If affected, disable that option and report the official-DLL issue to IFreemz. Proposed custom patches are not included. Real aircraft/headset testing remains necessary.

## Uninstall and files

Close MSFS and click Uninstall. Only dxgi.dll and glass-cockpit-dlss-fix.ini are installed, plus a receipt and original-file backups. Original MSFS binaries, Pimax files, OFXR settings and OpenXR registration are not edited. Migration removes owned legacy engine files. Unexpectedly changed DLLs are preserved; changed INIs are backed up beside this app. Original DLL diagnostics may remain after uninstall.

Keep Appearance.txt, WindowPlacement.json, SimulatorDirectory.txt and *.saved*.ini when moving or manually updating the external folder. Four retired .ps1 files remain for older updater compatibility; the app never executes them.

## Support and source

Installer, update, rollback or app-window questions: https://github.com/siglr/Standalone-DLSS-Glass-Cockpit-Refresh/issues or siglr through the WSG Discord at https://wesimglide.org/.

Screen issues or rendering crashes with the official DLL: https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix/issues. Include DLL version, aircraft, reproduction steps and ideally an optional Ctrl+Shift+F8 capture made by the official DLL.

The app is PolyForm Noncommercial (LICENSE.md). The official DLL is GPL-3.0-only with original NOTICE terms under licenses/IFreemz. Corresponding source is included in IFreemz-standalone-v1.0.1-source.zip, and the separate application-source download. See ENGINE-LICENSE.md and SECURITY.md.
