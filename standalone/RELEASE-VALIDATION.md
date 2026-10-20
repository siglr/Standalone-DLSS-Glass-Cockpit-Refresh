# v1.0.2.15 release preparation — 2026-10-20

This is a local release candidate, not a published release. The app and graphics versions are separate. Official graphics files remain unchanged; no simulator, OFXR or headset installation was modified.

## Official files and corresponding source

- Bundled graphics: IFreemz official v1.0.2 standalone DLL and ReShade add-on.
- Standalone archive SHA-256: `010abd02f3b25f35fe06ff7376ce43920ef808fbf6afe5c0b100a20785d1eb94`.
- Standalone dxgi.dll SHA-256: `5b505a00e5a072f6c2a9b37a6f19800f248850ace6fc51dcb802c0043a560d03`.
- ReShade add-on SHA-256: `9474b66e2e22bc005233463574fc9ba9c107d4bfa8364e5c3478588f0e8f2036`.
- Both downloads match the official release-note checksums and GitHub asset digests.
- Standalone source: `9dd06d34767441a42d5fb0795b370ef1940fdd4c`. Add-on source: `c4651ad67cbb38f523f1f82279bfa6c2732b2be9`, from annotated tag `3905f7b83bd45f58d14b3d01af3d34babdebdeaa`. Both archives include pinned submodules and original notices.
- Package checks compare each corresponding-source archive against its downloaded upstream source, and reject stale cached downloads when a release/source pin changes.

## App and installer checks

- 429 shortcut capture, naming, modifier, reserved-combination and dialog-navigation checks passed.
- 18 simulator-discovery checks, 44 native installer operations and 55 updater fixtures passed.
- 75 official-engine operations/window checks and 50 two-mode operations/window checks passed, including add-on running-state restrictions, rollback, recovery and settings preservation.
- 192 repaint checks passed. Automated UI checks cover both themes, simulated 100/125/150/200% scaling, dropdown stability, fixed frame/position, small working areas and navigation without INI writes.
- New tests cover once-per-session update navigation, modal/busy/dropdown deferral, rollback preservation, official v1.0.2 settings support, new shortcut INI round-trips and unchanged existing settings until explicit Save.

## Graphics checks

The real simulator was closed. Only isolated test hosts under this repository's build folder were run.

- Exact official standalone DLL: four startup probes, private-adapter/D3D11On12, 16 core rendering scenarios and 13 motion measurements passed.
- Exact official add-on under ReShade 6.8.0: 16 core rendering scenarios and 13 motion measurements passed. Full ReShade is only a local test dependency and is not distributed.
- External-settings and default-OFF first-glare tests passed in desktop and stereo fixtures. Optional **Dark screen fix ON** still failed both first-glare tests in official v1.0.2. The test script therefore returned failure for these two known upstream cases; the author-default OFF setting is retained. Full results are in `build/official-engine/regression-results.json` and companion logs.

## Production package and publication checks

Production packaging explicitly selects the existing protected publisher key, verifies it against the pinned public key, and enables official app updates. Test builds use their separate key and disable app updates. Main filenames remain unversioned.

After signing, run `tests/release_migration_tests.py` against the staged candidate. Its log is retained in `build/release-preparation/check-release-migration.txt`; the final candidate hashes and preparation status are recorded beside it. These checks exercise real historical updater chains, production UI in both modes, immutable compatibility assets, fixed filenames, source checksums and routing expressions. Never publish a candidate with a failing check.

Website staging uses a freshly read live index containing OpenXR FG Switcher. Only the Glass Cockpit Refresh card is changed; unrelated cards and routing rules are preserved. The four user-supplied screenshots remain byte-identical, with captions identifying the earlier test build. The mod page's cockpit illustration remains unchanged. Listing videos are to be retained. The shared changelog is under HangarAddons' 2,000-character limit.

Live Apache routing and listing downloads must be verified after publication. Offline graphics and automated UI checks do not establish real MSFS/aircraft/headset compatibility or complete manual keyboard-layout coverage. No new antivirus clearance or Windows code-signing claim is made.
