// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Filters;

/// <summary>Runs a stream's data through decryption and its filter chain, ping-ponging between two pooled buffers.</summary>
internal static class PdfStreamDecoder
{
    /// <summary>Gets the byte filters this library applies, in full and abbreviated form; the image codecs are named by <see cref="ImageCodec"/>.</summary>
    private static ReadOnlySpan<KnownName> ByteFilters =>
    [
        KnownName.FlateDecode,
        KnownName.Fl,
        KnownName.LZWDecode,
        KnownName.LZW,
        KnownName.ASCIIHexDecode,
        KnownName.AHx,
        KnownName.ASCII85Decode,
        KnownName.A85,
        KnownName.RunLengthDecode,
        KnownName.RL,
        KnownName.BrotliDecode,
        KnownName.Crypt,
    ];

    /// <summary>Decodes a stream.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="output">The buffer receiving the decoded data.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The image codec still to apply, if any.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageCodec Decode(PdfStream stream, ref PooledBuffer output, CancellationToken cancellationToken = default) =>
        Decode(stream, stream.Dictionary.Owner?.Context, ref output, cancellationToken);

    /// <summary>Decodes a stream, reporting to a given context instead of the document's own.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="context">The context that receives diagnostics and cancellation, or <see langword="null"/>.</param>
    /// <param name="output">The buffer receiving the decoded data.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The image codec still to apply, if any.</returns>
    internal static PdfImageCodec Decode(PdfStream stream, PdfOpenContext? context, ref PooledBuffer output, CancellationToken cancellationToken = default)
    {
        var dictionary = stream.Dictionary;
        using var raw = stream.LeaseRawData();
        var data = raw.Span;
        if (stream.IsEncrypted && dictionary.Owner?.Security is { } security && Decrypt(stream, data, security, context) is { } plain)
        {
            data = plain;
        }

        return Apply(data, dictionary.Get(KnownName.Filter), dictionary.Get(KnownName.DecodeParms), new(context, stream.Id.Number, cancellationToken), ref output);
    }

    /// <summary>Applies a filter chain to data.</summary>
    /// <param name="data">The encoded data.</param>
    /// <param name="filters">The /Filter value: a name, an array of names, or null.</param>
    /// <param name="parameters">The /DecodeParms value: a dictionary, an array, or null.</param>
    /// <param name="output">The buffer receiving the decoded data.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The image codec still to apply, if any.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageCodec Apply(ReadOnlySpan<byte> data, PdfValue filters, PdfValue parameters, ref PooledBuffer output, CancellationToken cancellationToken = default) =>
        Apply(data, filters, parameters, new(null, 0, cancellationToken), ref output);

    /// <summary>Applies a filter chain to data, checking for cancellation and reporting diagnostics.</summary>
    /// <param name="data">The encoded data.</param>
    /// <param name="filters">The /Filter value: a name, an array of names, or null.</param>
    /// <param name="parameters">The /DecodeParms value: a dictionary, an array, or null.</param>
    /// <param name="source">The cancellation and diagnostics context, and the stream's object number.</param>
    /// <param name="output">The buffer receiving the decoded data.</param>
    /// <returns>The image codec still to apply, if any.</returns>
    internal static PdfImageCodec Apply(ReadOnlySpan<byte> data, PdfValue filters, PdfValue parameters, in DecodeSource source, ref PooledBuffer output)
    {
        var filterArray = filters.AsArray();
        var declared = filterArray?.Count ?? (filters.Kind == PdfKind.Name ? 1 : 0);
        var count = Math.Min(declared, PdfLimits.MaxFilters);
        ReportExtraFilters(declared, count, source);
        var scratch = default(PooledBuffer);
        var maximumLength = ConfiguredOutputLength(source);
        output.MaximumLength = maximumLength;
        scratch.MaximumLength = maximumLength;
        try
        {
            var current = data;
            for (var i = 0; i < count; i++)
            {
                PdfOpenContext.ThrowIfCancelled(source.Context);
                source.CancellationToken.ThrowIfCancellationRequested();
                var name = filterArray?.GetName(i) ?? filters.AsName();
                var codec = ImageCodec(name);
                if (codec != PdfImageCodec.None)
                {
                    // Image codecs decode to pixels; hand over the bytes still encoded.
                    output.Length = 0;
                    output.Write(current);
                    return codec;
                }

                scratch.Length = 0;
                var complete = TryApplyOne(name, current, Parameters(filterArray, parameters, i), source, ref scratch);
                output.Swap(ref scratch);
                current = output.WrittenSpan;
                if (!complete)
                {
                    // The decoded size hit the cap; the output keeps what decoded and the rest of the chain is skipped.
                    return PdfImageCodec.None;
                }
            }

            if (count == 0)
            {
                output.Write(data);
            }

            return PdfImageCodec.None;
        }
        finally
        {
            scratch.Dispose();
        }
    }

    /// <summary>Gets the image codec a filter name stands for.</summary>
    /// <param name="name">The filter name.</param>
    /// <returns>The codec, or none for byte filters.</returns>
    internal static PdfImageCodec ImageCodec(PdfName name) => name.ToKnownName() switch
    {
        KnownName.DCTDecode or KnownName.DCT => PdfImageCodec.Jpeg,
        KnownName.JPXDecode => PdfImageCodec.Jpeg2000,
        KnownName.JBIG2Decode => PdfImageCodec.Jbig2,
        KnownName.CCITTFaxDecode or KnownName.CCF => PdfImageCodec.Ccitt,
        _ => PdfImageCodec.None,
    };

    /// <summary>Determines whether a filter chain names a filter this library does not know.</summary>
    /// <param name="filters">The /Filter value: a name, an array of names, or null.</param>
    /// <returns><see langword="true"/> when a filter within the limit is unknown.</returns>
    internal static bool HasUnknownFilter(PdfValue filters)
    {
        if (filters.AsArray() is not { } array)
        {
            return filters.Kind == PdfKind.Name && !IsKnownFilter(filters.AsName());
        }

        for (var i = 0; i < Math.Min(array.Count, PdfLimits.MaxFilters); i++)
        {
            if (!IsKnownFilter(array.GetName(i)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether this library applies a filter, or hands its data to an image codec.</summary>
    /// <param name="name">The filter name.</param>
    /// <returns><see langword="true"/> for a filter this library knows.</returns>
    internal static bool IsKnownFilter(PdfName name) => ByteFilters.Contains(name.ToKnownName()) || ImageCodec(name) != PdfImageCodec.None;

    /// <summary>Gets the output byte bound for this decoding operation.</summary>
    /// <param name="source">The context carrying the configured byte bound.</param>
    /// <returns>A narrower configured bound, or zero for the recoverable general-purpose limit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ConfiguredOutputLength(in DecodeSource source) =>
        source.Context is { MaxDecompressedBytes: < PdfLimits.MaxDecodedLength } context ? context.MaxDecompressedBytes : 0;

    /// <summary>Gets one filter's parameters.</summary>
    /// <param name="filterArray">The filter array, or <see langword="null"/> for a single filter.</param>
    /// <param name="parameters">The /DecodeParms value.</param>
    /// <param name="index">The filter's position.</param>
    /// <returns>The parameters, or <see langword="null"/>.</returns>
    private static PdfDictionary? Parameters(PdfArray? filterArray, PdfValue parameters, int index) =>
        filterArray is null ? parameters.AsDictionary() : parameters.AsArray()?.GetDictionary(index) ?? parameters.AsDictionary();

    /// <summary>Reports filters beyond the limit.</summary>
    /// <param name="declared">The filters the stream names.</param>
    /// <param name="applied">The filters that will run.</param>
    /// <param name="source">The diagnostics context and the stream's object number.</param>
    private static void ReportExtraFilters(int declared, int applied, in DecodeSource source)
    {
        if (declared > applied)
        {
            PdfOpenContext.Report(source.Context, PdfDiagnosticCode.RecursionLimit, "A stream names too many filters; the extra ones were ignored.", source.ObjectNumber, -1);
        }
    }

    /// <summary>Applies one byte filter, keeping partial output when the decoded size passes the cap.</summary>
    /// <param name="name">The filter name.</param>
    /// <param name="input">The encoded data.</param>
    /// <param name="parms">The filter's parameters.</param>
    /// <param name="source">The diagnostics context and the stream's object number.</param>
    /// <param name="output">The buffer receiving the decoded data.</param>
    /// <returns><see langword="false"/> when decoding stopped at the size cap.</returns>
    private static bool TryApplyOne(PdfName name, ReadOnlySpan<byte> input, PdfDictionary? parms, in DecodeSource source, ref PooledBuffer output)
    {
        try
        {
            ReportResult(ApplyOne(name, input, parms, ref output, source), source);
            return true;
        }
        catch (InvalidDataException)
        {
            PdfOpenContext.Report(source.Context, PdfDiagnosticCode.DecodeSizeCapped, "A stream decoded to more than the size cap and was cut short.", source.ObjectNumber, -1);
            return false;
        }
    }

    /// <summary>Reports an unknown filter or truncated data.</summary>
    /// <param name="result">How the filter went.</param>
    /// <param name="source">The diagnostics context and the stream's object number.</param>
    private static void ReportResult(FilterResult result, in DecodeSource source)
    {
        switch (result)
        {
            case FilterResult.Unknown:
                {
                    PdfOpenContext.Report(
                        source.Context,
                        PdfDiagnosticCode.UnknownFilter,
                        "A stream uses a filter this library does not know; its data passed through.",
                        source.ObjectNumber,
                        -1);
                    break;
                }

            case FilterResult.Damaged:
                {
                    PdfOpenContext.Report(
                        source.Context,
                        PdfDiagnosticCode.TruncatedStream,
                        "A stream's data ended early or was damaged; the part that could be read is used.",
                        source.ObjectNumber,
                        -1);
                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>Applies one byte filter. Crypt filters (decryption ran first) and unknown filters pass the data through.</summary>
    /// <param name="name">The filter name.</param>
    /// <param name="input">The encoded data.</param>
    /// <param name="parms">The filter's parameters.</param>
    /// <param name="output">The buffer receiving the decoded data.</param>
    /// <param name="source">The cancellation token and decoder owner.</param>
    /// <returns>How the filter went; <see cref="FilterResult.Unknown"/> when it is not one this library knows.</returns>
    private static FilterResult ApplyOne(PdfName name, ReadOnlySpan<byte> input, PdfDictionary? parms, ref PooledBuffer output, in DecodeSource source)
    {
        var known = name.ToKnownName();
        switch (known)
        {
            case KnownName.FlateDecode or KnownName.Fl or KnownName.LZWDecode or KnownName.LZW:
                {
                    return ApplyDictionaryCoder(known, input, parms, ref output, source);
                }

            case KnownName.ASCIIHexDecode or KnownName.AHx or KnownName.ASCII85Decode or KnownName.A85:
                {
                    return ApplyText(known, input, ref output, source.CancellationToken);
                }

            case KnownName.RunLengthDecode or KnownName.RL:
                {
                    RunLengthFilter.Decode(input, ref output, source.CancellationToken);
                    break;
                }

            case KnownName.BrotliDecode:
                {
                    BrotliFilter.Decode(input, ref output, source.CancellationToken);
                    PredictorFilter.Apply(parms, ref output, source.CancellationToken);
                    break;
                }

            default:
                {
                    output.Write(input);
                    return known == KnownName.Crypt ? FilterResult.Applied : FilterResult.Unknown;
                }
        }

        return FilterResult.Applied;
    }

    /// <summary>Applies ASCIIHexDecode or ASCII85Decode.</summary>
    /// <param name="known">The filter, which is one of the two ASCII filters in full or abbreviated form.</param>
    /// <param name="input">The encoded text.</param>
    /// <param name="output">The buffer receiving the decoded data.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns><see cref="FilterResult.Damaged"/> when the text broke the filter's rules.</returns>
    private static FilterResult ApplyText(KnownName known, ReadOnlySpan<byte> input, ref PooledBuffer output, CancellationToken cancellationToken)
    {
        var clean = known is KnownName.ASCIIHexDecode or KnownName.AHx
            ? AsciiHexFilter.TryDecode(input, ref output, cancellationToken)
            : Ascii85Filter.TryDecode(input, ref output, cancellationToken);
        return clean ? FilterResult.Applied : FilterResult.Damaged;
    }

    /// <summary>Applies Flate or LZW, then the predictor named in the parameters.</summary>
    /// <param name="known">The filter, which is Flate or LZW.</param>
    /// <param name="input">The encoded data.</param>
    /// <param name="parms">The filter's parameters.</param>
    /// <param name="output">The buffer receiving the decoded data.</param>
    /// <param name="source">The cancellation token and decoder owner.</param>
    /// <returns><see cref="FilterResult.Damaged"/> when the data was truncated or damaged.</returns>
    private static FilterResult ApplyDictionaryCoder(KnownName known, ReadOnlySpan<byte> input, PdfDictionary? parms, ref PooledBuffer output, in DecodeSource source)
    {
        var clean = known is KnownName.FlateDecode or KnownName.Fl
            ? FlateFilter.TryDecode(input, ref output, input.IsEmpty ? null : source.Context?.ScratchPools, source.CancellationToken)
            : LzwFilter.TryDecode(input, parms?.GetInt32(KnownName.EarlyChange, 1) ?? 1, ref output, source.CancellationToken);
        PredictorFilter.Apply(parms, ref output, source.CancellationToken);
        return clean ? FilterResult.Applied : FilterResult.Damaged;
    }

    /// <summary>Decrypts a stream with the crypt filter <see cref="StreamCrypt.Choose"/> picks. AES-GCM data that fails authentication decodes as empty.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="raw">The stream's encoded data.</param>
    /// <param name="security">The security handler.</param>
    /// <param name="context">The context that receives diagnostics, or <see langword="null"/>.</param>
    /// <returns>The plain bytes, or <see langword="null"/> when the stream is not encrypted.</returns>
    private static byte[]? Decrypt(PdfStream stream, ReadOnlySpan<byte> raw, PdfSecurityHandler security, PdfOpenContext? context)
    {
        var choice = StreamCrypt.Choose(stream.Dictionary, security);
        if (choice.Clear)
        {
            return null;
        }

        var decrypted = choice.Filter.IsNone
            ? security.TryDecryptStream(stream.Id, raw, out var plain)
            : security.TryDecryptStream(stream.Id, raw, choice.Filter, out plain);
        if (decrypted)
        {
            return plain;
        }

        // Data that fails authentication is never returned.
        PdfOpenContext.Report(context, PdfDiagnosticCode.AuthenticationFailed, "An encrypted stream failed authentication and was left out.", stream.Id.Number, -1);
        return [];
    }
}
