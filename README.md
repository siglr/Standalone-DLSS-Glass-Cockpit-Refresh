# Glass Cockpit Refresh v1.0.2.16

v1.0.2.16 removes the DLL version allowlist that disabled every setting after updating official graphics to v1.0.3. New official versions keep the existing settings editable; changes to upstream keys can be handled in a later app update. Incomplete installations, active operations and a running ReShade add-on retain their existing restrictions. The official graphics files remain unchanged. Existing users receive the hotfix through the normal automatic app update.

## Published v1.0.2.15

Release **1.0.2.15** opens Setup once per session when a newer official release is verified for the installed mode, with an accented **Update to v…** button. Explicit rollback choices, active dialogs and operations defer this navigation. Official graphics v1.0.2 retains the verified settings format, so the app's settings remain available after updating. Both bundled graphics files are now unmodified official **v1.0.2**, with matching corresponding source.

The shortcut recorder requires official DLL v1.0.2 or newer and follows its current rules, including modified arrows, Tab, Enter, Backspace, numpad operators and punctuation. Plain Tab navigates; plain Enter saves the captured shortcut; Esc cancels. Windows-reserved combinations remain blocked. Existing shortcuts are preserved. Run `python -B tests/shortcut_recorder_tests.py` for the key-capture checks.

Choose **Standalone DLL** or **ReShade add-on** in Setup. Settings contains the enable control, standalone shortcut recorder, smoothing and expandable Advanced/Experimental sections. Downloads are verified; settings and OFXR are preserved. [Full product description](standalone/RELEASE-DESCRIPTION.md).

Current user instructions: [RELEASE-README.md](standalone/RELEASE-README.md). The independent WeSimGlide installer uses IFreemz's unchanged official DLL, preserving the author's defaults and OFXR settings. [Release notes](standalone/RELEASE-NOTES.md) include the known optional dark-screen limitation. [Production packaging and legacy update migration](standalone/PUBLISHING.md) require explicit signing configuration. The custom-engine instructions below are historical.

This release supports IFreemz's untouched official standalone DLL and ReShade add-on, with one installation mode selector. ReShade itself remains separately installed. See the test instructions for settings and custom-folder limitations. Build the isolated **1.0.2.16 Settings hotfix test package** using `python -B scripts/official_test_package.py`; see [test instructions](standalone/OFFICIAL-DLL-TEST.md), [comparison](docs/OFFICIAL-DLL-COMPARISON.md) and [upstream improvement report](docs/IFREEMZ-ENHANCEMENTS.md).

After packaging, run `python -B tests/ui_paint_regression_tests.py`, `python -B tests/development_package_tests.py`, `python -B tests/official_engine_tests.py`, `python -B tests/installation_mode_tests.py`, `python -B tests/standalone_tests.py`, and `python -B tests/app_update_tests.py`. With the real simulator closed, run `python -B tests/official_graphics_tests.py`, `python -B tests/official_addon_graphics_tests.py`, and `python -B tests/official_fixture_regressions.py` sequentially. The latter deliberately returns failure for the documented official v1.0.2 Dark screen fix ON regression; default-OFF and external-settings cases must pass. Run installer/UI suites sequentially: the mode tests also create an inert simulator-named host. Do not run them concurrently with each other, live UI review or graphics fixtures.

The custom-engine CMake targets below remain historical regression references; they do not build the official DLL. The isolated test ZIP includes the exact upstream DLL, its license/notices and corresponding source; automatic app updates are disabled while official DLL discovery and manual version selection remain available. Production packaging builds the separate C# app with the production public key and enabled updates; it never rebuilds the official DLL.

The normal Install/Update button verifies the latest official stable release against the ZIP and DLL SHA-256 values in its release notes. The selector retains up to ten verified releases for explicit rollback. An occupied unverified dxgi.dll blocks standalone installation. A second copy of the glass fix blocks installation; changing modes removes only the managed other copy. ReShade itself and unrelated add-ons are allowed. Product metadata improves conflict messages but never replaces SHA-256 verification. Uninstall remains available. Installer support goes to siglr/WSG; unmodified official-DLL screen issues go to IFreemz. See the installation instructions above.

## Previous published v1.0.2 architecture

# Standalone DLSS Glass Cockpit Refresh

Glass Cockpit DLSS Fix for **Microsoft Flight Simulator 2024**, with the required ReShade D3D12 components built in and a small install/uninstall/settings window. Desktop and VR; no separate ReShade installation or menu is needed. It does not include frame generation. Official OFXR Bridge can be installed separately.

**Separate licenses:** the installer/settings/updater app is source-available under [PolyForm Noncommercial 1.0.0](LICENSE.md). The combined graphics engine is **GPL-3.0-only**, including this project's engine changes, with the upstream NOTICE terms. See [engine licensing and corresponding source](ENGINE-LICENSE.md) and [attribution](NOTICE.md).

Version **1.0.2** integrates **Glass Cockpit DLSS Fix by IFreemz v1.0.1** as a modified, unofficial **Standalone Glass Graphics** build. It fixes a standalone v1.0.0 startup crash when another graphics component copies a swapchain's method table. The upstream rendering corrections, per-mesh brightness, depth-based occlusion, experimental controls and author's defaults are retained. The separate official OFXR Bridge and its settings are untouched.

The upstream hotfix avoids opening protected late-loaded aircraft DLLs (FSLabs), prevents red radio digits turning yellow, and corrects false seam detection on stretched radio textures. This release also prepares the updater for four-part version numbers; see [release numbering](standalone/PUBLISHING.md#release-numbering).

## Use the mod

For the official prebuilt app, use the [WeSimGlide download and instructions page](https://wesimglide.org/otherdata/tools/Standalone-DLSS-Glass-Cockpit-Refresh.html). See [installation and settings](standalone/README.md), [testing guidance](standalone/TESTING.md), and [security behavior](standalone/SECURITY.md).

Already using full ReShade in MSFS? Use [IFreemz's original add-on](https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix) instead of installing a second dxgi.dll. MSFS 2020 is outside this package's scope. Existing glass settings and original uninstall backups are preserved during normal updates. No capture/report feature or keyboard shortcuts are added.

## Build from source

Windows x64, Visual Studio 2022 C++ Build Tools (MSVC v143, CMake tools), Windows SDK 10.0.26100.0 with DXC/FXC, Python 3.12 or newer, and Windows .NET Framework 4.x C# compiler are required. Other SDK paths can be supplied as `-DSDK_BIN=...`. Required third-party source/headers and licenses are included; no private repository or OFXR SDK is required.

```powershell
python -B scripts/build.py
python -B scripts/build.py --build build/standalone --config Release --parallel 8
```

The output includes the control EXE under `build/standalone/control` and two product DLLs under `build/standalone/Release`. Build/test output also contains fake upscalers and fixtures: **never copy test output into MSFS**. Do not install individual build files manually.

To produce a complete, installable local package with your own development signing key:

```powershell
python -B scripts/dev_package.py
```

Extract `build/Standalone-DLSS-Glass-Cockpit-Refresh-development.zip` outside the simulator. Close MSFS before using its Install/Uninstall controls. This script does not install anything. Development signing keys remain under ignored build output and must never be shared. The generated public key is embedded in the EXE; the signed inventory protects its install payload. No maintainer private key is needed.

Source builds are visibly labeled and **official automatic app updates are disabled by default**, so an upstream update cannot overwrite your modifications. The official update implementation remains available for review/testing. Fork authors must keep this disabled or implement their own channel and signing identity; see [packaging and signing](standalone/PUBLISHING.md). Do not overwrite an existing official package with a development build; use a separate external folder and uninstall the existing mod first when switching identities.

## Offline checks

```powershell
python -B tests/development_package_tests.py
python -B tests/standalone_tests.py
python -B tests/app_update_tests.py
python -B scripts/build.py ctest --test-dir build/standalone -C Release --output-on-failure
```

Installer/update tests use isolated fixture folders and disposable signing keys. They do not install into a real simulator or change OFXR/headset settings. Graphics tests require a D3D12-capable GPU and use fake upscalers, never real MSFS. Test programs are named FlightSimulator2024.exe to exercise the production app filter; do not close them during testing. Real aircraft/headset validation remains necessary. Prior validation and its limits are recorded in [VALIDATION.md](standalone/VALIDATION.md).

## Source layout and upstream changes

This repository is the authoritative source for standalone development and releases. Future changes, tests and release builds are made here directly; this is not a periodically exported mirror.

- `standalone/`: C# window, native setup/discovery, authenticated updater, defaults and package tooling.
- `src/`, `include/`: DXGI bootstrap and minimal rendering host. Internal UVR names are retained to minimize changes to the tested rendering ABI.
- `vendor/glass/`: glass fix v1.0.1 and offline rendering fixtures, with standalone adaptations.
- `vendor/reshade/`: selected D3D12/DXGI sources and required headers/dependencies, not the full ReShade application.
- `tests/`: installation, discovery, update and graphics checks.
- `tools/`, `scripts/`: shared settings controls, maintainer signing tool and build/development-package helpers.

[Upstream pins](standalone/upstream-lock.json) and [local adaptations](standalone/UPSTREAM.md) explain the imported versions and changes. Preserve upstream notices when updating them. This repository starts from the standalone v0.1.9 source with a standalone-only build and source-build safeguards; it does not promise byte-identical reproduction of the previously published ZIP.

## Support and contributions

For an MSFS issue, first uninstall through this app, then compare with the same official version and mode installed manually. If only this installer’s setup fails, open an issue here or contact me through the WSG Discord linked from [WeSimGlide](https://wesimglide.org/). If the manual official installation also fails, report the graphics issue to [IFreemz](https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix/issues). Never post credentials, signing keys or personal files. Keep antivirus enabled and do not treat a checksum or scan as a safety certification.

Contributions follow the license of the component being changed: GPL-3.0-only plus upstream NOTICE terms for the graphics engine, PolyForm Noncommercial for the separate app. Improvements and reproducible bug reports are welcome.
