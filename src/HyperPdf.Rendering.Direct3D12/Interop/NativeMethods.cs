// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>Source generated entry points of d3d12.dll, dxgi.dll and kernel32.dll. Everything else in Direct3D 12 is reached through COM interface pointers.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>The Direct3D 12 library.</summary>
    private const string Direct3D12 = "d3d12";

    /// <summary>The DXGI library.</summary>
    private const string Dxgi = "dxgi";

    /// <summary>The kernel library.</summary>
    private const string Kernel32 = "kernel32";

    /// <summary>Native <c>D3D12CreateDevice</c>.</summary>
    /// <param name="adapter">The adapter, or null for the default.</param>
    /// <param name="minimumFeatureLevel">The lowest feature level the device must have.</param>
    /// <param name="interfaceId">The interface the device is asked for.</param>
    /// <param name="device">Receives the ID3D12Device.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport(Direct3D12, EntryPoint = "D3D12CreateDevice")]
    internal static partial int CreateDevice(void* adapter, uint minimumFeatureLevel, Guid* interfaceId, void** device);

    /// <summary>Native <c>D3D12SerializeRootSignature</c>.</summary>
    /// <param name="description">The root signature description.</param>
    /// <param name="version">The root signature version.</param>
    /// <param name="blob">Receives the ID3DBlob with the serialised signature.</param>
    /// <param name="error">Receives the ID3DBlob with the error text, or null.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport(Direct3D12, EntryPoint = "D3D12SerializeRootSignature")]
    internal static partial int SerializeRootSignature(RootSignatureDescription* description, uint version, void** blob, void** error);

    /// <summary>Native <c>CreateDXGIFactory2</c>.</summary>
    /// <param name="flags">The creation flags.</param>
    /// <param name="interfaceId">The interface the factory is asked for.</param>
    /// <param name="factory">Receives the factory.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport(Dxgi, EntryPoint = "CreateDXGIFactory2")]
    internal static partial int CreateFactory(uint flags, Guid* interfaceId, void** factory);

    /// <summary>Native <c>CreateEventW</c> for an unnamed event that resets itself.</summary>
    /// <param name="attributes">The security attributes, or 0.</param>
    /// <param name="manualReset">1 when the event stays set until reset.</param>
    /// <param name="initialState">1 when the event starts set.</param>
    /// <param name="name">The name, or 0.</param>
    /// <returns>The event handle, or 0.</returns>
    [LibraryImport(Kernel32, EntryPoint = "CreateEventW")]
    internal static partial nint CreateEvent(nint attributes, int manualReset, int initialState, nint name);

    /// <summary>Native <c>WaitForSingleObject</c>.</summary>
    /// <param name="handle">The handle.</param>
    /// <param name="milliseconds">How long to wait at most.</param>
    /// <returns>0 when the handle was signalled.</returns>
    [LibraryImport(Kernel32, EntryPoint = "WaitForSingleObject")]
    internal static partial uint WaitForSingleObject(nint handle, uint milliseconds);

    /// <summary>Native <c>CloseHandle</c>.</summary>
    /// <param name="handle">The handle.</param>
    /// <returns>Non zero on success.</returns>
    [LibraryImport(Kernel32, EntryPoint = "CloseHandle")]
    internal static partial int CloseHandle(nint handle);
}
