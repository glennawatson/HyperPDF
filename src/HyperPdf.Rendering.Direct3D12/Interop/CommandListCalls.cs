// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>Calls the methods of ID3D12GraphicsCommandList through their slots in the method table. The slot numbers follow the declaration order in d3d12.h.</summary>
internal static unsafe class CommandListCalls
{
    /// <summary>The slot of ID3D12GraphicsCommandList::Close.</summary>
    private const int CloseSlot = 9;

    /// <summary>The slot of ID3D12GraphicsCommandList::Reset.</summary>
    private const int ResetSlot = 10;

    /// <summary>The slot of ID3D12GraphicsCommandList::DrawInstanced.</summary>
    private const int DrawInstancedSlot = 12;

    /// <summary>The slot of ID3D12GraphicsCommandList::CopyTextureRegion.</summary>
    private const int CopyTextureRegionSlot = 16;

    /// <summary>The slot of ID3D12GraphicsCommandList::IASetPrimitiveTopology.</summary>
    private const int SetPrimitiveTopologySlot = 20;

    /// <summary>The slot of ID3D12GraphicsCommandList::RSSetViewports.</summary>
    private const int SetViewportsSlot = 21;

    /// <summary>The slot of ID3D12GraphicsCommandList::RSSetScissorRects.</summary>
    private const int SetScissorRectsSlot = 22;

    /// <summary>The slot of ID3D12GraphicsCommandList::SetPipelineState.</summary>
    private const int SetPipelineStateSlot = 25;

    /// <summary>The slot of ID3D12GraphicsCommandList::ResourceBarrier.</summary>
    private const int ResourceBarrierSlot = 26;

    /// <summary>The slot of ID3D12GraphicsCommandList::SetDescriptorHeaps.</summary>
    private const int SetDescriptorHeapsSlot = 28;

    /// <summary>The slot of ID3D12GraphicsCommandList::SetGraphicsRootSignature.</summary>
    private const int SetRootSignatureSlot = 30;

    /// <summary>The slot of ID3D12GraphicsCommandList::SetGraphicsRootDescriptorTable.</summary>
    private const int SetRootTableSlot = 32;

    /// <summary>The slot of ID3D12GraphicsCommandList::SetGraphicsRoot32BitConstants.</summary>
    private const int SetRootConstantsSlot = 36;

    /// <summary>The slot of ID3D12GraphicsCommandList::IASetVertexBuffers.</summary>
    private const int SetVertexBuffersSlot = 44;

    /// <summary>The slot of ID3D12GraphicsCommandList::OMSetRenderTargets.</summary>
    private const int SetRenderTargetsSlot = 46;

    /// <summary>The slot of ID3D12GraphicsCommandList::ClearRenderTargetView.</summary>
    private const int ClearRenderTargetSlot = 48;

    /// <summary>The number of descriptor heaps a frame binds: shader resources and samplers.</summary>
    private const int HeapCount = 2;

    /// <summary>Calls Close, which ends the recording.</summary>
    /// <param name="list">The command list.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Close(void* list) =>
        ((delegate* unmanaged[Stdcall]<void*, int>)Slot(list, CloseSlot))(list);

    /// <summary>Calls Reset, which starts a new recording in the allocator.</summary>
    /// <param name="list">The command list.</param>
    /// <param name="allocator">The allocator, which must not be in use by the GPU.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Reset(void* list, void* allocator) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, void*, int>)Slot(list, ResetSlot))(list, allocator, null);

    /// <summary>Calls DrawInstanced for one instance.</summary>
    /// <param name="list">The command list.</param>
    /// <param name="vertexCount">The number of vertices.</param>
    /// <param name="firstVertex">The first vertex.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Draw(void* list, uint vertexCount, uint firstVertex) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, uint, uint, uint, void>)Slot(list, DrawInstancedSlot))(list, vertexCount, 1, firstVertex, 0);

    /// <summary>Calls CopyTextureRegion for a whole subresource.</summary>
    /// <param name="list">The command list.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="source">The source.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CopyTexture(void* list, TextureCopyLocation* destination, TextureCopyLocation* source) =>
        ((delegate* unmanaged[Stdcall]<void*, TextureCopyLocation*, uint, uint, uint, TextureCopyLocation*, void*, void>)Slot(list, CopyTextureRegionSlot))(
            list,
            destination,
            0,
            0,
            0,
            source,
            null);

    /// <summary>Calls IASetPrimitiveTopology.</summary>
    /// <param name="list">The command list.</param>
    /// <param name="topology">The topology.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetPrimitiveTopology(void* list, uint topology) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, void>)Slot(list, SetPrimitiveTopologySlot))(list, topology);

    /// <summary>Calls RSSetViewports with one viewport.</summary>
    /// <param name="list">The command list.</param>
    /// <param name="viewport">The viewport.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetViewport(void* list, Viewport* viewport) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, Viewport*, void>)Slot(list, SetViewportsSlot))(list, 1, viewport);

    /// <summary>Calls RSSetScissorRects with one rectangle.</summary>
    /// <param name="list">The command list.</param>
    /// <param name="rectangle">The rectangle.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetScissor(void* list, ScissorRect* rectangle) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, ScissorRect*, void>)Slot(list, SetScissorRectsSlot))(list, 1, rectangle);

    /// <summary>Calls SetPipelineState.</summary>
    /// <param name="list">The command list.</param>
    /// <param name="state">The pipeline state.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetPipelineState(void* list, void* state) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, void>)Slot(list, SetPipelineStateSlot))(list, state);

    /// <summary>Calls ResourceBarrier with one barrier.</summary>
    /// <param name="list">The command list.</param>
    /// <param name="barrier">The barrier.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Barrier(void* list, ResourceBarrier* barrier) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, ResourceBarrier*, void>)Slot(list, ResourceBarrierSlot))(list, 1, barrier);

    /// <summary>Calls SetDescriptorHeaps with two heaps: shader resources and samplers.</summary>
    /// <param name="list">The command list.</param>
    /// <param name="resourceHeap">The heap of shader resource views.</param>
    /// <param name="samplerHeap">The heap of samplers.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetDescriptorHeaps(void* list, void* resourceHeap, void* samplerHeap)
    {
        var heaps = stackalloc void*[HeapCount] { resourceHeap, samplerHeap };
        ((delegate* unmanaged[Stdcall]<void*, uint, void**, void>)Slot(list, SetDescriptorHeapsSlot))(list, HeapCount, heaps);
    }

    /// <summary>Calls SetGraphicsRootSignature.</summary>
    /// <param name="list">The command list.</param>
    /// <param name="signature">The root signature.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetRootSignature(void* list, void* signature) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, void>)Slot(list, SetRootSignatureSlot))(list, signature);

    /// <summary>Calls SetGraphicsRootDescriptorTable.</summary>
    /// <param name="list">The command list.</param>
    /// <param name="parameter">The root parameter.</param>
    /// <param name="handle">The GPU address of the first descriptor of the table.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetRootTable(void* list, uint parameter, ulong handle) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, ulong, void>)Slot(list, SetRootTableSlot))(list, parameter, handle);

    /// <summary>Calls SetGraphicsRoot32BitConstants.</summary>
    /// <param name="list">The command list.</param>
    /// <param name="parameter">The root parameter.</param>
    /// <param name="count">The number of 32 bit values.</param>
    /// <param name="values">The values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetRootConstants(void* list, uint parameter, uint count, void* values) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, uint, void*, uint, void>)Slot(list, SetRootConstantsSlot))(list, parameter, count, values, 0);

    /// <summary>Calls IASetVertexBuffers for slot 0.</summary>
    /// <param name="list">The command list.</param>
    /// <param name="view">The vertex buffer view.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetVertexBuffer(void* list, VertexBufferView* view) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, uint, VertexBufferView*, void>)Slot(list, SetVertexBuffersSlot))(list, 0, 1, view);

    /// <summary>Calls OMSetRenderTargets with one target and no depth buffer.</summary>
    /// <param name="list">The command list.</param>
    /// <param name="handle">The CPU descriptor of the render target view.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetRenderTarget(void* list, nuint handle) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, nuint*, int, void*, void>)Slot(list, SetRenderTargetsSlot))(list, 1, &handle, 1, null);

    /// <summary>Calls ClearRenderTargetView for the whole target.</summary>
    /// <param name="list">The command list.</param>
    /// <param name="handle">The CPU descriptor of the render target view.</param>
    /// <param name="color">Four floats: red, green, blue and alpha.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ClearRenderTarget(void* list, nuint handle, float* color) =>
        ((delegate* unmanaged[Stdcall]<void*, nuint, float*, uint, void*, void>)Slot(list, ClearRenderTargetSlot))(list, handle, color, 0, null);

    /// <summary>Reads a method pointer from an interface's method table.</summary>
    /// <param name="instance">The interface pointer.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The method pointer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void* Slot(void* instance, int slot) => (*(void***)instance)[slot];
}
