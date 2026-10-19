# Glass Cockpit Refresh v1.0.2.14

This release adds a choice of IFreemz's **official standalone DLL or ReShade add-on**, and a clearer **Setup / Settings** interface. Both bundled graphics files remain unmodified official v1.0.1 releases.

- Choose Standalone DLL, or ReShade add-on for an existing ReShade 6.7+ installation with full add-on support. Switch modes with MSFS closed while preserving settings and backups; duplicate glass fixes are prevented.
- Setup separates the installed mode/version from the selection. Latest official release is the default, with verified downloads, bundled offline installation and rollback choices.
- ReShade itself and unrelated add-ons no longer cause blanket rejection. Compatibility information identifies known graphics products when possible, explains filename collisions, and flags other helpers as possible interactions without automatically changing them.
- Settings puts the enable control and standalone shortcut recorder first, followed by smoothing. Advanced and Experimental sections expand when needed. ReShade settings become read-only while MSFS runs; use the in-game ReShade panel instead.
- A slightly taller fixed window, compact label/input spacing, improved DPI layout, correctly sized checkboxes and dropdowns, clean repainting, centered dialogs, and remembered position, theme and page.
- One short quick-start/troubleshooting README. Corresponding source archives are grouped under Source code; developer reports stay in the repository.
- Installer support and graphics support are clearly separated in Help. Existing OFXR, Pimax and simulator settings remain untouched.

**Updating:** open the app normally. Older apps may reopen more than once to complete compatibility updates. No uninstall is required. Close MSFS before updating installed graphics files. The app version and official graphics version are separate.

**Fresh installation:** extract the complete ZIP outside MSFS, run the EXE, and follow Setup. ReShade mode requires separately installed full-add-on ReShade. Use only one glass fix.

**Known limitation:** optional Dark screen fix ON still fails a glare regression in the unchanged official v1.0.1 standalone DLL. The author-default OFF setting is retained. Use one settings editor at a time with v1.0.1; for ReShade mode, edit settings here only with MSFS closed. Offline validation does not replace real aircraft/headset testing.

Glass Cockpit DLSS Fix by IFreemz: https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix

Installer/source: https://github.com/siglr/Standalone-DLSS-Glass-Cockpit-Refresh

Download/help: https://wesimglide.org/otherdata/tools/Standalone-DLSS-Glass-Cockpit-Refresh.html

App: PolyForm Noncommercial. Official graphics: GPL-3.0-only with original notices and corresponding source. Signed update metadata and SHA-256 checksums accompany the release; these are not Windows code-signing certificates or antivirus clearance.
