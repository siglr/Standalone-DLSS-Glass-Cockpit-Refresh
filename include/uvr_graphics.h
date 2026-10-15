#pragma once
#include <Windows.h>
#include <d3d12.h>
#include <stdint.h>
#ifdef UVR_GRAPHICS_EXPORTS
#define UVR_API extern "C" __declspec(dllexport)
#else
#define UVR_API extern "C" __declspec(dllimport)
#endif
// No external add-ons, effects runtime, OpenXR runtime, or SteamVR client.
UVR_API bool UvrInitialize();
UVR_API void UvrSetSessionActive(uint64_t session, bool active);
UVR_API bool UvrGlassActive();
UVR_API void UvrFrameTick();
UVR_API bool UvrCockpitEnabled();
UVR_API void UvrSetCockpitEnabled(bool enabled);
UVR_API ID3D12Device* UvrNativeDevice(ID3D12Device* device);
UVR_API ID3D12CommandQueue* UvrNativeQueue(ID3D12CommandQueue* queue);

UVR_API FARPROC UvrSystemDxgi(const char* name);

UVR_API uint64_t UvrSessionEpoch();

// A successful acquire pins the active CPU recording epoch until release on
// the same thread. Session transitions take the exclusive side of this lock.
UVR_API uint64_t UvrAcquireGlassWork();
UVR_API void UvrReleaseGlassWork();
class UvrGlassWorkGuard {
public:
    UvrGlassWorkGuard() : epoch(UvrAcquireGlassWork()) {}
    ~UvrGlassWorkGuard() { if (epoch) UvrReleaseGlassWork(); }
    UvrGlassWorkGuard(const UvrGlassWorkGuard&) = delete;
    UvrGlassWorkGuard& operator=(const UvrGlassWorkGuard&) = delete;
    const uint64_t epoch;
};
