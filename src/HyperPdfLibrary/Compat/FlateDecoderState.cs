// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET11_0_OR_GREATER
using System.IO.Compression;
#endif

namespace HyperPdfLibrary.Compat;

/// <summary>Owns a document's idle Flate decoders while active decodes keep exclusive leases.</summary>
internal sealed class FlateDecoderState : IDisposable
{
#if NET11_0_OR_GREATER
    /// <summary>One idle zlib decoder, or null while it is leased.</summary>
    private ZLibDecoder? _zlib;

    /// <summary>One idle raw deflate decoder, or null while it is leased.</summary>
    private DeflateDecoder? _deflate;

    /// <summary>1 once the owning store has been disposed.</summary>
    private int _disposed;
#endif

    /// <summary>Disposes idle decoders and causes later returns to dispose their decoders.</summary>
    public void Dispose()
    {
#if NET11_0_OR_GREATER
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref _zlib, null)?.Dispose();
        Interlocked.Exchange(ref _deflate, null)?.Dispose();
#endif
    }

    /// <summary>Creates state only where span decoders exist.</summary>
    /// <returns>The decoder state on .NET 11, or null on .NET 10.</returns>
    internal static FlateDecoderState? Create()
    {
#if NET11_0_OR_GREATER
        return new();
#else
        return null;
#endif
    }

#if NET11_0_OR_GREATER
    /// <summary>Gets a zlib decoder exclusively for one active decode.</summary>
    /// <returns>The decoder, which must be returned.</returns>
    internal ZLibDecoder RentZlib() => Interlocked.Exchange(ref _zlib, null) ?? new();

    /// <summary>Gets a raw deflate decoder exclusively for one active decode.</summary>
    /// <returns>The decoder, which must be returned.</returns>
    internal DeflateDecoder RentDeflate() => Interlocked.Exchange(ref _deflate, null) ?? new();

    /// <summary>Returns a zlib decoder after its decode ends.</summary>
    /// <param name="decoder">The decoder to return or dispose.</param>
    internal void Return(ZLibDecoder decoder) => Return(ref _zlib, decoder);

    /// <summary>Returns a raw deflate decoder after its decode ends.</summary>
    /// <param name="decoder">The decoder to return or dispose.</param>
    internal void Return(DeflateDecoder decoder) => Return(ref _deflate, decoder);
#endif

#if NET11_0_OR_GREATER
    /// <summary>Stores one idle decoder while the owner is live, disposing excess or late returns.</summary>
    /// <typeparam name="T">The decoder type.</typeparam>
    /// <param name="slot">The owner's idle slot.</param>
    /// <param name="decoder">The decoder to return.</param>
    private void Return<T>(ref T? slot, T decoder)
        where T : class, IDisposable
    {
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.CompareExchange(ref slot, decoder, null) is not null)
        {
            decoder.Dispose();
            return;
        }

        if (Volatile.Read(ref _disposed) != 0 && ReferenceEquals(Interlocked.CompareExchange(ref slot, null, decoder), decoder))
        {
            decoder.Dispose();
        }
    }
#endif
}
