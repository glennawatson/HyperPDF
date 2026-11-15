// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>A shared memory pool holding two frame buffers, so the compositor can read one while the next frame is drawn into the other.</summary>
internal sealed unsafe class WaylandShmPool : IDisposable
{
    /// <summary>The number of buffers in the pool.</summary>
    internal const int BufferCount = 2;

    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The wl_shm.create_pool opcode.</summary>
    private const int CreatePoolOpcode = 0;

    /// <summary>The wl_shm_pool.create_buffer opcode.</summary>
    private const int CreateBufferOpcode = 0;

    /// <summary>The wl_shm_pool.destroy opcode.</summary>
    private const int PoolDestroyOpcode = 1;

    /// <summary>The wl_buffer.destroy opcode.</summary>
    private const int BufferDestroyOpcode = 0;

    /// <summary>The wl_shm format for premultiplied ARGB, which is BGRA in memory on little endian hosts.</summary>
    private const uint Argb8888 = 0;

    /// <summary>The connection.</summary>
    private readonly WaylandConnection _connection;

    /// <summary>The object id of the pool.</summary>
    private readonly uint _poolId;

    /// <summary>The object ids of the buffers.</summary>
    private readonly uint[] _bufferIds = new uint[BufferCount];

    /// <summary>Whether the compositor still holds each buffer.</summary>
    private readonly bool[] _busy = new bool[BufferCount];

    /// <summary>The memory file.</summary>
    private readonly SafeFileHandle _file;

    /// <summary>The mapping.</summary>
    private readonly MemoryMappedFile _map;

    /// <summary>The mapped view.</summary>
    private readonly MemoryMappedViewAccessor _view;

    /// <summary>The start of the mapped memory.</summary>
    private readonly byte* _base;

    /// <summary>The bytes of one buffer.</summary>
    private readonly int _bufferBytes;

    /// <summary>Initializes a new instance of the <see cref="WaylandShmPool"/> class.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="shmId">The wl_shm object id.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <exception cref="IOException">The system could not create the shared memory file.</exception>
    internal WaylandShmPool(WaylandConnection connection, uint shmId, int width, int height)
    {
        _connection = connection;
        Width = width;
        Height = height;
        Stride = width * BytesPerPixel;
        _bufferBytes = Stride * height;
        var total = _bufferBytes * BufferCount;
        var descriptor = NativeMethods.MemfdCreate("hyperpdf-frame", 0);
        if (descriptor < 0)
        {
            throw new IOException("The system could not create a shared memory file.");
        }

        _file = new(descriptor, ownsHandle: true);
        RandomAccess.SetLength(_file, total);
        _map = MemoryMappedFile.CreateFromFile(_file, null, total, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: true);
        _view = _map.CreateViewAccessor(0, total, MemoryMappedFileAccess.ReadWrite);
        byte* pointer = null;
        _view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        _base = pointer;

        _poolId = connection.NewId();
        connection.Writer.Begin(shmId, CreatePoolOpcode);
        connection.Writer.WriteUInt(_poolId);
        connection.Writer.WriteInt(total);
        connection.Writer.End();
        connection.FlushWithDescriptor(descriptor);

        for (var i = 0; i < BufferCount; i++)
        {
            _bufferIds[i] = connection.NewId();
            connection.Writer.Begin(_poolId, CreateBufferOpcode);
            connection.Writer.WriteUInt(_bufferIds[i]);
            connection.Writer.WriteInt(i * _bufferBytes);
            connection.Writer.WriteInt(width);
            connection.Writer.WriteInt(height);
            connection.Writer.WriteInt(Stride);
            connection.Writer.WriteUInt(Argb8888);
            connection.Writer.End();
        }

        connection.Flush();
    }

    /// <summary>Gets the width of each buffer in pixels.</summary>
    internal int Width { get; }

    /// <summary>Gets the height of each buffer in pixels.</summary>
    internal int Height { get; }

    /// <summary>Gets the bytes per line.</summary>
    internal int Stride { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        for (var i = 0; i < BufferCount; i++)
        {
            _connection.Writer.Begin(_bufferIds[i], BufferDestroyOpcode);
            _connection.Writer.End();
        }

        _connection.Writer.Begin(_poolId, PoolDestroyOpcode);
        _connection.Writer.End();
        _connection.Flush();
        _view.SafeMemoryMappedViewHandle.ReleasePointer();
        _view.Dispose();
        _map.Dispose();
        _file.Dispose();
    }

    /// <summary>Finds a buffer the compositor has released and marks it busy.</summary>
    /// <param name="index">Receives the buffer number.</param>
    /// <returns><see langword="true"/> when a buffer was free.</returns>
    internal bool TryAcquire(out int index)
    {
        index = Array.IndexOf(_busy, false);
        if (index < 0)
        {
            return false;
        }

        _busy[index] = true;
        return true;
    }

    /// <summary>Gets the pixels of a buffer.</summary>
    /// <param name="index">The buffer number.</param>
    /// <returns>The pixels.</returns>
    internal Span<byte> Pixels(int index) => new(_base + (index * _bufferBytes), _bufferBytes);

    /// <summary>Gets the wl_buffer object id of a buffer.</summary>
    /// <param name="index">The buffer number.</param>
    /// <returns>The object id.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal uint BufferId(int index) => _bufferIds[index];

    /// <summary>Marks a buffer free after the compositor released it.</summary>
    /// <param name="bufferId">The wl_buffer object id.</param>
    /// <returns><see langword="true"/> when the buffer belongs to this pool.</returns>
    internal bool Release(uint bufferId)
    {
        var index = Array.IndexOf(_bufferIds, bufferId);
        if (index < 0)
        {
            return false;
        }

        _busy[index] = false;
        return true;
    }
}
