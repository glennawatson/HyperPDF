// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>Encodes stream data with the filter or filter chain a <see cref="PdfStreamEncoding"/> names, and names those filters in a stream dictionary.</summary>
internal static class StreamEncoder
{
    /// <summary>The /EarlyChange value LZW streams use when the entry is absent.</summary>
    private const int DefaultEarlyChange = 1;

    /// <summary>Gets the encoding a file may use: LZW becomes Flate when the file claims PDF/A (parts 1 to 3 forbid LZW) or PDF/UA.</summary>
    /// <param name="requested">The encoding asked for.</param>
    /// <param name="claimsStandard">Whether the file claims PDF/A or PDF/UA.</param>
    /// <returns>The encoding to write.</returns>
    internal static PdfStreamEncoding ForClaim(PdfStreamEncoding requested, bool claimsStandard) => requested switch
    {
        PdfStreamEncoding.Lzw when claimsStandard => PdfStreamEncoding.Flate,
        PdfStreamEncoding.LzwAscii85 when claimsStandard => PdfStreamEncoding.FlateAscii85,
        _ => requested,
    };

    /// <summary>Determines whether an encoding uses LZW, which a file that claims PDF/A or PDF/UA may not.</summary>
    /// <param name="encoding">The encoding.</param>
    /// <returns><see langword="true"/> for the LZW encodings.</returns>
    internal static bool UsesLzw(PdfStreamEncoding encoding) => encoding is PdfStreamEncoding.Lzw or PdfStreamEncoding.LzwAscii85;

    /// <summary>Determines whether an encoding ends in ASCII text, which is bigger than its input, so a size check cannot be used to refuse it.</summary>
    /// <param name="encoding">The encoding.</param>
    /// <returns><see langword="true"/> for the encodings that end in base-85 or hexadecimal text.</returns>
    internal static bool IsTextArmoured(PdfStreamEncoding encoding) =>
        encoding is PdfStreamEncoding.FlateAscii85 or PdfStreamEncoding.FlateAsciiHex or PdfStreamEncoding.LzwAscii85;

    /// <summary>Encodes data.</summary>
    /// <param name="encoding">The encoding.</param>
    /// <param name="data">The data.</param>
    /// <param name="level">The Flate level, used by the Flate encodings.</param>
    /// <param name="output">Receives the encoded data.</param>
    internal static void Encode(PdfStreamEncoding encoding, ReadOnlySpan<byte> data, PdfCompressionLevel level, ref PooledBuffer output)
    {
        switch (encoding)
        {
            case PdfStreamEncoding.Lzw:
            {
                LzwEncoder.Encode(data, DefaultEarlyChange, ref output);
                break;
            }

            case PdfStreamEncoding.RunLength:
            {
                RunLengthFilter.Encode(data, ref output);
                break;
            }

            case PdfStreamEncoding.FlateAscii85 or PdfStreamEncoding.FlateAsciiHex or PdfStreamEncoding.LzwAscii85:
            {
                EncodeArmoured(encoding, data, level, ref output);
                break;
            }

            default:
            {
                FlateFilter.Encode(data, level, ref output);
                break;
            }
        }
    }

    /// <summary>Sets a stream dictionary's /Filter to the chain an encoding writes and removes /DecodeParms, which none of these filters need.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="encoding">The encoding.</param>
    internal static void SetFilters(PdfDictionary dictionary, PdfStreamEncoding encoding)
    {
        _ = dictionary.Remove(KnownName.DecodeParms);
        switch (encoding)
        {
            case PdfStreamEncoding.Lzw:
            {
                dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.LZWDecode));
                break;
            }

            case PdfStreamEncoding.RunLength:
            {
                dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.RunLengthDecode));
                break;
            }

            case PdfStreamEncoding.FlateAscii85:
            {
                dictionary.Set(KnownName.Filter, Chain(dictionary, KnownName.ASCII85Decode, KnownName.FlateDecode));
                break;
            }

            case PdfStreamEncoding.FlateAsciiHex:
            {
                dictionary.Set(KnownName.Filter, Chain(dictionary, KnownName.ASCIIHexDecode, KnownName.FlateDecode));
                break;
            }

            case PdfStreamEncoding.LzwAscii85:
            {
                dictionary.Set(KnownName.Filter, Chain(dictionary, KnownName.ASCII85Decode, KnownName.LZWDecode));
                break;
            }

            default:
            {
                dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
                break;
            }
        }
    }

    /// <summary>Compresses data and then writes the result as ASCII text.</summary>
    /// <param name="encoding">The encoding, which is one of the three that end in text.</param>
    /// <param name="data">The data.</param>
    /// <param name="level">The Flate level.</param>
    /// <param name="output">Receives the text.</param>
    private static void EncodeArmoured(PdfStreamEncoding encoding, ReadOnlySpan<byte> data, PdfCompressionLevel level, ref PooledBuffer output)
    {
        var compressed = default(PooledBuffer);
        try
        {
            if (encoding == PdfStreamEncoding.LzwAscii85)
            {
                LzwEncoder.Encode(data, DefaultEarlyChange, ref compressed);
            }
            else
            {
                FlateFilter.Encode(data, level, ref compressed);
            }

            if (encoding == PdfStreamEncoding.FlateAsciiHex)
            {
                AsciiHexFilter.Encode(compressed.WrittenSpan, ref output);
            }
            else
            {
                Ascii85Filter.Encode(compressed.WrittenSpan, ref output);
            }
        }
        finally
        {
            compressed.Dispose();
        }
    }

    /// <summary>Builds a /Filter array, which lists the filters in the order a reader applies them.</summary>
    /// <param name="dictionary">The stream dictionary, whose store owns the array.</param>
    /// <param name="first">The filter a reader applies first.</param>
    /// <param name="second">The filter a reader applies second.</param>
    /// <returns>The array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfValue Chain(PdfDictionary dictionary, KnownName first, KnownName second) =>
        PdfValue.FromArray(new(dictionary.Owner, [PdfValue.FromName(first), PdfValue.FromName(second)]));
}
