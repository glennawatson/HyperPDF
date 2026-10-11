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
        if (stream.IsEncrypted && dictionary.Owner?.Security is { } security && Decrypt(stream, data, security) is { } plain)
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
            ReportResult(ApplyOne(name, input, parms, ref output, source.CancellationToken), source);
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
                PdfOpenContext.Report(source.Context, PdfDiagnosticCode.UnknownFilter, "A stream uses a filter this library does not know; its data passed through.", source.ObjectNumber, -1);
                break;
            }

            case FilterResult.Damaged:
            {
                PdfOpenContext.Report(source.Context, PdfDiagnosticCode.TruncatedStream, "A stream's data ended early or was damaged; the part that could be read is used.", source.ObjectNumber, -1);
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
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>How the filter went; <see cref="FilterResult.Unknown"/> when it is not one this library knows.</returns>
    private static FilterResult ApplyOne(PdfName name, ReadOnlySpan<byte> input, PdfDictionary? parms, ref PooledBuffer output, CancellationToken cancellationToken)
    {
        var known = name.ToKnownName();
        switch (known)
        {
            case KnownName.FlateDecode or KnownName.Fl or KnownName.LZWDecode or KnownName.LZW:
            {
                return ApplyDictionaryCoder(known, input, parms, ref output, cancellationToken);
            }

            case KnownName.ASCIIHexDecode or KnownName.AHx:
            {
                AsciiHexFilter.Decode(input, ref output);
                break;
            }

            case KnownName.ASCII85Decode or KnownName.A85:
            {
                Ascii85Filter.Decode(input, ref output);
                break;
            }

            case KnownName.RunLengthDecode or KnownName.RL:
            {
                RunLengthFilter.Decode(input, ref output);
                break;
            }

            case KnownName.BrotliDecode:
            {
                BrotliFilter.Decode(input, ref output);
                PredictorFilter.Apply(parms, ref output);
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

    /// <summary>Applies Flate or LZW, then the predictor named in the parameters.</summary>
    /// <param name="known">The filter, which is Flate or LZW.</param>
    /// <param name="input">The encoded data.</param>
    /// <param name="parms">The filter's parameters.</param>
    /// <param name="output">The buffer receiving the decoded data.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns><see cref="FilterResult.Damaged"/> when the data was truncated or damaged.</returns>
    private static FilterResult ApplyDictionaryCoder(KnownName known, ReadOnlySpan<byte> input, PdfDictionary? parms, ref PooledBuffer output, CancellationToken cancellationToken)
    {
        var clean = known is KnownName.FlateDecode or KnownName.Fl
            ? FlateFilter.TryDecode(input, ref output, cancellationToken)
            : LzwFilter.TryDecode(input, parms?.GetInt32(KnownName.EarlyChange, 1) ?? 1, ref output, cancellationToken);
        PredictorFilter.Apply(parms, ref output);
        return clean ? FilterResult.Applied : FilterResult.Damaged;
    }

    /// <summary>
    /// Decrypts a stream. Cross-reference streams, Identity crypt filters and (when metadata is not encrypted) metadata
    /// streams are exempt; a /Crypt filter with a /Name uses that crypt filter's method.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="raw">The stream's encoded data.</param>
    /// <param name="security">The security handler.</param>
    /// <returns>The plain bytes, or <see langword="null"/> when the stream is not encrypted.</returns>
    private static byte[]? Decrypt(PdfStream stream, ReadOnlySpan<byte> raw, PdfSecurityHandler security)
    {
        var dictionary = stream.Dictionary;
        var filters = dictionary.Get(KnownName.Filter);
        var first = filters.AsArray()?.GetName(0) ?? filters.AsName();
        if (first.Is(KnownName.Crypt))
        {
            return DecryptNamed(stream, raw, security);
        }

        return IsExempt(dictionary, security) ? null : security.DecryptStream(stream.Id, raw);
    }

    /// <summary>Decrypts a stream whose first filter is /Crypt, with the crypt filter named in its parameters.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="raw">The stream's encoded data.</param>
    /// <param name="security">The security handler.</param>
    /// <returns>The plain bytes, or <see langword="null"/> for the Identity filter.</returns>
    private static byte[]? DecryptNamed(PdfStream stream, ReadOnlySpan<byte> raw, PdfSecurityHandler security)
    {
        var parms = stream.Dictionary.Get(KnownName.DecodeParms);
        var crypt = parms.AsArray()?.GetDictionary(0) ?? parms.AsDictionary();
        var name = crypt?.GetName(KnownName.Name) ?? default;
        return name.IsNone || name.Is(KnownName.Identity) ? null : security.DecryptStream(stream.Id, raw, name);
    }

    /// <summary>Determines whether a stream without a crypt filter is stored in the clear.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="security">The security handler.</param>
    /// <returns><see langword="true"/> for cross-reference streams, and metadata streams when metadata is not encrypted.</returns>
    private static bool IsExempt(PdfDictionary dictionary, PdfSecurityHandler security) =>
        dictionary.IsName(KnownName.Type, KnownName.XRef)
        || (!security.EncryptMetadata && dictionary.IsName(KnownName.Type, KnownName.Metadata));
}
