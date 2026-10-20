# v1.0.2.16 hotfix validation — 2026-10-20

## Reproduced defect and regression coverage

The installed app v1.0.2.15 was inspected through Windows Computer Use with official graphics v1.0.3. Every Settings control was disabled by the exact-version allowlist. The new UI regression failed on the old code with `Official 1.0.3 settings must remain editable`.

The allowlist is removed. Tests cover v1.0.2, v1.0.3 and simulated future v1.0.4/v2.0.0 in both modes, including real INI edits, shortcut recording and resets. Navigation preserves INI bytes. Active operations still disable edits and restore them afterward; running add-on settings remain read-only. Incomplete installation/recovery, rollback, dropdown and theme/scaling checks remain in the UI suite.

The settings loaders/defaults in upstream v1.0.3 standalone commit a4eb07d and add-on tag v1.0.3 were compared with their reviewed v1.0.2 sources and are byte-identical. Future version numbers no longer gate editing: actual upstream setting changes can be handled in a later app update.

## Package and release checks

- Official-engine: 75 archive/selection/installer/recovery checks and UI self-test.
- Both modes: 50 download/install/update/switch/rollback/uninstall checks, including running add-on UI restrictions.
- Development ZIP: isolated signing, disabled official app updates, install/uninstall and independent settings preservation.
- Repaint, standalone installer/discovery and updater suites are run for the release; production migration logs are retained in build/release-preparation/check-release-migration.txt.
- Production migration exercises historical updaters from 0.1.9, 1.0.1, 1.0.2, 1.0.2.10, 1.0.2.14 and 1.0.2.15, the exact signed production EXE in both modes, immutable compatibility assets and unversioned routes/files.

The bundled official v1.0.2 graphics bytes, corresponding source and notices are unchanged from v1.0.2.15. Latest official release remains the normal installation default; updating the app does not replace an installed v1.0.3 DLL. Existing settings, OFXR files, original backups and screenshots/videos are preserved. Graphics/runtime tests from v1.0.2.15 remain applicable to the identical bundled binaries; no new graphics changes or real MSFS/VR validation is claimed. The known optional Dark screen fix ON glare failure remains; default OFF is retained.

Production signing uses the established protected publisher key and pinned public key. Development packages use the separate test key. No publisher key, user settings or generated output is included in source commits. Live publication and older-app HTTP routes are verified after deployment and recorded under build/release-preparation.
