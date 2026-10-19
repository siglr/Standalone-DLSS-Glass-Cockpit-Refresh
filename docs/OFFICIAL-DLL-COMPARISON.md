# Official standalone DLL comparison

Compared custom package v1.0.2 (`f99ea4d`) with IFreemz's official standalone v1.0.1, source `e8a26fa9b99dba6b74213e999f871da933384da0`. This standalone binary is built from a different commit than the v1.0.1 add-on tag.

Official DLL SHA-256: `06f11b0abeb4d3a9b1a14b7ec22faf6de214c8f218353f1e062471015db2a685`.

## Findings

| Area | Custom engine | Official standalone | Assessment |
|---|---|---|---|
| Copied swapchain tables | Per-implementation trampolines fixed the reproduced v1.0.0 CTD | Calls stored original function pointers rather than finding them from the current object's table | Exact old failure does not transfer; normal/copied Present and Present1 probes passed |
| Multiple presentation implementations | Tracks up to 16 implementations independently | One installed flag and original function per hook | More limited coverage if another distinct implementation appears; no crash reproduced |
| DLL initialization | Minimal bootstrap DllMain; defers backend loading to factory exports | DllMain starts logging/settings/threads and installs the device-creation hook | Loader-lock risk worth reviewing; offline import/startup tests pass |
| Private DXGI adapters | Explicit passthrough guard needed by our ReShade wrappers | Native COM interfaces, no corresponding adapter wrapper | Private LUID/preference and D3D11On12 probes passed |
| First accepted screen | Composite keeps native output when there are no current residual samples | That local guard is absent | Reproduced one-frame glare error with Dark screen fix ON; comparison guard fixes it; not a CTD |
| Frame timing | Upscaler-driven fallback and monotonic expiry adaptations | Native Present tracking with worker fallback | Different assumptions; upstream repeated-pass, early-device and stereo tests passed |
| Diagnostics | Unfenced GPU diagnostic readbacks excluded | Capture/cost/probe readbacks wait frame counts rather than queue-fence completion | Potential stale-data/lifetime issue under stalled queues; not reproduced by normal fixtures |
| Settings | External INI + custom tint event | Native INI, popup and hotkeys; re-read once per second | Section mapping must change; transient tint stays in official popup |
| Two settings editors | External UI writes individual keys | Popup writes whole in-memory settings snapshot | Possible lost external edit inside its reload interval; propose reload-before-save or per-key writes |

After normalizing line endings, `display_map.hlsl` and `cockpit_vertex.hlsli` match. `overlay.hlsl` differs in capture instrumentation and our no-current-residual guard. The upstream colour-gain and seam hotfixes are present; this is not a different cockpit algorithm.

## Validation limits

Tests use the actual downloaded DLL, isolated fake simulator processes, stand-in upscalers and real D3D12 rendering on RTX 4090. They do not prove startup compatibility with every third-party mod or exercise a real Pimax/OpenXR session. No simulator/headset installation or OFXR configuration is touched. Additional results and proposed upstream improvements are recorded in `IFREEMZ-ENHANCEMENTS.md`.

The core suite passes, but the additional dark-screen first-recognition regression fails in the official DLL. Do not describe this trial as passing every offline quality gate. The author's defaults are retained, including Dark screen fix OFF. Production packaging remains gated while this issue and the old app-updater payload-layout transition are addressed.
