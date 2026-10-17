// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// The decoded images of one document, kept least recently used first and bounded by their pixel bytes. Images are
/// decoded outside the lock, so a slow decode does not stall other threads, and a stream is decoded by one thread at a
/// time: threads that need the same image wait for that decode and share its result. An evicted image is disposed once no
/// recording still holds it. Streams that fail to decode are remembered, so they are not decoded again.
/// </summary>
[DebuggerDisplay("ImageCache: {Count} images, {Bytes} bytes")]
internal sealed class ImageCache
{
    /// <summary>
    /// The default limit: 64 MiB. A full-page 300 dpi scan of a Letter page decodes to about 8.4 MB of gray (33.7 MB of
    /// BGRA), so this keeps the images of several scanned pages (and any number of small logos and backgrounds) decoded
    /// while neighbouring pages are recorded, without letting a long scanned document grow without bound. Recorded page
    /// pictures hold their own references, so a cached page never needs an evicted image again.
    /// </summary>
    internal const long DefaultCapacity = 64L * 1024 * 1024;

    /// <summary>Guards the map, the list and the byte count.</summary>
    private readonly Lock _gate = new();

    /// <summary>The cached images by stream.</summary>
    private readonly Dictionary<ImageKey, ImageEntry> _map = [];

    /// <summary>The decodes in progress by stream, so a second thread that needs the image waits instead of decoding it again.</summary>
    private readonly Dictionary<ImageKey, ImageDecode> _decoding = [];

    /// <summary>The streams that could not be decoded.</summary>
    private HashSet<ImageKey>? _failed;

    /// <summary>The most recently used cached image; the entries link to each other, so listing one makes no node.</summary>
    private ImageEntry? _newest;

    /// <summary>The least recently used cached image.</summary>
    private ImageEntry? _oldest;

    /// <summary>The pixel bytes held.</summary>
    private long _bytes;

    /// <summary>Whether the cache has been closed and takes no more images.</summary>
    private bool _closed;

    /// <summary>The most pixel bytes kept.</summary>
    private long _capacity = DefaultCapacity;

    /// <summary>Gets the number of cached images.</summary>
    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return _map.Count;
            }
        }
    }

    /// <summary>Gets the pixel bytes held.</summary>
    internal long Bytes
    {
        get
        {
            lock (_gate)
            {
                return _bytes;
            }
        }
    }

    /// <summary>Gets or sets the most pixel bytes kept; lowering it evicts images at once.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    internal long Capacity
    {
        get
        {
            lock (_gate)
            {
                return _capacity;
            }
        }

        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            lock (_gate)
            {
                _capacity = value;
                EvictLocked(null);
            }
        }
    }

    /// <summary>Gets an image, decoding it when it is not cached, and marks it in use.</summary>
    /// <typeparam name="TState">The type of the state given to the decoder.</typeparam>
    /// <param name="stream">The image stream.</param>
    /// <param name="state">The state given to the decoder.</param>
    /// <param name="decode">Decodes the stream; may return null.</param>
    /// <returns>The entry, which the caller must release; null when the image cannot be decoded.</returns>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ImageEntry? Acquire<TState>(PdfStream stream, TState state, Func<PdfStream, TState, ImageEntry?> decode) =>
        Acquire(new(stream, 0), (Decode: decode, State: state), static (key, value) => value.Decode(key.Stream, value.State), CancellationToken.None);

    /// <summary>Gets one resolution of an image, decoding it when needed.</summary>
    /// <typeparam name="TState">The decoder state type.</typeparam>
    /// <param name="key">The image and selected resolution.</param>
    /// <param name="state">The decoder state.</param>
    /// <param name="decode">Decodes the image.</param>
    /// <returns>The acquired image, or null when decoding fails.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ImageEntry? Acquire<TState>(in ImageKey key, TState state, Func<ImageKey, TState, ImageEntry?> decode) =>
        Acquire(key, state, decode, CancellationToken.None);

    /// <summary>
    /// Gets an image, decoding it when it is not cached, and marks it in use. When another thread is already decoding the
    /// stream this thread waits for that decode instead of starting its own.
    /// </summary>
    /// <typeparam name="TState">The type of the state given to the decoder.</typeparam>
    /// <param name="stream">The image stream.</param>
    /// <param name="state">The state given to the decoder.</param>
    /// <param name="decode">Decodes the stream; may return null.</param>
    /// <param name="cancellationToken">Stops this caller's wait for another thread's decode; it never stops a decode.</param>
    /// <returns>The entry, which the caller must release; null when the image cannot be decoded.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled while waiting for another thread's decode.</exception>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ImageEntry? Acquire<TState>(PdfStream stream, TState state, Func<PdfStream, TState, ImageEntry?> decode, CancellationToken cancellationToken) =>
        Acquire(new(stream, 0), new StreamDecodeState<TState>(decode, state), static (key, value) => value.Decode(key.Stream, value.State), cancellationToken);

    /// <summary>Gets one resolution of an image, waiting for an active decode if needed.</summary>
    /// <typeparam name="TState">The decoder state type.</typeparam>
    /// <param name="key">The image and selected resolution.</param>
    /// <param name="state">The decoder state.</param>
    /// <param name="decode">Decodes the image.</param>
    /// <param name="cancellationToken">Cancels a wait for another decode.</param>
    /// <returns>The acquired image, or null when decoding fails.</returns>
    internal ImageEntry? Acquire<TState>(in ImageKey key, TState state, Func<ImageKey, TState, ImageEntry?> decode, CancellationToken cancellationToken)
    {
        while (true)
        {
            var pending = Begin(key, out var entry, out var leader);
            if (pending is null)
            {
                return entry;
            }

            if (leader)
            {
                return Lead(key, state, decode, pending);
            }

            // An image whose decode needs itself (a mask that refers back to the image) would wait on its own decode
            // forever; it cannot be drawn, so it is treated like any image that fails to decode.
            if (pending.IsOwnedByCurrentThread)
            {
                return null;
            }

            pending.Wait(cancellationToken);
        }
    }

    /// <summary>Gets an image like Acquire, but waits for another thread's decode without holding a thread.</summary>
    /// <typeparam name="TState">The type of the state given to the decoder.</typeparam>
    /// <param name="stream">The image stream.</param>
    /// <param name="state">The state given to the decoder.</param>
    /// <param name="decode">Decodes the stream; may return null. It runs synchronously on the calling thread when this caller starts the decode.</param>
    /// <param name="cancellationToken">Stops this caller's wait for another thread's decode; it never stops a decode.</param>
    /// <returns>The entry, which the caller must release; null when the image cannot be decoded.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled while waiting for another thread's decode.</exception>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    internal async ValueTask<ImageEntry?> AcquireAsync<TState>(PdfStream stream, TState state, Func<PdfStream, TState, ImageEntry?> decode, CancellationToken cancellationToken) =>
        await AcquireAsync(new(stream, 0), new StreamDecodeState<TState>(decode, state), static (key, value) => value.Decode(key.Stream, value.State), cancellationToken).ConfigureAwait(false);

    /// <summary>Gets one resolution of an image without holding a thread while another decode runs.</summary>
    /// <typeparam name="TState">The decoder state type.</typeparam>
    /// <param name="key">The image and selected resolution.</param>
    /// <param name="state">The decoder state.</param>
    /// <param name="decode">Decodes the image.</param>
    /// <param name="cancellationToken">Cancels a wait for another decode.</param>
    /// <returns>The acquired image, or null when decoding fails.</returns>
    internal async ValueTask<ImageEntry?> AcquireAsync<TState>(ImageKey key, TState state, Func<ImageKey, TState, ImageEntry?> decode, CancellationToken cancellationToken)
    {
        while (true)
        {
            var pending = Begin(key, out var entry, out var leader);
            if (pending is null)
            {
                return entry;
            }

            if (leader)
            {
                return Lead(key, state, decode, pending);
            }

            // The decode runs synchronously on its leader's thread, so a request from that thread is the decode needing itself.
            if (pending.IsOwnedByCurrentThread)
            {
                return null;
            }

            await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Evicts every image.</summary>
    internal void Clear()
    {
        lock (_gate)
        {
            ClearLocked();
        }
    }

    /// <summary>
    /// Evicts every image and refuses new ones. Renders that are using an image finish with it; a render that asks for
    /// another image gets an <see cref="ObjectDisposedException"/>.
    /// </summary>
    internal void Close()
    {
        lock (_gate)
        {
            _closed = true;
            ClearLocked();
        }
    }

    /// <summary>Finds a cached result, or says who decodes the stream.</summary>
    /// <param name="key">The image stream and resolution.</param>
    /// <param name="entry">Receives the result, already acquired, when the stream is cached (null for a stream that failed).</param>
    /// <param name="leader">Receives whether the caller must decode the stream and complete the returned decode.</param>
    /// <returns>Null when the stream has a cached result; otherwise the decode in progress, which the caller leads or waits for.</returns>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    private ImageDecode? Begin(in ImageKey key, out ImageEntry? entry, out bool leader)
    {
        lock (_gate)
        {
            ThrowIfClosedLocked();
            leader = false;
            if (TryUseLocked(key, out entry))
            {
                return null;
            }

            if (_decoding.TryGetValue(key, out var running))
            {
                return running;
            }

            leader = true;
            var started = new ImageDecode();
            _decoding[key] = started;
            return started;
        }
    }

    /// <summary>Decodes a stream, caches the result and wakes the threads waiting for it.</summary>
    /// <typeparam name="TState">The type of the state given to the decoder.</typeparam>
    /// <param name="key">The image stream and resolution.</param>
    /// <param name="state">The state given to the decoder.</param>
    /// <param name="decode">Decodes the stream; may return null.</param>
    /// <param name="pending">The decode this caller registered in <see cref="Begin"/>.</param>
    /// <returns>The entry, already acquired; null when the image cannot be decoded.</returns>
    private ImageEntry? Lead<TState>(in ImageKey key, TState state, Func<ImageKey, TState, ImageEntry?> decode, ImageDecode pending)
    {
        try
        {
            return Publish(key, decode(key, state));
        }
        catch (OutOfMemoryException)
        {
            // Memory may be free again on the next request, so the stream is not remembered as failed.
            return null;
        }
        finally
        {
            // Waiters wake whether the decode produced an image, failed or threw; they look the result up again, and one of
            // them decodes for itself after an exception.
            lock (_gate)
            {
                _ = _decoding.Remove(key);
            }

            pending.Complete();
        }
    }

    /// <summary>Caches a decoded image, or remembers that the stream failed.</summary>
    /// <param name="key">The image stream and resolution.</param>
    /// <param name="created">The decoded entry, or null.</param>
    /// <returns>The entry, already acquired; null when the image cannot be decoded.</returns>
    /// <exception cref="ObjectDisposedException">The document was disposed while the image decoded.</exception>
    private ImageEntry? Publish(in ImageKey key, ImageEntry? created)
    {
        lock (_gate)
        {
            if (_closed)
            {
                // The document was disposed while this image decoded: nothing may keep it.
                created?.Evict();
                ThrowIfClosedLocked();
            }

            if (created is null)
            {
                _ = (_failed ??= []).Add(key);
                return null;
            }

            created.Acquire();
            created.Key = key;
            LinkNewest(created);
            _map[key] = created;
            _bytes += created.Bytes;
            EvictLocked(created);
            return created;
        }
    }

    /// <summary>Evicts every image. The caller holds the lock.</summary>
    private void ClearLocked()
    {
        for (var entry = _newest; entry is not null;)
        {
            var older = entry.Older;
            Unlink(entry);
            entry.Evict();
            entry = older;
        }

        _map.Clear();
        _failed?.Clear();
        _bytes = 0;
    }

    /// <summary>Throws when the cache has been closed. The caller holds the lock.</summary>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    private void ThrowIfClosedLocked()
    {
        if (_closed)
        {
            throw new ObjectDisposedException("PdfDocument");
        }
    }

    /// <summary>Finds a cached result and marks it used. The caller holds the lock.</summary>
    /// <param name="key">The image stream and resolution.</param>
    /// <param name="entry">Receives the entry, already acquired, or null for a stream that failed.</param>
    /// <returns><see langword="true"/> when the stream has a cached result.</returns>
    private bool TryUseLocked(in ImageKey key, out ImageEntry? entry)
    {
        if (_map.TryGetValue(key, out entry))
        {
            if (entry.IsLinked && entry != _newest)
            {
                Unlink(entry);
                LinkNewest(entry);
            }

            entry.Acquire();
            return true;
        }

        entry = null;
        return _failed?.Contains(key) == true;
    }

    /// <summary>Evicts the least recently used images until the bytes fit. The caller holds the lock.</summary>
    /// <param name="keep">An entry that stays even when it alone is over the limit, or null.</param>
    private void EvictLocked(ImageEntry? keep)
    {
        while (_bytes > _capacity && _oldest is { } entry && entry != keep)
        {
            Unlink(entry);
            _bytes -= entry.Bytes;
            if (entry.Key is { } key)
            {
                _ = _map.Remove(key);
            }

            entry.Evict();
        }
    }

    /// <summary>Puts an entry at the most recently used end of the list. The caller holds the lock.</summary>
    /// <param name="entry">The entry, which is not in the list.</param>
    private void LinkNewest(ImageEntry entry)
    {
        entry.Older = _newest;
        entry.Newer = null;
        if (_newest is not null)
        {
            _newest.Newer = entry;
        }

        _newest = entry;
        _oldest ??= entry;
        entry.IsLinked = true;
    }

    /// <summary>Takes an entry out of the list. The caller holds the lock.</summary>
    /// <param name="entry">The entry, which is in the list.</param>
    private void Unlink(ImageEntry entry)
    {
        if (entry.Newer is not null)
        {
            entry.Newer.Older = entry.Older;
        }
        else
        {
            _newest = entry.Older;
        }

        if (entry.Older is not null)
        {
            entry.Older.Newer = entry.Newer;
        }
        else
        {
            _oldest = entry.Newer;
        }

        entry.Newer = null;
        entry.Older = null;
        entry.IsLinked = false;
    }

    /// <summary>The stream decoder and its explicitly supplied state.</summary>
    /// <typeparam name="TState">The decoder state type.</typeparam>
    /// <param name="Decode">Decodes one source stream.</param>
    /// <param name="State">The caller's decoder state.</param>
    private readonly record struct StreamDecodeState<TState>(Func<PdfStream, TState, ImageEntry?> Decode, TState State);
}
