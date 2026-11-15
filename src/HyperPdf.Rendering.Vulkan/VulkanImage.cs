// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Rendering.Vulkan.Interop;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>A 2D BGRA image in GPU memory with a view, used for tiles and for the frame the compositor draws.</summary>
[DebuggerDisplay("VulkanImage: {Width} x {Height}")]
internal sealed unsafe class VulkanImage : IDisposable
{
    /// <summary>A size that holds any structure built here.</summary>
    private const int InfoSize = 96;

    /// <summary>The image type 2D.</summary>
    private const uint Type2D = 1;

    /// <summary>The sample count of one sample per pixel.</summary>
    private const uint OneSample = 1;

    /// <summary>The context.</summary>
    private readonly VulkanContext _context;

    /// <summary>The memory handle.</summary>
    private readonly ulong _memory;

    /// <summary>Initializes a new instance of the <see cref="VulkanImage"/> class.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="usage">The image usage bits.</param>
    /// <exception cref="InvalidOperationException">The GPU could not create or back the image.</exception>
    internal VulkanImage(VulkanContext context, int width, int height, uint usage)
    {
        _context = context;
        Width = width;
        Height = height;
        byte* info = stackalloc byte[InfoSize];
        var create = new VkWriter(new(info, InfoSize));
        create.U32(VkEnums.ImageCreateInfoType);
        create.U64(0);
        create.U32(0);
        create.U32(Type2D);
        create.U32(VkEnums.FormatB8G8R8A8Unorm);
        create.U32((uint)width);
        create.U32((uint)height);
        create.U32(1);
        create.U32(1);
        create.U32(1);
        create.U32(OneSample);
        create.U32(0);
        create.U32(usage);
        create.U32(0);
        create.U32(0);
        create.U64(0);
        create.U32(VkEnums.LayoutUndefined);
        ulong image;
        if (NativeMethods.CreateImage(context.Device, info, null, &image) != VkConstants.Success)
        {
            throw new InvalidOperationException("The GPU could not create an image.");
        }

        Handle = image;
        byte* requirements = stackalloc byte[VkEnums.MemoryRequirementsSize];
        NativeMethods.GetImageMemoryRequirements(context.Device, image, requirements);
        _memory = VulkanMemory.Allocate(context, requirements, VkEnums.MemoryDeviceLocal);
        _ = NativeMethods.BindImageMemory(context.Device, image, _memory, 0);
        View = CreateView(context, image);
    }

    /// <summary>Gets the width in pixels.</summary>
    internal int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    internal int Height { get; }

    /// <summary>Gets the image handle.</summary>
    internal ulong Handle { get; }

    /// <summary>Gets the view handle.</summary>
    internal ulong View { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        NativeMethods.DestroyImageView(_context.Device, View, null);
        NativeMethods.DestroyImage(_context.Device, Handle, null);
        NativeMethods.FreeMemory(_context.Device, _memory, null);
    }

    /// <summary>Creates the view of an image.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="image">The image.</param>
    /// <returns>The view handle.</returns>
    /// <exception cref="InvalidOperationException">The GPU could not create the view.</exception>
    private static ulong CreateView(VulkanContext context, ulong image)
    {
        byte* info = stackalloc byte[InfoSize];
        var create = new VkWriter(new(info, InfoSize));
        create.U32(VkEnums.ImageViewCreateInfoType);
        create.U64(0);
        create.U32(0);
        create.U64(image);
        create.U32(Type2D);
        create.U32(VkEnums.FormatB8G8R8A8Unorm);

        // The four component swizzles stay identity, which is zero.
        create.U32(0);
        create.U32(0);
        create.U32(0);
        create.U32(0);
        create.U32(VkEnums.AspectColor);
        create.U32(0);
        create.U32(1);
        create.U32(0);
        create.U32(1);
        ulong view;
        return NativeMethods.CreateImageView(context.Device, info, null, &view) == VkConstants.Success
            ? view
            : throw new InvalidOperationException("The GPU could not create an image view.");
    }
}
