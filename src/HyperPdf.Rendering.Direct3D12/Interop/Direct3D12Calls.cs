// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>
/// Calls the methods of the Direct3D 12 device, queue, fence, heap and resource interfaces and the DXGI factory and swap chain, each
/// through its slot in the interface's method table. The slot numbers follow the declaration order in d3d12.h and dxgi1_4.h.
/// </summary>
internal static unsafe class Direct3D12Calls
{
    /// <summary>The slot of IUnknown::QueryInterface.</summary>
    private const int QueryInterfaceSlot = 0;

    /// <summary>The slot of IUnknown::Release.</summary>
    private const int ReleaseSlot = 2;

    /// <summary>The slot of ID3D12Device::CreateCommandQueue.</summary>
    private const int CreateCommandQueueSlot = 8;

    /// <summary>The slot of ID3D12Device::CreateCommandAllocator.</summary>
    private const int CreateCommandAllocatorSlot = 9;

    /// <summary>The slot of ID3D12Device::CreateGraphicsPipelineState.</summary>
    private const int CreatePipelineStateSlot = 10;

    /// <summary>The slot of ID3D12Device::CreateCommandList.</summary>
    private const int CreateCommandListSlot = 12;

    /// <summary>The slot of ID3D12Device::CreateDescriptorHeap.</summary>
    private const int CreateDescriptorHeapSlot = 14;

    /// <summary>The slot of ID3D12Device::GetDescriptorHandleIncrementSize.</summary>
    private const int DescriptorIncrementSlot = 15;

    /// <summary>The slot of ID3D12Device::CreateRootSignature.</summary>
    private const int CreateRootSignatureSlot = 16;

    /// <summary>The slot of ID3D12Device::CreateShaderResourceView.</summary>
    private const int CreateShaderResourceViewSlot = 18;

    /// <summary>The slot of ID3D12Device::CreateRenderTargetView.</summary>
    private const int CreateRenderTargetViewSlot = 20;

    /// <summary>The slot of ID3D12Device::CreateSampler.</summary>
    private const int CreateSamplerSlot = 22;

    /// <summary>The slot of ID3D12Device::CreateCommittedResource.</summary>
    private const int CreateCommittedResourceSlot = 27;

    /// <summary>The slot of ID3D12Device::CreateFence.</summary>
    private const int CreateFenceSlot = 36;

    /// <summary>The slot of ID3D12Device::GetDeviceRemovedReason.</summary>
    private const int RemovedReasonSlot = 37;

    /// <summary>The slot of ID3D12CommandQueue::ExecuteCommandLists.</summary>
    private const int ExecuteCommandListsSlot = 10;

    /// <summary>The slot of ID3D12CommandQueue::Signal.</summary>
    private const int QueueSignalSlot = 14;

    /// <summary>The slot of ID3D12Fence::GetCompletedValue.</summary>
    private const int GetCompletedValueSlot = 8;

    /// <summary>The slot of ID3D12Fence::SetEventOnCompletion.</summary>
    private const int SetEventOnCompletionSlot = 9;

    /// <summary>The slot of ID3D12CommandAllocator::Reset.</summary>
    private const int AllocatorResetSlot = 8;

    /// <summary>The slot of ID3D12Resource::Map.</summary>
    private const int MapSlot = 8;

    /// <summary>The slot of ID3D12Resource::Unmap.</summary>
    private const int UnmapSlot = 9;

    /// <summary>The slot of ID3D12Resource::GetGPUVirtualAddress.</summary>
    private const int GpuAddressSlot = 11;

    /// <summary>The slot of ID3D12DescriptorHeap::GetCPUDescriptorHandleForHeapStart.</summary>
    private const int CpuHeapStartSlot = 9;

    /// <summary>The slot of ID3D12DescriptorHeap::GetGPUDescriptorHandleForHeapStart.</summary>
    private const int GpuHeapStartSlot = 10;

    /// <summary>The slot of ID3DBlob::GetBufferPointer.</summary>
    private const int BlobPointerSlot = 3;

    /// <summary>The slot of ID3DBlob::GetBufferSize.</summary>
    private const int BlobSizeSlot = 4;

    /// <summary>The slot of IDXGIFactory::MakeWindowAssociation.</summary>
    private const int MakeWindowAssociationSlot = 8;

    /// <summary>The slot of IDXGIFactory2::CreateSwapChainForHwnd.</summary>
    private const int CreateSwapChainForWindowSlot = 15;

    /// <summary>The slot of IDXGIFactory4::EnumWarpAdapter.</summary>
    private const int EnumWarpAdapterSlot = 27;

    /// <summary>The slot of IDXGISwapChain::Present.</summary>
    private const int PresentSlot = 8;

    /// <summary>The slot of IDXGISwapChain::GetBuffer.</summary>
    private const int GetBufferSlot = 9;

    /// <summary>The slot of IDXGISwapChain::ResizeBuffers.</summary>
    private const int ResizeBuffersSlot = 13;

    /// <summary>The slot of IDXGISwapChain3::GetCurrentBackBufferIndex.</summary>
    private const int CurrentBackBufferSlot = 36;

    /// <summary>Releases a COM object, if there is one.</summary>
    /// <param name="unknown">The object, or null.</param>
    internal static void Release(void* unknown)
    {
        if (unknown is not null)
        {
            _ = ((delegate* unmanaged[Stdcall]<void*, uint>)Slot(unknown, ReleaseSlot))(unknown);
        }
    }

    /// <summary>Calls IUnknown::QueryInterface.</summary>
    /// <param name="unknown">The object.</param>
    /// <param name="interfaceId">The interface asked for.</param>
    /// <param name="result">Receives the interface.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int QueryInterface(void* unknown, Guid* interfaceId, void** result) =>
        ((delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)Slot(unknown, QueryInterfaceSlot))(unknown, interfaceId, result);

    /// <summary>Calls ID3D12Device::CreateCommandQueue.</summary>
    /// <param name="device">The device.</param>
    /// <param name="description">The queue description.</param>
    /// <param name="queue">Receives the queue.</param>
    /// <returns>An HRESULT.</returns>
    internal static int CreateCommandQueue(void* device, CommandQueueDescription* description, void** queue)
    {
        var id = Guids.CommandQueue;
        return ((delegate* unmanaged[Stdcall]<void*, CommandQueueDescription*, Guid*, void**, int>)Slot(device, CreateCommandQueueSlot))(device, description, &id, queue);
    }

    /// <summary>Calls ID3D12Device::CreateCommandAllocator for a direct allocator.</summary>
    /// <param name="device">The device.</param>
    /// <param name="allocator">Receives the allocator.</param>
    /// <returns>An HRESULT.</returns>
    internal static int CreateCommandAllocator(void* device, void** allocator)
    {
        var id = Guids.CommandAllocator;
        return ((delegate* unmanaged[Stdcall]<void*, uint, Guid*, void**, int>)Slot(device, CreateCommandAllocatorSlot))(device, 0, &id, allocator);
    }

    /// <summary>Calls ID3D12Device::CreateGraphicsPipelineState.</summary>
    /// <param name="device">The device.</param>
    /// <param name="description">The pipeline description.</param>
    /// <param name="state">Receives the pipeline state.</param>
    /// <returns>An HRESULT.</returns>
    internal static int CreatePipelineState(void* device, GraphicsPipelineDescription* description, void** state)
    {
        var id = Guids.PipelineState;
        return ((delegate* unmanaged[Stdcall]<void*, GraphicsPipelineDescription*, Guid*, void**, int>)Slot(device, CreatePipelineStateSlot))(device, description, &id, state);
    }

    /// <summary>Calls ID3D12Device::CreateCommandList for a direct list that starts closed.</summary>
    /// <param name="device">The device.</param>
    /// <param name="allocator">The allocator.</param>
    /// <param name="list">Receives the command list.</param>
    /// <returns>An HRESULT.</returns>
    internal static int CreateCommandList(void* device, void* allocator, void** list)
    {
        var id = Guids.GraphicsCommandList;
        return ((delegate* unmanaged[Stdcall]<void*, uint, uint, void*, void*, Guid*, void**, int>)Slot(device, CreateCommandListSlot))(device, 0, 0, allocator, null, &id, list);
    }

    /// <summary>Calls ID3D12Device::CreateDescriptorHeap.</summary>
    /// <param name="device">The device.</param>
    /// <param name="description">The heap description.</param>
    /// <param name="heap">Receives the heap.</param>
    /// <returns>An HRESULT.</returns>
    internal static int CreateDescriptorHeap(void* device, DescriptorHeapDescription* description, void** heap)
    {
        var id = Guids.DescriptorHeap;
        return ((delegate* unmanaged[Stdcall]<void*, DescriptorHeapDescription*, Guid*, void**, int>)Slot(device, CreateDescriptorHeapSlot))(device, description, &id, heap);
    }

    /// <summary>Calls ID3D12Device::GetDescriptorHandleIncrementSize.</summary>
    /// <param name="device">The device.</param>
    /// <param name="type">The kind of descriptors.</param>
    /// <returns>The bytes between two descriptors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint DescriptorIncrement(void* device, uint type) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, uint>)Slot(device, DescriptorIncrementSlot))(device, type);

    /// <summary>Calls ID3D12Device::CreateRootSignature.</summary>
    /// <param name="device">The device.</param>
    /// <param name="blob">The serialised signature.</param>
    /// <param name="size">The size of the serialised signature.</param>
    /// <param name="signature">Receives the root signature.</param>
    /// <returns>An HRESULT.</returns>
    internal static int CreateRootSignature(void* device, void* blob, nuint size, void** signature)
    {
        var id = Guids.RootSignature;
        return ((delegate* unmanaged[Stdcall]<void*, uint, void*, nuint, Guid*, void**, int>)Slot(device, CreateRootSignatureSlot))(device, 0, blob, size, &id, signature);
    }

    /// <summary>Calls ID3D12Device::CreateShaderResourceView.</summary>
    /// <param name="device">The device.</param>
    /// <param name="resource">The texture.</param>
    /// <param name="handle">The CPU descriptor the view is written to.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CreateShaderResourceView(void* device, void* resource, nuint handle) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, void*, nuint, void>)Slot(device, CreateShaderResourceViewSlot))(device, resource, null, handle);

    /// <summary>Calls ID3D12Device::CreateRenderTargetView with the default view of the whole resource.</summary>
    /// <param name="device">The device.</param>
    /// <param name="resource">The texture.</param>
    /// <param name="handle">The CPU descriptor the view is written to.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CreateRenderTargetView(void* device, void* resource, nuint handle) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, void*, nuint, void>)Slot(device, CreateRenderTargetViewSlot))(device, resource, null, handle);

    /// <summary>Calls ID3D12Device::CreateSampler.</summary>
    /// <param name="device">The device.</param>
    /// <param name="description">The sampler description.</param>
    /// <param name="handle">The CPU descriptor the sampler is written to.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CreateSampler(void* device, SamplerDescription* description, nuint handle) =>
        ((delegate* unmanaged[Stdcall]<void*, SamplerDescription*, nuint, void>)Slot(device, CreateSamplerSlot))(device, description, handle);

    /// <summary>Calls ID3D12Device::CreateCommittedResource.</summary>
    /// <param name="device">The device.</param>
    /// <param name="heap">The heap properties.</param>
    /// <param name="description">The resource description.</param>
    /// <param name="state">The state the resource starts in.</param>
    /// <param name="resource">Receives the resource.</param>
    /// <returns>An HRESULT.</returns>
    internal static int CreateCommittedResource(void* device, HeapProperties* heap, ResourceDescription* description, uint state, void** resource)
    {
        var id = Guids.Resource;
        return ((delegate* unmanaged[Stdcall]<void*, HeapProperties*, uint, ResourceDescription*, uint, void*, Guid*, void**, int>)Slot(device, CreateCommittedResourceSlot))(
            device,
            heap,
            0,
            description,
            state,
            null,
            &id,
            resource);
    }

    /// <summary>Calls ID3D12Device::CreateFence.</summary>
    /// <param name="device">The device.</param>
    /// <param name="fence">Receives the fence.</param>
    /// <returns>An HRESULT.</returns>
    internal static int CreateFence(void* device, void** fence)
    {
        var id = Guids.Fence;
        return ((delegate* unmanaged[Stdcall]<void*, ulong, uint, Guid*, void**, int>)Slot(device, CreateFenceSlot))(device, 0, 0, &id, fence);
    }

    /// <summary>Calls ID3D12Device::GetDeviceRemovedReason.</summary>
    /// <param name="device">The device.</param>
    /// <returns>An HRESULT: 0 while the device works, and why it stopped otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int RemovedReason(void* device) =>
        ((delegate* unmanaged[Stdcall]<void*, int>)Slot(device, RemovedReasonSlot))(device);

    /// <summary>Calls ID3D12CommandQueue::ExecuteCommandLists with one list.</summary>
    /// <param name="queue">The queue.</param>
    /// <param name="list">The closed command list.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Execute(void* queue, void* list) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, void**, void>)Slot(queue, ExecuteCommandListsSlot))(queue, 1, &list);

    /// <summary>Calls ID3D12CommandQueue::Signal.</summary>
    /// <param name="queue">The queue.</param>
    /// <param name="fence">The fence.</param>
    /// <param name="value">The value the fence takes when the queue gets there.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Signal(void* queue, void* fence, ulong value) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, ulong, int>)Slot(queue, QueueSignalSlot))(queue, fence, value);

    /// <summary>Calls ID3D12Fence::GetCompletedValue.</summary>
    /// <param name="fence">The fence.</param>
    /// <returns>The last value the queue reached.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong CompletedValue(void* fence) =>
        ((delegate* unmanaged[Stdcall]<void*, ulong>)Slot(fence, GetCompletedValueSlot))(fence);

    /// <summary>Calls ID3D12Fence::SetEventOnCompletion.</summary>
    /// <param name="fence">The fence.</param>
    /// <param name="value">The value to wait for.</param>
    /// <param name="signal">The event that is set when the fence reaches the value.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int SetEventOnCompletion(void* fence, ulong value, nint signal) =>
        ((delegate* unmanaged[Stdcall]<void*, ulong, nint, int>)Slot(fence, SetEventOnCompletionSlot))(fence, value, signal);

    /// <summary>Calls ID3D12CommandAllocator::Reset.</summary>
    /// <param name="allocator">The allocator.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Reset(void* allocator) =>
        ((delegate* unmanaged[Stdcall]<void*, int>)Slot(allocator, AllocatorResetSlot))(allocator);

    /// <summary>Calls ID3D12Resource::Map on the first subresource, with no range the CPU reads.</summary>
    /// <param name="resource">The buffer.</param>
    /// <param name="data">Receives the memory.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Map(void* resource, void** data) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, void*, void**, int>)Slot(resource, MapSlot))(resource, 0, null, data);

    /// <summary>Calls ID3D12Resource::Unmap on the first subresource.</summary>
    /// <param name="resource">The buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Unmap(void* resource) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, void*, void>)Slot(resource, UnmapSlot))(resource, 0, null);

    /// <summary>Calls ID3D12Resource::GetGPUVirtualAddress.</summary>
    /// <param name="resource">The buffer.</param>
    /// <returns>The address shaders and the input assembler use.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong GpuAddress(void* resource) =>
        ((delegate* unmanaged[Stdcall]<void*, ulong>)Slot(resource, GpuAddressSlot))(resource);

    /// <summary>Calls ID3D12DescriptorHeap::GetCPUDescriptorHandleForHeapStart.</summary>
    /// <param name="heap">The heap.</param>
    /// <returns>The CPU address of the first descriptor.</returns>
    internal static nuint CpuHeapStart(void* heap)
    {
        nuint handle;
        _ = ((delegate* unmanaged[Stdcall]<void*, nuint*, nuint*>)Slot(heap, CpuHeapStartSlot))(heap, &handle);
        return handle;
    }

    /// <summary>Calls ID3D12DescriptorHeap::GetGPUDescriptorHandleForHeapStart.</summary>
    /// <param name="heap">The shader visible heap.</param>
    /// <returns>The GPU address of the first descriptor.</returns>
    internal static ulong GpuHeapStart(void* heap)
    {
        ulong handle;
        _ = ((delegate* unmanaged[Stdcall]<void*, ulong*, ulong*>)Slot(heap, GpuHeapStartSlot))(heap, &handle);
        return handle;
    }

    /// <summary>Calls ID3DBlob::GetBufferPointer.</summary>
    /// <param name="blob">The blob.</param>
    /// <returns>The first byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void* BlobPointer(void* blob) =>
        ((delegate* unmanaged[Stdcall]<void*, void*>)Slot(blob, BlobPointerSlot))(blob);

    /// <summary>Calls ID3DBlob::GetBufferSize.</summary>
    /// <param name="blob">The blob.</param>
    /// <returns>The size in bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static nuint BlobSize(void* blob) =>
        ((delegate* unmanaged[Stdcall]<void*, nuint>)Slot(blob, BlobSizeSlot))(blob);

    /// <summary>Calls IDXGIFactory::MakeWindowAssociation so DXGI does not toggle full screen on Alt+Enter.</summary>
    /// <param name="factory">The factory.</param>
    /// <param name="window">The window handle.</param>
    /// <param name="flags">The association flags.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int MakeWindowAssociation(void* factory, nint window, uint flags) =>
        ((delegate* unmanaged[Stdcall]<void*, nint, uint, int>)Slot(factory, MakeWindowAssociationSlot))(factory, window, flags);

    /// <summary>Calls IDXGIFactory2::CreateSwapChainForHwnd.</summary>
    /// <param name="factory">The factory.</param>
    /// <param name="queue">The command queue that presents.</param>
    /// <param name="window">The window handle.</param>
    /// <param name="description">The swap chain description.</param>
    /// <param name="swapChain">Receives the IDXGISwapChain1.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CreateSwapChainForWindow(void* factory, void* queue, nint window, SwapChainDescription1* description, void** swapChain) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, nint, SwapChainDescription1*, void*, void*, void**, int>)Slot(factory, CreateSwapChainForWindowSlot))(
            factory,
            queue,
            window,
            description,
            null,
            null,
            swapChain);

    /// <summary>Calls IDXGIFactory4::EnumWarpAdapter.</summary>
    /// <param name="factory">The factory.</param>
    /// <param name="adapter">Receives the IDXGIAdapter of the software rasteriser.</param>
    /// <returns>An HRESULT.</returns>
    internal static int EnumWarpAdapter(void* factory, void** adapter)
    {
        var id = Guids.Adapter;
        return ((delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)Slot(factory, EnumWarpAdapterSlot))(factory, &id, adapter);
    }

    /// <summary>Calls IDXGISwapChain::Present.</summary>
    /// <param name="swapChain">The swap chain.</param>
    /// <param name="syncInterval">The display refreshes to wait for.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Present(void* swapChain, uint syncInterval) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, uint, int>)Slot(swapChain, PresentSlot))(swapChain, syncInterval, 0);

    /// <summary>Calls IDXGISwapChain::GetBuffer for a back buffer as a resource.</summary>
    /// <param name="swapChain">The swap chain.</param>
    /// <param name="index">The buffer index.</param>
    /// <param name="resource">Receives the ID3D12Resource.</param>
    /// <returns>An HRESULT.</returns>
    internal static int GetBackBuffer(void* swapChain, uint index, void** resource)
    {
        var id = Guids.Resource;
        return ((delegate* unmanaged[Stdcall]<void*, uint, Guid*, void**, int>)Slot(swapChain, GetBufferSlot))(swapChain, index, &id, resource);
    }

    /// <summary>Calls IDXGISwapChain::ResizeBuffers, keeping the buffer count and format.</summary>
    /// <param name="swapChain">The swap chain.</param>
    /// <param name="width">The new width.</param>
    /// <param name="height">The new height.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ResizeBuffers(void* swapChain, uint width, uint height) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, uint, uint, uint, uint, int>)Slot(swapChain, ResizeBuffersSlot))(swapChain, 0, width, height, 0, 0);

    /// <summary>Calls IDXGISwapChain3::GetCurrentBackBufferIndex.</summary>
    /// <param name="swapChain">The swap chain, as an IDXGISwapChain3.</param>
    /// <returns>The index of the buffer the next frame draws into.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint CurrentBackBuffer(void* swapChain) =>
        ((delegate* unmanaged[Stdcall]<void*, uint>)Slot(swapChain, CurrentBackBufferSlot))(swapChain);

    /// <summary>Reads a method pointer from an interface's method table.</summary>
    /// <param name="instance">The interface pointer.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The method pointer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void* Slot(void* instance, int slot) => (*(void***)instance)[slot];
}
