// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Makes the Flate-compressed streams of embedded fonts.</summary>
internal static class FontStreams
{
    /// <summary>The entries of a font stream's dictionary.</summary>
    private const int StreamEntries = 4;

    /// <summary>Creates a Flate-compressed stream, recording the uncompressed length when a key is given.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="data">The immutable data, which may be retained by the document.</param>
    /// <param name="lengthKey">The key of the uncompressed length, or no name.</param>
    /// <returns>The stream.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfStream Compress(IPdfObjectSink sink, byte[] data, PdfName lengthKey) =>
        Compress(sink, data, lengthKey, default);

    /// <summary>Creates a Flate-compressed stream with a subtype, recording the uncompressed length when a key is given.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="data">The immutable data, which may be retained by the document.</param>
    /// <param name="lengthKey">The key of the uncompressed length, or no name.</param>
    /// <param name="subtype">The stream's <c>/Subtype</c>, or no name.</param>
    /// <returns>The stream.</returns>
    internal static PdfStream Compress(IPdfObjectSink sink, byte[] data, PdfName lengthKey, PdfName subtype)
    {
        var dictionary = new PdfDictionary(sink.Owner, StreamEntries);
        dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
        if (!lengthKey.IsNone)
        {
            dictionary.Set(lengthKey, PdfValue.FromInteger(data.Length));
        }

        if (!subtype.IsNone)
        {
            dictionary.Set(KnownName.Subtype, PdfValue.FromName(subtype));
        }

        // Streams in this document can share immutable compressed bytes for the same program.
        return new(dictionary, FontStreamCache.GetOrCompress(sink.FontEmbedding, data));
    }
}
