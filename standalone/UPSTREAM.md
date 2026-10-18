# Glass v1.0.0 integration

Source: https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix/releases/tag/v1.0.0
Commit: 9040e14ab26f6d8f48f53c05c7f4fb0ec7e805fa.

Modified/unofficial Standalone Glass Graphics, based on Glass Cockpit DLSS Fix by IFreemz (https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix).

Imported the v1.0.0 shaders, per-mesh brightness variants, 32-entry descriptor ring, repeated desktop upscale recovery, VR exhausted-view recovery, stable edges, full-size sampling, glass filtering, optional temporal and Gaussian smoothing, depth occlusion, texture replacement resets, wrapped UV/iFly draw handling and NGX hook reattachment.

Author defaults are retained: smoothing 35, strength 100, normal jitter; seam fix/stable edges/full-size sampling/occlusion ON; other experimental options OFF or zero. Existing INI files are preserved; new keys fall back to these defaults. The window exposes the settings and resets without a ReShade UI. Temporal and soft smoothing are mutually exclusive.

## Local adaptations

- Minimal ReShade D3D12/DXGI host remains built in. No OFXR engine, OpenXR registration, OFXR settings access or keyboard shortcuts.
- Standalone v1.0.1 attaches Present/Present1 through per-implementation function trampolines instead of changing swapchain vtables. A private copy of the table made by another graphics component no longer breaks the original-function lookup. The detours preserve the original result, test-present exclusion and nested-frame guard. Unsupported additional implementations are left untouched.
- Settings polling and maintenance run from both upscaler calls and native presents, so late-loaded or displaced NGX hooks can be found again. Native Present/Present1 provides actual frame boundaries for v1.0.0 VR view tracking. Test presents do not count; nested Present calls count once. Native swapchains are retained.
- Guarded enable epochs retained. Temporal history is invalidated across enable epochs and after a 135 ms gap. Largest-output selection expires after two seconds; desktop map reuse expires after 135 ms. VR recovery uses the author's eight-present window, so both eyes share the same frame lifetime even during slow frames.
- Preserve native upscaled pixels until current glass residuals exist, avoiding the first accepted frame's flat-glare flash.
- No capture/report/pixel-probe export, capture shaders or unfenced diagnostic readbacks in the built engine. Original upstream panel/build documentation is retained as reference and is not part of the embedded control app.
- External controls poll every 250 ms under the recording guard. Green tint is a temporary event, never a persisted key binding.
- Direct Update preserves the whole installed INI and original uninstall backups. Engine license and corresponding source are documented in ENGINE-LICENSE.md.

Offline tests cannot establish every aircraft/headset combination; real MSFS/VR validation is still needed. See VALIDATION.md.

## Further optimization work

The upstream descriptor ring is now 32 entries. Explicit GPU completion tracking would be needed before safely recycling a smaller ring or freeing retired temporal histories during repeated resolution changes. Temporal history is optional and allocates two output-resolution slices per view (24 bytes per output pixel per view); keep it off by default. These are follow-up opportunities, not a change to OFXR's Lower VRAM setting or a promise of lower VRAM usage in this release.
