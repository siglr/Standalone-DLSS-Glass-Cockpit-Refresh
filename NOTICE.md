# License and attribution boundaries

Required Notice: Copyright (c) 2026 siglr (https://github.com/siglr)

The separate original C# application, installer, updater and their support code remain under **PolyForm Noncommercial 1.0.0**, in LICENSE.md. The **combined graphics engine is GPL-3.0-only**, including the host integration and local engine changes, with the upstream section 7 terms. See [ENGINE-LICENSE.md](ENGINE-LICENSE.md) for the explicit file boundary, commercial redistribution rights for the engine, and corresponding-source delivery.

Based on **Glass Cockpit DLSS Fix by IFreemz**: https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix. **Standalone Glass Graphics is a modified, unofficial build**, not an IFreemz release or endorsement. Package support: https://github.com/siglr/Standalone-DLSS-Glass-Cockpit-Refresh/issues.

Third-party licenses and copyright notices remain in force for their respective components.

| Component | Upstream | License location |
|---|---|---|
| Glass Cockpit DLSS Fix v1.0.0 | IFreemz | vendor/glass/LICENSE (GPL-3.0-only) and vendor/glass/NOTICE.md |
| ReShade D3D12 subset | Patrick Mours / crosire | vendor/reshade/LICENSE.md (BSD-3-Clause; preserve file-specific notices) |
| DirectX-Headers | Microsoft | vendor/reshade/deps/d3d12/LICENSE (MIT) |
| D3D12On7 header | Microsoft | vendor/reshade/deps/d3d12on7/LICENSE.txt |
| MinHook | Tsuda Kageyu and contributors | vendor/reshade/deps/minhook/LICENSE.txt (BSD-2-Clause and embedded notices) |
| UTF8-CPP | Nemanja Trifunovic | vendor/reshade/deps/utfcpp/LICENSE (Boost Software License) |
| Dear ImGui headers | Omar Cornut and contributors | vendor/glass/third_party/imgui/LICENSE.txt (MIT) |

Preserve all applicable copyright, license and attribution notices in forks and distributions. The root license does not imply third-party endorsement or grant rights to upstream names, logos or trademarks. Original upstream identities and pins are recorded in standalone/upstream-lock.json and standalone/UPSTREAM.md. The selected source is vendored directly; a normal clone does not require private dependencies or submodule access.

The original glass add-on's THIRD_PARTY_NOTICES.md is retained for attribution. Its submodule paths describe that upstream project; in this repository the dependency paths are those listed above. No OFXR code is included. OFXR may be used separately by users under its own terms.

The WeSimGlide logo and app icon in `standalone/assets/` are existing project branding supplied by the maintainer. Their inclusion does not grant trademark rights or permission to imply that a fork is an official WeSimGlide release.
