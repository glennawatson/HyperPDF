// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Rendering.Vulkan.Interop;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>Allocates, fills and frees the descriptor sets that let the tile shader sample a tile image.</summary>
internal static unsafe class VulkanDescriptors
{
    /// <summary>A size that holds any structure built here.</summary>
    private const int StructSize = 96;

    /// <summary>Allocates one descriptor set.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="pipelines">The pipelines that own the pool and the set layout.</param>
    /// <returns>The set.</returns>
    /// <exception cref="InvalidOperationException">The pool is full.</exception>
    internal static ulong Allocate(VulkanContext context, VulkanPipelines pipelines)
    {
        var layout = pipelines.SetLayout;
        byte* info = stackalloc byte[StructSize];
        var writer = new VkWriter(new(info, StructSize));
        writer.U32(VkEnums.DescriptorSetAllocateInfoType);
        writer.U64(0);
        writer.U64(pipelines.DescriptorPool);
        writer.U32(1);
        writer.U64((ulong)&layout);
        ulong set;
        return NativeMethods.AllocateDescriptorSets(context.Device, info, &set) == VkConstants.Success
            ? set
            : throw new InvalidOperationException("The GPU has no room for another tile.");
    }

    /// <summary>Points a descriptor set at an image and a sampler.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="set">The descriptor set.</param>
    /// <param name="sampler">The sampler.</param>
    /// <param name="view">The image view.</param>
    internal static void Write(VulkanContext context, ulong set, ulong sampler, ulong view)
    {
        byte* image = stackalloc byte[StructSize];
        var imageWriter = new VkWriter(new(image, StructSize));
        imageWriter.U64(sampler);
        imageWriter.U64(view);
        imageWriter.U32(VkEnums.LayoutShaderReadOnly);

        byte* write = stackalloc byte[StructSize];
        var writer = new VkWriter(new(write, StructSize));
        writer.U32(VkEnums.WriteDescriptorSetType);
        writer.U64(0);
        writer.U64(set);
        writer.U32(0);
        writer.U32(0);
        writer.U32(1);
        writer.U32(VkEnums.DescriptorCombinedImageSampler);
        writer.U64((ulong)image);
        writer.U64(0);
        writer.U64(0);
        NativeMethods.UpdateDescriptorSets(context.Device, 1, write, 0, null);
    }

    /// <summary>Returns two descriptor sets to the pool.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="pipelines">The pipelines that own the pool.</param>
    /// <param name="first">The first set, or zero.</param>
    /// <param name="second">The second set, or zero.</param>
    internal static void Free(VulkanContext context, VulkanPipelines pipelines, ulong first, ulong second)
    {
        ulong* sets = stackalloc ulong[] { first, second };
        _ = NativeMethods.FreeDescriptorSets(context.Device, pipelines.DescriptorPool, VkEnums.SetsPerTile, sets);
    }
}
