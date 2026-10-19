# Official DLL test package — app 1.0.2.10

Unofficial installer/settings package containing the **unchanged official** Glass Cockpit DLSS Fix by IFreemz v1.0.1:
https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix

The upstream standalone DLL is an early beta. This is a local test version, not a published replacement for app v1.0.2.

**Known test failure:** with the optional **Dark screen fix ON**, the official DLL briefly misrenders glare on a newly recognized dark display. Desktop and stereo reproductions are documented in `COMPARISON.md`. Author defaults remain unchanged (that option is OFF). This package is prepared for a private flight test using those defaults; it has **not passed every offline quality gate** and is not a production release. The proposed fixes in PR #29 are not included in this official DLL.

The app has a locked layout and remembers its window position in `WindowPlacement.json` beside the EXE. It restores the normal position when closed while minimized and keeps the window on an available monitor. Sizes saved by earlier resizable test builds are ignored. Delete that file to reset the placement.

## Try it

1. Close MSFS and the previous settings app. Keep your previous package for rollback.
2. Extract this complete ZIP to a **new folder outside MSFS**.
3. Run `Standalone-DLSS-Glass-Cockpit-Refresh.exe`, confirm the detected game folder, then click **Update** (or **Install**). This checks and installs the latest official stable release. It requires online verification and never silently substitutes an older DLL.
4. On the Experimental tab, leave **Dark screen fix OFF** for the first test. Existing settings are preserved, so check this if you previously enabled it.
5. Start MSFS normally using your usual shortcut. Leave your usual official OFXR Bridge setup in place. Test desktop startup, then VR entry, cockpit clarity, and switching the fix off/on.

No uninstall is needed to migrate from the previous package. The installer replaces the graphics payload transactionally, removes its obsolete DLL, converts the old INI's section names and saves an unchanged copy as `StandaloneGlass.saved.ini` beside this app. Original overwritten-file backups remain available for uninstall. OFXR settings and runtime registration are never changed.

**Settings window shortcut:** click the current shortcut on the Glass settings tab, press the desired keys together, then click **Save** (or Enter). Escape, Cancel or switching away discards the recording. The existing binding stays unchanged until you save. Examples: `Ctrl+5`, `Pause`, `ScrollLock`, or `F13`–`F24`. Pick a key not already used by MSFS or other tools. Keys are recorded only inside this dialog; no global keyboard hooks or simulated input are used. Glass reset buttons leave the shortcut alone. Use it while MSFS has focus, then click green tint in the original window. Main-window **Help** explains settings, OFXR and support; **About** opens the website.

Both settings windows use `glass-cockpit-dlss-fix.ini`. The installer window refreshes it every second and writes only the changed setting. The original window, green tint and diagnostic tools are available through the official DLL's shortcuts; its settings window defaults to **Ctrl+Shift+F6** and can be remapped there. Green tint itself uses **Ctrl+Shift+F11** or the original window's tint button. No simulated keyboard input is sent. Allow about a second for settings to synchronize and use one editor at a time. Upstream `[advanced]` keys can change between versions. The external controls are enabled only for the verified v1.0.0/v1.0.1 settings format; a newer DLL can still be installed, but use its original settings window until that format is verified. Unknown INI keys and hotkeys are preserved.

## Choose an official DLL version

At launch, the app checks IFreemz's GitHub releases and lists the newest **up to ten releases containing a verifiable standalone ZIP**, plus the bundled version when necessary. Add-on-only releases, drafts and prereleases are excluded. The bundled DLL remains available without a network connection. The identical bundled release appears only once. The latest verifiable version is selected by default, and installing that same version again is disabled. Currently GitHub offers v1.0.1 and v1.0.0; only bundled v1.0.1 has been tested here.

Select a version and click **Install selected DLL** with MSFS closed. The app downloads only from the official repository, verifies both the ZIP and extracted DLL against the SHA-256 hashes explicitly named in the release notes (and cross-checks GitHub's ZIP digest), validates the archive and x64 DLL, then uses the same reversible installer. Older selections are allowed for rollback after confirmation. Version 1.0.0 has a known FSLabs aircraft crash. Releases without unambiguous ZIP and DLL hashes in their notes are not offered; there is no checksum bypass. Existing settings stay in place; an older DLL may not implement every newer option. A fresh downloaded-DLL installation leaves settings unset so that DLL supplies its own defaults. The downloaded original archive, including its licenses, is kept in `upstream-downloads` beside the app. A network/validation failure leaves the installed files unchanged.

Only the bundled v1.0.1 DLL has been tested with this package. A newer or older release appearing in the list does **not** imply that it passed these offline checks. Upstream download verification uses HTTPS, the author's release-note ZIP and DLL checksums, and GitHub's ZIP checksum, not the WeSimGlide release-signing key or Windows Authenticode.

## ReShade conflicts

Use either the official standalone DLL or the ReShade add-on, never both. The installer blocks installation/update when it finds ReShade configuration, `.addon64`/`.addon` files in the game folder or common add-on folders, or a configured ReShade `AddonPath`. It also refuses to overwrite an unowned, unrecognized `dxgi.dll`, even after an overwrite request. It leaves those files intact. Use the other tool's uninstaller or its documented removal process first; do not delete unrelated files blindly. If you use ReShade, keep the original ReShade add-on instead of this installer. Uninstall remains available even if a conflict appears later.

Unusual external loaders cannot all be detected. Also remove/disable the glass fix in OptiShade or any other tool managing it before installing this standalone DLL.

## Support

This separate installer/settings app is by **siglr / WeSimGlide**, not IFreemz. For installation, update, rollback or app-window issues, use **Installer support** in the app: https://github.com/siglr/Standalone-DLSS-Glass-Cockpit-Refresh/issues, or the WSG Discord linked from https://wesimglide.org/.

For incorrect screens or crashes caused by the **unmodified official standalone DLL**, use **Screen issues**: https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix/issues. Include the official DLL version, aircraft, reproduction steps and ideally a **Ctrl+Shift+F8** capture following IFreemz's instructions. Reports about an old custom/modified engine belong with the installer maintainer first. A capture is optional and performed by the official DLL; this app adds no capture feature.

## Roll back

To return to the bundled official DLL, select **v1.0.1 (bundled)** and install it with MSFS closed. To return to the old custom graphics engine, uninstall using this test app first, then install from your retained v1.0.2 package. Copy `StandaloneGlass.saved.ini` from this test app's folder back into the game folder as `StandaloneGlass.ini` only after that installation, with MSFS closed. If several saved copies exist, choose the one from before migration. Uninstall preserves edited official settings beside this test app too.

The official DLL may create logs, status files and requested diagnostic captures. These user diagnostic files are preserved on uninstall; only installer-owned files are removed/restored.

## Offline checks for this test build

The untouched v1.0.1 DLL passed four startup probes (including copied swapchain tables), private-adapter/D3D11On12 checks, 16 core rendering scenarios and 13 motion-quality measurements. Installer tests cover migration, interrupted updates, rollback, original-file restoration and keeping independent OFXR settings. The app was checked in both light and dark themes; signed-package and automatic-update safeguards were also tested.

These checks use isolated simulator fixtures on the RTX 4090. They are not a real MSFS/Pimax flight test. The Dark screen fix ON failure described above remains a known upstream limitation.

## Licenses and updates

The separate app remains PolyForm Noncommercial (`LICENSE.md`). The official DLL and its complete corresponding source remain GPL-3.0-only with the author's additional NOTICE terms. The unmodified LICENSE, NOTICE and README are in `licenses/IFreemz`; its matching source archive includes the pinned submodules and build scripts. This package is not endorsed by IFreemz.

This local build uses a separate development signing key. **Automatic app updates are disabled** so a production update cannot overwrite the test. Upstream version discovery/selection remains available. Do not upload this test ZIP to the production auto-update feed.
