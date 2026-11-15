// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Rendering.Vulkan.Interop;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>
/// A few GPU images shared with the window system as dmabufs. Each finished frame is copied into one the window system is not using, on
/// the GPU, and the window then shows that image. No pixel passes through the CPU.
/// </summary>
[DebuggerDisplay("VulkanDmabufChain: {_width}x{_height}, usable {_usable}")]
internal sealed unsafe class VulkanDmabufChain : IVulkanFrameOutput
{
    /// <summary>The images in the chain: one on screen, one waiting and one to draw into.</summary>
    private const int ImageCount = 3;

    /// <summary>A size that holds any structure built here.</summary>
    private const int InfoSize = 96;

    /// <summary>The size of the small structures chained to a request.</summary>
    private const int ChainedSize = 24;

    /// <summary>The size of a VkSubresourceLayout.</summary>
    private const int LayoutSize = 40;

    /// <summary>The offset of the row pitch in a VkSubresourceLayout.</summary>
    private const int RowPitchOffset = 16;

    /// <summary>The image type 2D.</summary>
    private const uint Type2D = 1;

    /// <summary>The tiling whose lines are laid out one after another, which any consumer of the dmabuf can read.</summary>
    private const uint TilingLinear = 1;

    /// <summary>The structure type of VkExternalMemoryImageCreateInfo.</summary>
    private const uint ExternalMemoryImageType = 1_000_072_001;

    /// <summary>The structure type of VkExportMemoryAllocateInfo.</summary>
    private const uint ExportMemoryAllocateType = 1_000_072_002;

    /// <summary>The structure type of VkMemoryGetFdInfoKHR.</summary>
    private const uint MemoryGetFdType = 1_000_074_002;

    /// <summary>The external memory handle type of a dmabuf.</summary>
    private const uint DmabufHandle = 0x200;

    /// <summary>The image transition of a shared image before the frame is copied into it. Its old contents are not kept.</summary>
    private static readonly ImageTransition ToCopyTarget = new(
        VkEnums.LayoutUndefined,
        VkEnums.LayoutTransferDestination,
        0,
        VkEnums.AccessTransferWrite,
        VkEnums.StageTopOfPipe,
        VkEnums.StageTransfer);

    /// <summary>The Vulkan context.</summary>
    private readonly VulkanContext _context;

    /// <summary>The window that shows the images.</summary>
    private readonly IDmabufWindow _window;

    /// <summary>The device's vkGetMemoryFdKHR.</summary>
    private readonly delegate* unmanaged<ulong, byte*, int*, int> _getMemoryFd;

    /// <summary>The image handles, 0 where none is made.</summary>
    private readonly ulong[] _images = new ulong[ImageCount];

    /// <summary>The memory handles of the images.</summary>
    private readonly ulong[] _memories = new ulong[ImageCount];

    /// <summary>The width of the images.</summary>
    private int _width;

    /// <summary>The height of the images.</summary>
    private int _height;

    /// <summary>Whether the images exist and the window system accepted them.</summary>
    private bool _usable;

    /// <summary>The image the current frame was copied into, or -1 when the frame was skipped.</summary>
    private int _current = -1;

    /// <summary>Initializes a new instance of the <see cref="VulkanDmabufChain"/> class.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="window">The window that shows the images.</param>
    /// <param name="getMemoryFd">The device's vkGetMemoryFdKHR.</param>
    private VulkanDmabufChain(VulkanContext context, IDmabufWindow window, nint getMemoryFd)
    {
        _context = context;
        _window = window;
        _getMemoryFd = (delegate* unmanaged<ulong, byte*, int*, int>)getMemoryFd;
    }

    /// <summary>Gets a value indicating whether the images exist and the window system accepted them.</summary>
    internal bool IsUsable => _usable;

    /// <inheritdoc/>
    public void Dispose()
    {
        _ = NativeMethods.DeviceWaitIdle(_context.Device);
        Release();
    }

    /// <inheritdoc/>
    public void Resize(int width, int height)
    {
        _ = NativeMethods.DeviceWaitIdle(_context.Device);
        Release();
        _width = width;
        _height = height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        Span<DmabufFrame> frames = stackalloc DmabufFrame[ImageCount];
        for (var i = 0; i < ImageCount; i++)
        {
            if (TryCreate(i, out frames[i]))
            {
                continue;
            }

            // The descriptors made so far have no other owner.
            CloseAll(frames[..i]);
            Release();
            return;
        }

        _usable = _window.SetFrames(frames);
    }

    /// <inheritdoc/>
    public void RecordCopy(ulong commandBuffer, VulkanFrameTarget frame)
    {
        _current = -1;
        if (!_usable || frame.Width != _width || frame.Height != _height)
        {
            return;
        }

        var index = _window.TakeFreeFrame();
        if ((uint)index >= ImageCount)
        {
            return;
        }

        _current = index;
        VulkanCommands.Barrier(commandBuffer, frame.Image.Handle, ReadTransitions.ToRead);
        VulkanCommands.Barrier(commandBuffer, _images[index], ToCopyTarget);
        VulkanCommands.CopyImage(commandBuffer, frame.Image.Handle, _images[index], _width, _height);
        VulkanCommands.Barrier(commandBuffer, frame.Image.Handle, ReadTransitions.ToDraw);
    }

    /// <inheritdoc/>
    public void Present()
    {
        if (_current < 0)
        {
            return;
        }

        var index = _current;
        _current = -1;
        _window.ShowFrame(index);
    }

    /// <summary>Makes a chain when the GPU can share images as dmabufs.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="window">The window that shows the images.</param>
    /// <returns>The chain, with no images yet, or <see langword="null"/> when the GPU cannot share images.</returns>
    internal static VulkanDmabufChain? TryCreate(VulkanContext context, IDmabufWindow window)
    {
        if (!context.CanExportDmabuf)
        {
            return null;
        }

        fixed (byte* name = "vkGetMemoryFdKHR\0"u8)
        {
            var function = NativeMethods.GetDeviceProcAddr(context.Device, name);
            return function == 0 ? null : new(context, window, function);
        }
    }

    /// <summary>Closes the descriptors of images the window never took.</summary>
    /// <param name="frames">The images.</param>
    private static void CloseAll(ReadOnlySpan<DmabufFrame> frames)
    {
        foreach (var frame in frames)
        {
            using var owned = new Microsoft.Win32.SafeHandles.SafeFileHandle(frame.Descriptor, true);
        }
    }

    /// <summary>Makes one shared image and its dmabuf.</summary>
    /// <param name="index">The image's place in the chain.</param>
    /// <param name="frame">Receives what the window system needs to read the image.</param>
    /// <returns><see langword="true"/> when the image was made and shared.</returns>
    private bool TryCreate(int index, out DmabufFrame frame)
    {
        frame = default;
        byte* external = stackalloc byte[ChainedSize];
        var chained = new VkWriter(new(external, ChainedSize));
        chained.U32(ExternalMemoryImageType);
        chained.U64(0);
        chained.U32(DmabufHandle);

        byte* info = stackalloc byte[InfoSize];
        var create = new VkWriter(new(info, InfoSize));
        create.U32(VkEnums.ImageCreateInfoType);
        create.U64((ulong)external);
        create.U32(0);
        create.U32(Type2D);
        create.U32(VkEnums.FormatB8G8R8A8Unorm);
        create.U32((uint)_width);
        create.U32((uint)_height);
        create.U32(1);
        create.U32(1);
        create.U32(1);
        create.U32(1);
        create.U32(TilingLinear);
        create.U32(VkEnums.ImageUsageTransferDestination);
        create.U32(0);
        create.U32(0);
        create.U64(0);
        create.U32(VkEnums.LayoutUndefined);
        ulong image;
        if (NativeMethods.CreateImage(_context.Device, info, null, &image) != VkConstants.Success)
        {
            return false;
        }

        _images[index] = image;
        return TryShare(index, out frame);
    }

    /// <summary>Backs an image with memory that can be shared and gets its dmabuf.</summary>
    /// <param name="index">The image's place in the chain.</param>
    /// <param name="frame">Receives what the window system needs to read the image.</param>
    /// <returns><see langword="true"/> when the memory was made and shared.</returns>
    private bool TryShare(int index, out DmabufFrame frame)
    {
        frame = default;
        var image = _images[index];
        byte* export = stackalloc byte[ChainedSize];
        var chained = new VkWriter(new(export, ChainedSize));
        chained.U32(ExportMemoryAllocateType);
        chained.U64(0);
        chained.U32(DmabufHandle);
        byte* requirements = stackalloc byte[VkEnums.MemoryRequirementsSize];
        NativeMethods.GetImageMemoryRequirements(_context.Device, image, requirements);
        try
        {
            _memories[index] = VulkanMemory.Allocate(_context, requirements, 0, export);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        if (NativeMethods.BindImageMemory(_context.Device, image, _memories[index], 0) != VkConstants.Success)
        {
            return false;
        }

        byte* get = stackalloc byte[InfoSize];
        var writer = new VkWriter(new(get, InfoSize));
        writer.U32(MemoryGetFdType);
        writer.U64(0);
        writer.U64(_memories[index]);
        writer.U32(DmabufHandle);
        int descriptor;
        if (_getMemoryFd(_context.Device, get, &descriptor) != VkConstants.Success)
        {
            return false;
        }

        // A linear image has one plane; the driver says where its first line starts and how far apart its lines are.
        byte* subresource = stackalloc byte[ChainedSize];
        var which = new VkWriter(new(subresource, ChainedSize));
        which.U32(VkEnums.AspectColor);
        byte* layout = stackalloc byte[LayoutSize];
        NativeMethods.GetImageSubresourceLayout(_context.Device, image, subresource, layout);
        frame = new(descriptor, _width, _height, (int)*(ulong*)(layout + RowPitchOffset), (int)*(ulong*)layout);
        return true;
    }

    /// <summary>Destroys the images and frees their memory.</summary>
    private void Release()
    {
        _usable = false;
        _current = -1;
        for (var i = 0; i < ImageCount; i++)
        {
            if (_images[i] != 0)
            {
                NativeMethods.DestroyImage(_context.Device, _images[i], null);
                _images[i] = 0;
            }

            if (_memories[i] == 0)
            {
                continue;
            }

            NativeMethods.FreeMemory(_context.Device, _memories[i], null);
            _memories[i] = 0;
        }
    }
}
