// Offline device/resource diagnostics. Never copied into the simulator.
#include <Windows.h>
#include <d3d12.h>
#include <d3d12sdklayers.h>
#include <dxgi1_6.h>
#include <dxgidebug.h>
#include <wrl/client.h>
#include <cstdio>
#include <filesystem>
#include <stdexcept>
#include <string>
#include <vector>

using Microsoft::WRL::ComPtr;
static void check(HRESULT hr, const char* operation) {
    std::printf("%s: 0x%08lx\n", operation, static_cast<unsigned long>(hr));
    if (FAILED(hr)) throw std::runtime_error(operation);
}
static void dump(ID3D12InfoQueue* queue) {
    if (!queue) return;
    for (UINT64 i = 0; i < queue->GetNumStoredMessages(); ++i) {
        SIZE_T size = 0;
        queue->GetMessage(i, nullptr, &size);
        std::vector<char> bytes(size);
        auto* message = reinterpret_cast<D3D12_MESSAGE*>(bytes.data());
        if (SUCCEEDED(queue->GetMessage(i, message, &size)))
            std::printf("D3D12 id=%u severity=%u: %s\n", message->ID, message->Severity, message->pDescription);
    }
    queue->ClearStoredMessages();
}

int main(int argc, char** argv) {
    // Flush diagnostics even if driver/proxy code terminates the process.
    std::setvbuf(stdout, nullptr, _IONBF, 0);
    try {
        if (argc < 3) throw std::runtime_error("usage: probe <native|wrapped-native-adapter|wrapped-adapter|bootstrap> <backend.dll> [--no-debug]");
        const std::string mode = argv[1];
        if (mode != "native" && mode != "wrapped-native-adapter" && mode != "wrapped-adapter" && mode != "bootstrap")
            throw std::runtime_error("unknown probe mode");
        const bool debug_enabled = !(argc > 3 && std::string(argv[3]) == "--no-debug");
        std::printf("mode=%s debug=%d\n", mode.c_str(), debug_enabled);
        ComPtr<ID3D12Debug> debug;
        if (debug_enabled) {
            check(D3D12GetDebugInterface(IID_PPV_ARGS(&debug)), "D3D12 debug layer (install Windows Graphics Tools if missing)");
            debug->EnableDebugLayer();
        }
        wchar_t system_dir[MAX_PATH]{};
        if (!GetSystemDirectoryW(system_dir, MAX_PATH)) throw std::runtime_error("system directory unavailable");
        const auto system_dxgi = LoadLibraryExW((std::filesystem::path(system_dir) / L"dxgi.dll").c_str(), nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
        if (!system_dxgi) throw std::runtime_error("system DXGI unavailable");
        auto create_factory = reinterpret_cast<decltype(&CreateDXGIFactory1)>(GetProcAddress(system_dxgi, "CreateDXGIFactory1"));
        if (!create_factory) throw std::runtime_error("system factory export unavailable");
        ComPtr<IDXGIFactory6> native_factory;
        check(create_factory(IID_PPV_ARGS(&native_factory)), "native factory");

        HMODULE backend = nullptr;
        if (mode != "native") {
            backend = LoadLibraryExW(std::filesystem::absolute(argv[2]).c_str(), nullptr,
                LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
            if (!backend) throw std::runtime_error("backend load failed");
            create_factory = mode == "bootstrap" ? &CreateDXGIFactory1 :
                reinterpret_cast<decltype(&CreateDXGIFactory1)>(GetProcAddress(backend, "CreateDXGIFactory1"));
            if (!create_factory) throw std::runtime_error("backend factory export unavailable");
        }
        ComPtr<IDXGIFactory6> factory;
        check(create_factory(IID_PPV_ARGS(&factory)), "selected factory");
        ComPtr<IDXGIAdapter1> adapter;
        check(factory->EnumAdapterByGpuPreference(0, DXGI_GPU_PREFERENCE_HIGH_PERFORMANCE, IID_PPV_ARGS(&adapter)), "selected adapter");
        DXGI_ADAPTER_DESC1 desc{};
        check(adapter->GetDesc1(&desc), "adapter description");
        std::printf("adapter=%ls vendor=%04x device=%04x vram=%llu luid=%08lx:%08lx\n", desc.Description,
            desc.VendorId, desc.DeviceId, static_cast<unsigned long long>(desc.DedicatedVideoMemory),
            static_cast<unsigned long>(desc.AdapterLuid.HighPart), desc.AdapterLuid.LowPart);
        if (mode == "wrapped-native-adapter") {
            adapter.Reset();
            check(native_factory->EnumAdapterByLuid(desc.AdapterLuid, IID_PPV_ARGS(&adapter)), "matching native adapter");
        }
        ComPtr<ID3D12Device> device;
        check(D3D12CreateDevice(adapter.Get(), D3D_FEATURE_LEVEL_12_0, IID_PPV_ARGS(&device)), "device FL12_0");
        auto* native = device.Get();
        if (backend) {
            const auto unwrap = reinterpret_cast<ID3D12Device*(*)(ID3D12Device*)>(GetProcAddress(backend, "UvrNativeDevice"));
            if (!unwrap) throw std::runtime_error("device unwrap export unavailable");
            native = unwrap(device.Get());
            if (native == device.Get()) throw std::runtime_error("integrated path did not return a wrapped device");
        }
        ComPtr<ID3D12InfoQueue> queue;
        if (debug_enabled) check(native->QueryInterface(IID_PPV_ARGS(&queue)), "device debug queue");
        D3D12_FEATURE_DATA_SHADER_MODEL shader{D3D_SHADER_MODEL_6_6};
        check(native->CheckFeatureSupport(D3D12_FEATURE_SHADER_MODEL, &shader, sizeof(shader)), "shader model query");
        std::printf("shader_model=0x%x\n", shader.HighestShaderModel);
        if (shader.HighestShaderModel < D3D_SHADER_MODEL_6_6) throw std::runtime_error("cockpit requires SM6.6");
        D3D12_FEATURE_DATA_FORMAT_SUPPORT format{DXGI_FORMAT_R11G11B10_FLOAT};
        check(native->CheckFeatureSupport(D3D12_FEATURE_FORMAT_SUPPORT, &format, sizeof(format)), "R11G11B10 support");
        std::printf("R11G11B10 support1=0x%x support2=0x%x\n", format.Support1, format.Support2);

        // Same invalid clear-format combination as the saved fixture; both the
        // native device and wrapper must reject it, then accept the corrected one.
        for (auto* resource_device : {native, device.Get()}) {
            std::printf("resource_path=%s\n", resource_device == native ? "native" : "wrapped");
            D3D12_HEAP_PROPERTIES heap{D3D12_HEAP_TYPE_DEFAULT};
            D3D12_RESOURCE_DESC resource{D3D12_RESOURCE_DIMENSION_TEXTURE2D, 0, 1280, 720, 2, 1,
                DXGI_FORMAT_D32_FLOAT, {1, 0}, D3D12_TEXTURE_LAYOUT_UNKNOWN, D3D12_RESOURCE_FLAG_ALLOW_DEPTH_STENCIL};
            D3D12_CLEAR_VALUE clear{DXGI_FORMAT_D24_UNORM_S8_UINT};
            clear.DepthStencil.Depth = 1;
            ComPtr<ID3D12Resource> texture;
            const HRESULT rejected = resource_device->CreateCommittedResource(&heap, D3D12_HEAP_FLAG_NONE, &resource,
                D3D12_RESOURCE_STATE_DEPTH_WRITE, &clear, IID_PPV_ARGS(&texture));
            std::printf("mismatched_clear=0x%08lx (expected E_INVALIDARG)\n", static_cast<unsigned long>(rejected));
            dump(queue.Get());
            if (rejected != E_INVALIDARG || texture) throw std::runtime_error("invalid clear-format negative control failed");
            clear.Format = DXGI_FORMAT_D32_FLOAT;
            const HRESULT valid = resource_device->CreateCommittedResource(&heap, D3D12_HEAP_FLAG_NONE, &resource,
                D3D12_RESOURCE_STATE_DEPTH_WRITE, &clear, IID_PPV_ARGS(&texture));
            dump(queue.Get());
            check(valid, "matching D32 clear");
        }
        std::puts("PROBE PASSED");
        return 0;
    } catch (const std::exception& e) {
        std::printf("PROBE FAILED: %s\n", e.what());
        return 1;
    }
}
