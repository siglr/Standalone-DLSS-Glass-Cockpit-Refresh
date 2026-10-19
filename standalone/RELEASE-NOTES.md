# Standalone DLSS Glass Cockpit Refresh v1.0.2.10

This release switches to IFreemz's **unmodified official standalone DLL** (bundled v1.0.1), with the separate WeSimGlide installer and settings window.

- Install/Update selects the latest official stable DLL and verifies the release-note SHA-256 for both ZIP and DLL.
- A version selector offers up to ten verified releases for rollback.
- ReShade/add-on conflicts are blocked to prevent the fix running twice.
- Existing glass settings, original uninstall backups and independent OFXR settings are preserved.
- A compact Dark/Light interface remembers its fixed-size window's position. Dialogs center over the app, and the version dropdown stays open during status refreshes.
- Click the settings-window shortcut to record a key combination. The existing shortcut is retained unless you save a change. Green tint and diagnostics remain in the official in-sim window.
- Installer support goes to siglr / WeSimGlide; official-DLL screen issues go to IFreemz.

**Existing users:** open the app normally. Compatibility updates may reopen it more than once. Then close MSFS and click Update if prompted. No uninstall is needed. Fresh users should extract the complete ZIP outside MSFS and run the EXE.

**Known limitation:** optional Dark screen fix ON still reproduces an official v1.0.1 glare issue in an offline test. Its author-default OFF setting is unchanged. The official DLL is not patched; real aircraft/headset testing remains necessary.

The exact official DLL passes the four normal/copied-table startup probes, private-adapter check, 16 core scenarios and 13 motion measurements. The optional dark-screen regression remains disclosed rather than counted as a pass. App, installer and legacy automatic-update migration checks are supplied with the release validation results.

Glass Cockpit DLSS Fix by IFreemz: https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix

Installer source/support: https://github.com/siglr/Standalone-DLSS-Glass-Cockpit-Refresh

Download/help: https://wesimglide.org/otherdata/tools/Standalone-DLSS-Glass-Cockpit-Refresh.html

The app remains PolyForm Noncommercial; the official engine remains GPL-3.0-only with its original NOTICE. Corresponding source, checksums and signed update metadata accompany the release. Release signatures are not Windows Authenticode signing or antivirus clearance.
