// Reproduces the private adapter request made by D3D11On12 during VR entry.
// Unknown COM interfaces must pass through without IDXGIAdapter vtable calls.
#include <Windows.h>
#include <dxgi1_6.h>
#include <d3d12.h>
#include <d3d11on12.h>
#include <wrl/client.h>
#include <cstdio>
#include <filesystem>
#include <stdexcept>
using Microsoft::WRL::ComPtr;
static constexpr GUID private_adapter = {0x7abb6563, 0x02bc, 0x47c4, {0x8e, 0xf9, 0xac, 0xc4, 0x79, 0x5e, 0xdb, 0xcf}};
static HRESULT enumerate(IDXGIFactory6* factory, LUID luid, bool preference, IUnknown** result, DWORD* exception) {
    __try {
        return preference ? factory->EnumAdapterByGpuPreference(0, DXGI_GPU_PREFERENCE_HIGH_PERFORMANCE, private_adapter, reinterpret_cast<void**>(result)) :
            factory->EnumAdapterByLuid(luid, private_adapter, reinterpret_cast<void**>(result));
    } __except ((*exception = GetExceptionCode()), EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
}
static HRESULT create_on12(ID3D12Device* device, IUnknown* queue, ID3D11Device** result, DWORD* exception) {
    __try {
        return D3D11On12CreateDevice(device, 0, nullptr, 0, &queue, 1, 0, result, nullptr, nullptr);
    } __except ((*exception = GetExceptionCode()), EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
}
int main(int argc, char** argv) {
    std::setvbuf(stdout, nullptr, _IONBF, 0);
    if (argc != 3) return 1;
    const bool wrapped = std::string(argv[1]) == "wrapped";
    HMODULE module = wrapped ? LoadLibraryExW(std::filesystem::absolute(argv[2]).c_str(), nullptr,
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS) : LoadLibraryExW(L"dxgi.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
    if (!module) return 1;
    auto create = reinterpret_cast<decltype(&CreateDXGIFactory1)>(GetProcAddress(module, "CreateDXGIFactory1"));
    ComPtr<IDXGIFactory6> factory;
    if (!create || FAILED(create(IID_PPV_ARGS(&factory)))) return 1;
    ComPtr<IDXGIAdapter1> adapter;
    if (FAILED(factory->EnumAdapterByGpuPreference(0, DXGI_GPU_PREFERENCE_HIGH_PERFORMANCE, IID_PPV_ARGS(&adapter)))) return 1;
    DXGI_ADAPTER_DESC1 desc{};
    if (FAILED(adapter->GetDesc1(&desc))) return 1;
    for (bool preference : {false, true}) {
        ComPtr<IUnknown> result;
        DWORD exception{};
        const auto hr = enumerate(factory.Get(), desc.AdapterLuid, preference, &result, &exception);
        std::printf("%s %s: hr=%08lx exception=%08lx pointer=%p\n", wrapped ? "wrapped" : "native",
            preference ? "GPU preference" : "LUID", hr, exception, result.Get());
        if (exception || FAILED(hr) || !result) return 1;
        ComPtr<IUnknown> identity;
        if (FAILED(result.As(&identity))) return 1;
    }
    ComPtr<ID3D12Device> device;
    if (FAILED(D3D12CreateDevice(adapter.Get(), D3D_FEATURE_LEVEL_11_0, IID_PPV_ARGS(&device)))) return 1;
    ComPtr<ID3D12CommandQueue> queue;
    D3D12_COMMAND_QUEUE_DESC queue_desc{};
    if (FAILED(device->CreateCommandQueue(&queue_desc, IID_PPV_ARGS(&queue)))) return 1;
    ComPtr<ID3D11Device> device11;
    DWORD exception{};
    const auto hr = create_on12(device.Get(), queue.Get(), &device11, &exception);
    std::printf("D3D11On12CreateDevice: hr=%08lx exception=%08lx\n", hr, exception);
    if (exception || FAILED(hr) || !device11) return 1;
    std::puts("PRIVATE ADAPTER PASS: no standard-adapter calls on private interfaces");
    return 0;
}
