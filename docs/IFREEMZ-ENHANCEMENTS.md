# Review for IFreemz — standalone v1.0.1

The implemented fixes and tint-control enhancement are submitted in [upstream PR #29](https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix/pull/29), from the siglr fork. The follow-up concerns below remain investigations. It compares the official standalone binary from source commit `e8a26fa9b99dba6b74213e999f871da933384da0` with the custom engine previously shipped in Standalone DLSS Glass Cockpit Refresh v1.0.2. The new test package uses your official DLL byte-for-byte.

Thank you for making the standalone DLL available. I would like to use the official binary with a separate installer/settings app, and share a few observations from migrating away from my custom host. Some are potential hardening opportunities rather than reproduced bugs.

## 1. Settings changes from two windows

In `src/glass_cockpit_dlss_fix.cpp`, `reload_settings()` polls file changes once per second. `change_settings()` saves the full in-memory settings snapshot without first checking for an external edit. If the external app changes one setting, then the original popup changes another before that poll, the full save can overwrite the first edit.

Suggested improvement: under `settings_mutex`, check for and load pending INI edits before applying the user's change; alternatively, save only the fields changed by that operation. Keep the existing generation change when re-enabling. A small documented cross-process settings API would also be useful, but is not necessary for an INI-based solution. A test should edit line smoothing externally, immediately change another field through the popup, and check that both survive.

## 2. GPU diagnostic completion

`capture_ready_at`, `capture_flush()`, cost timing and pixel-probe paths use elapsed frame counts before mapping readback resources. Ten or thirty presents are a useful delay but do not prove completion of the queue that submitted the copies/queries, especially with a stalled queue, multiple queues, or the worker's frame-clock fallback.

Suggested improvement: signal a D3D12 fence on the submitting queue, retain the readback resources/command allocators until that value completes, and poll completion without blocking the render thread. On device removal, abandon the diagnostic safely. The former custom host avoided this risk by omitting those readbacks; that omission is not a proposed replacement for your useful diagnostic tools.

This is a code-review concern. The ordinary rendering fixtures do not simulate a permanently stalled GPU queue and cannot establish safety under that condition.

## 3. Loader-lock work

`src/dxgi_proxy.cpp::DllMain` calls `glass::start()`, initializes the settings-window callback, and installs the device-creation hook. `start()` does filesystem/settings/logging work and creates background threads. My bootstrap deferred heavier initialization until a DXGI factory export was called.

Suggested investigation: reduce loader-lock work while retaining the requirement to intercept device creation early. Deferring everything blindly could miss D3D12CreateDevice, so this needs startup-order tests rather than simply moving all initialization into a worker. Current import/startup probes did not reproduce a deadlock.

## 4. More than one presentation implementation

`install_present_hooks()` installs once and stores one `original_present`/`original_present1`. The custom host tracks each distinct target implementation with its own trampoline (bounded to 16). That supports native/proxy swapchains with different implementations.

Suggested improvement: track successfully hooked target addresses and use a per-target trampoline. Publish successful installation only after hooks have been enabled, so a failed first attempt can be retried. This is a compatibility improvement, not the copied-vtable crash: your stored function pointers already avoid that specific lookup failure, and the copied-table tests passed.

## 5. Reproduced: newly accepted dark display loses glare for one frame

The custom `overlay.hlsl` composite has `if (count == 0) return;` before applying the glass residual. It leaves the native upscaler result in place for the first accepted screen frame when fresh residual samples are unavailable. Your version does not contain that local guard.

Reproduction: use a mostly dark display with sparse light lines, strong spatial glare, and **Dark screen fix ON**. Enable the fixture's real INI loader (`GLASS_FIXTURE_SETTINGS=1`). On the first frame where the display is accepted, its glass residual has not been fitted yet. The composite replaces the valid upscaler picture with a poor approximation of the glare. This is a transient rendering regression, not a startup crash.

Measured normalized interior error (lower is better; the existing fixture threshold is 0.02):

| Case | Official v1.0.1 DLL | Rebuilt unchanged source | Same source with guard |
|---|---:|---:|---:|
| Desktop, first accepted screen | 0.587132 | 0.587132 | 0.007674 |
| Stereo, first accepted screen/eye | 0.586385 | 0.586385 | 0.007680 |

Both the official binary and the rebuilt unmodified source fail. The comparison build with the guard passes desktop and stereo, then resumes replacing ghosted display content on subsequent frames. The default **Dark screen fix OFF** passes the bright-glare test. This narrows the demonstrated issue to that experimental setting and scenario; it is not evidence that every cockpit flashes.

The proposed one-line guard is a good candidate for a focused PR with the regression fixture. The comparison build is separate and is **not** bundled with the installer. The official DLL remains unchanged.

Reproduction script in the wrapper repository: `tests/official_fixture_regressions.py`. It derives an isolated test host from the pinned upstream fixture, changes the display pattern/glare, and asserts the initial-frame quality instead of treating all warmup frames as informational. It also verifies real INI enable/resume behavior. It never patches the official DLL.

## 6. External green-tint control

The official engine currently exposes tint only to its own window, ReShade tab and hotkey. A separate installer/settings window cannot use those in-process functions to control the simulator. Loading a second copy of the DLL in the installer would not control the engine in MSFS.

The contribution branch adds a small, versioned Windows event protocol scoped to the simulator PID and the current Windows session. The engine advertises support; one controller owns a mutex and a manual-reset event carries the shared tint state. Both the native UI and the external app can turn it off or on. The engine clears an observed external preview when its owner closes or crashes, without persisting debug tint in the INI. No simulated keys, DLL injection, arbitrary commands or permissive security ACLs are used.

The contract and client sequence are in `docs/EXTERNAL-CONTROLS.md` in the fork. Real child-process tests cover clean exit, abnormal termination, reconnect, target isolation and both directions of control. The separate wrapper should enable its checkbox only when this capability is present; published v1.0.1 does not have it.

The proposed PR implements the reproduced glare fix, pending-settings merge (including changed experimental fields), and this optional tint protocol. The loader-lock, GPU completion and multiple-presentation-implementation items remain review investigations, not claimed fixes.

## Checks already completed

- Unchanged official DLL hash verified against the published release.
- Normal and copied-table Present/Present1 startup probes passed.
- Private adapter lookup by LUID/preference and D3D11On12 creation passed.
- All 16 core desktop/VR/upscaler fixture scenarios passed, including redirected DLSS, OptiScaler, nested upscaling, repeated FG upscales and early device creation.
- All 13 existing motion/occlusion/brightness/history/texture-replacement measurements passed the custom package's quality assertions.
- External-INI disable/re-enable passed in desktop and stereo; independent OFXR settings were unchanged in installer tests.
- The upstream capture/pixel-probe fixture passed with the actual official DLL (fit input, draw input, screen map and probe hits saved). This ordinary synchronized fixture does not resolve the stalled-queue concern above.
- The one-line guarded comparison build also passed all 13 motion/occlusion/brightness/history/texture-replacement quality measurements.
- The additional first-recognition glare test found the reproducible experimental-setting regression above. This prevents claiming that every offline check passed.

These are offline results on an RTX 4090, not confirmation of every MSFS/VR/overlay combination. PR #29 contains the tested source changes, protocol documentation and reproduction tests. No separate issue or message has been posted.

Final contribution checks (2026-10-18): both DLL variants compiled; the standalone passed two unit suites, four startup probes, 16 core rendering cases, capture/pixel probe, two external-tint rendering cases, four glare regressions and 13 motion-quality measurements. The ReShade add-on runtime suite was not run; no real MSFS/VR flight was performed. Your installed packages and OFXR settings were not changed.

## PR review follow-up

Both automated P2 findings were reproduced and fixed in fork commit `e912697c311dad7cc0b0a749606e93b53e0f334d`. The shader guard now uses usable residual weight after the outline fallback, preserving valid outline-only samples. The tint setter clears stale attachment so a native request made immediately after controller exit survives the next poll. New GPU and cross-process regressions failed before each fix and pass afterwards; the full standalone suite and 13 motion checks also passed. [Fix summary and renewed review request](https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix/pull/29#issuecomment-6258279932). No installed packages or OFXR settings changed.

The follow-up Codex review completed on `e912697` with no findings: https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix/pull/29#issuecomment-6258383984. PR #29 remains open; none of its fixes are in the bundled official v1.0.1 DLL.

IFreemz closed PR #29 without merging: https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix/pull/29#issuecomment-6261581124. He cannot test broader changes at present and declined external tint control. The contribution remains in the fork; the installer continues using unmodified official releases and points to the original tint button/hotkey.
