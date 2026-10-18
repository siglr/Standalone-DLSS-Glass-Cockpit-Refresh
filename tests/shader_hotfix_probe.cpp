// Executes a small shader extracted from the production overlay. No game DLLs.
#include <Windows.h>
#include <d3d12.h>
#include <d3d12sdklayers.h>
#include <dxgi1_6.h>
#include <wrl/client.h>
#include <filesystem>
#include <fstream>
#include <vector>
#include <cstdio>
#include <cmath>
#include <stdexcept>
using Microsoft::WRL::ComPtr;
static void check(HRESULT hr) { if (FAILED(hr)) throw std::runtime_error("D3D12 operation failed"); }
int main(int argc, char** argv) {
    try {
        if (argc != 2) throw std::runtime_error("expected compiled shader path");
        std::ifstream input(std::filesystem::path(argv[1]), std::ios::binary);
        std::vector<char> code((std::istreambuf_iterator<char>(input)), {});
        if (code.empty()) throw std::runtime_error("empty shader");
        ComPtr<ID3D12Debug> debug;
        if (SUCCEEDED(D3D12GetDebugInterface(IID_PPV_ARGS(&debug)))) debug->EnableDebugLayer();
        ComPtr<ID3D12Device> device; check(D3D12CreateDevice(nullptr, D3D_FEATURE_LEVEL_12_0, IID_PPV_ARGS(&device)));
        D3D12_ROOT_PARAMETER param{}; param.ParameterType = D3D12_ROOT_PARAMETER_TYPE_UAV;
        D3D12_ROOT_SIGNATURE_DESC desc{1, &param};
        ComPtr<ID3DBlob> blob; check(D3D12SerializeRootSignature(&desc, D3D_ROOT_SIGNATURE_VERSION_1, &blob, nullptr));
        ComPtr<ID3D12RootSignature> root; check(device->CreateRootSignature(0, blob->GetBufferPointer(), blob->GetBufferSize(), IID_PPV_ARGS(&root)));
        D3D12_COMPUTE_PIPELINE_STATE_DESC pipeline{}; pipeline.pRootSignature = root.Get(); pipeline.CS = {code.data(), code.size()};
        ComPtr<ID3D12PipelineState> pso; check(device->CreateComputePipelineState(&pipeline, IID_PPV_ARGS(&pso)));
        D3D12_RESOURCE_DESC rd{}; rd.Dimension = D3D12_RESOURCE_DIMENSION_BUFFER; rd.Width = 256; rd.Height = 1;
        rd.DepthOrArraySize = rd.MipLevels = 1; rd.SampleDesc.Count = 1; rd.Layout = D3D12_TEXTURE_LAYOUT_ROW_MAJOR;
        rd.Flags = D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS;
        D3D12_HEAP_PROPERTIES heap{}; heap.Type = D3D12_HEAP_TYPE_DEFAULT;
        ComPtr<ID3D12Resource> output, readback;
        check(device->CreateCommittedResource(&heap, D3D12_HEAP_FLAG_NONE, &rd, D3D12_RESOURCE_STATE_UNORDERED_ACCESS, nullptr, IID_PPV_ARGS(&output)));
        heap.Type = D3D12_HEAP_TYPE_READBACK; rd.Flags = D3D12_RESOURCE_FLAG_NONE;
        check(device->CreateCommittedResource(&heap, D3D12_HEAP_FLAG_NONE, &rd, D3D12_RESOURCE_STATE_COPY_DEST, nullptr, IID_PPV_ARGS(&readback)));
        ComPtr<ID3D12CommandQueue> queue; D3D12_COMMAND_QUEUE_DESC qd{};
        check(device->CreateCommandQueue(&qd, IID_PPV_ARGS(&queue)));
        ComPtr<ID3D12CommandAllocator> allocator; check(device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT, IID_PPV_ARGS(&allocator)));
        ComPtr<ID3D12GraphicsCommandList> list; check(device->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT, allocator.Get(), pso.Get(), IID_PPV_ARGS(&list)));
        list->SetComputeRootSignature(root.Get()); list->SetComputeRootUnorderedAccessView(0, output->GetGPUVirtualAddress()); list->Dispatch(1, 1, 1);
        D3D12_RESOURCE_BARRIER barrier{}; barrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
        barrier.Transition = {output.Get(), D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES, D3D12_RESOURCE_STATE_UNORDERED_ACCESS, D3D12_RESOURCE_STATE_COPY_SOURCE};
        list->ResourceBarrier(1, &barrier); list->CopyResource(readback.Get(), output.Get()); check(list->Close());
        ID3D12CommandList* commands[]{list.Get()}; queue->ExecuteCommandLists(1, commands);
        ComPtr<ID3D12Fence> fence; check(device->CreateFence(0, D3D12_FENCE_FLAG_NONE, IID_PPV_ARGS(&fence)));
        HANDLE event = CreateEventW(nullptr, FALSE, FALSE, nullptr); if (!event) throw std::runtime_error("event failed");
        check(queue->Signal(fence.Get(), 1)); check(fence->SetEventOnCompletion(1, event));
        auto waited = WaitForSingleObject(event, 10000); CloseHandle(event);
        if (waited != WAIT_OBJECT_0) throw std::runtime_error("GPU timeout");
        float* values = nullptr; D3D12_RANGE range{0, 6 * sizeof(float)}; check(readback->Map(0, &range, reinterpret_cast<void**>(&values)));
        bool pass = true;
        // Low-variance channels are bounded; real contrast keeps its fitted gain.
        const float expected[]{2, 4, 4, 0, 1, 1};
        for (int i = 0; i < 6; ++i) { std::printf("case %d: %.6f expected %.6f\n", i, values[i], expected[i]); pass &= std::isfinite(values[i]) && std::abs(values[i] - expected[i]) < 0.0001f; }
        readback->Unmap(0, nullptr);
        ComPtr<ID3D12InfoQueue> info;
        if (SUCCEEDED(device.As(&info))) for (UINT64 i = 0; i < info->GetNumStoredMessages(); ++i) {
            SIZE_T size = 0; info->GetMessage(i, nullptr, &size); std::vector<char> bytes(size);
            auto* message = reinterpret_cast<D3D12_MESSAGE*>(bytes.data()); check(info->GetMessage(i, message, &size));
            if (message->Severity <= D3D12_MESSAGE_SEVERITY_ERROR) { std::puts(message->pDescription); pass = false; }
        }
        return pass ? 0 : 1;
    } catch (const std::exception& e) { std::fprintf(stderr, "%s\n", e.what()); return 1; }
}
