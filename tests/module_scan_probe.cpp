// Run the production scanner against fake module APIs. Any attempt to open
// the protected late-loaded aircraft file fails this test, even with no log.
#include <algorithm>
#include <cstdint>
#include <cwctype>
#include <filesystem>
#include <mutex>
#include <set>
#include <string>
#include <stdexcept>
#include <cstdio>
using DWORD = unsigned long;
using HMODULE = uintptr_t;
constexpr int MAX_PATH = 260;
uint64_t init_tick = 0;
std::mutex info_mutex;
std::string dlss_version;
int protected_reads = 0, relevant_reads = 0, late_logs = 0;
bool late_loaded = false;
uint64_t GetTickCount64() { return late_loaded ? 61000 : 0; }
int GetCurrentProcess() { return 0; }
bool EnumProcessModules(int, HMODULE* modules, size_t, DWORD* needed) {
    modules[0] = 1; modules[1] = 2;
    *needed = DWORD((late_loaded ? 2 : 1) * sizeof(HMODULE)); return true;
}
DWORD GetModuleFileNameW(HMODULE module, wchar_t* path, DWORD) {
    const std::wstring name = module == 1 ? L"C:\\fixture\\nvngx_dlss.dll" : L"C:\\fixture\\protected_aircraft.dll";
    std::copy(name.begin(), name.end(), path); path[name.size()] = 0; return DWORD(name.size());
}
std::string file_version(const wchar_t* path) {
    if (std::wstring(path).find(L"protected_aircraft") != std::wstring::npos) { ++protected_reads; throw std::runtime_error("opened protected aircraft DLL"); }
    ++relevant_reads; return "test-version";
}
std::wstring file_company(const wchar_t* path) { file_version(path); return L"test-company"; }
void log(const char* message, ...) { if (std::string(message).starts_with("module loaded later:")) ++late_logs; }
#include "module_scan.inc"
int main() {
    try {
        scan_modules(); late_loaded = true; scan_modules(); scan_modules();
        if (protected_reads || relevant_reads != 1 || late_logs != 1 || dlss_version != "test-version")
            throw std::runtime_error("scanner behavior mismatch");
        std::puts("PASS late protected DLL is never opened; known DLSS metadata and deduplication retained"); return 0;
    } catch (const std::exception& e) { std::puts(e.what()); return 1; }
}
