# v1.0.2.14 release preparation — 2026-10-19

Production app uses the existing publisher verification key and enables automatic updates. The two official v1.0.1 graphics files remain byte-unchanged: standalone dxgi.dll SHA-256 `06f11b0abeb4d3a9b1a14b7ec22faf6de214c8f218353f1e062471015db2a685`, ReShade add-on SHA-256 `a637456d2a93780ccfea73043f428e4f2a0f5f009423a3ca22b694b198565517`. Release-note checksums and matching GitHub asset digests are verified. No graphics rebuild or OFXR/headset modification is part of this release.

## App, installer and update validation

- 18 discovery checks, 44 native installer operations and 55 isolated updater fixtures passed again during release preparation.
- Final UI source passed 75 official-engine operations/window checks and 50 two-mode operations/window checks, including running add-on read-only state, rollback, recovery and settings preservation.
- The 192 reused-buffer repaint cases pass. The prior rendering defect was reproduced live and by a failing regression; a negative-control test confirms the test rejects that bug.
- Both themes at simulated 100/125/150/200% scaling, complete dropdown/header/footer containment, compact input spacing, fixed frame/position, smaller working areas and page navigation without settings writes are checked. Normal standalone Setup must fit without vertical scrolling at 100%.
- Signed real update chains from published v0.1.9, v1.0.1 and v1.0.2 pass through their historical updaters and immutable compatibility packages to v1.0.2.14. Direct signed upgrade from v1.0.2.10 also passes. Personal files, navigation/theme preferences and independent OFXR settings are preserved.
- The production ZIP passes custom-to-official graphics migration, uninstall restoration, both-mode UI self-tests and a standalone-to-ReShade switch preserving INI bytes.
- Published compatibility assets are checked against the pinned hashes from GitHub release v1.0.2.10; none are rebuilt or re-signed. Staged route expressions and fixed filenames are checked.
- Production package documentation contains only README.txt and required license/notice material; both corresponding-source archives are under Source code. The separate app-source snapshot includes exact source hashes and build instructions.

## Graphics validation and limits

The exact standalone DLL was rechecked during release preparation: four normal/copied-table startup probes, private-adapter/D3D11On12, 16 core graphics scenarios and 13 motion measurements passed. No real MSFS process was running for those probes.

Earlier checks on the same unchanged official add-on under ReShade 6.8.0 passed 16 core scenarios and 13 motion measurements (2026-10-19). The same day's default-OFF first-glare and external-INI regressions passed in desktop and stereo fixtures. These existing results are retained because graphics bytes did not change.

Known failure: optional **Dark screen fix ON** still fails the official v1.0.1 standalone first-glare regression in desktop and stereo. The author-default OFF behavior passes. The official binaries are not patched. Real aircraft, headset and third-party-helper compatibility is not established by offline checks.

## Visual and publication limits

Focused live testing of the final UI confirmed complete dropdown borders in dark/light, menus staying open across refresh, selection without installation, and normal Setup fitting without unnecessary scrolling. Installed Settings also opened cleanly. Extra installed-Setup interaction was interrupted by user input; automated tests cover that state. Windows display scaling itself was not changed; the broader DPI matrix is automated. Native shortcut recording and every possible helper combination do not have full manual coverage.

The webpage and listing copy cover both modes, correct support ownership and the same-version/manual-install comparison. Final screenshot assets are the user's four original test-build captures, copied byte for byte without retouching. Captions explain that automatic app updates are enabled in official releases despite the test-build notice in the screenshots. Shared-index edits are restricted to this tool's card; other cards/assets remain unchanged.

Preparation does not publish anything. Before publication completes, verify live Apache routing for browser/old/new User-Agents, HTTP 200 without redirects, no-store/Vary headers and the signed archive hashes. Live checks require deploying the files.
