// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Rendering.Vulkan.Interop;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>One command buffer, its pool and a fence. A frame records into it, submits it and waits, so the next frame can reuse everything.</summary>
[DebuggerDisplay("VulkanFrameCommands")]
internal sealed unsafe class VulkanFrameCommands : IDisposable
{
    /// <summary>A size that holds any structure built here.</summary>
    private const int StructSize = 96;

    /// <summary>The context.</summary>
    private readonly VulkanContext _context;

    /// <summary>The command pool.</summary>
    private readonly ulong _pool;

    /// <summary>The fence signalled when a submission finishes.</summary>
    private readonly ulong _fence;

    /// <summary>Initializes a new instance of the <see cref="VulkanFrameCommands"/> class.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <exception cref="InvalidOperationException">The GPU could not create the objects.</exception>
    internal VulkanFrameCommands(VulkanContext context)
    {
        _context = context;
        byte* poolInfo = stackalloc byte[StructSize];
        var pool = new VkWriter(new(poolInfo, StructSize));
        pool.U32(VkEnums.CommandPoolCreateInfoType);
        pool.U64(0);
        pool.U32(0);
        pool.U32(context.QueueFamily);
        ulong poolHandle;
        if (NativeMethods.CreateCommandPool(context.Device, poolInfo, null, &poolHandle) != VkConstants.Success)
        {
            throw new InvalidOperationException("The GPU could not create a command pool.");
        }

        _pool = poolHandle;
        byte* allocateInfo = stackalloc byte[StructSize];
        var allocate = new VkWriter(new(allocateInfo, StructSize));
        allocate.U32(VkEnums.CommandBufferAllocateInfoType);
        allocate.U64(0);
        allocate.U64(poolHandle);
        allocate.U32(VkEnums.CommandBufferPrimary);
        allocate.U32(1);
        ulong buffer;
        if (NativeMethods.AllocateCommandBuffers(context.Device, allocateInfo, &buffer) != VkConstants.Success)
        {
            throw new InvalidOperationException("The GPU could not allocate a command buffer.");
        }

        Buffer = buffer;
        byte* fenceInfo = stackalloc byte[StructSize];
        var fence = new VkWriter(new(fenceInfo, StructSize));
        fence.U32(VkEnums.FenceCreateInfoType);
        fence.U64(0);
        fence.U32(0);
        ulong fenceHandle;
        if (NativeMethods.CreateFence(context.Device, fenceInfo, null, &fenceHandle) != VkConstants.Success)
        {
            throw new InvalidOperationException("The GPU could not create a fence.");
        }

        _fence = fenceHandle;
    }

    /// <summary>Gets the command buffer handle.</summary>
    internal ulong Buffer { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        NativeMethods.DestroyFence(_context.Device, _fence, null);
        NativeMethods.DestroyCommandPool(_context.Device, _pool, null);
    }

    /// <summary>Starts recording a new command buffer.</summary>
    internal void Begin()
    {
        _ = NativeMethods.ResetCommandPool(_context.Device, _pool, 0);
        byte* info = stackalloc byte[StructSize];
        var writer = new VkWriter(new(info, StructSize));
        writer.U32(VkEnums.CommandBufferBeginInfoType);
        writer.U64(0);
        writer.U32(VkEnums.CommandBufferOneTimeSubmit);
        writer.U64(0);
        _ = NativeMethods.BeginCommandBuffer(Buffer, info);
    }

    /// <summary>Finishes recording, submits the commands and waits for the GPU to finish them.</summary>
    /// <exception cref="InvalidOperationException">The GPU did not finish in time.</exception>
    internal void SubmitAndWait()
    {
        _ = NativeMethods.EndCommandBuffer(Buffer);
        var buffer = Buffer;
        byte* submit = stackalloc byte[StructSize];
        var writer = new VkWriter(new(submit, StructSize));
        writer.U32(VkEnums.SubmitInfoType);
        writer.U64(0);
        writer.U32(0);
        writer.U64(0);
        writer.U64(0);
        writer.U32(1);
        writer.U64((ulong)&buffer);
        writer.U32(0);
        writer.U64(0);
        var fence = _fence;
        _ = NativeMethods.QueueSubmit(_context.Queue, 1, submit, fence);
        var result = NativeMethods.WaitForFences(_context.Device, 1, &fence, 1, VkEnums.FenceTimeout);
        _ = NativeMethods.ResetFences(_context.Device, 1, &fence);
        if (result != VkConstants.Success)
        {
            throw new InvalidOperationException("The GPU did not finish the frame in time.");
        }
    }
}
