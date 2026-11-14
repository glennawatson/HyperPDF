// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Rendering.Vulkan.Interop;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>
/// The images of a window that the GPU presents. Each frame is drawn into the backend's own frame image and then copied into the next
/// window image on the GPU, so drawing is the same whether or not a window is attached.
/// </summary>
[DebuggerDisplay("VulkanSwapchain: {_width} x {_height}, {_imageCount} images")]
internal sealed unsafe class VulkanSwapchain : IDisposable
{
    /// <summary>A size that holds any structure built here.</summary>
    private const int StructSize = 112;

    /// <summary>The most window images the swapchain keeps.</summary>
    private const int MaxImages = 8;

    /// <summary>The most surface formats read from the driver.</summary>
    private const int MaxFormats = 64;

    /// <summary>The size of a VkSurfaceFormatKHR.</summary>
    private const int SurfaceFormatSize = 8;

    /// <summary>The size of VkSurfaceCapabilitiesKHR.</summary>
    private const int CapabilitiesSize = 52;

    /// <summary>The offset of the largest image count in the capabilities.</summary>
    private const int MaxImageCountOffset = 4;

    /// <summary>The offset of the current transform in the capabilities.</summary>
    private const int CurrentTransformOffset = 40;

    /// <summary>The window images asked for: one shown and one being drawn.</summary>
    private const uint WantedImages = 2;

    /// <summary>The image transition of a window image before the frame is copied into it. What it held is not kept.</summary>
    private static readonly ImageTransition ToCopyTarget = new(
        VkEnums.LayoutUndefined,
        VkEnums.LayoutTransferDestination,
        0,
        VkEnums.AccessTransferWrite,
        VkEnums.StageTopOfPipe,
        VkEnums.StageTransfer);

    /// <summary>The image transition of a window image once the frame is in it.</summary>
    private static readonly ImageTransition ToPresent = new(
        VkEnums.LayoutTransferDestination,
        VkEnums.LayoutPresentSource,
        VkEnums.AccessTransferWrite,
        0,
        VkEnums.StageTransfer,
        VkEnums.StageColorAttachmentOutput);

    /// <summary>The context.</summary>
    private readonly VulkanContext _context;

    /// <summary>The window surface.</summary>
    private readonly ulong _surface;

    /// <summary>The fence signalled when a window image is ready to draw into.</summary>
    private readonly ulong _acquired;

    /// <summary>The window images.</summary>
    private readonly ulong[] _images = new ulong[MaxImages];

    /// <summary>The format of the window images.</summary>
    private readonly uint _format;

    /// <summary>The swapchain handle, or 0 while the window has no size.</summary>
    private ulong _swapchain;

    /// <summary>The number of window images.</summary>
    private uint _imageCount;

    /// <summary>The index of the window image this frame goes to.</summary>
    private uint _current;

    /// <summary>The width of the window images.</summary>
    private int _width;

    /// <summary>The height of the window images.</summary>
    private int _height;

    /// <summary>Whether a window image was acquired for the frame being recorded.</summary>
    private bool _hasImage;

    /// <summary>Initializes a new instance of the <see cref="VulkanSwapchain"/> class.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="surface">The window surface, which the swapchain owns from here on.</param>
    /// <param name="acquired">The fence signalled when a window image is ready.</param>
    private VulkanSwapchain(VulkanContext context, ulong surface, ulong acquired)
    {
        _context = context;
        _surface = surface;
        _acquired = acquired;
        _format = ChooseFormat(context, surface);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        DestroySwapchain();
        NativeMethods.DestroyFence(_context.Device, _acquired, null);
        NativeMethods.DestroySurface(_context.Instance, _surface, null);
    }

    /// <summary>Makes a swapchain for an X11 window.</summary>
    /// <param name="context">The Vulkan context, made for a window.</param>
    /// <param name="display">The Xlib display pointer.</param>
    /// <param name="window">The X11 window id.</param>
    /// <returns>The swapchain, or <see langword="null"/> when the GPU cannot present to the window.</returns>
    internal static VulkanSwapchain? TryCreateForXlib(VulkanContext context, nint display, nuint window)
    {
        if (!context.CanPresent)
        {
            return null;
        }

        byte* info = stackalloc byte[StructSize];
        var writer = new VkWriter(new(info, StructSize));
        writer.U32(VkEnums.XlibSurfaceCreateInfoType);
        writer.U64(0);
        writer.U32(0);
        writer.U64((ulong)display);
        writer.U64(window);
        ulong surface;
        if (NativeMethods.CreateXlibSurface(context.Instance, info, null, &surface) != VkConstants.Success)
        {
            return null;
        }

        uint supported;
        var asked = NativeMethods.GetSurfaceSupport(context.PhysicalDevice, context.QueueFamily, surface, &supported);
        if (asked != VkConstants.Success || supported == 0 || !TryCreateFence(context, out var fence))
        {
            NativeMethods.DestroySurface(context.Instance, surface, null);
            return null;
        }

        return new(context, surface, fence);
    }

    /// <summary>Makes the window images again for a new size.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    internal void Resize(int width, int height)
    {
        _ = NativeMethods.DeviceWaitIdle(_context.Device);
        var old = _swapchain;
        _swapchain = 0;
        _imageCount = 0;
        _width = width;
        _height = height;
        if (width > 0 && height > 0)
        {
            Create(old);
        }

        if (old != 0)
        {
            NativeMethods.DestroySwapchain(_context.Device, old, null);
        }
    }

    /// <summary>Gets the next window image and records the copy of the finished frame into it.</summary>
    /// <param name="commandBuffer">The command buffer, after the render pass has ended.</param>
    /// <param name="frame">The backend's frame image, in the colour attachment layout.</param>
    internal void RecordCopy(ulong commandBuffer, VulkanFrameTarget frame)
    {
        _hasImage = false;
        if (_swapchain == 0 || !Acquire())
        {
            return;
        }

        _hasImage = true;
        var image = _images[_current];
        VulkanCommands.Barrier(commandBuffer, frame.Image.Handle, ReadTransitions.ToRead);
        VulkanCommands.Barrier(commandBuffer, image, ToCopyTarget);
        VulkanCommands.Blit(commandBuffer, frame.Image.Handle, image, new(frame.Width, frame.Height, _width, _height));
        VulkanCommands.Barrier(commandBuffer, image, ToPresent);
        VulkanCommands.Barrier(commandBuffer, frame.Image.Handle, ReadTransitions.ToDraw);
    }

    /// <summary>Shows the window image the frame was copied into. The copy must have finished on the GPU.</summary>
    internal void Present()
    {
        if (!_hasImage)
        {
            return;
        }

        _hasImage = false;
        var swapchain = _swapchain;
        var index = _current;
        byte* info = stackalloc byte[StructSize];
        var writer = new VkWriter(new(info, StructSize));
        writer.U32(VkEnums.PresentInfoType);
        writer.U64(0);
        writer.U32(0);
        writer.U64(0);
        writer.U32(1);
        writer.U64((ulong)&swapchain);
        writer.U64((ulong)&index);
        writer.U64(0);
        var result = NativeMethods.QueuePresent(_context.Queue, info);
        if (result is VkEnums.OutOfDate or VkEnums.Suboptimal)
        {
            // The window changed size since the images were made. The head's resize makes them again, and until then frames are skipped.
            Resize(_width, _height);
        }
    }

    /// <summary>Picks the format of the window images: BGRA when the surface has it, otherwise its first format, which the copy converts to.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="surface">The surface.</param>
    /// <returns>The format.</returns>
    private static uint ChooseFormat(VulkanContext context, ulong surface)
    {
        uint count = MaxFormats;
        byte* formats = stackalloc byte[MaxFormats * SurfaceFormatSize];
        _ = NativeMethods.GetSurfaceFormats(context.PhysicalDevice, surface, &count, formats);
        for (var i = 0; i < count; i++)
        {
            if (*(uint*)(formats + (i * SurfaceFormatSize)) == VkEnums.FormatB8G8R8A8Unorm)
            {
                return VkEnums.FormatB8G8R8A8Unorm;
            }
        }

        // The buffer starts as zeros and no real format is zero, so a zero here means the driver listed nothing.
        var first = *(uint*)formats;
        return first != 0 ? first : VkEnums.FormatB8G8R8A8Unorm;
    }

    /// <summary>Makes an unsignalled fence.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="fence">Receives the fence.</param>
    /// <returns><see langword="true"/> when the fence was made.</returns>
    private static bool TryCreateFence(VulkanContext context, out ulong fence)
    {
        byte* info = stackalloc byte[StructSize];
        var writer = new VkWriter(new(info, StructSize));
        writer.U32(VkEnums.FenceCreateInfoType);
        writer.U64(0);
        writer.U32(0);
        ulong handle;
        var made = NativeMethods.CreateFence(context.Device, info, null, &handle) == VkConstants.Success;
        fence = made ? handle : 0;
        return made;
    }

    /// <summary>Creates the swapchain and reads its images.</summary>
    /// <param name="old">The swapchain being replaced, or 0.</param>
    private void Create(ulong old)
    {
        byte* capabilities = stackalloc byte[CapabilitiesSize];
        if (NativeMethods.GetSurfaceCapabilities(_context.PhysicalDevice, _surface, capabilities) != VkConstants.Success)
        {
            return;
        }

        var most = *(uint*)(capabilities + MaxImageCountOffset);
        var wanted = Math.Max(WantedImages, *(uint*)capabilities);
        byte* info = stackalloc byte[StructSize];
        var writer = new VkWriter(new(info, StructSize));
        writer.U32(VkEnums.SwapchainCreateInfoType);
        writer.U64(0);
        writer.U32(0);
        writer.U64(_surface);
        writer.U32(most == 0 ? wanted : Math.Min(wanted, most));
        writer.U32(_format);
        writer.U32(0);
        writer.U32((uint)_width);
        writer.U32((uint)_height);
        writer.U32(1);
        writer.U32(VkEnums.ImageUsageTransferDestination | VkEnums.ImageUsageColorAttachment);
        writer.U32(0);
        writer.U32(0);
        writer.U64(0);
        writer.U32(*(uint*)(capabilities + CurrentTransformOffset));
        writer.U32(VkEnums.CompositeAlphaOpaque);
        writer.U32(VkEnums.PresentModeFifo);
        writer.U32(1);
        writer.U64(old);
        ulong swapchain;
        if (NativeMethods.CreateSwapchain(_context.Device, info, null, &swapchain) != VkConstants.Success)
        {
            return;
        }

        _swapchain = swapchain;
        uint count = MaxImages;
        fixed (ulong* images = _images)
        {
            _ = NativeMethods.GetSwapchainImages(_context.Device, swapchain, &count, images);
        }

        _imageCount = Math.Min(count, MaxImages);
    }

    /// <summary>Waits for the next window image.</summary>
    /// <returns><see langword="true"/> when an image is ready for this frame.</returns>
    private bool Acquire()
    {
        uint index;
        var fence = _acquired;
        var result = NativeMethods.AcquireNextImage(_context.Device, _swapchain, VkEnums.FenceTimeout, 0, fence, &index);
        if (result is not (VkConstants.Success or VkEnums.Suboptimal))
        {
            return false;
        }

        _ = NativeMethods.WaitForFences(_context.Device, 1, &fence, 1, VkEnums.FenceTimeout);
        _ = NativeMethods.ResetFences(_context.Device, 1, &fence);
        _current = index;
        return index < _imageCount;
    }

    /// <summary>Destroys the swapchain, if there is one.</summary>
    private void DestroySwapchain()
    {
        if (_swapchain == 0)
        {
            return;
        }

        _ = NativeMethods.DeviceWaitIdle(_context.Device);
        NativeMethods.DestroySwapchain(_context.Device, _swapchain, null);
        _swapchain = 0;
    }
}
