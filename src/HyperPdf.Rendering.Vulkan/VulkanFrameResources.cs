// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using HyperPdf.Rendering.Vulkan.Interop;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>Everything a frame uses on the GPU: the fixed pipelines, the command buffer, the image being drawn into and the staging buffer for tile uploads.</summary>
[DebuggerDisplay("VulkanFrameResources")]
internal sealed class VulkanFrameResources : IDisposable
{
    /// <summary>The smallest staging buffer, so small frames do not recreate it often.</summary>
    private const int MinimumStaging = 1 << 20;

    /// <summary>Initializes a new instance of the <see cref="VulkanFrameResources"/> class.</summary>
    /// <param name="context">The Vulkan context.</param>
    internal VulkanFrameResources(VulkanContext context)
    {
        Context = context;
        Pipelines = new(context);
        Commands = new(context);
    }

    /// <summary>Gets the Vulkan context.</summary>
    internal VulkanContext Context { get; }

    /// <summary>Gets the fixed pipelines.</summary>
    internal VulkanPipelines Pipelines { get; }

    /// <summary>Gets the command buffer.</summary>
    internal VulkanFrameCommands Commands { get; }

    /// <summary>Gets the image being drawn into, or <see langword="null"/> before the first resize.</summary>
    internal VulkanFrameTarget? TargetOrNull { get; private set; }

    /// <summary>Gets the image being drawn into.</summary>
    /// <exception cref="InvalidOperationException">The backend has not been resized yet.</exception>
    internal VulkanFrameTarget Target => TargetOrNull ?? throw new InvalidOperationException("The Vulkan backend has no size yet.");

    /// <summary>Gets the staging buffer, or <see langword="null"/> before the first upload.</summary>
    internal VulkanBuffer? Staging { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _ = NativeMethods.DeviceWaitIdle(Context.Device);
        Staging?.Dispose();
        TargetOrNull?.Dispose();
        Commands.Dispose();
        Pipelines.Dispose();
    }

    /// <summary>Replaces the frame image with one of a new size.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    internal void Resize(int width, int height)
    {
        _ = NativeMethods.DeviceWaitIdle(Context.Device);
        TargetOrNull?.Dispose();
        TargetOrNull = null;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var target = new VulkanFrameTarget(Context, Pipelines.RenderPass, width, height);
        Commands.Begin();
        VulkanCommands.Barrier(
            Commands.Buffer,
            target.Image.Handle,
            new(VkEnums.LayoutUndefined, VkEnums.LayoutColorAttachment, 0, VkEnums.AccessColorAttachmentWrite, VkEnums.StageTopOfPipe, VkEnums.StageColorAttachmentOutput));
        Commands.SubmitAndWait();
        TargetOrNull = target;
    }

    /// <summary>Makes sure the staging buffer holds at least a number of bytes.</summary>
    /// <param name="bytes">The bytes needed.</param>
    internal void EnsureStaging(int bytes)
    {
        if (bytes == 0 || (Staging is not null && Staging.Size >= bytes))
        {
            return;
        }

        Staging?.Dispose();
        Staging = new(Context, Math.Max(bytes, MinimumStaging), VkEnums.BufferUsageTransferSource | VkEnums.BufferUsageTransferDestination);
    }

    /// <summary>Frees the GPU objects of tiles that were disposed, which is safe because the last frame has finished.</summary>
    /// <param name="retired">The queue of disposed tiles' objects.</param>
    internal void ReleaseRetired(ConcurrentQueue<VulkanRetired> retired)
    {
        while (retired.TryDequeue(out var tile))
        {
            VulkanDescriptors.Free(Context, Pipelines, tile.NearestSet, tile.LinearSet);
            tile.Image?.Dispose();
        }
    }
}
