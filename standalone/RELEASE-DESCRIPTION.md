# Glass Cockpit Refresh for MSFS 2024

A small installer and settings app by **siglr / WeSimGlide** for **Glass Cockpit DLSS Fix by IFreemz**. Help reduce instrument ghosting with DLSS or FSR upscaling, on a monitor or in VR. The official graphics files are used unchanged.

Choose **Standalone DLL** for a setup without full ReShade, or **ReShade add-on** for an existing ReShade 6.7+ installation with full add-on support. ReShade itself is not included or installed by this app. Only one copy of the glass fix should run at a time.

## Simple setup and settings

- **Setup** contains the game folder, installation mode, installed version, version selection and compatibility information. The selected version is clearly separate from the installed version.
- Newer official releases automatically open Setup once per session, with a prominent **Update to v…** button. Manual rollback choices and active dialogs are respected.
- **Latest official release** is the default. Downloads must match IFreemz's release-note SHA-256 checksums and GitHub asset digests. Choose the bundled version for offline installation, or select an older verified release for rollback.
- **Settings** contains the enable control, standalone settings-window shortcut recorder and line smoothing. Advanced and Experimental options expand when needed, with separate resets.
- Dark and light themes, a fixed window that remembers its position, centered dialogs and scrolling for smaller screens.
- Switching modes and updating preserve existing glass settings, shortcuts and original uninstall backups. OFXR, Pimax and simulator settings remain unchanged.

For standalone mode, adjust settings here or in IFreemz's own in-game window; the green-tint button and diagnostics are available there. The shortcut can be recorded in this app and used while MSFS has focus. Official standalone v1.0.2 reloads the INI before saving, preserving changes from this app. The recorder supports modified arrows, Tab, Enter, Backspace, numpad operators and punctuation, while rejecting reserved combinations. For ReShade mode, edit settings here with MSFS closed; during a flight, use the fix's ReShade panel.

## Install

Extract the complete ZIP to its own folder outside MSFS. Close the simulator, run **Standalone-DLSS-Glass-Cockpit-Refresh.exe**, confirm the folder containing **FlightSimulator2024.exe**, choose the installation mode, and click **Install**. Launch MSFS normally afterward. Keep all package files together.

Existing users can open the app normally to update. Older apps may reopen more than once while compatibility updates complete. No uninstall is required. Close MSFS before changing installed graphics files.

## Compatibility and support

Setup lists relevant **dxgi.dll**, **version.dll** and **winmm.dll** files and their reported product names when available. Filename collisions and duplicate copies of the glass fix block installation. Other graphics helpers are flagged as possible interactions, not automatically treated as incompatible. ReShade itself and unrelated add-ons are allowed. The app does not rename or remove another tool for you.

If MSFS fails to start or screens look wrong, compare the **same official version and installation mode installed manually**. If manual installation works, contact **siglr / WSG** for installer support. If it also fails, contact **IFreemz** with the graphics version, reproduction steps and **glass-cockpit-dlss-fix.log**; follow his instructions for any diagnostic capture.

Bundled graphics version: **official v1.0.2**, unchanged standalone DLL and ReShade add-on. This includes IFreemz’s screen-brightness flash fix. Optional **Dark screen fix ON** still fails an offline glare test in v1.0.2; its author-default **OFF** setting is retained. Offline checks do not establish compatibility with every aircraft, graphics helper or VR headset. This is for **MSFS 2024**, not MSFS 2020. Frame generation is separate; OFXR is not included.

Installer support/source: https://github.com/siglr/Standalone-DLSS-Glass-Cockpit-Refresh

Original fix/support: https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix

Download, instructions and checksum verification: https://wesimglide.org/otherdata/tools/Standalone-DLSS-Glass-Cockpit-Refresh.html

The separate app uses PolyForm Noncommercial; IFreemz's graphics files retain GPL-3.0-only and the original notices. Corresponding source is included under **Source code**. Checksums and release signatures identify and verify the package; they are not antivirus clearance.

Screenshots illustrate the dark and light themes using the earlier v1.0.2.14 test build; current downloads are v1.0.2.15. Official release downloads have automatic app updates enabled; the disabled-update notice in the screenshots applies only to the test build. These are app-window screenshots, not in-game captures.
