// Early entry point. All heavy initialization runs after leaving DllMain.
#include <Windows.h>
#include "uvr_product.h"
#include <filesystem>
static HMODULE self;
static FARPROC backend(const char* name) {
 static HMODULE module = [] {
  wchar_t path[32768]{};
  if (!GetModuleFileNameW(self, path, 32768)) return HMODULE{};
  return LoadLibraryExW((std::filesystem::path(path).parent_path()/UVR_GRAPHICS_DLL).c_str(), nullptr,
      LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
 }();
 return module ? GetProcAddress(module, name) : nullptr;
}
extern "C" HRESULT WINAPI CreateDXGIFactory(REFIID id, void** output) {
 auto fn=reinterpret_cast<HRESULT(WINAPI*)(REFIID,void**)>(backend("CreateDXGIFactory"));
 return fn ? fn(id,output) : E_FAIL;
}
extern "C" HRESULT WINAPI CreateDXGIFactory1(REFIID id, void** output) {
 auto fn=reinterpret_cast<HRESULT(WINAPI*)(REFIID,void**)>(backend("CreateDXGIFactory1"));
 return fn ? fn(id,output) : E_FAIL;
}
extern "C" HRESULT WINAPI CreateDXGIFactory2(UINT flags, REFIID id, void** output) {
 auto fn=reinterpret_cast<HRESULT(WINAPI*)(UINT,REFIID,void**)>(backend("CreateDXGIFactory2"));
 return fn ? fn(flags,id,output) : E_FAIL;
}
extern "C" HRESULT WINAPI DXGIGetDebugInterface1(UINT flags, REFIID id, void** output) {
 auto fn=reinterpret_cast<HRESULT(WINAPI*)(UINT,REFIID,void**)>(backend("DXGIGetDebugInterface1"));
 return fn ? fn(flags,id,output) : E_NOINTERFACE;
}
extern "C" HRESULT WINAPI DXGIDeclareAdapterRemovalSupport() {
 auto fn=reinterpret_cast<HRESULT(WINAPI*)()>(backend("DXGIDeclareAdapterRemovalSupport"));
 return fn ? fn() : E_FAIL;
}
BOOL WINAPI DllMain(HMODULE module, DWORD reason, LPVOID) {
 if(reason==DLL_PROCESS_ATTACH) { self=module; DisableThreadLibraryCalls(module); }
 return TRUE;
}
