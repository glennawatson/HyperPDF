// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET11_0_OR_GREATER
using System.IO.Compression;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Compat;

/// <summary>
/// Keeps a few idle Flate decoders for every document and standalone decode to share. A decode leases a decoder
/// exclusively and returns it, so opening and closing documents creates no decoders once a few are idle.
/// </summary>
internal static class FlateDecoderPool
{
    /// <summary>The idle decoders kept of each kind; each holds a native zlib window, so only a few are kept.</summary>
    private const int IdleDecoders = 4;

    /// <summary>Idle zlib decoders.</summary>
    private static readonly ReusableSlots<ZLibDecoder> Zlib = new(IdleDecoders);

    /// <summary>Idle raw deflate decoders.</summary>
    private static readonly ReusableSlots<DeflateDecoder> Deflate = new(IdleDecoders);

    /// <summary>Gets a zlib decoder exclusively for one active decode.</summary>
    /// <returns>The decoder, which must be returned.</returns>
    internal static ZLibDecoder RentZlib() => Zlib.Take() ?? new();

    /// <summary>Gets a raw deflate decoder exclusively for one active decode.</summary>
    /// <returns>The decoder, which must be returned.</returns>
    internal static DeflateDecoder RentDeflate() => Deflate.Take() ?? new();

    /// <summary>Returns a zlib decoder after its decode ends; a decoder with no free slot is disposed.</summary>
    /// <param name="decoder">The decoder to return.</param>
    internal static void Return(ZLibDecoder decoder)
    {
        if (!Zlib.Offer(decoder))
        {
            decoder.Dispose();
        }
    }

    /// <summary>Returns a raw deflate decoder after its decode ends; a decoder with no free slot is disposed.</summary>
    /// <param name="decoder">The decoder to return.</param>
    internal static void Return(DeflateDecoder decoder)
    {
        if (!Deflate.Offer(decoder))
        {
            decoder.Dispose();
        }
    }
}
#endif
