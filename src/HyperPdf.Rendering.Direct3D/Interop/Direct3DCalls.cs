// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Rendering.Direct3D.Interop;

/// <summary>
/// Calls the methods of the Direct3D 11 and DXGI COM interfaces the backend uses, each through its slot in the interface's method
/// table. The slot numbers follow the declaration order in d3d11.h and dxgi.h.
/// </summary>
internal static unsafe class Direct3DCalls
{
    /// <summary>The slot of IUnknown::Release.</summary>
    private const int ReleaseSlot = 2;

    /// <summary>The slot of ID3D11Device::CreateBuffer.</summary>
    private const int CreateBufferSlot = 3;

    /// <summary>The slot of ID3D11Device::CreateTexture2D.</summary>
    private const int CreateTexture2DSlot = 5;

    /// <summary>The slot of ID3D11Device::CreateShaderResourceView.</summary>
    private const int CreateShaderResourceViewSlot = 7;

    /// <summary>The slot of ID3D11Device::CreateRenderTargetView.</summary>
    private const int CreateRenderTargetViewSlot = 9;

    /// <summary>The slot of ID3D11Device::CreateInputLayout.</summary>
    private const int CreateInputLayoutSlot = 11;

    /// <summary>The slot of ID3D11Device::CreateVertexShader.</summary>
    private const int CreateVertexShaderSlot = 12;

    /// <summary>The slot of ID3D11Device::CreatePixelShader.</summary>
    private const int CreatePixelShaderSlot = 15;

    /// <summary>The slot of ID3D11Device::CreateBlendState.</summary>
    private const int CreateBlendStateSlot = 20;

    /// <summary>The slot of ID3D11Device::CreateRasterizerState.</summary>
    private const int CreateRasterizerStateSlot = 22;

    /// <summary>The slot of ID3D11Device::CreateSamplerState.</summary>
    private const int CreateSamplerStateSlot = 23;

    /// <summary>The slot of ID3D11DeviceContext::VSSetConstantBuffers.</summary>
    private const int VertexSetConstantBuffersSlot = 7;

    /// <summary>The slot of ID3D11DeviceContext::PSSetShaderResources.</summary>
    private const int PixelSetShaderResourcesSlot = 8;

    /// <summary>The slot of ID3D11DeviceContext::PSSetShader.</summary>
    private const int PixelSetShaderSlot = 9;

    /// <summary>The slot of ID3D11DeviceContext::PSSetSamplers.</summary>
    private const int PixelSetSamplersSlot = 10;

    /// <summary>The slot of ID3D11DeviceContext::VSSetShader.</summary>
    private const int VertexSetShaderSlot = 11;

    /// <summary>The slot of ID3D11DeviceContext::Draw.</summary>
    private const int DrawSlot = 13;

    /// <summary>The slot of ID3D11DeviceContext::Map.</summary>
    private const int MapSlot = 14;

    /// <summary>The slot of ID3D11DeviceContext::Unmap.</summary>
    private const int UnmapSlot = 15;

    /// <summary>The slot of ID3D11DeviceContext::IASetInputLayout.</summary>
    private const int SetInputLayoutSlot = 17;

    /// <summary>The slot of ID3D11DeviceContext::IASetVertexBuffers.</summary>
    private const int SetVertexBuffersSlot = 18;

    /// <summary>The slot of ID3D11DeviceContext::IASetPrimitiveTopology.</summary>
    private const int SetPrimitiveTopologySlot = 24;

    /// <summary>The slot of ID3D11DeviceContext::OMSetRenderTargets.</summary>
    private const int SetRenderTargetsSlot = 33;

    /// <summary>The slot of ID3D11DeviceContext::OMSetBlendState.</summary>
    private const int SetBlendStateSlot = 35;

    /// <summary>The slot of ID3D11DeviceContext::RSSetState.</summary>
    private const int SetRasterizerStateSlot = 43;

    /// <summary>The slot of ID3D11DeviceContext::RSSetViewports.</summary>
    private const int SetViewportsSlot = 44;

    /// <summary>The slot of ID3D11DeviceContext::CopyResource.</summary>
    private const int CopyResourceSlot = 47;

    /// <summary>The slot of ID3D11DeviceContext::UpdateSubresource.</summary>
    private const int UpdateSubresourceSlot = 48;

    /// <summary>The slot of ID3D11DeviceContext::ClearRenderTargetView.</summary>
    private const int ClearRenderTargetViewSlot = 50;

    /// <summary>The slot of IDXGISwapChain::Present.</summary>
    private const int PresentSlot = 8;

    /// <summary>The slot of IDXGISwapChain::GetBuffer.</summary>
    private const int GetBufferSlot = 9;

    /// <summary>The slot of IDXGISwapChain::ResizeBuffers.</summary>
    private const int ResizeBuffersSlot = 13;

    /// <summary>Releases a COM object, if there is one.</summary>
    /// <param name="unknown">The object, or null.</param>
    internal static void Release(void* unknown)
    {
        if (unknown is not null)
        {
            _ = ((delegate* unmanaged[Stdcall]<void*, uint>)Slot(unknown, ReleaseSlot))(unknown);
        }
    }

    /// <summary>Calls ID3D11Device::CreateBuffer.</summary>
    /// <param name="device">The device.</param>
    /// <param name="description">The buffer description.</param>
    /// <param name="initial">The first contents, or null.</param>
    /// <param name="buffer">Receives the buffer.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CreateBuffer(void* device, BufferDescription* description, SubresourceData* initial, void** buffer) =>
        ((delegate* unmanaged[Stdcall]<void*, BufferDescription*, SubresourceData*, void**, int>)Slot(device, CreateBufferSlot))(device, description, initial, buffer);

    /// <summary>Calls ID3D11Device::CreateTexture2D.</summary>
    /// <param name="device">The device.</param>
    /// <param name="description">The texture description.</param>
    /// <param name="initial">The first contents, or null.</param>
    /// <param name="texture">Receives the texture.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CreateTexture2D(void* device, Texture2DDescription* description, SubresourceData* initial, void** texture) =>
        ((delegate* unmanaged[Stdcall]<void*, Texture2DDescription*, SubresourceData*, void**, int>)Slot(device, CreateTexture2DSlot))(device, description, initial, texture);

    /// <summary>Calls ID3D11Device::CreateShaderResourceView with the default view of the whole resource.</summary>
    /// <param name="device">The device.</param>
    /// <param name="resource">The texture.</param>
    /// <param name="view">Receives the view.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CreateShaderResourceView(void* device, void* resource, void** view) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, void*, void**, int>)Slot(device, CreateShaderResourceViewSlot))(device, resource, null, view);

    /// <summary>Calls ID3D11Device::CreateRenderTargetView with the default view of the whole resource.</summary>
    /// <param name="device">The device.</param>
    /// <param name="resource">The texture.</param>
    /// <param name="view">Receives the view.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CreateRenderTargetView(void* device, void* resource, void** view) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, void*, void**, int>)Slot(device, CreateRenderTargetViewSlot))(device, resource, null, view);

    /// <summary>Calls ID3D11Device::CreateInputLayout.</summary>
    /// <param name="device">The device.</param>
    /// <param name="elements">The vertex elements.</param>
    /// <param name="count">The number of elements.</param>
    /// <param name="shader">The vertex shader bytecode the layout is checked against.</param>
    /// <param name="shaderLength">The bytecode length.</param>
    /// <param name="layout">Receives the layout.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CreateInputLayout(void* device, InputElementDescription* elements, uint count, byte* shader, nuint shaderLength, void** layout) =>
        ((delegate* unmanaged[Stdcall]<void*, InputElementDescription*, uint, byte*, nuint, void**, int>)Slot(device, CreateInputLayoutSlot))(device, elements, count, shader, shaderLength, layout);

    /// <summary>Calls ID3D11Device::CreateVertexShader.</summary>
    /// <param name="device">The device.</param>
    /// <param name="shader">The bytecode.</param>
    /// <param name="shaderLength">The bytecode length.</param>
    /// <param name="vertexShader">Receives the shader.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CreateVertexShader(void* device, byte* shader, nuint shaderLength, void** vertexShader) =>
        ((delegate* unmanaged[Stdcall]<void*, byte*, nuint, void*, void**, int>)Slot(device, CreateVertexShaderSlot))(device, shader, shaderLength, null, vertexShader);

    /// <summary>Calls ID3D11Device::CreatePixelShader.</summary>
    /// <param name="device">The device.</param>
    /// <param name="shader">The bytecode.</param>
    /// <param name="shaderLength">The bytecode length.</param>
    /// <param name="pixelShader">Receives the shader.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CreatePixelShader(void* device, byte* shader, nuint shaderLength, void** pixelShader) =>
        ((delegate* unmanaged[Stdcall]<void*, byte*, nuint, void*, void**, int>)Slot(device, CreatePixelShaderSlot))(device, shader, shaderLength, null, pixelShader);

    /// <summary>Calls ID3D11Device::CreateBlendState.</summary>
    /// <param name="device">The device.</param>
    /// <param name="description">The D3D11_BLEND_DESC bytes.</param>
    /// <param name="state">Receives the state.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CreateBlendState(void* device, byte* description, void** state) =>
        ((delegate* unmanaged[Stdcall]<void*, byte*, void**, int>)Slot(device, CreateBlendStateSlot))(device, description, state);

    /// <summary>Calls ID3D11Device::CreateRasterizerState.</summary>
    /// <param name="device">The device.</param>
    /// <param name="description">The rasteriser description.</param>
    /// <param name="state">Receives the state.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CreateRasterizerState(void* device, RasterizerDescription* description, void** state) =>
        ((delegate* unmanaged[Stdcall]<void*, RasterizerDescription*, void**, int>)Slot(device, CreateRasterizerStateSlot))(device, description, state);

    /// <summary>Calls ID3D11Device::CreateSamplerState.</summary>
    /// <param name="device">The device.</param>
    /// <param name="description">The sampler description.</param>
    /// <param name="state">Receives the state.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CreateSamplerState(void* device, SamplerDescription* description, void** state) =>
        ((delegate* unmanaged[Stdcall]<void*, SamplerDescription*, void**, int>)Slot(device, CreateSamplerStateSlot))(device, description, state);

    /// <summary>Calls ID3D11DeviceContext::VSSetConstantBuffers for slot 0.</summary>
    /// <param name="context">The context.</param>
    /// <param name="buffer">The constant buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetVertexConstantBuffer(void* context, void* buffer) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, uint, void**, void>)Slot(context, VertexSetConstantBuffersSlot))(context, 0, 1, &buffer);

    /// <summary>Calls ID3D11DeviceContext::PSSetShaderResources for slot 0.</summary>
    /// <param name="context">The context.</param>
    /// <param name="view">The shader resource view.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetPixelTexture(void* context, void* view) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, uint, void**, void>)Slot(context, PixelSetShaderResourcesSlot))(context, 0, 1, &view);

    /// <summary>Calls ID3D11DeviceContext::PSSetShader.</summary>
    /// <param name="context">The context.</param>
    /// <param name="shader">The pixel shader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetPixelShader(void* context, void* shader) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, void*, uint, void>)Slot(context, PixelSetShaderSlot))(context, shader, null, 0);

    /// <summary>Calls ID3D11DeviceContext::PSSetSamplers for slot 0.</summary>
    /// <param name="context">The context.</param>
    /// <param name="sampler">The sampler state.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetPixelSampler(void* context, void* sampler) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, uint, void**, void>)Slot(context, PixelSetSamplersSlot))(context, 0, 1, &sampler);

    /// <summary>Calls ID3D11DeviceContext::VSSetShader.</summary>
    /// <param name="context">The context.</param>
    /// <param name="shader">The vertex shader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetVertexShader(void* context, void* shader) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, void*, uint, void>)Slot(context, VertexSetShaderSlot))(context, shader, null, 0);

    /// <summary>Calls ID3D11DeviceContext::Draw.</summary>
    /// <param name="context">The context.</param>
    /// <param name="vertexCount">The number of vertices.</param>
    /// <param name="firstVertex">The first vertex.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Draw(void* context, uint vertexCount, uint firstVertex) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, uint, void>)Slot(context, DrawSlot))(context, vertexCount, firstVertex);

    /// <summary>Calls ID3D11DeviceContext::Map on the first subresource.</summary>
    /// <param name="context">The context.</param>
    /// <param name="resource">The resource.</param>
    /// <param name="mapType">How the memory is used.</param>
    /// <param name="mapped">Receives the memory.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Map(void* context, void* resource, uint mapType, MappedSubresource* mapped) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, uint, uint, uint, MappedSubresource*, int>)Slot(context, MapSlot))(context, resource, 0, mapType, 0, mapped);

    /// <summary>Calls ID3D11DeviceContext::Unmap on the first subresource.</summary>
    /// <param name="context">The context.</param>
    /// <param name="resource">The resource.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Unmap(void* context, void* resource) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, uint, void>)Slot(context, UnmapSlot))(context, resource, 0);

    /// <summary>Calls ID3D11DeviceContext::IASetInputLayout.</summary>
    /// <param name="context">The context.</param>
    /// <param name="layout">The input layout.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetInputLayout(void* context, void* layout) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, void>)Slot(context, SetInputLayoutSlot))(context, layout);

    /// <summary>Calls ID3D11DeviceContext::IASetVertexBuffers for slot 0.</summary>
    /// <param name="context">The context.</param>
    /// <param name="buffer">The vertex buffer.</param>
    /// <param name="stride">The bytes per vertex.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetVertexBuffer(void* context, void* buffer, uint stride)
    {
        uint offset = 0;
        ((delegate* unmanaged[Stdcall]<void*, uint, uint, void**, uint*, uint*, void>)Slot(context, SetVertexBuffersSlot))(context, 0, 1, &buffer, &stride, &offset);
    }

    /// <summary>Calls ID3D11DeviceContext::IASetPrimitiveTopology.</summary>
    /// <param name="context">The context.</param>
    /// <param name="topology">The topology.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetPrimitiveTopology(void* context, uint topology) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, void>)Slot(context, SetPrimitiveTopologySlot))(context, topology);

    /// <summary>Calls ID3D11DeviceContext::OMSetRenderTargets with one target and no depth buffer.</summary>
    /// <param name="context">The context.</param>
    /// <param name="target">The render target view.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetRenderTarget(void* context, void* target) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, void**, void*, void>)Slot(context, SetRenderTargetsSlot))(context, 1, &target, null);

    /// <summary>Calls ID3D11DeviceContext::OMSetBlendState with no blend factor and every sample.</summary>
    /// <param name="context">The context.</param>
    /// <param name="state">The blend state.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetBlendState(void* context, void* state) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, float*, uint, void>)Slot(context, SetBlendStateSlot))(context, state, null, uint.MaxValue);

    /// <summary>Calls ID3D11DeviceContext::RSSetState.</summary>
    /// <param name="context">The context.</param>
    /// <param name="state">The rasteriser state.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetRasterizerState(void* context, void* state) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, void>)Slot(context, SetRasterizerStateSlot))(context, state);

    /// <summary>Calls ID3D11DeviceContext::RSSetViewports with one viewport.</summary>
    /// <param name="context">The context.</param>
    /// <param name="viewport">The viewport.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetViewport(void* context, Viewport* viewport) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, Viewport*, void>)Slot(context, SetViewportsSlot))(context, 1, viewport);

    /// <summary>Calls ID3D11DeviceContext::CopyResource.</summary>
    /// <param name="context">The context.</param>
    /// <param name="destination">The destination resource.</param>
    /// <param name="source">The source resource.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CopyResource(void* context, void* destination, void* source) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, void*, void>)Slot(context, CopyResourceSlot))(context, destination, source);

    /// <summary>Calls ID3D11DeviceContext::UpdateSubresource for the whole first subresource.</summary>
    /// <param name="context">The context.</param>
    /// <param name="resource">The destination resource.</param>
    /// <param name="data">The new contents.</param>
    /// <param name="rowPitch">The bytes per row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void UpdateSubresource(void* context, void* resource, byte* data, uint rowPitch) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, uint, void*, byte*, uint, uint, void>)Slot(context, UpdateSubresourceSlot))(context, resource, 0, null, data, rowPitch, 0);

    /// <summary>Calls ID3D11DeviceContext::ClearRenderTargetView.</summary>
    /// <param name="context">The context.</param>
    /// <param name="target">The render target view.</param>
    /// <param name="color">Four floats: red, green, blue and alpha.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ClearRenderTarget(void* context, void* target, float* color) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, float*, void>)Slot(context, ClearRenderTargetViewSlot))(context, target, color);

    /// <summary>Calls IDXGISwapChain::Present.</summary>
    /// <param name="swapChain">The swap chain.</param>
    /// <param name="syncInterval">The display refreshes to wait for.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Present(void* swapChain, uint syncInterval) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, uint, int>)Slot(swapChain, PresentSlot))(swapChain, syncInterval, 0);

    /// <summary>Calls IDXGISwapChain::GetBuffer for the back buffer as a texture.</summary>
    /// <param name="swapChain">The swap chain.</param>
    /// <param name="texture">Receives the ID3D11Texture2D.</param>
    /// <returns>An HRESULT.</returns>
    internal static int GetBackBuffer(void* swapChain, void** texture)
    {
        // IID_ID3D11Texture2D, 6f15aaf2-d208-4e89-9ab4-489535d34f9c.
        var texture2D = new Guid(0x6F15AAF2, 0xD208, 0x4E89, 0x9A, 0xB4, 0x48, 0x95, 0x35, 0xD3, 0x4F, 0x9C);
        return ((delegate* unmanaged[Stdcall]<void*, uint, Guid*, void**, int>)Slot(swapChain, GetBufferSlot))(swapChain, 0, &texture2D, texture);
    }

    /// <summary>Calls IDXGISwapChain::ResizeBuffers, keeping the buffer count and format.</summary>
    /// <param name="swapChain">The swap chain.</param>
    /// <param name="width">The new width.</param>
    /// <param name="height">The new height.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ResizeBuffers(void* swapChain, uint width, uint height) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, uint, uint, uint, uint, int>)Slot(swapChain, ResizeBuffersSlot))(swapChain, 0, width, height, 0, 0);

    /// <summary>Reads a method pointer from an interface's method table.</summary>
    /// <param name="instance">The interface pointer.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The method pointer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void* Slot(void* instance, int slot) => (*(void***)instance)[slot];
}
