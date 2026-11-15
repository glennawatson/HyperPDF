// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Rendering.Vulkan;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>
/// The window's buffers when the GPU draws its frames: each is a GPU image the compositor reads as a dmabuf, so a frame reaches the
/// screen without a copy through the CPU. The compositor makes the buffers and says when each is free to draw into again.
/// </summary>
/// <param name="connection">The compositor connection.</param>
/// <param name="manager">The zwp_linux_dmabuf_v1 object.</param>
[DebuggerDisplay("WaylandDmabufPool: {_count} buffers, {_waiting} waiting")]
internal sealed class WaylandDmabufPool(WaylandConnection connection, uint manager)
{
    /// <summary>The zwp_linux_dmabuf_v1 version the head binds, the last that lists the formats in plain events.</summary>
    internal const uint Version = 3;

    /// <summary>The interface name of the global.</summary>
    internal const string Interface = "zwp_linux_dmabuf_v1";

    /// <summary>The most buffers the pool holds.</summary>
    private const int MaxBuffers = 4;

    /// <summary>The zwp_linux_dmabuf_v1.create_params request.</summary>
    private const int CreateParams = 1;

    /// <summary>The zwp_linux_dmabuf_v1.modifier event.</summary>
    private const int ModifierEvent = 1;

    /// <summary>The zwp_linux_buffer_params_v1.destroy request.</summary>
    private const int ParamsDestroy = 0;

    /// <summary>The zwp_linux_buffer_params_v1.add request.</summary>
    private const int ParamsAdd = 1;

    /// <summary>The zwp_linux_buffer_params_v1.create request.</summary>
    private const int ParamsCreate = 2;

    /// <summary>The zwp_linux_buffer_params_v1.created event.</summary>
    private const int ParamsCreated = 0;

    /// <summary>The wl_buffer.destroy request.</summary>
    private const int BufferDestroy = 0;

    /// <summary>The DRM format of 32-bit pixels with blue in the lowest byte and the top byte unused.</summary>
    private const uint Xrgb8888 = 0x34325258;

    /// <summary>The high half of the modifier that leaves the layout to the driver, for a compositor that does not list the linear one.</summary>
    private const uint ImplicitModifierHigh = 0x00FFFFFF;

    /// <summary>The low half of the modifier that leaves the layout to the driver.</summary>
    private const uint ImplicitModifierLow = 0xFFFFFFFF;

    /// <summary>The params objects of the buffers being made, 0 once the compositor answered.</summary>
    private readonly uint[] _params = new uint[MaxBuffers];

    /// <summary>The wl_buffer ids, 0 until the compositor made the buffer.</summary>
    private readonly uint[] _buffers = new uint[MaxBuffers];

    /// <summary>Whether the compositor is showing or reading each buffer.</summary>
    private readonly bool[] _busy = new bool[MaxBuffers];

    /// <summary>The params objects of buffers that were replaced before the compositor answered. Their buffers are destroyed on arrival.</summary>
    private readonly HashSet<uint> _stale = [];

    /// <summary>The buffers in use.</summary>
    private int _count;

    /// <summary>The buffers the compositor has not answered for yet.</summary>
    private int _waiting;

    /// <summary>Whether the compositor lists the linear layout for the pixel format.</summary>
    private bool _linear;

    /// <summary>Gets a value indicating whether the compositor has answered for every buffer.</summary>
    internal bool IsSettled => _waiting == 0;

    /// <summary>Gets a value indicating whether the compositor refused a buffer.</summary>
    internal bool HasFailed { get; private set; }

    /// <summary>Gets the size of the buffers.</summary>
    internal Windowing.Rendering.SurfaceSize Size { get; private set; }

    /// <summary>Asks the compositor to make a buffer of each GPU image, in place of the buffers made before.</summary>
    /// <param name="frames">The images. Their descriptors are closed once sent.</param>
    internal void SetFrames(ReadOnlySpan<DmabufFrame> frames)
    {
        Release();
        _count = Math.Min(frames.Length, MaxBuffers);
        _waiting = _count;
        HasFailed = false;
        Size = _count > 0 ? new(frames[0].Width, frames[0].Height) : default;
        var writer = connection.Writer;
        for (var i = 0; i < _count; i++)
        {
            var frame = frames[i];
            using var descriptor = new SafeFileHandle(frame.Descriptor, true);
            _params[i] = connection.NewId();
            writer.Begin(manager, CreateParams);
            writer.WriteUInt(_params[i]);
            writer.End();
            writer.Begin(_params[i], ParamsAdd);
            writer.WriteUInt(0);
            writer.WriteInt(frame.Offset);
            writer.WriteInt(frame.Stride);
            writer.WriteUInt(_linear ? 0 : ImplicitModifierHigh);
            writer.WriteUInt(_linear ? 0 : ImplicitModifierLow);
            writer.End();

            // The descriptor travels beside the request, and the compositor keeps its own copy.
            connection.FlushWithDescriptor(frame.Descriptor);
            writer.Begin(_params[i], ParamsCreate);
            writer.WriteInt(frame.Width);
            writer.WriteInt(frame.Height);
            writer.WriteUInt(Xrgb8888);
            writer.WriteUInt(0);
            writer.End();
        }

        connection.Flush();
    }

    /// <summary>Finds a buffer the compositor is not using and marks it as in use.</summary>
    /// <returns>The buffer's place, or -1 when none is free or made yet.</returns>
    internal int TakeFree()
    {
        for (var i = 0; i < _count; i++)
        {
            if (_buffers[i] == 0 || _busy[i])
            {
                continue;
            }

            _busy[i] = true;
            return i;
        }

        return -1;
    }

    /// <summary>Gets the wl_buffer id of a buffer.</summary>
    /// <param name="index">The buffer's place.</param>
    /// <returns>The id.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal uint BufferId(int index) => _buffers[index];

    /// <summary>Handles an event of the dmabuf global, of a buffer being made or of a buffer.</summary>
    /// <param name="objectId">The object the event is for.</param>
    /// <param name="opcode">The event.</param>
    /// <param name="reader">The event arguments.</param>
    /// <returns><see cref="DmabufEvent.BuffersReady"/> when the last buffer was made, so a frame can be drawn.</returns>
    internal DmabufEvent Handle(uint objectId, int opcode, ref WaylandMessageReader reader)
    {
        if (objectId == manager)
        {
            // Formats arrive once, after the global is bound: the format and the two halves of a layout modifier, where zero is linear.
            _linear |= opcode == ModifierEvent && reader.ReadUInt() == Xrgb8888 && reader.ReadUInt() == 0 && reader.ReadUInt() == 0;
            return DmabufEvent.Handled;
        }

        if (_stale.Remove(objectId))
        {
            Discard(objectId, opcode == ParamsCreated ? reader.ReadUInt() : 0);
            return DmabufEvent.Handled;
        }

        var made = Array.IndexOf(_params, objectId);
        return made >= 0 && made < _count ? Answer(made, opcode, ref reader) : Free(objectId, opcode);
    }

    /// <summary>Destroys the buffers and forgets the ones still being made.</summary>
    internal void Release()
    {
        var writer = connection.Writer;
        for (var i = 0; i < _count; i++)
        {
            if (_params[i] != 0)
            {
                _ = _stale.Add(_params[i]);
            }

            if (_buffers[i] != 0)
            {
                writer.Begin(_buffers[i], BufferDestroy);
                writer.End();
            }

            _params[i] = 0;
            _buffers[i] = 0;
            _busy[i] = false;
        }

        _count = 0;
        _waiting = 0;
        connection.Flush();
    }

    /// <summary>Marks a buffer free when the compositor is done with it.</summary>
    /// <param name="objectId">The object the event is for.</param>
    /// <param name="opcode">The event, where 0 is a buffer's release.</param>
    /// <returns>Whether the event was a release of one of the pool's buffers.</returns>
    private DmabufEvent Free(uint objectId, int opcode)
    {
        var released = opcode == 0 ? Array.IndexOf(_buffers, objectId) : -1;
        if (released < 0 || released >= _count)
        {
            return DmabufEvent.None;
        }

        _busy[released] = false;
        return DmabufEvent.Handled;
    }

    /// <summary>Takes the compositor's answer for one buffer.</summary>
    /// <param name="index">The buffer's place.</param>
    /// <param name="opcode">The event: created or failed.</param>
    /// <param name="reader">The event arguments.</param>
    /// <returns>Whether every buffer is now made.</returns>
    private DmabufEvent Answer(int index, int opcode, ref WaylandMessageReader reader)
    {
        var parameters = _params[index];
        _params[index] = 0;
        _waiting--;
        if (opcode == ParamsCreated)
        {
            _buffers[index] = reader.ReadUInt();
        }
        else
        {
            HasFailed = true;
        }

        connection.Writer.Begin(parameters, ParamsDestroy);
        connection.Writer.End();
        connection.Flush();
        return _waiting == 0 && !HasFailed ? DmabufEvent.BuffersReady : DmabufEvent.Handled;
    }

    /// <summary>Destroys what the compositor made for a buffer that was replaced before it answered.</summary>
    /// <param name="parameters">The params object.</param>
    /// <param name="buffer">The buffer it made, or 0 when it failed.</param>
    private void Discard(uint parameters, uint buffer)
    {
        var writer = connection.Writer;
        if (buffer != 0)
        {
            writer.Begin(buffer, BufferDestroy);
            writer.End();
        }

        writer.Begin(parameters, ParamsDestroy);
        writer.End();
        connection.Flush();
    }
}
