# Setup / Settings test package — app 1.0.2.14

Independent installer/settings app by siglr / WeSimGlide. Both graphics files are unchanged official releases of **Glass Cockpit DLSS Fix by IFreemz v1.0.2**:
https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix

This is a local test build. Automatic app updates are disabled; official fix downloads and version selection are available. Do not upload this ZIP to the production update feed.

## Choose your installation

Extract the complete ZIP outside MSFS, close MSFS, then run `Standalone-DLSS-Glass-Cockpit-Refresh.exe`. Confirm the folder containing `FlightSimulator2024.exe`.

On **Setup**, confirm the game folder. In **Install as**, choose:

- **Standalone DLL** if you do not use ReShade. Installs the official `dxgi.dll`.
- **ReShade add-on** if you already use ReShade **6.7 or newer with full add-on support**. Installs the official `glass-cockpit-dlss-fix.addon64`. This app does not install, configure, rename or remove ReShade itself. Download ReShade separately from https://reshade.me/ if needed.

An existing installation managed by this app selects its current mode. An existing add-on beside the game executable preselects add-on mode; that file alone does not prove ReShade is installed correctly.

**Latest official release** is selected by default. The primary action changes to **Install**, **Update**, **Switch mode** or **Install selected version** for your selection. Installed mode/version are shown separately from your selection. Click the primary action to install the latest verified official release in that mode. For rollback, choose a version and click **Install selected version**. Each mode has its own list of up to ten verifiable releases and a bundled v1.0.2 option usable offline. Older releases may have known bugs: v1.0.0 can crash with FSLabs aircraft.

The test app manages add-ons beside the game executable. If ReShade uses a custom add-on folder, installation stops and identifies that folder. Update that installation manually rather than placing a second copy in the game folder.

## Switch modes or uninstall

Choose the other mode, then click **Switch mode**. Confirm the change with MSFS closed. The app removes its managed copy of the other mode and keeps your shared INI, including unknown keys and shortcuts. It does not change OFXR, Pimax or MSFS settings.

Original files that were backed up remain recoverable. A backed-up copy of the other glass fix stays inactive during a switch so that both fixes do not run together. Uninstall restores original files and saves edited settings beside the app. Unexpectedly modified binaries are preserved and reported rather than removed.

A manually installed fix is not automatically deleted. Select its existing mode and a matching verified version to let the app manage it first, accepting the backup prompt. Then switch modes. Other filenames or externally managed copies may need removal using their own instructions.

## Settings

Open **Settings** after installation. The enable checkbox is followed by the shortcut recorder and line smoothing. **Advanced** and **Experimental** start collapsed; each section has its own reset. Before installation, Settings offers **Go to Setup**. Page changes never install files or save simulator settings. The last-used page is remembered, and the fixed window remembers its position. On smaller screens, content scrolls without shrinking text.

Both official versions use `glass-cockpit-dlss-fix.ini` beside the installed file. Author defaults are retained; existing settings are preserved byte for byte during installation and switching.

- **Standalone:** settings changes here apply during upscaling without leaving VR. The shortcut recorder opens the official standalone settings window; green tint and diagnostics stay in that window. The v1.0.2 standalone window reloads the INI before saving, preserving external edits.
- **ReShade add-on v1.0.2:** edit settings here **with MSFS closed**. During a flight, use the Glass Cockpit DLSS Fix panel in ReShade. The add-on reads its INI at startup; it does not reload external edits. The standalone settings-window shortcut is not offered for this mode.

Unknown future settings formats disable the local controls; use that release's own settings interface. Reset buttons leave shortcuts alone.

## If installation is blocked

- **ReShade uses dxgi.dll:** choose ReShade add-on mode. ReShade's file stays in place.
- **OptiScaler uses dxgi.dll:** the standalone glass fix needs the same filename. Follow OptiScaler's instructions to use an available supported filename such as winmm.dll or version.dll instead. Do not overwrite a file belonging to another tool. This app does not rename it for you.
- **Another copy of IFreemz's fix:** use one copy only. The message identifies the file and explains whether to choose its matching mode or remove it using its original installation instructions.
- **Unknown file:** the app names the reported product when available and preserves the file. Identify its owner before removing it. If unsure, contact Installer support with the message.

ReShade itself and unrelated add-ons are allowed. Detection uses filenames and product metadata to explain conflicts; official download verification uses release-note SHA-256 values and GitHub's matching asset digest. Standalone ZIP layout, extracted DLL hash and x64 DLL format are checked too. There is no verification bypass. A failed download or interrupted transaction leaves the previous installation intact or recoverable.

## Compatibility information

Setup lists `dxgi.dll`, `version.dll` and `winmm.dll`, using sanitized product/version information when available. An unidentified DLL stays unidentified. Metadata helps explain a file's role; it is not proof of authenticity or compatibility. The installer never renames or removes another graphics tool automatically.

DLSS Enabler as `version.dll` has a reported startup conflict with the standalone fix. IFreemz separately reported MFG Unlocker as `winmm.dll` working with his standalone fix without ReShade/OptiScaler; with OptiScaler, use the unlocker's plugin method instead. These are reported setups, not universal compatibility guarantees. See Help for troubleshooting and the upstream instructions.

## Support and limitations

Installer, update, mode selection and app-window issues: https://github.com/siglr/Standalone-DLSS-Glass-Cockpit-Refresh/issues or siglr through the WSG Discord linked from https://wesimglide.org/.

For an MSFS problem, uninstall this package and test the same official version and mode installed manually. If manual installation works, contact Installer support. If it also fails, contact IFreemz at https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix/issues with the fix version, aircraft, steps and `glass-cockpit-dlss-fix.log`. Follow his instructions for an optional diagnostic capture.

Official v1.0.2 still fails the targeted offline glare test with optional **Dark screen fix ON** in desktop and stereo. Default-OFF and external-settings tests pass; see RELEASE-VALIDATION.md. The author default remains OFF. This app does not modify or patch either graphics binary. Offline results do not replace a real MSFS/ReShade/VR flight test.

The separate app remains PolyForm Noncommercial. IFreemz's binaries and corresponding source retain GPL-3.0-only and original NOTICE terms. Both pinned source archives, including build scripts and submodules, are in `Source code/`. They are for developers; do not extract them to install the app. License/notice files are under `licenses/IFreemz`. Full ReShade is not distributed in this package.
