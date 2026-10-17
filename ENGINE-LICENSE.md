# Graphics engine licensing

The combined `StandaloneGlassGraphics.dll` is **GPL-3.0-only**, with the
additional attribution and modified-version terms in `vendor/glass/NOTICE.md`.
The complete GPL text is in `vendor/glass/LICENSE`. In the binary package, these files are `licenses/glass/LICENSE` and `licenses/glass/NOTICE.md`.

Based on **Glass Cockpit DLSS Fix by IFreemz**:
https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix

This is a modified, unofficial build named **Standalone Glass Graphics**,
maintained at https://github.com/siglr/Standalone-DLSS-Glass-Cockpit-Refresh.
Report package-specific issues there, not as defects in IFreemz's official build.

For avoidance of doubt, siglr's code and modifications in the following engine
and build-support files are licensed under GPL-3.0-only, not PolyForm:

- `src/` and `include/` (including the DXGI bootstrap).
- `CMakeLists.txt`, `standalone/Window.cmake` and `scripts/build.py`.
- `tests/graphics_probe.cpp`, `tests/dxgi_private_adapter.cpp` and
  `tests/upstream_v1_tests.py`.
- Changes under `vendor/glass/` and `vendor/reshade/` used by the combined DLL.

Existing permissive third-party licenses and copyright notices remain in force
for their respective components. They are not replaced by this statement.
The resulting combined graphics DLL is distributed under GPL-3.0-only and the
upstream section 7 terms; commercial redistribution of that engine is permitted
under those terms. No noncommercial restriction applies to the engine.

The separate C# installer/settings/updater executable remains under PolyForm
Noncommercial 1.0.0 (`LICENSE.md`). It copies files, edits the INI and signals a
Windows event; it does not link or load the graphics engine. Bundling these two
programs does not place a noncommercial condition on the GPL engine.

The version-specific `Standalone-DLSS-Glass-Cockpit-Refresh-VERSION-source.zip`
is the corresponding source for each prepared binary release, including local
changes, shaders, dependencies, tests and build scripts. Distribute it beside
the binary ZIP and keep both available together. The root README documents the
build; graphics-only builds use the `UnifiedVRGraphics` and
`UnifiedVRBootstrap` CMake targets and do not require the application or a
private signing key. Source revisions and dependency pins are recorded in
`standalone/upstream-lock.json`.
