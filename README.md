# Standalone DLSS Glass Cockpit Refresh

Glass Cockpit DLSS Fix for **Microsoft Flight Simulator 2024**, with the required ReShade D3D12 components built in and a small install/uninstall/settings window. Desktop and VR; no separate ReShade installation or menu is needed. It does not include frame generation. Official OFXR Bridge can be installed separately.

**Separate licenses:** the installer/settings/updater app is source-available under [PolyForm Noncommercial 1.0.0](LICENSE.md). The combined graphics engine is **GPL-3.0-only**, including this project's engine changes, with the upstream NOTICE terms. See [engine licensing and corresponding source](ENGINE-LICENSE.md) and [attribution](NOTICE.md).

Version **1.0.1** integrates **Glass Cockpit DLSS Fix by IFreemz v1.0.0** as a modified, unofficial **Standalone Glass Graphics** build. It fixes a standalone v1.0.0 startup crash when another graphics component copies a swapchain's method table. The upstream rendering corrections, per-mesh brightness, depth-based occlusion, experimental controls and author's defaults are retained. The separate official OFXR Bridge and its settings are untouched.

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
- `vendor/glass/`: glass fix v1.0.0 and offline rendering fixtures, with standalone adaptations.
- `vendor/reshade/`: selected D3D12/DXGI sources and required headers/dependencies, not the full ReShade application.
- `tests/`: installation, discovery, update and graphics checks.
- `tools/`, `scripts/`: shared settings controls, maintainer signing tool and build/development-package helpers.

[Upstream pins](standalone/upstream-lock.json) and [local adaptations](standalone/UPSTREAM.md) explain the imported versions and changes. Preserve upstream notices when updating them. This repository starts from the standalone v0.1.9 source with a standalone-only build and source-build safeguards; it does not promise byte-identical reproduction of the previously published ZIP.

## Support and contributions

For an MSFS issue, first compare with the original add-on installed/configured through ReShade, with this package uninstalled. If only this package fails, open an issue here or contact me through the WSG Discord linked from [WeSimGlide](https://wesimglide.org/). Never post credentials, signing keys or personal files. Keep antivirus enabled and do not treat a checksum or scan as a safety certification.

Contributions follow the license of the component being changed: GPL-3.0-only plus upstream NOTICE terms for the graphics engine, PolyForm Noncommercial for the separate app. Improvements and reproducible bug reports are welcome.
