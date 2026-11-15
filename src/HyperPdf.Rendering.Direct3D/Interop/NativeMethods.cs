// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D.Interop;

/// <summary>Source generated entry points of d3d11.dll. Everything else in Direct3D is reached through COM interface pointers.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>The Direct3D 11 library.</summary>
    private const string Direct3D11 = "d3d11";

    /// <summary>Native <c>D3D11CreateDevice</c>.</summary>
    /// <param name="adapter">The adapter, or null for the default.</param>
    /// <param name="driverType">The driver type.</param>
    /// <param name="software">A software rasteriser module, or 0.</param>
    /// <param name="flags">The creation flags.</param>
    /// <param name="featureLevels">The feature levels to try, or null for the defaults.</param>
    /// <param name="featureLevelCount">The number of feature levels.</param>
    /// <param name="sdkVersion">The SDK version, 7.</param>
    /// <param name="device">Receives the ID3D11Device.</param>
    /// <param name="featureLevel">Receives the feature level chosen, or null.</param>
    /// <param name="context">Receives the ID3D11DeviceContext.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport(Direct3D11, EntryPoint = "D3D11CreateDevice")]
    internal static partial int CreateDevice(
        void* adapter,
        uint driverType,
        nint software,
        uint flags,
        uint* featureLevels,
        uint featureLevelCount,
        uint sdkVersion,
        void** device,
        uint* featureLevel,
        void** context);

    /// <summary>Native <c>D3D11CreateDeviceAndSwapChain</c>.</summary>
    /// <param name="adapter">The adapter, or null for the default.</param>
    /// <param name="driverType">The driver type.</param>
    /// <param name="software">A software rasteriser module, or 0.</param>
    /// <param name="flags">The creation flags.</param>
    /// <param name="featureLevels">The feature levels to try, or null for the defaults.</param>
    /// <param name="featureLevelCount">The number of feature levels.</param>
    /// <param name="sdkVersion">The SDK version, 7.</param>
    /// <param name="swapChainDescription">The DXGI_SWAP_CHAIN_DESC.</param>
    /// <param name="swapChain">Receives the IDXGISwapChain.</param>
    /// <param name="device">Receives the ID3D11Device.</param>
    /// <param name="featureLevel">Receives the feature level chosen, or null.</param>
    /// <param name="context">Receives the ID3D11DeviceContext.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport(Direct3D11, EntryPoint = "D3D11CreateDeviceAndSwapChain")]
    internal static partial int CreateDeviceAndSwapChain(
        void* adapter,
        uint driverType,
        nint software,
        uint flags,
        uint* featureLevels,
        uint featureLevelCount,
        uint sdkVersion,
        SwapChainDescription* swapChainDescription,
        void** swapChain,
        void** device,
        uint* featureLevel,
        void** context);
}
