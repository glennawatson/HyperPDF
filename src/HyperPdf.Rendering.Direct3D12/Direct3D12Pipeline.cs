// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Rendering.Direct3D;
using HyperPdf.Rendering.Direct3D12.Interop;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Direct3D12;

/// <summary>
/// The Direct3D 12 objects every frame draws with: the root signature, the pipeline state with the two shaders, premultiplied blending
/// and the vertex layout, the two samplers, the heap of texture views, a white texture for solid fills and the buffer that holds a
/// frame's quads.
/// </summary>
[DebuggerDisplay("Direct3D12Pipeline: room for {_vertices.Capacity} vertex bytes")]
internal sealed unsafe class Direct3D12Pipeline : IDisposable
{
    /// <summary>The DXGI format of two floats.</summary>
    private const uint FormatFloat2 = 16;

    /// <summary>The DXGI format of four normalised bytes.</summary>
    private const uint FormatBytes4 = 28;

    /// <summary>The DXGI format of 8 bit BGRA.</summary>
    private const uint FormatBgra = 87;

    /// <summary>The offset of the texture coordinate in a vertex.</summary>
    private const uint UvOffset = 8;

    /// <summary>The offset of the colour in a vertex.</summary>
    private const uint ColorOffset = 16;

    /// <summary>The number of vertex elements.</summary>
    private const int ElementCount = 3;

    /// <summary>The blend factor one.</summary>
    private const uint BlendOne = 2;

    /// <summary>The blend factor one minus the source alpha.</summary>
    private const uint BlendInverseSourceAlpha = 6;

    /// <summary>The blend operation add.</summary>
    private const uint BlendAdd = 1;

    /// <summary>The logic operation that does nothing, which a target without logic operations carries.</summary>
    private const uint LogicNoOperation = 4;

    /// <summary>The write mask of all four channels.</summary>
    private const uint WriteAll = 0x0F;

    /// <summary>The fill mode solid.</summary>
    private const uint FillSolid = 3;

    /// <summary>The cull mode none.</summary>
    private const uint CullNone = 1;

    /// <summary>The stencil operation that keeps the value, and the comparison that always passes.</summary>
    private const uint StencilKeep = 1;

    /// <summary>The comparison that always passes.</summary>
    private const uint ComparisonAlways = 8;

    /// <summary>The stencil mask that lets every bit through.</summary>
    private const byte StencilMask = 0xFF;

    /// <summary>The primitive topology type of triangles in a pipeline state.</summary>
    private const uint TriangleTopologyType = 3;

    /// <summary>The primitive topology of a triangle list.</summary>
    private const uint TriangleList = 4;

    /// <summary>The filter that picks the nearest texel.</summary>
    private const uint FilterPoint = 0;

    /// <summary>The filter that blends the four nearest texels.</summary>
    private const uint FilterLinear = 0x14;

    /// <summary>The address mode that clamps at the edges.</summary>
    private const uint AddressClamp = 3;

    /// <summary>The comparison function never, for samplers that do not compare.</summary>
    private const uint CompareNever = 1;

    /// <summary>The descriptor heap type of shader resource views.</summary>
    private const uint ResourceHeapType = 0;

    /// <summary>The descriptor heap type of samplers.</summary>
    private const uint SamplerHeapType = 1;

    /// <summary>The descriptor heap flag that makes shaders see the heap.</summary>
    private const uint ShaderVisible = 1;

    /// <summary>The number of root parameters: the constants, the texture and the sampler.</summary>
    private const int ParameterCount = 3;

    /// <summary>The root parameter that holds the texture table.</summary>
    private const uint TextureParameter = 1;

    /// <summary>The root parameter that holds the sampler table.</summary>
    private const uint SamplerParameter = 2;

    /// <summary>The root parameter type of a descriptor table.</summary>
    private const uint ParameterTable = 0;

    /// <summary>The root parameter type of 32 bit constants.</summary>
    private const uint ParameterConstants = 1;

    /// <summary>The shader visibility of the vertex shader.</summary>
    private const uint VisibleToVertex = 1;

    /// <summary>The shader visibility of the pixel shader.</summary>
    private const uint VisibleToPixel = 5;

    /// <summary>The descriptor range type of shader resource views.</summary>
    private const uint RangeResources = 0;

    /// <summary>The descriptor range type of samplers.</summary>
    private const uint RangeSamplers = 3;

    /// <summary>The offset value that places a range right after the one before it.</summary>
    private const uint AppendRange = 0xFFFFFFFF;

    /// <summary>The root signature flag that allows the input assembler to read vertex buffers.</summary>
    private const uint AllowInputLayout = 1;

    /// <summary>The root signature version of the serialiser.</summary>
    private const uint RootSignatureVersion = 1;

    /// <summary>The number of 32 bit values of the frame constants: two scale floats and two unused.</summary>
    private const uint ConstantCount = 4;

    /// <summary>The width and height of clip space, which runs from -1 to 1.</summary>
    private const float ClipSpan = 2F;

    /// <summary>The vertices the first vertex buffer holds, enough for a few hundred quads.</summary>
    private const int FirstVertexCapacity = 4096;

    /// <summary>The view descriptors the heap holds at first.</summary>
    private const int FirstDescriptorCapacity = 1024;

    /// <summary>The number of samplers: nearest and linear.</summary>
    private const int SamplerCount = 2;

    /// <summary>The device.</summary>
    private readonly Direct3D12Device _device;

    /// <summary>The buffer that holds a frame's vertices.</summary>
    private readonly Direct3D12UploadBuffer _vertices;

    /// <summary>The ID3D12RootSignature.</summary>
    private ulong _signature;

    /// <summary>The ID3D12PipelineState.</summary>
    private ulong _state;

    /// <summary>The ID3D12DescriptorHeap of texture views.</summary>
    private ulong _resourceHeap;

    /// <summary>The ID3D12DescriptorHeap of the two samplers.</summary>
    private ulong _samplerHeap;

    /// <summary>The white texture solid fills sample.</summary>
    private ulong _white;

    /// <summary>The CPU address of the first texture view.</summary>
    private nuint _resourceStart;

    /// <summary>The GPU address of the first texture view.</summary>
    private ulong _resourceGpuStart;

    /// <summary>The GPU address of the first sampler.</summary>
    private ulong _samplerGpuStart;

    /// <summary>The bytes between two texture views.</summary>
    private uint _resourceStride;

    /// <summary>The bytes between two samplers.</summary>
    private uint _samplerStride;

    /// <summary>The number of views the heap holds.</summary>
    private int _descriptorCapacity;

    /// <summary>True until the white texture has its pixel.</summary>
    private bool _whitePending = true;

    /// <summary>Initializes a new instance of the <see cref="Direct3D12Pipeline"/> class.</summary>
    /// <param name="device">The device.</param>
    /// <exception cref="InvalidOperationException">Direct3D 12 rejected one of the objects.</exception>
    internal Direct3D12Pipeline(Direct3D12Device device)
    {
        _device = device;
        _vertices = new(device);
        try
        {
            CreateRootSignature();
            CreatePipelineState();
            CreateSamplers();
            CreateViewHeap(FirstDescriptorCapacity);
            _vertices.Ensure(FirstVertexCapacity * sizeof(QuadVertex));
            _white = (ulong)device.CreateTexture(1, 1, 0, Direct3D12Device.StateCopyDestination);
            WriteView(0, _white);
        }
        catch (InvalidOperationException)
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Gets a value indicating whether the white texture still waits for its pixel.</summary>
    internal bool WhitePending => _whitePending;

    /// <summary>Gets the white texture.</summary>
    internal ulong White => _white;

    /// <inheritdoc/>
    public void Dispose()
    {
        _vertices.Dispose();
        Direct3D12Calls.Release((void*)_white);
        Direct3D12Calls.Release((void*)_resourceHeap);
        Direct3D12Calls.Release((void*)_samplerHeap);
        Direct3D12Calls.Release((void*)_state);
        Direct3D12Calls.Release((void*)_signature);
        _white = 0;
        _resourceHeap = 0;
        _samplerHeap = 0;
        _state = 0;
        _signature = 0;
    }

    /// <summary>Marks the white texture as written, once the frame has recorded its upload.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WhiteWritten() => _whitePending = false;

    /// <summary>Makes the buffers and the view heap big enough for a frame. The GPU must be idle, as it is between frames.</summary>
    /// <param name="vertexCount">The vertices of the frame.</param>
    /// <param name="viewCount">The texture views the frame writes, including the white one.</param>
    /// <exception cref="InvalidOperationException">Direct3D 12 could not make a larger buffer or heap.</exception>
    internal void Reserve(int vertexCount, int viewCount)
    {
        _vertices.Ensure(vertexCount * sizeof(QuadVertex));
        if (viewCount <= _descriptorCapacity)
        {
            return;
        }

        Direct3D12Calls.Release((void*)_resourceHeap);
        _resourceHeap = 0;
        CreateViewHeap(Math.Max(viewCount, _descriptorCapacity + _descriptorCapacity));
        WriteView(0, _white);
    }

    /// <summary>Records everything a frame draws with and copies the frame's vertices into the vertex buffer.</summary>
    /// <param name="vertices">The vertices of the frame.</param>
    internal void BeginFrame(ReadOnlySpan<QuadVertex> vertices)
    {
        var list = _device.List;
        if (!vertices.IsEmpty)
        {
            vertices.CopyTo(new(_vertices.Mapped, vertices.Length));
        }

        CommandListCalls.SetRootSignature(list, (void*)_signature);
        CommandListCalls.SetPipelineState(list, (void*)_state);
        CommandListCalls.SetDescriptorHeaps(list, (void*)_resourceHeap, (void*)_samplerHeap);
        CommandListCalls.SetRenderTarget(list, _device.Target);
        var viewport = new Viewport(0, 0, _device.Width, _device.Height, 0, 1);
        CommandListCalls.SetViewport(list, &viewport);
        var scissor = new ScissorRect(0, 0, _device.Width, _device.Height);
        CommandListCalls.SetScissor(list, &scissor);
        CommandListCalls.SetPrimitiveTopology(list, TriangleList);
        var view = new VertexBufferView(_vertices.GpuAddress, (uint)(vertices.Length * sizeof(QuadVertex)), (uint)sizeof(QuadVertex));
        CommandListCalls.SetVertexBuffer(list, &view);
        var scale = stackalloc float[(int)ConstantCount] { ClipSpan / _device.Width, ClipSpan / _device.Height, 0F, 0F };
        CommandListCalls.SetRootConstants(list, 0, ConstantCount, scale);
    }

    /// <summary>Writes the view of a texture into a slot of the view heap.</summary>
    /// <param name="slot">The slot, which the draw that follows binds.</param>
    /// <param name="texture">The ID3D12Resource.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteView(int slot, ulong texture) =>
        Direct3D12Calls.CreateShaderResourceView(_device.Device, (void*)texture, _resourceStart + (nuint)(slot * (int)_resourceStride));

    /// <summary>Binds the texture view of a slot for the draws that follow.</summary>
    /// <param name="slot">The slot.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void BindView(int slot) => CommandListCalls.SetRootTable(_device.List, TextureParameter, _resourceGpuStart + (ulong)(slot * (long)_resourceStride));

    /// <summary>Binds one of the two samplers for the draws that follow.</summary>
    /// <param name="smooth">True for the one that blends the four nearest texels, false for the nearest.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void BindSampler(bool smooth) => CommandListCalls.SetRootTable(_device.List, SamplerParameter, _samplerGpuStart + (smooth ? _samplerStride : 0));

    /// <summary>Gets the address of static data.</summary>
    /// <param name="data">A UTF-8 literal or a static array property, whose bytes sit in the assembly image.</param>
    /// <returns>The address of the first byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong StaticAddress(ReadOnlySpan<byte> data) => (ulong)Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));

    /// <summary>Makes the root signature: four constants for the vertex shader, one texture and one sampler for the pixel shader.</summary>
    private void CreateRootSignature()
    {
        var textureRange = new DescriptorRange(RangeResources, 1, 0, 0, AppendRange);
        var samplerRange = new DescriptorRange(RangeSamplers, 1, 0, 0, AppendRange);
        var parameters = stackalloc RootParameter[ParameterCount];
        parameters[0] = new(ParameterConstants, 0, 0, 0, ConstantCount, VisibleToVertex);
        parameters[1] = new(ParameterTable, 0, 1, 0, (ulong)&textureRange, VisibleToPixel);
        parameters[2] = new(ParameterTable, 0, 1, 0, (ulong)&samplerRange, VisibleToPixel);
        var description = new RootSignatureDescription(ParameterCount, (ulong)parameters, 0, 0, AllowInputLayout);
        void* blob;
        void* error;
        var serialised = NativeMethods.SerializeRootSignature(&description, RootSignatureVersion, &blob, &error);
        Direct3D12Calls.Release(error);
        HResult.Check(serialised, "serialise the root signature");
        void* signature;
        var made = Direct3D12Calls.CreateRootSignature(_device.Device, Direct3D12Calls.BlobPointer(blob), Direct3D12Calls.BlobSize(blob), &signature);
        Direct3D12Calls.Release(blob);
        HResult.Check(made, "make the root signature");
        _signature = (ulong)signature;
    }

    /// <summary>Makes the pipeline state: both shaders, the vertex layout, premultiplied blending and no culling.</summary>
    private void CreatePipelineState()
    {
        var premultiplied = new RenderTargetBlend(1, 0, BlendOne, BlendInverseSourceAlpha, BlendAdd, BlendOne, BlendInverseSourceAlpha, BlendAdd, LogicNoOperation, WriteAll);
        var targets = new RenderTargetBlends(premultiplied, default, default, default, default, default, default, default);
        var formats = new RenderTargetFormats(FormatBgra, 0, 0, 0, 0, 0, 0, 0);
        var face = new DepthStencilOperation(StencilKeep, StencilKeep, StencilKeep, ComparisonAlways);

        // The shader bytes and the semantic names are static data of this assembly, which never moves, so their addresses stay valid without pinning.
        var elements = stackalloc InputElementDescription[ElementCount];
        elements[0] = new(StaticAddress("POSITION\0"u8), 0, FormatFloat2, 0, 0, 0, 0);
        elements[1] = new(StaticAddress("TEXCOORD\0"u8), 0, FormatFloat2, 0, UvOffset, 0, 0);
        elements[ElementCount - 1] = new(StaticAddress("COLOR\0"u8), 0, FormatBytes4, 0, ColorOffset, 0, 0);
        var description = new GraphicsPipelineDescription(
            _signature,
            new(StaticAddress(Direct3DShaderCode.Vertex), (ulong)Direct3DShaderCode.Vertex.Length),
            new(StaticAddress(Direct3DShaderCode.Pixel), (ulong)Direct3DShaderCode.Pixel.Length),
            default,
            default,
            default,
            default,
            new(0, 0, targets),
            uint.MaxValue,
            new(FillSolid, CullNone, 0, 0, 0F, 0F, 1, 0, 0, 0, 0),
            new(0, 0, ComparisonAlways, 0, StencilMask, StencilMask, face, face),
            new((ulong)elements, ElementCount),
            0,
            TriangleTopologyType,
            1,
            formats,
            0,
            new(1, 0),
            0,
            default,
            0);
        void* state;
        HResult.Check(Direct3D12Calls.CreatePipelineState(_device.Device, &description, &state), "make the pipeline state");
        _state = (ulong)state;
    }

    /// <summary>Makes the heap with the two samplers and writes them.</summary>
    private void CreateSamplers()
    {
        var description = new DescriptorHeapDescription(SamplerHeapType, SamplerCount, ShaderVisible, 0);
        void* heap;
        HResult.Check(Direct3D12Calls.CreateDescriptorHeap(_device.Device, &description, &heap), "make the sampler heap");
        _samplerHeap = (ulong)heap;
        var start = Direct3D12Calls.CpuHeapStart(heap);
        _samplerGpuStart = Direct3D12Calls.GpuHeapStart(heap);
        _samplerStride = Direct3D12Calls.DescriptorIncrement(_device.Device, SamplerHeapType);
        WriteSampler(start, FilterPoint);
        WriteSampler(start + _samplerStride, FilterLinear);
    }

    /// <summary>Writes a sampler that clamps at the edges.</summary>
    /// <param name="handle">The CPU descriptor.</param>
    /// <param name="filter">The filter.</param>
    private void WriteSampler(nuint handle, uint filter)
    {
        var description = new SamplerDescription(filter, AddressClamp, AddressClamp, AddressClamp, 0F, 1, CompareNever, 0F, 0F, 0F, 0F, 0F, float.MaxValue);
        Direct3D12Calls.CreateSampler(_device.Device, &description, handle);
    }

    /// <summary>Makes the shader visible heap of texture views.</summary>
    /// <param name="capacity">The number of views it holds.</param>
    private void CreateViewHeap(int capacity)
    {
        var description = new DescriptorHeapDescription(ResourceHeapType, (uint)capacity, ShaderVisible, 0);
        void* heap;
        HResult.Check(Direct3D12Calls.CreateDescriptorHeap(_device.Device, &description, &heap), "make the texture heap");
        _resourceHeap = (ulong)heap;
        _resourceStart = Direct3D12Calls.CpuHeapStart(heap);
        _resourceGpuStart = Direct3D12Calls.GpuHeapStart(heap);
        _resourceStride = Direct3D12Calls.DescriptorIncrement(_device.Device, ResourceHeapType);
        _descriptorCapacity = capacity;
    }
}
