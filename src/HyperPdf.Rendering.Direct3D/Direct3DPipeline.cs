// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Rendering.Direct3D.Interop;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Direct3D;

/// <summary>
/// The Direct3D objects every frame draws with: the two shaders, the vertex layout, premultiplied blending, the two samplers, the
/// frame constants, a white texture for solid fills and the vertex buffer that holds a frame's quads.
/// </summary>
[DebuggerDisplay("Direct3DPipeline: room for {_vertexCapacity} vertices")]
internal sealed unsafe class Direct3DPipeline : IDisposable
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

    /// <summary>The write mask of all four channels.</summary>
    private const uint WriteAll = 0x0F;

    /// <summary>The size of D3D11_BLEND_DESC: two flags and eight render target entries.</summary>
    private const int BlendDescriptionSize = 264;

    /// <summary>The offset of the first render target entry in D3D11_BLEND_DESC.</summary>
    private const int FirstTargetOffset = 8;

    /// <summary>The fill mode solid.</summary>
    private const uint FillSolid = 3;

    /// <summary>The cull mode none.</summary>
    private const uint CullNone = 1;

    /// <summary>The filter that picks the nearest texel.</summary>
    private const uint FilterPoint = 0;

    /// <summary>The filter that blends the four nearest texels.</summary>
    private const uint FilterLinear = 0x14;

    /// <summary>The address mode that clamps at the edges.</summary>
    private const uint AddressClamp = 3;

    /// <summary>The comparison function never, for samplers that do not compare.</summary>
    private const uint CompareNever = 1;

    /// <summary>The usage of a buffer the CPU rewrites every frame.</summary>
    private const uint UsageDynamic = 2;

    /// <summary>The bind flag of a vertex buffer.</summary>
    private const uint BindVertexBuffer = 0x1;

    /// <summary>The bind flag of a constant buffer.</summary>
    private const uint BindConstantBuffer = 0x4;

    /// <summary>The bind flag of a texture a shader reads.</summary>
    private const uint BindShaderResource = 0x8;

    /// <summary>The CPU access flag for writing.</summary>
    private const uint CpuWrite = 0x10000;

    /// <summary>The map type that throws away the old contents.</summary>
    private const uint MapWriteDiscard = 4;

    /// <summary>The topology of a triangle list.</summary>
    private const uint TriangleList = 4;

    /// <summary>The size of the frame constants: the two scale floats and two unused floats.</summary>
    private const uint ConstantsSize = 16;

    /// <summary>The width and height of clip space, which runs from -1 to 1.</summary>
    private const float ClipSpan = 2F;

    /// <summary>The vertices the first vertex buffer holds, enough for a few hundred quads.</summary>
    private const int FirstVertexCapacity = 4096;

    /// <summary>The device.</summary>
    private readonly Direct3DDevice _device;

    /// <summary>The vertex shader.</summary>
    private ulong _vertexShader;

    /// <summary>The pixel shader.</summary>
    private ulong _pixelShader;

    /// <summary>The vertex layout.</summary>
    private ulong _layout;

    /// <summary>The premultiplied alpha blend state.</summary>
    private ulong _blend;

    /// <summary>The rasteriser state: filled triangles, no culling.</summary>
    private ulong _rasterizer;

    /// <summary>The sampler that picks the nearest texel.</summary>
    private ulong _nearest;

    /// <summary>The sampler that blends the four nearest texels.</summary>
    private ulong _linear;

    /// <summary>The constant buffer with the frame scale.</summary>
    private ulong _constants;

    /// <summary>The vertex buffer.</summary>
    private ulong _vertices;

    /// <summary>The white texture solid fills sample.</summary>
    private ulong _white;

    /// <summary>The view of the white texture.</summary>
    private ulong _whiteView;

    /// <summary>The number of vertices the vertex buffer holds.</summary>
    private int _vertexCapacity;

    /// <summary>Initializes a new instance of the <see cref="Direct3DPipeline"/> class.</summary>
    /// <param name="device">The device.</param>
    /// <exception cref="InvalidOperationException">Direct3D rejected one of the objects.</exception>
    internal Direct3DPipeline(Direct3DDevice device)
    {
        _device = device;
        try
        {
            CreateShaders();
            CreateStates();
            CreateBuffers();
        }
        catch (InvalidOperationException)
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Gets the view of the white texture, which solid fills sample.</summary>
    internal void* WhiteView => (void*)_whiteView;

    /// <summary>Gets the sampler that picks the nearest texel.</summary>
    internal void* Nearest => (void*)_nearest;

    /// <summary>Gets the sampler that blends the four nearest texels.</summary>
    internal void* Linear => (void*)_linear;

    /// <inheritdoc/>
    public void Dispose()
    {
        Direct3DCalls.Release((void*)_whiteView);
        Direct3DCalls.Release((void*)_white);
        Direct3DCalls.Release((void*)_vertices);
        Direct3DCalls.Release((void*)_constants);
        Direct3DCalls.Release((void*)_linear);
        Direct3DCalls.Release((void*)_nearest);
        Direct3DCalls.Release((void*)_rasterizer);
        Direct3DCalls.Release((void*)_blend);
        Direct3DCalls.Release((void*)_layout);
        Direct3DCalls.Release((void*)_pixelShader);
        Direct3DCalls.Release((void*)_vertexShader);
        _whiteView = 0;
        _white = 0;
        _vertices = 0;
        _constants = 0;
        _linear = 0;
        _nearest = 0;
        _rasterizer = 0;
        _blend = 0;
        _layout = 0;
        _pixelShader = 0;
        _vertexShader = 0;
    }

    /// <summary>Binds everything a frame draws with and copies the frame's vertices to the GPU.</summary>
    /// <param name="vertices">The vertices of the frame.</param>
    /// <exception cref="InvalidOperationException">Direct3D could not take the vertices.</exception>
    internal void BeginFrame(ReadOnlySpan<QuadVertex> vertices)
    {
        var context = _device.Context;
        Upload(vertices);
        var scale = stackalloc float[] { ClipSpan / _device.Width, ClipSpan / _device.Height, 0F, 0F };
        Direct3DCalls.UpdateSubresource(context, (void*)_constants, (byte*)scale, 0);
        var viewport = new Viewport(0, 0, _device.Width, _device.Height, 0, 1);
        Direct3DCalls.SetRenderTarget(context, _device.Target);
        Direct3DCalls.SetViewport(context, &viewport);
        Direct3DCalls.SetInputLayout(context, (void*)_layout);
        Direct3DCalls.SetPrimitiveTopology(context, TriangleList);
        Direct3DCalls.SetVertexBuffer(context, (void*)_vertices, (uint)sizeof(QuadVertex));
        Direct3DCalls.SetVertexShader(context, (void*)_vertexShader);
        Direct3DCalls.SetVertexConstantBuffer(context, (void*)_constants);
        Direct3DCalls.SetPixelShader(context, (void*)_pixelShader);
        Direct3DCalls.SetBlendState(context, (void*)_blend);
        Direct3DCalls.SetRasterizerState(context, (void*)_rasterizer);
    }

    /// <summary>Copies the frame's vertices into the vertex buffer, making a larger buffer when they do not fit.</summary>
    /// <param name="vertices">The vertices.</param>
    private void Upload(ReadOnlySpan<QuadVertex> vertices)
    {
        if (vertices.Length > _vertexCapacity)
        {
            Direct3DCalls.Release((void*)_vertices);
            _vertices = 0;
            _vertexCapacity = Math.Max(vertices.Length, _vertexCapacity + _vertexCapacity);
            _vertices = (ulong)CreateBuffer(new((uint)(_vertexCapacity * sizeof(QuadVertex)), UsageDynamic, BindVertexBuffer, CpuWrite, 0, 0), null);
        }

        if (vertices.IsEmpty)
        {
            return;
        }

        MappedSubresource mapped;
        HResult.Check(Direct3DCalls.Map(_device.Context, (void*)_vertices, MapWriteDiscard, &mapped), "write the frame's vertices");
        vertices.CopyTo(new((void*)mapped.Data, vertices.Length));
        Direct3DCalls.Unmap(_device.Context, (void*)_vertices);
    }

    /// <summary>Makes the two shaders and the vertex layout that matches the vertex shader's input.</summary>
    private void CreateShaders()
    {
        var device = _device.Device;
        fixed (byte* vertex = Direct3DShaderCode.Vertex)
        {
            void* shader;
            HResult.Check(Direct3DCalls.CreateVertexShader(device, vertex, (nuint)Direct3DShaderCode.Vertex.Length, &shader), "make the vertex shader");
            _vertexShader = (ulong)shader;
            fixed (byte* position = "POSITION\0"u8)
            {
                fixed (byte* uv = "TEXCOORD\0"u8)
                {
                    fixed (byte* color = "COLOR\0"u8)
                    {
                        var elements = stackalloc InputElementDescription[ElementCount];
                        elements[0] = new((ulong)position, 0, FormatFloat2, 0, 0, 0, 0);
                        elements[1] = new((ulong)uv, 0, FormatFloat2, 0, UvOffset, 0, 0);
                        elements[ElementCount - 1] = new((ulong)color, 0, FormatBytes4, 0, ColorOffset, 0, 0);
                        void* layout;
                        HResult.Check(
                            Direct3DCalls.CreateInputLayout(device, elements, ElementCount, vertex, (nuint)Direct3DShaderCode.Vertex.Length, &layout),
                            "make the vertex layout");
                        _layout = (ulong)layout;
                    }
                }
            }
        }

        fixed (byte* pixel = Direct3DShaderCode.Pixel)
        {
            void* shader;
            HResult.Check(Direct3DCalls.CreatePixelShader(device, pixel, (nuint)Direct3DShaderCode.Pixel.Length, &shader), "make the pixel shader");
            _pixelShader = (ulong)shader;
        }
    }

    /// <summary>Makes the blend, rasteriser and sampler states.</summary>
    private void CreateStates()
    {
        var device = _device.Device;
        var blend = stackalloc byte[BlendDescriptionSize];
        new Span<byte>(blend, BlendDescriptionSize).Clear();
        *(TargetBlendDescription*)(blend + FirstTargetOffset) = new(1, BlendOne, BlendInverseSourceAlpha, BlendAdd, BlendOne, BlendInverseSourceAlpha, BlendAdd, WriteAll);
        void* state;
        HResult.Check(Direct3DCalls.CreateBlendState(device, blend, &state), "make the blend state");
        _blend = (ulong)state;

        var rasterizer = new RasterizerDescription(FillSolid, CullNone, 0, 0, 0, 0, 1, 0, 0, 0);
        HResult.Check(Direct3DCalls.CreateRasterizerState(device, &rasterizer, &state), "make the rasteriser state");
        _rasterizer = (ulong)state;
        _nearest = (ulong)CreateSampler(FilterPoint);
        _linear = (ulong)CreateSampler(FilterLinear);
    }

    /// <summary>Makes the constant buffer, the first vertex buffer and the white texture.</summary>
    private void CreateBuffers()
    {
        _constants = (ulong)CreateBuffer(new(ConstantsSize, 0, BindConstantBuffer, 0, 0, 0), null);
        _vertexCapacity = FirstVertexCapacity;
        _vertices = (ulong)CreateBuffer(new((uint)(FirstVertexCapacity * sizeof(QuadVertex)), UsageDynamic, BindVertexBuffer, CpuWrite, 0, 0), null);

        var white = uint.MaxValue;
        var pixels = new SubresourceData((ulong)(&white), sizeof(uint), 0);
        var description = new Texture2DDescription(1, 1, 1, 1, FormatBgra, 1, 0, 0, BindShaderResource, 0, 0);
        void* texture;
        HResult.Check(Direct3DCalls.CreateTexture2D(_device.Device, &description, &pixels, &texture), "make the fill texture");
        _white = (ulong)texture;
        void* view;
        HResult.Check(Direct3DCalls.CreateShaderResourceView(_device.Device, texture, &view), "make the fill texture view");
        _whiteView = (ulong)view;
    }

    /// <summary>Makes a sampler that clamps at the edges.</summary>
    /// <param name="filter">The filter.</param>
    /// <returns>The sampler state.</returns>
    private void* CreateSampler(uint filter)
    {
        var description = new SamplerDescription(filter, AddressClamp, AddressClamp, AddressClamp, 0, 1, CompareNever, 0, 0, 0, 0, 0, float.MaxValue);
        void* state;
        HResult.Check(Direct3DCalls.CreateSamplerState(_device.Device, &description, &state), "make a sampler");
        return state;
    }

    /// <summary>Makes a buffer.</summary>
    /// <param name="description">The buffer description.</param>
    /// <param name="initial">The first contents, or null.</param>
    /// <returns>The buffer.</returns>
    private void* CreateBuffer(BufferDescription description, SubresourceData* initial)
    {
        void* buffer;
        HResult.Check(Direct3DCalls.CreateBuffer(_device.Device, &description, initial, &buffer), "make a buffer");
        return buffer;
    }
}
