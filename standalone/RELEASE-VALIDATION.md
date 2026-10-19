# v1.0.2.10 release preparation — 2026-10-19

Production app uses the existing publisher verification key and enables automatic updates. Development builds use an isolated test key and disable official app updates. The graphics DLL is the unchanged official IFreemz standalone v1.0.1, SHA-256 `06f11b0abeb4d3a9b1a14b7ec22faf6de214c8f218353f1e062471015db2a685`. The ZIP and DLL were checked against the release notes. Latest upstream at preparation was v1.0.1.

Passed offline:

- 18 Steam/Xbox discovery checks and 44 native installer operations, including recovery, locks, backups and settings preservation.
- 55 isolated updater fixtures, including official two-file payload support, invalid/mixed payload rejection, signatures, interruption, helper handoff and settings preservation.
- 69 official-engine selection/archive/install/migration/rollback operations plus UI self-tests.
- Development-package signature/install/uninstall and disabled official-update entry points.
- Real signed automatic-update chains from published v0.1.9, v1.0.1 and v1.0.2 using their historical updater sources; settings/OFXR preservation; final engine migration and original-proxy restoration on uninstall.
- Exact production EXE UI self-test, including dropdown stability, themes, shortcut recording, window position and fixed-size behavior.
- Four startup probes: normal/copied swapchain tables, Present/Present1. Private-adapter/D3D11On12 check, 16 core graphics scenarios and 13 motion measurements.
- External-settings enable/resume and first-glare checks with author-default Dark screen fix OFF, desktop and stereo.
- Staged compatibility routing expressions and identical latest shared index containing OpenXR FG Switcher.

Known failure, unchanged official DLL: first-glare tests with optional **Dark screen fix ON** fail in desktop and stereo. The author-default OFF behavior passes. This is disclosed in user instructions and release notes; proposed custom engine fixes are not included. Real MSFS/aircraft/headset testing is not established by these offline checks.

Before publication completes: verify live Apache routing for old/new User-Agents, HTTP 200 responses, no redirects, no-store/Vary headers and matching signed archive hashes. These require deploying the prepared files; local routing-expression checks do not establish live-server configuration. No release has been published by preparation alone.
