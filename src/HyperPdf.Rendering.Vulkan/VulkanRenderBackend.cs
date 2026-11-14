// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Rendering;
using HyperPdf.Rendering.Vulkan.Interop;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>Draws frames on the GPU with Vulkan: tiles become images and each one is a textured quad in a single render pass. Every method must run on one thread.</summary>
[DebuggerDisplay("VulkanRenderBackend: {Size}")]
public sealed class VulkanRenderBackend : IRenderBackend
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The GPU objects of tiles that were disposed and wait for the next frame to free them.</summary>
    private readonly ConcurrentQueue<VulkanRetired> _retired = new();

    /// <summary>The operations recorded for the current frame, reused each frame.</summary>
    private readonly List<VulkanDrawOp> _ops = [];

    /// <summary>The tiles uploaded in the current frame, reused each frame.</summary>
    private readonly List<VulkanTileSurface> _uploads = [];

    /// <summary>The context the backend owns.</summary>
    private readonly VulkanContext _context;

    /// <summary>The GPU objects every frame uses.</summary>
    private readonly VulkanFrameResources _frame;

    /// <summary>The pass handed out for every frame, so a frame allocates nothing. It is made on the first frame.</summary>
    private VulkanFramePass? _pass;

    /// <summary>The buffer that receives frame readbacks, made on first use.</summary>
    private VulkanBuffer? _readback;

    /// <summary>One once the backend is disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="VulkanRenderBackend"/> class.</summary>
    /// <param name="context">The Vulkan context. The backend owns it and disposes it.</param>
    public VulkanRenderBackend(VulkanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _frame = new(context);
        Surfaces = new VulkanSurfaceFactory(_retired);
    }

    /// <inheritdoc/>
    public RenderBackendKind Kind => RenderBackendKind.Vulkan;

    /// <inheritdoc/>
    public IRenderSurfaceFactory Surfaces { get; }

    /// <inheritdoc/>
    public bool IsAlive => Volatile.Read(ref _disposed) == 0;

    /// <summary>Gets the current frame size in pixels.</summary>
    public SurfaceSize Size { get; private set; }

    /// <summary>Gets a value indicating whether the GPU can export frames as dmabufs, which Wayland presentation needs.</summary>
    public bool CanExportDmabuf => _context.CanExportDmabuf;

    /// <summary>Gets the name of the GPU.</summary>
    public string DeviceName => _context.DeviceName;

    /// <inheritdoc/>
    public void Resize(in SurfaceSize size)
    {
        ObjectDisposedException.ThrowIf(!IsAlive, this);
        if (size == Size)
        {
            return;
        }

        Size = size.IsEmpty ? default : size;
        _frame.Resize(Size.Width, Size.Height);
    }

    /// <inheritdoc/>
    public IFramePass BeginFrame()
    {
        ObjectDisposedException.ThrowIf(!IsAlive, this);
        _frame.ReleaseRetired(_retired);
        _ops.Clear();
        return _pass ??= new(this);
    }

    /// <summary>Reads the frame just drawn as premultiplied BGRA with the top line first, the layout the CPU backend uses.</summary>
    /// <param name="destination">Receives the pixels. It must hold at least width * height * 4 bytes.</param>
    public void ReadPixels(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(!IsAlive, this);
        var length = Size.Width * Size.Height * BytesPerPixel;
        ArgumentOutOfRangeException.ThrowIfLessThan(destination.Length, length);
        if (_readback is null || _readback.Size < length)
        {
            _readback?.Dispose();
            _readback = new(_context, length, VkEnums.BufferUsageTransferDestination);
        }

        var target = _frame.Target;
        _frame.Commands.Begin();
        VulkanCommands.Barrier(_frame.Commands.Buffer, target.Image.Handle, ReadTransitions.ToRead);
        VulkanCommands.CopyImageToBuffer(_frame.Commands.Buffer, target.Image.Handle, _readback.Handle, target.Width, target.Height);
        VulkanCommands.Barrier(_frame.Commands.Buffer, target.Image.Handle, ReadTransitions.ToDraw);
        _frame.Commands.SubmitAndWait();
        _readback.Memory[..length].CopyTo(destination);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _ = NativeMethods.DeviceWaitIdle(_context.Device);
        _frame.ReleaseRetired(_retired);
        _readback?.Dispose();
        _frame.Dispose();
        _context.Dispose();

        // Disposing a pass presents its frame, which does nothing once the backend is disposed.
        _pass?.Dispose();
    }

    /// <summary>Adds an operation to the current frame.</summary>
    /// <param name="op">The operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Record(in VulkanDrawOp op) => _ops.Add(op);

    /// <summary>Draws the recorded operations and finishes the frame.</summary>
    internal void Present()
    {
        if (Size.IsEmpty || !IsAlive)
        {
            return;
        }

        VulkanFrameRecorder.Render(_frame, _ops, _uploads);
    }
}
