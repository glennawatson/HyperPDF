// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Compresses streams losslessly at zlib's best level. Uncompressed streams are compressed; Flate streams are inflated
/// and deflated again with their predictor parameters kept, so the predicted bytes are unchanged; LZW streams become
/// Flate with the same predictor; streams that are only ASCII-armoured are decoded and compressed. A result is used only
/// when it is smaller. Metadata streams stay uncompressed, and crypt-filtered and image-codec streams are left alone.
/// </summary>
internal static class StreamRecompressor
{
    /// <summary>Gets the byte filters this library decodes, in full and abbreviated forms.</summary>
    private static ReadOnlySpan<int> ByteFilters =>
    [
        (int)KnownName.FlateDecode,
        (int)KnownName.Fl,
        (int)KnownName.LZWDecode,
        (int)KnownName.LZW,
        (int)KnownName.ASCII85Decode,
        (int)KnownName.A85,
        (int)KnownName.ASCIIHexDecode,
        (int)KnownName.AHx,
        (int)KnownName.RunLengthDecode,
        (int)KnownName.RL,
    ];

    /// <summary>Recompresses a stream when that makes it smaller.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The smaller stream, or <see langword="null"/> when nothing was gained.</returns>
    internal static PdfStream? TryRecompress(PdfStream stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dictionary = stream.Dictionary;
        if (dictionary.IsName(KnownName.Type, KnownName.Metadata) || dictionary.ContainsKey(KnownName.F))
        {
            return null;
        }

        var filters = dictionary.Get(KnownName.Filter);
        var count = ImageFilters.Count(filters);
        var plain = dictionary.GetRaw(KnownName.DecodeParms).IsNull;
        if (count == 0)
        {
            return plain ? Compress(stream, dictionary, default, false, cancellationToken) : null;
        }

        var first = ImageFilters.At(filters, 0);
        if (count == 1 && IsPredictable(first))
        {
            return RecompressSingle(stream, first, Parameters(dictionary), cancellationToken);
        }

        return plain && IsByteChain(filters, count) ? Compress(stream, dictionary, filters, true, cancellationToken) : null;
    }

    /// <summary>Compresses data at the best level.</summary>
    /// <param name="data">The data.</param>
    /// <param name="output">Receives the compressed bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Deflate(ReadOnlySpan<byte> data, ref PooledBuffer output) => ZLibSmallest.Compress(data, ref output);

    /// <summary>Determines whether a filter takes predictor parameters: Flate or LZW.</summary>
    /// <param name="filter">The filter.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsPredictable(KnownName filter) => filter is KnownName.FlateDecode or KnownName.Fl or KnownName.LZWDecode or KnownName.LZW;

    /// <summary>Gets the single filter's parameters.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <returns>The parameters, or <see langword="null"/>.</returns>
    private static PdfDictionary? Parameters(PdfDictionary dictionary)
    {
        var parameters = dictionary.Get(KnownName.DecodeParms);
        return parameters.AsArray()?.GetDictionary(0) ?? parameters.AsDictionary();
    }

    /// <summary>Determines whether every filter is a byte filter this library decodes and none is a crypt or image codec.</summary>
    /// <param name="filters">The /Filter value.</param>
    /// <param name="count">The number of filters.</param>
    /// <returns><see langword="true"/> when the chain can be decoded and replaced.</returns>
    private static bool IsByteChain(PdfValue filters, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (!ByteFilters.Contains((int)ImageFilters.At(filters, i)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Re-encodes a stream whose only filter is Flate or LZW, keeping its predictor.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="filter">The filter.</param>
    /// <param name="parameters">Its parameters, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The smaller stream, or <see langword="null"/>.</returns>
    private static PdfStream? RecompressSingle(PdfStream stream, KnownName filter, PdfDictionary? parameters, CancellationToken cancellationToken)
    {
        using var raw = stream.LeaseRawData();
        var plain = PdfObjectWriter.PlainData(stream, raw.Span);
        var inflated = default(PooledBuffer);
        var deflated = default(PooledBuffer);
        try
        {
            Inflate(plain, filter, parameters, ref inflated, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Deflate(inflated.WrittenSpan, ref deflated);
            cancellationToken.ThrowIfCancellationRequested();
            return deflated.Length < stream.RawLength ? Finish(FlateDictionary(stream.Dictionary, parameters), deflated.WrittenSpan) : null;
        }
        finally
        {
            inflated.Dispose();
            deflated.Dispose();
        }
    }

    /// <summary>Undoes a Flate or LZW filter without its predictor.</summary>
    /// <param name="data">The encoded data.</param>
    /// <param name="filter">The filter.</param>
    /// <param name="parameters">Its parameters, or <see langword="null"/>.</param>
    /// <param name="output">Receives the predicted bytes.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void Inflate(ReadOnlySpan<byte> data, KnownName filter, PdfDictionary? parameters, ref PooledBuffer output, CancellationToken cancellationToken)
    {
        if (filter is KnownName.FlateDecode or KnownName.Fl)
        {
            _ = FlateFilter.TryDecode(data, ref output, cancellationToken);
            return;
        }

        LzwFilter.Decode(data, parameters?.GetInt32(KnownName.EarlyChange, 1) ?? 1, ref output, cancellationToken);
    }

    /// <summary>Copies a stream dictionary as a Flate stream with the same predictor.</summary>
    /// <param name="source">The stream dictionary.</param>
    /// <param name="parameters">The old filter's parameters, or <see langword="null"/>.</param>
    /// <returns>The new dictionary.</returns>
    private static PdfDictionary FlateDictionary(PdfDictionary source, PdfDictionary? parameters)
    {
        var dictionary = source.Clone();
        dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
        if (parameters is not null)
        {
            var kept = parameters.Clone();
            _ = kept.Remove(KnownName.EarlyChange);
            dictionary.Set(KnownName.DecodeParms, PdfValue.FromDictionary(kept));
        }

        return dictionary;
    }

    /// <summary>Decodes a stream's byte filters, if any, and compresses the result.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="filters">The filters to decode, or a null value for none.</param>
    /// <param name="decode">Whether to decode the filters first.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The smaller stream, or <see langword="null"/>.</returns>
    private static PdfStream? Compress(PdfStream stream, PdfDictionary dictionary, PdfValue filters, bool decode, CancellationToken cancellationToken)
    {
        using var raw = stream.LeaseRawData();
        var plain = PdfObjectWriter.PlainData(stream, raw.Span);
        var decoded = default(PooledBuffer);
        var deflated = default(PooledBuffer);
        try
        {
            var data = plain;
            if (decode)
            {
                _ = PdfStreamDecoder.Apply(plain, filters, default, ref decoded, cancellationToken);
                data = decoded.WrittenSpan;
            }

            cancellationToken.ThrowIfCancellationRequested();
            Deflate(data, ref deflated);
            cancellationToken.ThrowIfCancellationRequested();
            if (deflated.Length >= stream.RawLength)
            {
                return null;
            }

            var copy = dictionary.Clone();
            copy.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            _ = copy.Remove(KnownName.DecodeParms);
            return Finish(copy, deflated.WrittenSpan);
        }
        finally
        {
            decoded.Dispose();
            deflated.Dispose();
        }
    }

    /// <summary>Makes the new stream, without the old /Length.</summary>
    /// <param name="dictionary">The new dictionary.</param>
    /// <param name="data">The new data.</param>
    /// <returns>The stream.</returns>
    private static PdfStream Finish(PdfDictionary dictionary, ReadOnlySpan<byte> data)
    {
        _ = dictionary.Remove(KnownName.Length);
        return new(dictionary, data.ToArray());
    }
}
