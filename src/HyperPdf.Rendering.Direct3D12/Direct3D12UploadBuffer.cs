// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Rendering.Direct3D12.Interop;

namespace HyperPdf.Rendering.Direct3D12;

/// <summary>A buffer in upload memory that stays mapped, so the CPU writes into it directly and the GPU reads from it. It only grows, and only while the GPU is idle.</summary>
/// <param name="device">The device that makes the buffer.</param>
[DebuggerDisplay("Direct3D12UploadBuffer: {Capacity} bytes")]
internal sealed unsafe class Direct3D12UploadBuffer(Direct3D12Device device) : IDisposable
{
    /// <summary>The smallest buffer made, in bytes.</summary>
    private const int MinimumCapacity = 64 * 1024;

    /// <summary>The ID3D12Resource, or 0 before the first <see cref="Ensure"/>.</summary>
    private ulong _resource;

    /// <summary>Gets the ID3D12Resource.</summary>
    internal void* Resource => (void*)_resource;

    /// <summary>Gets the first byte of the mapped memory.</summary>
    internal byte* Mapped { get; private set; }

    /// <summary>Gets the size of the buffer in bytes.</summary>
    internal int Capacity { get; private set; }

    /// <summary>Gets the GPU address of the buffer.</summary>
    internal ulong GpuAddress => Direct3D12Calls.GpuAddress((void*)_resource);

    /// <inheritdoc/>
    public void Dispose()
    {
        Release();
        Capacity = 0;
    }

    /// <summary>Makes the buffer at least this big. A larger buffer replaces the old one, which the GPU must no longer use.</summary>
    /// <param name="bytes">The size needed.</param>
    /// <exception cref="InvalidOperationException">Direct3D 12 could not make or map the buffer.</exception>
    internal void Ensure(int bytes)
    {
        if (bytes <= Capacity)
        {
            return;
        }

        var capacity = Math.Max(Math.Max(bytes, Capacity + Capacity), MinimumCapacity);
        Release();
        Capacity = 0;
        _resource = (ulong)device.CreateUploadBuffer((ulong)capacity);
        Capacity = capacity;
        void* mapped;
        HResult.Check(Direct3D12Calls.Map((void*)_resource, &mapped), "map an upload buffer");
        Mapped = (byte*)mapped;
    }

    /// <summary>Unmaps and releases the buffer.</summary>
    private void Release()
    {
        if (_resource != 0)
        {
            Direct3D12Calls.Unmap((void*)_resource);
            Direct3D12Calls.Release((void*)_resource);
        }

        _resource = 0;
        Mapped = null;
    }
}
