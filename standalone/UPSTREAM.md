# Glass v0.8.3 integration

Source: https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix/releases/tag/v0.8.3
Commit: e7f4ff574ae8684f738f10c8d48574e74714830c.

Selected changes: optional dark-screen correction (OFF), UV seam correction (ON), correct selection among extra DLSS passes, desktop frame-overlap fallback, and optional Extra line smoothing (OFF). Normal smoothing remains 35. The frame-overlap fix is desktop-only; upstream has not provided its VR equivalent yet.

The required ReShade D3D12 interception stays built in. No separate ReShade install or menu, capture/report feature, or keyboard shortcuts are added. This package includes no OFXR engine or OpenXR registration; official OFXR remains separate and its settings are untouched.

## Local adaptations

- Preserve guarded enable/session transitions and upscaler-driven desktop/stereo processing.
- Preserve native upscaled pixels until a newly accepted display has current glass residuals, preventing a first-frame flat-glare flash reproduced offline.
- Upstream counts display presents for expiry. This host ticks per upscaler call, so use monotonic time: 2 seconds for largest-output selection and 135 ms for desktop map reuse. Reset output maxima after expiry for resolution changes. The pending-view fallback never applies to stereo maps.
- No unfenced GPU diagnostic readbacks or capture worker/readback subsystem.
- External live controls poll every 250 ms under the recording guard. Green tint uses a temporary event and ends when the control window closes; it is not saved to the INI. Each group has its own reset button.
- Direct Update preserves the whole installed INI and original third-party backups. An interrupted replacement can recover through Update or Uninstall. No separate updater service or version manager.

Offline tests do not establish compatibility with every aircraft or headset. Test official OFXR separately with its own controls. See VALIDATION.md for results and limitations.
