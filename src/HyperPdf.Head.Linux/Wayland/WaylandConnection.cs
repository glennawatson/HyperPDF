// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>A client connection to a Wayland compositor: it sends the requests built in <see cref="Writer"/> and reads events into a reused buffer.</summary>
internal sealed unsafe class WaylandConnection : IDisposable
{
    /// <summary>The receive buffer size in bytes.</summary>
    private const int ReceiveCapacity = 64 * 1024;

    /// <summary>The size of a msghdr on 64-bit Linux.</summary>
    private const int MessageHeaderSize = 56;

    /// <summary>The size of one iovec.</summary>
    private const int IoVectorSize = 16;

    /// <summary>The space a control message carrying one descriptor takes, padded.</summary>
    private const int ControlSize = 24;

    /// <summary>The length of that control message without padding.</summary>
    private const int ControlLength = 20;

    /// <summary>The socket level for control messages.</summary>
    private const int SocketLevel = 1;

    /// <summary>The control message type that passes descriptors.</summary>
    private const int RightsType = 1;

    /// <summary>The offset of the iovec pointer in msghdr.</summary>
    private const int IoVectorOffset = 16;

    /// <summary>The offset of the iovec count in msghdr.</summary>
    private const int IoVectorCountOffset = 24;

    /// <summary>The offset of the control pointer in msghdr.</summary>
    private const int ControlOffset = 32;

    /// <summary>The offset of the control length in msghdr.</summary>
    private const int ControlLengthOffset = 40;

    /// <summary>The offset of the length in an iovec.</summary>
    private const int IoVectorLengthOffset = 8;

    /// <summary>The offset of the level in a control message.</summary>
    private const int LevelOffset = 8;

    /// <summary>The offset of the type in a control message.</summary>
    private const int TypeOffset = 12;

    /// <summary>The offset of the descriptor in a control message.</summary>
    private const int DataOffset = 16;

    /// <summary>The socket.</summary>
    private readonly Socket _socket;

    /// <summary>The bytes received and not yet handled.</summary>
    private readonly byte[] _received = new byte[ReceiveCapacity];

    /// <summary>How many bytes of <see cref="_received"/> are in use.</summary>
    private int _receivedLength;

    /// <summary>The next object id to give out.</summary>
    private uint _nextId = 1;

    /// <summary>Initializes a new instance of the <see cref="WaylandConnection"/> class.</summary>
    /// <param name="path">The socket path.</param>
    private WaylandConnection(string path)
    {
        _socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _socket.Connect(new UnixDomainSocketEndPoint(path));
    }

    /// <summary>Gets the writer for building requests. Call <see cref="Flush"/> to send them.</summary>
    internal WaylandMessageWriter Writer { get; } = new();

    /// <summary>Gets the descriptor of the socket, so a poller can wait on it.</summary>
    internal Socket Socket => _socket;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _socket.Dispose();

    /// <summary>Connects to the compositor named by the environment.</summary>
    /// <returns>The connection, or <see langword="null"/> when no compositor is reachable.</returns>
    internal static WaylandConnection? TryConnect()
    {
        var name = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var path = Path.IsPathRooted(name) ? name : Path.Combine(Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? string.Empty, name);
        try
        {
            return new(path);
        }
        catch (SocketException)
        {
            return null;
        }
    }

    /// <summary>Gives out the next object id for a new object.</summary>
    /// <returns>The id.</returns>
    internal uint NewId() => ++_nextId;

    /// <summary>Sends everything built in the writer.</summary>
    internal void Flush()
    {
        _ = _socket.Send(Writer.Written);
        Writer.Clear();
    }

    /// <summary>Sends everything built in the writer with a file descriptor attached to it.</summary>
    /// <param name="descriptor">The descriptor to pass.</param>
    /// <exception cref="IOException">The compositor connection did not take the whole request.</exception>
    internal void FlushWithDescriptor(int descriptor)
    {
        byte* header = stackalloc byte[MessageHeaderSize];
        byte* vector = stackalloc byte[IoVectorSize];
        byte* control = stackalloc byte[ControlSize];
        new Span<byte>(header, MessageHeaderSize).Clear();
        new Span<byte>(control, ControlSize).Clear();
        var length = Writer.Length;
        fixed (byte* data = Writer.Written)
        {
            *(byte**)vector = data;
            *(nuint*)(vector + IoVectorLengthOffset) = (nuint)length;
            *(byte**)(header + IoVectorOffset) = vector;
            *(nuint*)(header + IoVectorCountOffset) = 1;
            *(byte**)(header + ControlOffset) = control;
            *(nuint*)(header + ControlLengthOffset) = ControlSize;
            *(nuint*)control = ControlLength;
            *(int*)(control + LevelOffset) = SocketLevel;
            *(int*)(control + TypeOffset) = RightsType;
            *(int*)(control + DataOffset) = descriptor;
            var sent = NativeMethods.SendMessage((int)_socket.Handle, header, 0);
            if (sent < length)
            {
                throw new IOException("The compositor connection did not accept the request.");
            }
        }

        Writer.Clear();
    }

    /// <summary>Waits for data, reads what has arrived and hands each whole event to the handler.</summary>
    /// <param name="timeoutMilliseconds">How long to wait for data, 0 to only read what is there, or -1 to wait without limit.</param>
    /// <param name="wake">The wake-up that can end the wait early.</param>
    /// <param name="handler">Receives each event.</param>
    /// <returns><see langword="true"/> when at least one event was handled.</returns>
    /// <exception cref="IOException">The compositor closed the connection.</exception>
    internal bool Pump(int timeoutMilliseconds, EventWake wake, WaylandMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!wake.Wait((int)_socket.Handle, timeoutMilliseconds))
        {
            return false;
        }

        var count = _socket.Receive(_received.AsSpan(_receivedLength));
        if (count == 0)
        {
            throw new IOException("The compositor closed the connection.");
        }

        _receivedLength += count;
        return Dispatch(handler);
    }

    /// <summary>Handles every whole event in the receive buffer and keeps any partial one.</summary>
    /// <param name="handler">Receives each event.</param>
    /// <returns><see langword="true"/> when at least one event was handled.</returns>
    private bool Dispatch(WaylandMessageHandler handler)
    {
        var offset = 0;
        var handled = false;
        while (WaylandMessageReader.TryReadHeader(_received.AsSpan(offset, _receivedLength - offset), out var header))
        {
            var reader = new WaylandMessageReader(header, _received.AsSpan(offset + WaylandHeader.Length, header.Size - WaylandHeader.Length));
            handler(ref reader);
            offset += header.Size;
            handled = true;
        }

        _received.AsSpan(offset, _receivedLength - offset).CopyTo(_received);
        _receivedLength -= offset;
        return handled;
    }
}
