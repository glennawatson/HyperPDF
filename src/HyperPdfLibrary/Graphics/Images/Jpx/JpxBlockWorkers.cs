// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The working objects one decode shares between its threads: the code-block decoders, the tile streams and the decoder
/// itself, and the cache and token every stage of the decode uses. A thread takes a decoder when it starts work and hands
/// it back when it ends, so the decoders (and the buffers they hold) are made at most once for each thread that works at
/// the same time. When a decode ends the objects stay with the cache that owns them, without the buffers they rented, and
/// the next decode on that cache takes them again.
/// </summary>
[DebuggerDisplay("JpxBlockWorkers: {_idleCount} idle")]
internal sealed class JpxBlockWorkers : IDisposable
{
    /// <summary>Guards the idle decoders.</summary>
    private readonly Lock _gate = new();

    /// <summary>The decoders no thread is using.</summary>
    private readonly JpxBlockState[] _idle = new JpxBlockState[Math.Max(Environment.ProcessorCount, 1)];

    /// <summary>The number of idle decoders.</summary>
    private int _idleCount;

    /// <summary>Initializes a new instance of the <see cref="JpxBlockWorkers"/> class.</summary>
    /// <param name="scratchPools">The bounded cache every stage rents from.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    internal JpxBlockWorkers(ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        Pools = scratchPools;
        Token = cancellationToken;
    }

    /// <summary>Gets the bounded cache every stage rents from.</summary>
    internal ScratchPools Pools { get; }

    /// <summary>Gets the token that cancels the decode.</summary>
    internal CancellationToken Token { get; private set; }

    /// <summary>Gets the tile streams that are free for the next tile of the decode, or for the next decode.</summary>
    internal Stack<JpxTileStream> SpareStreams { get; } = [];

    /// <summary>Gets or sets the decoder that works with these objects.</summary>
    internal JpxDecoder? Decoder { get; set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            while (_idleCount > 0)
            {
                _idleCount--;
                _idle[_idleCount].Dispose();
                _idle[_idleCount] = null!;
            }
        }

        while (SpareStreams.TryPop(out var stream))
        {
            stream.Dispose();
        }

        Decoder = null;
    }

    /// <summary>Takes the idle objects of a cache for a decode, or makes new ones when none are idle.</summary>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    /// <returns>Objects no other decode uses, which the decode gives back with <see cref="Return()"/>.</returns>
    internal static JpxBlockWorkers Rent(ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        var workers = scratchPools.Objects<JpxBlockWorkers>().Take() ?? new(scratchPools, cancellationToken);
        workers.Token = cancellationToken;
        return workers;
    }

    /// <summary>Takes an idle decoder, or makes one when none is idle.</summary>
    /// <returns>A decoder no other thread uses.</returns>
    internal JpxBlockState Rent()
    {
        lock (_gate)
        {
            if (_idleCount > 0)
            {
                _idleCount--;
                var idle = _idle[_idleCount];
                _idle[_idleCount] = null!;
                return idle;
            }
        }

        return new(Pools);
    }

    /// <summary>Hands a decoder back, or disposes it when enough are idle already.</summary>
    /// <param name="decoder">The decoder.</param>
    internal void Return(JpxBlockState decoder)
    {
        lock (_gate)
        {
            if (_idleCount < _idle.Length)
            {
                _idle[_idleCount] = decoder;
                _idleCount++;
                return;
            }
        }

        decoder.Dispose();
    }

    /// <summary>
    /// Ends the decode: gives back the buffers the idle objects hold and keeps the objects for the next decode of the
    /// cache, or disposes them when the cache does not keep them.
    /// </summary>
    internal void Return()
    {
        Token = default;
        lock (_gate)
        {
            for (var i = 0; i < _idleCount; i++)
            {
                _idle[i].Dispose();
            }
        }

        foreach (var stream in SpareStreams)
        {
            stream.Dispose();
        }

        if (!Pools.Objects<JpxBlockWorkers>().Offer(this))
        {
            Dispose();
        }
    }
}
