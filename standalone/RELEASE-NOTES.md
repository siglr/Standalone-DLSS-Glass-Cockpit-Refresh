# Glass Cockpit Refresh v1.0.2.15

- Bundles IFreemz’s unchanged official v1.0.2 standalone DLL and ReShade add-on for offline installation. Both downloads match the SHA-256 checksums in the official release notes, with corresponding source and original notices included.
- Includes IFreemz’s fix for brief bright-screen flashes reported on the iniBuilds A380 and Fenix.
- The official standalone settings window reloads the INI before saving, preserving settings changed by this app.
- When a newer official release is verified, the app opens Setup once per session and highlights “Update to v…”. It respects explicit rollback choices and waits for active dialogs or operations to finish.
- The shortcut recorder supports the current official key rules: arrows, Tab, Enter, Backspace, numpad operators and punctuation with Ctrl, Shift or Alt. Reserved combinations, including Alt+Tab, Alt+Space and Alt+Enter, are rejected. Existing shortcuts are preserved.
- Settings controls remain available with official graphics v1.0.2. ReShade settings remain read-only here while MSFS runs; use the in-game ReShade panel instead.

Known limitation: optional Dark screen fix ON still fails the offline glare test. The author-default OFF setting is retained.

Close MSFS before updating installed graphics files. Existing glass settings, uninstall backups and separate OFXR settings are preserved. Older apps retain their automatic compatibility-update route; no uninstall is required.

The app and graphics versions are separate. Use one glass fix only. Installer support: siglr / WeSimGlide. Graphics issues reproduced with the same unmodified official version installed manually: IFreemz.
