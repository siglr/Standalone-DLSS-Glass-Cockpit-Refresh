# License and attribution boundaries

Required Notice: Copyright (c) 2026 siglr (https://github.com/siglr)

Original standalone application, installer, host integration, build scripts, tests and documentation outside `vendor/` are offered under **PolyForm Noncommercial License 1.0.0**, in LICENSE.md. This permits the noncommercial uses, modifications and redistribution specified by that license. Commercial use of that original code is not granted. This is source-available software, not OSI open-source software.

Third-party code is **not relicensed** under PolyForm. Files under `vendor/`, including retained local adaptations in those files, remain under their existing upstream terms and notices. Those licenses can permit commercial reuse of those components separately; that does not grant commercial rights to the original standalone application code.

| Component | Upstream | License location |
|---|---|---|
| Glass Cockpit DLSS Fix v0.8.3 | IFreemz | vendor/glass/LICENSE (MIT) |
| ReShade D3D12 subset | Patrick Mours / crosire | vendor/reshade/LICENSE.md (BSD-3-Clause; preserve file-specific notices) |
| DirectX-Headers | Microsoft | vendor/reshade/deps/d3d12/LICENSE (MIT) |
| D3D12On7 header | Microsoft | vendor/reshade/deps/d3d12on7/LICENSE.txt |
| MinHook | Tsuda Kageyu and contributors | vendor/reshade/deps/minhook/LICENSE.txt (BSD-2-Clause and embedded notices) |
| UTF8-CPP | Nemanja Trifunovic | vendor/reshade/deps/utfcpp/LICENSE (Boost Software License) |
| Dear ImGui headers | Omar Cornut and contributors | vendor/glass/third_party/imgui/LICENSE.txt (MIT) |

Preserve all applicable copyright, license and attribution notices in forks and distributions. The root license does not imply third-party endorsement or grant rights to upstream names, logos or trademarks. Original upstream identities and pins are recorded in standalone/upstream-lock.json and standalone/UPSTREAM.md. The selected source is vendored directly; a normal clone does not require private dependencies or submodule access.

The original glass add-on's THIRD_PARTY_NOTICES.md is retained for attribution. Its submodule paths describe that upstream project; in this repository the dependency paths are those listed above. No OFXR code is included. OFXR may be used separately by users under its own terms.
