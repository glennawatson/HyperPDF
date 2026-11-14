// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Rendering.Vulkan.Interop;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>A buffer in memory the CPU can write and the GPU can read, mapped for its whole life. It stages tile uploads and receives frame readbacks.</summary>
[DebuggerDisplay("VulkanBuffer: {Size} bytes")]
internal sealed unsafe class VulkanBuffer : IDisposable
{
    /// <summary>The size of a VkBufferCreateInfo in bytes.</summary>
    private const int CreateInfoSize = 56;

    /// <summary>The context.</summary>
    private readonly VulkanContext _context;

    /// <summary>The memory handle.</summary>
    private readonly ulong _memory;

    /// <summary>The mapped address, held as a number so no raw pointer is stored.</summary>
    private readonly long _mapped;

    /// <summary>Initializes a new instance of the <see cref="VulkanBuffer"/> class.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="size">The size in bytes.</param>
    /// <param name="usage">The buffer usage bits.</param>
    /// <exception cref="InvalidOperationException">The GPU could not create or back the buffer.</exception>
    internal VulkanBuffer(VulkanContext context, int size, uint usage)
    {
        _context = context;
        Size = size;
        byte* info = stackalloc byte[CreateInfoSize];
        var writer = new VkWriter(new(info, CreateInfoSize));
        writer.U32(VkEnums.BufferCreateInfoType);
        writer.U64(0);
        writer.U32(0);
        writer.U64((ulong)size);
        writer.U32(usage);
        ulong buffer;
        if (NativeMethods.CreateBuffer(context.Device, info, null, &buffer) != VkConstants.Success)
        {
            throw new InvalidOperationException("The GPU could not create a buffer.");
        }

        Handle = buffer;
        byte* requirements = stackalloc byte[VkEnums.MemoryRequirementsSize];
        NativeMethods.GetBufferMemoryRequirements(context.Device, buffer, requirements);
        _memory = VulkanMemory.Allocate(context, requirements, VkEnums.MemoryHostVisible | VkEnums.MemoryHostCoherent);
        _ = NativeMethods.BindBufferMemory(context.Device, buffer, _memory, 0);
        void* mapped;
        _ = NativeMethods.MapMemory(context.Device, _memory, 0, (ulong)size, 0, &mapped);
        _mapped = (long)mapped;
    }

    /// <summary>Gets the size in bytes.</summary>
    internal int Size { get; }

    /// <summary>Gets the buffer handle.</summary>
    internal ulong Handle { get; }

    /// <summary>Gets the mapped memory.</summary>
    internal Span<byte> Memory => new((void*)_mapped, Size);

    /// <inheritdoc/>
    public void Dispose()
    {
        NativeMethods.UnmapMemory(_context.Device, _memory);
        NativeMethods.DestroyBuffer(_context.Device, Handle, null);
        NativeMethods.FreeMemory(_context.Device, _memory, null);
    }
}
