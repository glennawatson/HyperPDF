// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Rendering.Vulkan.Interop;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>The image a frame is drawn into and the framebuffer that draws onto it.</summary>
[DebuggerDisplay("VulkanFrameTarget: {Width} x {Height}")]
internal sealed unsafe class VulkanFrameTarget : IDisposable
{
    /// <summary>A size that holds any structure built here.</summary>
    private const int StructSize = 96;

    /// <summary>The context.</summary>
    private readonly VulkanContext _context;

    /// <summary>Initializes a new instance of the <see cref="VulkanFrameTarget"/> class.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="renderPass">The render pass the framebuffer is compatible with.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <exception cref="InvalidOperationException">The GPU could not create the target.</exception>
    internal VulkanFrameTarget(VulkanContext context, ulong renderPass, int width, int height)
    {
        _context = context;
        Width = width;
        Height = height;
        Image = new(context, width, height, VkEnums.ImageUsageColorAttachment | VkEnums.ImageUsageTransferSource);
        var view = Image.View;
        byte* info = stackalloc byte[StructSize];
        var writer = new VkWriter(new(info, StructSize));
        writer.U32(VkEnums.FramebufferCreateInfoType);
        writer.U64(0);
        writer.U32(0);
        writer.U64(renderPass);
        writer.U32(1);
        writer.U64((ulong)&view);
        writer.U32((uint)width);
        writer.U32((uint)height);
        writer.U32(1);
        ulong framebuffer;
        if (NativeMethods.CreateFramebuffer(context.Device, info, null, &framebuffer) != VkConstants.Success)
        {
            Image.Dispose();
            throw new InvalidOperationException("The GPU could not create a framebuffer.");
        }

        Framebuffer = framebuffer;
    }

    /// <summary>Gets the width in pixels.</summary>
    internal int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    internal int Height { get; }

    /// <summary>Gets the image frames are drawn into.</summary>
    internal VulkanImage Image { get; }

    /// <summary>Gets the framebuffer handle.</summary>
    internal ulong Framebuffer { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        NativeMethods.DestroyFramebuffer(_context.Device, Framebuffer, null);
        Image.Dispose();
    }
}
