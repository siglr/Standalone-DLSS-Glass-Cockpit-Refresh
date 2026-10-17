#include "uvr_graphics.h"
#include "uvr_product.h"
#include "d3d12/d3d12_device.hpp"
#include "d3d12/d3d12_command_queue.hpp"
#include "addon_manager.hpp"
#include "dll_resources.hpp"
#include "dll_log.hpp"
#include "runtime_manager.hpp"
#include "hook_manager.hpp"
#include <reshade.hpp>
#include <mutex>
#include <shared_mutex>
#include <atomic>
#include <algorithm>
HMODULE g_module_handle = nullptr;
std::filesystem::path g_reshade_dll_path;
thread_local bool g_in_dxgi_runtime = false;
bool is_windows7() { return false; }
extern "C" bool AddonInit(HMODULE, HMODULE);
extern "C" void UvrGlassTick();
extern "C" void UvrGlassPollSettings(const wchar_t* path);
extern "C" bool UvrGlassEnabled();
extern "C" void UvrGlassSetEnabled(bool value);
extern "C" void UvrGlassPresent(ID3D12Device* device);
namespace {
std::once_flag initialize_once;
std::once_flag glass_once;
std::shared_mutex sessions_mutex;
std::mutex tick_mutex;
// Follow MSFS upscaler calls without a session provider.
std::atomic<bool> active{true};
std::atomic<uint64_t> epoch{1};
bool initialized = false;
}
namespace reshade {
bool addon_all_loaded = true;
std::vector<void*> addon_event_list[static_cast<uint32_t>(addon_event::max)];
std::vector<addon_info> addon_loaded_info;
thread_local const addon_info* addon_current = nullptr;
void load_addons() { std::call_once(glass_once, [] { AddonInit(g_module_handle, g_module_handle); }); }
void unload_addons() {} // Callbacks and hook code remain pinned until process exit.
bool has_loaded_addons() { return true; }
addon_info* find_addon(const void*) { return nullptr; }
void create_effect_runtime(api::swapchain*, api::command_queue*, bool) {}
void destroy_effect_runtime(api::swapchain*) {}
void init_effect_runtime(api::swapchain*) {}
void reset_effect_runtime(api::swapchain*) {}
void present_effect_runtime(api::swapchain*) {}
namespace resources {
data_resource load_data_resource(unsigned short) { return {0, nullptr}; }
}
}
extern "C" bool ReShadeRegisterAddon(void*, uint32_t version) { return version == RESHADE_API_VERSION; }
extern "C" void ReShadeUnregisterAddon(void*) {}
extern "C" void ReShadeRegisterEvent(reshade::addon_event event, void* callback) {
 auto& callbacks = reshade::addon_event_list[static_cast<uint32_t>(event)];
 if (std::find(callbacks.begin(), callbacks.end(), callback) == callbacks.end()) callbacks.push_back(callback);
}
extern "C" void ReShadeRegisterEventForAddon(void*, reshade::addon_event event, void* callback) { ReShadeRegisterEvent(event, callback); }
extern "C" void ReShadeUnregisterEvent(reshade::addon_event, void*) {}
extern "C" void ReShadeUnregisterEventForAddon(void*, reshade::addon_event, void*) {}
extern "C" void ReShadeLogMessage(void*, int level, const char* message) { reshade::log::message(static_cast<reshade::log::level>(level), "%s", message); }
UVR_API bool UvrInitialize() {
 std::call_once(initialize_once, [] {
  wchar_t executable[32768]{}; GetModuleFileNameW(nullptr, executable, 32768);
  if (_wcsicmp(std::filesystem::path(executable).filename().c_str(), L"FlightSimulator2024.exe") != 0) return;
  HMODULE pinned{};
  GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN, reinterpret_cast<LPCWSTR>(&UvrInitialize), &pinned);
  wchar_t path[32768]{}; GetModuleFileNameW(g_module_handle, path, 32768);
  g_reshade_dll_path = path;
  std::error_code ec;
  reshade::log::open_log_file(g_reshade_dll_path.parent_path() / UVR_GRAPHICS_LOG, ec);
  HMODULE system = LoadLibraryExW(L"d3d12.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
  if (!system) return;
  const auto target = GetProcAddress(system, "D3D12CreateDevice");
  initialized = target && reshade::hooks::install("D3D12CreateDevice", reinterpret_cast<void*>(target), reinterpret_cast<void*>(&D3D12CreateDevice));
 });
 return initialized;
}
UVR_API void UvrSetSessionActive(uint64_t, bool) {} // ABI compatibility; no session provider.
UVR_API bool UvrCockpitEnabled() { return UvrGlassEnabled(); }
UVR_API void UvrSetCockpitEnabled(bool value) {
 std::lock_guard lock(sessions_mutex);
 if (value == UvrGlassEnabled()) return;
 epoch.fetch_add(1, std::memory_order_acq_rel);
 UvrGlassSetEnabled(value);
}
UVR_API bool UvrGlassActive() { return active.load(std::memory_order_acquire) && UvrCockpitEnabled(); }
UVR_API void UvrFrameTick() {
 std::lock_guard lock(tick_mutex);
 static ULONGLONG next_settings = 0;
 const auto now = GetTickCount64();
 if (now >= next_settings && !g_reshade_dll_path.empty()) {
  next_settings = now + 250;
  const auto ini = g_reshade_dll_path.parent_path() / UVR_SETTINGS_FILE;
  UvrSetCockpitEnabled(GetPrivateProfileIntW(L"cockpit", L"enabled", 1, ini.c_str()) != 0);
  std::lock_guard settings_lock(sessions_mutex);
  UvrGlassPollSettings(ini.c_str());
 }
 UvrGlassTick();
}
UVR_API ID3D12Device* UvrNativeDevice(ID3D12Device* object) {
 if (!object) return nullptr;
 D3D12Device* proxy{};
 if (SUCCEEDED(object->QueryInterface(__uuidof(D3D12Device), reinterpret_cast<void**>(&proxy)))) {
  auto* native = proxy->_orig; proxy->Release(); return native;
 }
 return object;
}
UVR_API ID3D12CommandQueue* UvrNativeQueue(ID3D12CommandQueue* object) {
 if (!object) return nullptr;
 D3D12CommandQueue* proxy{};
 if (SUCCEEDED(object->QueryInterface(__uuidof(D3D12CommandQueue), reinterpret_cast<void**>(&proxy)))) {
  auto* native = proxy->_orig; proxy->Release(); return native;
 }
 return object;
}
BOOL WINAPI DllMain(HMODULE module, DWORD reason, LPVOID) {
 if (reason == DLL_PROCESS_ATTACH) { g_module_handle = module; DisableThreadLibraryCalls(module); }
 return TRUE;
}

UVR_API FARPROC UvrSystemDxgi(const char* name) {
 static HMODULE system = [] {
  wchar_t path[32768]{};
  const UINT size = GetSystemDirectoryW(path, 32768);
  if (!size || size >= 32768) return HMODULE{};
  return LoadLibraryExW((std::filesystem::path(path) / L"dxgi.dll").c_str(), nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
 }();
 return system ? GetProcAddress(system, name) : nullptr;
}

UVR_API uint64_t UvrSessionEpoch() { return epoch.load(std::memory_order_acquire); }

UVR_API uint64_t UvrAcquireGlassWork() {
 sessions_mutex.lock_shared();
 if (active.load(std::memory_order_acquire) && UvrCockpitEnabled()) return epoch.load(std::memory_order_acquire);
 sessions_mutex.unlock_shared();
 return 0;
}
UVR_API void UvrReleaseGlassWork() { sessions_mutex.unlock_shared(); }

namespace {
thread_local unsigned present_depth = 0;
struct PresentScope {
 explicit PresentScope(IDXGISwapChain* chain, UINT flags) {
  if (present_depth++ || (flags & DXGI_PRESENT_TEST)) return;
  ID3D12Device* device = nullptr;
  if (SUCCEEDED(chain->GetDevice(IID_PPV_ARGS(&device)))) {
   UvrGlassPresent(UvrNativeDevice(device));
   device->Release();
  }
 }
 ~PresentScope() { --present_depth; }
};
HRESULT STDMETHODCALLTYPE GlassPresent(IDXGISwapChain* chain, UINT interval, UINT flags) {
 const auto original = reshade::hooks::call(GlassPresent, reshade::hooks::vtable_from_instance(chain) + 8);
 PresentScope scope(chain, flags);
 return original(chain, interval, flags);
}
HRESULT STDMETHODCALLTYPE GlassPresent1(IDXGISwapChain1* chain, UINT interval, UINT flags, const DXGI_PRESENT_PARAMETERS* parameters) {
 const auto original = reshade::hooks::call(GlassPresent1, reshade::hooks::vtable_from_instance(chain) + 22);
 PresentScope scope(chain, flags);
 return original(chain, interval, flags, parameters);
}
}
void UvrObserveSwapchain(IDXGISwapChain* chain) {
 if (!chain) return;
 static std::mutex mutex;
 std::lock_guard lock(mutex);
 reshade::hooks::install("Glass frame boundary: Present", reshade::hooks::vtable_from_instance(chain), 8, reinterpret_cast<void*>(&GlassPresent));
 IDXGISwapChain1* newer = nullptr;
 if (SUCCEEDED(chain->QueryInterface(IID_PPV_ARGS(&newer)))) {
  reshade::hooks::install("Glass frame boundary: Present1", reshade::hooks::vtable_from_instance(newer), 22, reinterpret_cast<void*>(&GlassPresent1));
  newer->Release();
 }
}
