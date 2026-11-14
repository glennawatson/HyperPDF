// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Rendering.Vulkan.Interop;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>Finds a memory type and allocates device memory for buffers and images.</summary>
internal static unsafe class VulkanMemory
{
    /// <summary>The most memory types a GPU lists.</summary>
    private const int MaxMemoryTypes = 32;

    /// <summary>Allocates memory that suits a resource and has the wanted properties.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="requirements">The VkMemoryRequirements of the resource, 24 bytes.</param>
    /// <param name="properties">The memory property bits the memory must have.</param>
    /// <returns>The memory handle.</returns>
    /// <exception cref="InvalidOperationException">No memory type fits, or the allocation failed.</exception>
    internal static ulong Allocate(VulkanContext context, byte* requirements, uint properties)
    {
        var size = *(ulong*)requirements;
        var typeIndex = FindType(context, *(uint*)(requirements + VkEnums.MemoryTypeBitsOffset), properties);
        byte* info = stackalloc byte[VkEnums.MemoryRequirementsSize];
        var writer = new VkWriter(new(info, VkEnums.MemoryRequirementsSize));
        writer.U32(VkEnums.MemoryAllocateInfoType);
        writer.U64(0);
        writer.U64(size);
        writer.U32((uint)typeIndex);
        ulong memory;
        return NativeMethods.AllocateMemory(context.Device, info, null, &memory) == VkConstants.Success
            ? memory
            : throw new InvalidOperationException("The GPU could not allocate memory.");
    }

    /// <summary>Finds a memory type that a resource may use and that has the wanted properties.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="typeBits">The memory types the resource may use, one bit each.</param>
    /// <param name="properties">The property bits the type must have.</param>
    /// <returns>The memory type index.</returns>
    /// <exception cref="InvalidOperationException">No memory type fits.</exception>
    private static int FindType(VulkanContext context, uint typeBits, uint properties)
    {
        byte* memory = stackalloc byte[VkEnums.MemoryPropertiesSize];
        NativeMethods.GetPhysicalDeviceMemoryProperties(context.PhysicalDevice, memory);
        var count = *(uint*)memory;
        for (var i = 0; i < count && i < MaxMemoryTypes; i++)
        {
            var flags = *(uint*)(memory + VkEnums.MemoryTypesOffset + (i * VkEnums.MemoryTypeSize));
            if ((typeBits & (1U << i)) != 0 && (flags & properties) == properties)
            {
                return i;
            }
        }

        throw new InvalidOperationException("The GPU has no memory type that fits.");
    }
}
