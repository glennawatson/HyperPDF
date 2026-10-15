// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using Microsoft.Win32.SafeHandles;

namespace HyperPdfLibrary.IO;

/// <summary>
/// A file read through a seekable stream or a file handle, with a bounded cache of fixed-size pages that drops the least
/// recently used page when full. Small reads come from the cache; large reads go straight to the file. Windows are copied
/// into pooled buffers. Works for files of any size and for streams that cannot be mapped.
/// </summary>
[DebuggerDisplay("StreamPdfByteSource: {Length} bytes")]
public sealed class StreamPdfByteSource : PdfByteSource
{
    /// <summary>The size of a cached page.</summary>
    private const int PageLength = 1 << PageShift;

    /// <summary>The default cache budget in bytes.</summary>
    private const long DefaultCache = 4L << MegabyteShift;

    /// <summary>The share of the cache one load-ahead may fill, as a divisor, so it does not push out its own pages.</summary>
    private const int PrefetchShare = 2;

    /// <summary>The longest, in milliseconds, a wait for the stream's turn lasts before it checks whether the source was disposed.</summary>
    private const int StreamPollMilliseconds = 100;

    /// <summary>The most pages one async read loads.</summary>
    private const int MaxRunPages = 16;

    /// <summary>The power of two of the page size.</summary>
    private const int PageShift = 16;

    /// <summary>The power of two of a megabyte.</summary>
    private const int MegabyteShift = 20;

    /// <summary>The pages a read must span before it bypasses the cache.</summary>
    private const int DirectReadPages = 4;

    /// <summary>The shortest read that bypasses the cache, so scans and large streams do not flush it.</summary>
    private const int DirectReadLength = PageLength * DirectReadPages;

    /// <summary>The cache tables, which go back to the pool when the source is closed.</summary>
    private readonly StreamPageCache _cache;

    /// <summary>The host's cache pool, when the source was opened with registered services.</summary>
    private readonly ScratchSlots<StreamPageCache>? _cachePool;

    /// <summary>Guards the cache, and the stream's position when reading a stream.</summary>
    private readonly Lock _gate = new();

    /// <summary>Serialises stream positions across awaits; file handles use independent offsets and need no semaphore.</summary>
    private readonly SemaphoreSlim? _streamAccess;

    /// <summary>The stream, or <see langword="null"/> when reading a file handle.</summary>
    private readonly Stream? _stream;

    /// <summary>The file handle, or <see langword="null"/> when reading a stream.</summary>
    private readonly SafeFileHandle? _file;

    /// <summary>Whether disposal closes the stream or handle.</summary>
    private readonly bool _ownsInput;

    /// <summary>The cached pages by slot; each is rented from the shared pool.</summary>
    private readonly byte[]?[] _pages;

    /// <summary>The page number in each slot, or -1 when empty.</summary>
    private readonly long[] _pageNumbers;

    /// <summary>The bytes held in each slot; the last page of the file is short.</summary>
    private readonly int[] _pageLengths;

    /// <summary>The clock value of each slot's last use.</summary>
    private readonly long[] _lastUse;

    /// <summary>The slot holding each cached page.</summary>
    private readonly Dictionary<long, int> _slots;

    /// <summary>The use clock.</summary>
    private long _clock;

    /// <summary>1 once the cache holds every page of the file, so nothing is left to load.</summary>
    private int _complete;

    /// <summary>The pages dropped from the cache to make room, so a caller can tell that what it loaded ahead may be gone.</summary>
    private long _evictions;

    /// <summary>Whether the source has been closed; guarded by <see cref="_gate"/>.</summary>
    private bool _closed;

    /// <summary>Initializes a new instance of the <see cref="StreamPdfByteSource"/> class over a stream it does not own.</summary>
    /// <param name="stream">A readable, seekable stream that must stay open and unchanged while the source is used.</param>
    /// <exception cref="ArgumentException">The stream cannot read or seek.</exception>
    public StreamPdfByteSource(Stream stream)
        : this(stream, DefaultCache, false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StreamPdfByteSource"/> class over a stream.</summary>
    /// <param name="stream">A readable, seekable stream that must stay unchanged while the source is used.</param>
    /// <param name="cacheBytes">The most bytes the page cache holds; at least one page is kept.</param>
    /// <param name="ownsStream">Whether disposing the source disposes the stream.</param>
    /// <exception cref="ArgumentException">The stream cannot read or seek.</exception>
    public StreamPdfByteSource(Stream stream, long cacheBytes, bool ownsStream)
        : this(stream, cacheBytes, ownsStream, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StreamPdfByteSource"/> class over a file handle.</summary>
    /// <param name="handle">A handle opened for reading.</param>
    /// <param name="cacheBytes">The most bytes the page cache holds; at least one page is kept.</param>
    /// <param name="ownsHandle">Whether disposing the source closes the handle.</param>
    public StreamPdfByteSource(SafeFileHandle handle, long cacheBytes, bool ownsHandle)
        : this(handle, cacheBytes, ownsHandle, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StreamPdfByteSource"/> class using the host's cache pool.</summary>
    /// <param name="stream">The readable input.</param>
    /// <param name="cacheBytes">The cache budget.</param>
    /// <param name="ownsStream">Whether disposal closes the input.</param>
    /// <param name="pools">The registered host's pools, when available.</param>
    /// <exception cref="ArgumentException">The stream cannot read or seek.</exception>
    internal StreamPdfByteSource(Stream stream, long cacheBytes, bool ownsStream, ScratchPools? pools)
        : this(cacheBytes, ownsStream, pools)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new ArgumentException("The stream must be readable and seekable.", nameof(stream));
        }

        _stream = stream;
        _streamAccess = new(1, 1);
        Length = stream.Length;
    }

    /// <summary>Initializes a new instance of the <see cref="StreamPdfByteSource"/> class using the host's cache pool.</summary>
    /// <param name="handle">The readable input.</param>
    /// <param name="cacheBytes">The cache budget.</param>
    /// <param name="ownsHandle">Whether disposal closes the input.</param>
    /// <param name="pools">The registered host's pools, when available.</param>
    internal StreamPdfByteSource(SafeFileHandle handle, long cacheBytes, bool ownsHandle, ScratchPools? pools)
        : this(cacheBytes, ownsHandle, pools)
    {
        ArgumentNullException.ThrowIfNull(handle);
        _file = handle;
        Length = RandomAccess.GetLength(handle);
    }

    /// <summary>Initializes a new instance of the <see cref="StreamPdfByteSource"/> class with an empty cache.</summary>
    /// <param name="cacheBytes">The cache budget.</param>
    /// <param name="ownsInput">Whether disposal closes the input.</param>
    /// <param name="pools">The registered host's pools, when available.</param>
    private StreamPdfByteSource(long cacheBytes, bool ownsInput, ScratchPools? pools)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cacheBytes);
        var slots = (int)Math.Clamp(cacheBytes / PageLength, 1, int.MaxValue / PageLength);
        _ownsInput = ownsInput;
        _cachePool = pools?.Slots<StreamPageCache>();
        _cache = StreamPageCache.Rent(slots, _cachePool);
        _pages = _cache.Pages;
        _pageNumbers = _cache.PageNumbers;
        _pageLengths = _cache.PageLengths;
        _lastUse = _cache.LastUse;
        _slots = _cache.Slots;
    }

    /// <summary>Gets the size of a cached page.</summary>
    public static int PageSize => PageLength;

    /// <summary>Gets the default cache budget in bytes.</summary>
    public static long DefaultCacheBytes => DefaultCache;

    /// <inheritdoc/>
    public override long Length { get; }

    /// <summary>Gets the number of pages the cache can hold.</summary>
    public int CachePages => _pages.Length;

    /// <inheritdoc/>
    internal override bool NeedsPrefetch => Volatile.Read(ref _complete) == 0;

    /// <inheritdoc/>
    internal override long CacheGeneration => Volatile.Read(ref _evictions);

    /// <inheritdoc/>
    internal override long PrefetchBudget => (long)_pages.Length * PageLength / PrefetchShare;

    /// <summary>Opens a file with the default cache budget.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The source, which owns the file handle.</returns>
    /// <exception cref="IOException">The file cannot be opened.</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be read.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StreamPdfByteSource Open(string path) => Open(path, DefaultCache);

    /// <summary>Opens a file.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="cacheBytes">The most bytes the page cache holds.</param>
    /// <returns>The source, which owns the file handle.</returns>
    /// <exception cref="IOException">The file cannot be opened.</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be read.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StreamPdfByteSource Open(string path, long cacheBytes) => Open(path, cacheBytes, null);

    /// <summary>Opens a file for async reads (the handle is opened for overlapped I/O).</summary>
    /// <param name="path">The file path.</param>
    /// <param name="cacheBytes">The most bytes the page cache holds.</param>
    /// <returns>The source, which owns the file handle.</returns>
    /// <exception cref="IOException">The file cannot be opened.</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be read.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StreamPdfByteSource OpenAsynchronous(string path, long cacheBytes) => OpenAsynchronous(path, cacheBytes, null);

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        var available = Length - offset;
        if (available <= 0 || destination.IsEmpty)
        {
            return 0;
        }

        var count = (int)Math.Min(destination.Length, available);
        if (count >= DirectReadLength && !IsRangeCached(offset, count))
        {
            return await ReadInputAsync(offset, destination[..count], cancellationToken).ConfigureAwait(false);
        }

        await PrefetchAsync(offset, count, cancellationToken).ConfigureAwait(false);
        return Read(offset, destination.Span[..count]);
    }

    /// <inheritdoc/>
    public override async ValueTask PrefetchAsync(long offset, long length, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        offset = Math.Max(0, offset);
        var end = Math.Min(Length, offset + length);
        if (end <= offset)
        {
            return;
        }

        var page = offset >> PageShift;
        var last = Math.Min((end - 1) >> PageShift, page + _pages.Length - 1);
        while (page <= last)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (IsCached(page))
            {
                page++;
                continue;
            }

            var run = page + 1;
            while (run <= last && run - page < MaxRunPages && !IsCached(run))
            {
                run++;
            }

            await LoadRunAsync(page, (int)(run - page), cancellationToken).ConfigureAwait(false);
            page = run;
        }
    }

    /// <summary>Opens a file using the host's cache pool.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="cacheBytes">The cache budget.</param>
    /// <param name="pools">The registered host's pools, when available.</param>
    /// <returns>The source, which owns its handle.</returns>
    internal static StreamPdfByteSource Open(string path, long cacheBytes, ScratchPools? pools)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileOptions.RandomAccess);
        try
        {
            return new(handle, cacheBytes, true, pools);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    /// <summary>Opens a file using the host's cache pool.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="cacheBytes">The cache budget.</param>
    /// <param name="pools">The registered host's pools, when available.</param>
    /// <returns>The source, which owns its handle.</returns>
    internal static StreamPdfByteSource OpenAsynchronous(string path, long cacheBytes, ScratchPools? pools)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileOptions.RandomAccess | FileOptions.Asynchronous);
        try
        {
            return new(handle, cacheBytes, true, pools);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_gate)
            {
                _closed = true;
                ReturnPages();
                StreamPageCache.Return(_cache, _cachePool);
            }

            _streamAccess?.Dispose();
            if (_ownsInput)
            {
                _stream?.Dispose();
                _file?.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    private protected override int ReadCore(long offset, Span<byte> destination)
    {
        if (destination.Length >= DirectReadLength && !IsRangeCached(offset, destination.Length))
        {
            return ReadInput(offset, destination);
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            var copied = 0;
            while (copied < destination.Length)
            {
                var position = offset + copied;
                var slot = GetSlot(position >> PageShift);
                var within = (int)(position & (PageLength - 1));
                var available = _pageLengths[slot] - within;
                if (available <= 0)
                {
                    break;
                }

                var count = Math.Min(available, destination.Length - copied);
                _pages[slot].AsSpan(within, count).CopyTo(destination[copied..]);
                copied += count;
            }

            return copied;
        }
    }

    /// <inheritdoc/>
    private protected override PdfByteLease LeaseCore(long offset, int length)
    {
        var rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var read = ReadCore(offset, rented.AsSpan(0, length));

            // A file that shrank since it was opened reads as zeros past its new end.
            rented.AsSpan(read, length - read).Clear();
            return new(rented, length);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(rented);
            throw;
        }
    }

    /// <summary>Reads from a file handle until the buffer is full or the file ends.</summary>
    /// <param name="file">The handle.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="destination">The buffer.</param>
    /// <returns>The number of bytes read.</returns>
    private static int ReadFile(SafeFileHandle file, long offset, Span<byte> destination)
    {
        var total = 0;
        while (total < destination.Length)
        {
            var read = RandomAccess.Read(file, destination[total..], offset + total);
            if (read <= 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    /// <summary>Gets the slot holding a page, loading it into the least recently used slot when it is not cached.</summary>
    /// <param name="page">The page number.</param>
    /// <returns>The slot.</returns>
    private int GetSlot(long page)
    {
        _clock++;
        if (_slots.TryGetValue(page, out var slot))
        {
            _lastUse[slot] = _clock;
            return slot;
        }

        slot = OldestSlot();
        if (_pageNumbers[slot] >= 0)
        {
            _ = _slots.Remove(_pageNumbers[slot]);
            _pageNumbers[slot] = -1;
            _ = Interlocked.Increment(ref _evictions);
        }

        var buffer = _pages[slot] ??= ArrayPool<byte>.Shared.Rent(PageLength);
        var start = page << PageShift;
        _pageLengths[slot] = ReadInput(start, buffer.AsSpan(0, (int)Math.Min(PageLength, Length - start)));
        _pageNumbers[slot] = page;
        _lastUse[slot] = _clock;
        _slots[page] = slot;
        return slot;
    }

    /// <summary>Checks whether every page of a range is in the cache, so a large read can be served from it instead of the file.</summary>
    /// <param name="offset">The first byte.</param>
    /// <param name="length">The number of bytes.</param>
    /// <returns><see langword="true"/> when the whole range is cached.</returns>
    private bool IsRangeCached(long offset, int length)
    {
        if (Volatile.Read(ref _complete) != 0)
        {
            return true;
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            for (var page = offset >> PageShift; page <= (offset + length - 1) >> PageShift; page++)
            {
                if (!_slots.ContainsKey(page))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Checks whether a page is in the cache.</summary>
    /// <param name="page">The page number.</param>
    /// <returns><see langword="true"/> when it is cached.</returns>
    private bool IsCached(long page)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            return _slots.ContainsKey(page);
        }
    }

    /// <summary>Reads a run of missing pages with one async read and caches them.</summary>
    /// <param name="first">The first page number.</param>
    /// <param name="count">The number of pages.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the pages are cached.</returns>
    private async ValueTask LoadRunAsync(long first, int count, CancellationToken cancellationToken)
    {
        var start = first << PageShift;
        var bytes = (int)Math.Min((long)count << PageShift, Length - start);
        var rented = ArrayPool<byte>.Shared.Rent(bytes);
        try
        {
            var read = await ReadInputAsync(start, rented.AsMemory(0, bytes), cancellationToken).ConfigureAwait(false);
            StorePages(first, count, rented, read);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Copies pages read in one run into the cache, keeping pages that arrived meanwhile.</summary>
    /// <param name="first">The first page number.</param>
    /// <param name="count">The number of pages.</param>
    /// <param name="run">The bytes read.</param>
    /// <param name="read">The number of bytes read.</param>
    private void StorePages(long first, int count, byte[] run, int read)
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            for (var i = 0; i < count && (i << PageShift) < read; i++)
            {
                var page = first + i;
                if (_slots.ContainsKey(page))
                {
                    continue;
                }

                var slot = OldestSlot();
                if (_pageNumbers[slot] >= 0)
                {
                    _ = _slots.Remove(_pageNumbers[slot]);
                    _ = Interlocked.Increment(ref _evictions);
                }

                var length = Math.Min(PageLength, read - (i << PageShift));
                var buffer = _pages[slot] ??= ArrayPool<byte>.Shared.Rent(PageLength);
                run.AsSpan(i << PageShift, length).CopyTo(buffer);
                _clock++;
                _pageLengths[slot] = length;
                _pageNumbers[slot] = page;
                _lastUse[slot] = _clock;
                _slots[page] = slot;
            }

            // A file that fits the cache and is wholly cached is never evicted from it, so later reads never touch the file.
            var totalPages = (Length + PageLength - 1) >> PageShift;
            if (totalPages <= _pages.Length && _slots.Count == totalPages)
            {
                Volatile.Write(ref _complete, 1);
            }
        }
    }

    /// <summary>Reads from the stream or file with async I/O, filling the buffer unless the input ends first.</summary>
    /// <param name="offset">The offset.</param>
    /// <param name="destination">The buffer.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The number of bytes read.</returns>
    private async ValueTask<int> ReadInputAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken)
    {
        if (_file is not null)
        {
            var total = 0;
            while (total < destination.Length)
            {
                var read = await RandomAccess.ReadAsync(_file, destination[total..], offset + total, cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                total += read;
            }

            return total;
        }

        await EnterStreamAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            _stream!.Position = offset;
            return await _stream.ReadAtLeastAsync(destination, destination.Length, false, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ExitStream();
        }
    }

    /// <summary>Waits for the stream's turn; gives up with <see cref="ObjectDisposedException"/> once the source is disposed.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the stream is free.</returns>
    private async ValueTask EnterStreamAsync(CancellationToken cancellationToken)
    {
        // A wait that outlives the semaphore would never wake, so each wait is bounded and re-checks disposal.
        while (!await _streamAccess!.WaitAsync(StreamPollMilliseconds, cancellationToken).ConfigureAwait(false))
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
        }
    }

    /// <summary>Waits for the stream's turn without async I/O; gives up with <see cref="ObjectDisposedException"/> once the source is disposed.</summary>
    private void EnterStream()
    {
        while (!_streamAccess!.Wait(StreamPollMilliseconds))
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
        }
    }

    /// <summary>Gives the stream's turn up; a source disposed while it was held has nothing left to release.</summary>
    private void ExitStream()
    {
        try
        {
            _ = _streamAccess!.Release();
        }
        catch (ObjectDisposedException)
        {
            // Disposed while held: there are no waiters left that this release could wake.
        }
    }

    /// <summary>Finds an empty slot, or else the least recently used one.</summary>
    /// <returns>The slot.</returns>
    private int OldestSlot()
    {
        var oldest = 0;
        for (var i = 0; i < _lastUse.Length; i++)
        {
            if (_pageNumbers[i] < 0)
            {
                return i;
            }

            if (_lastUse[i] < _lastUse[oldest])
            {
                oldest = i;
            }
        }

        return oldest;
    }

    /// <summary>Reads from the stream or file, filling the buffer unless the input ends first.</summary>
    /// <param name="offset">The offset.</param>
    /// <param name="destination">The buffer.</param>
    /// <returns>The number of bytes read.</returns>
    private int ReadInput(long offset, Span<byte> destination)
    {
        if (_file is not null)
        {
            return ReadFile(_file, offset, destination);
        }

        // The async path holds this semaphore across its awaits, so a synchronous read waits for it instead of moving the position.
        EnterStream();
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            _stream!.Position = offset;
            return _stream.ReadAtLeast(destination, destination.Length, false);
        }
        finally
        {
            ExitStream();
        }
    }

    /// <summary>Returns the cached pages to the pool.</summary>
    private void ReturnPages()
    {
        for (var i = 0; i < _pages.Length; i++)
        {
            if (_pages[i] is { } page)
            {
                ArrayPool<byte>.Shared.Return(page);
                _pages[i] = null;
            }

            _pageNumbers[i] = -1;
        }
    }
}
