// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Writes the stream objects of a whole-file save under a <see cref="StreamPolicy"/> (ISO 32000-2 §7.3.8, §7.4.4). A stream
/// is changed only when every filter it has is a byte filter this library decodes, so image codecs, crypt filters and
/// external-file streams are always copied as they are.
/// </summary>
internal static class PdfStreamWriting
{
    /// <summary>Gets the line end and keyword that close an indirect object.</summary>
    private static ReadOnlySpan<byte> EndObject => "\nendobj\n"u8;

    /// <summary>Writes one stream object under its new number.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The stream value.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="id">The stream's new object id.</param>
    /// <param name="encrypt">Whether the stream is encrypted for its new number.</param>
    /// <param name="policy">What to do with the stream's data.</param>
    /// <remarks>
    /// Metadata streams are written without byte filters whatever the mode, as XMP readers expect them plain and PDF/A-1
    /// forbids a filtered metadata stream (ISO 19005-1 §6.7.2, Cor.2).
    /// </remarks>
    internal static void Write(ref PdfObjectWriter writer, PdfValue value, PdfStream stream, PdfObjectId id, bool encrypt, StreamPolicy policy)
    {
        var written = stream.Dictionary.IsName(KnownName.Type, KnownName.Metadata)
            ? !IsUnfiltered(stream.Dictionary) && TryWritePlain(ref writer, stream, id, encrypt)
            : TryWrite(ref writer, stream, id, encrypt, policy);
        if (!written)
        {
            writer.WriteIndirectObject(id, value, encrypt);
        }
    }

    /// <summary>Writes an unfiltered stream's data as it is or Flate-compressed, to the rule of the mode.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="id">The stream's new object id.</param>
    /// <param name="encrypt">Whether the stream is encrypted for its new number.</param>
    /// <param name="policy">What to do with the stream's data.</param>
    /// <returns><see langword="true"/> when the stream was written; <see langword="false"/> when the caller copies it.</returns>
    private static bool TryWrite(ref PdfObjectWriter writer, PdfStream stream, PdfObjectId id, bool encrypt, StreamPolicy policy)
    {
        var unfiltered = IsUnfiltered(stream.Dictionary);
        switch (policy.Mode)
        {
            case PdfStreamMode.Preserve:
                {
                    // A damaged Flate or LZW stream is still written again from the part that decoded, so the saved file needs no repair.
                    return !unfiltered && TryWriteRepaired(ref writer, stream, id, encrypt, policy.Level);
                }

            case PdfStreamMode.Uncompressed:
                {
                    return !unfiltered && TryWritePlain(ref writer, stream, id, encrypt);
                }

            case PdfStreamMode.Recompress:
                {
                    return TryRecompress(ref writer, stream, id, encrypt, policy.Level);
                }

            default:
                {
                    if (!unfiltered)
                    {
                        return TryWriteRepaired(ref writer, stream, id, encrypt, policy.Level);
                    }

                    writer.WriteObjectHeader(id);
                    WriteCompressed(ref writer, stream, encrypt ? id : default, policy.Level);
                    writer.WriteRaw(EndObject);
                    return true;
                }
        }
    }

    /// <summary>Writes a stream filtered by byte filters again as clean Flate when its data was truncated or damaged.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="id">The stream's new object id.</param>
    /// <param name="encrypt">Whether the stream is encrypted for its new number.</param>
    /// <param name="level">The Flate level.</param>
    /// <returns><see langword="true"/> when the stream was damaged and has been written; <see langword="false"/> when nothing was written.</returns>
    private static bool TryWriteRepaired(ref PdfObjectWriter writer, PdfStream stream, PdfObjectId id, bool encrypt, PdfCompressionLevel level)
    {
        if (!IsByteFilterChain(stream.Dictionary))
        {
            return false;
        }

        var decoded = default(PooledBuffer);
        var compressed = default(PooledBuffer);
        try
        {
            var probe = new PdfOpenContext(null, null, CancellationToken.None);
            _ = PdfStreamDecoder.Decode(stream, probe, ref decoded);
            if (!probe.HasRepairs)
            {
                return false;
            }

            WriteFlate(ref writer, stream, id, encrypt, decoded.WrittenSpan, level, ref compressed);
            return true;
        }
        finally
        {
            compressed.Dispose();
            decoded.Dispose();
        }
    }

    /// <summary>
    /// Writes a stream that has no filter, or only byte filters, as Flate at a level, when that is smaller than the data it
    /// has. A stream with an image codec, a crypt filter or external data is left to be copied.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="id">The stream's new object id.</param>
    /// <param name="encrypt">Whether the stream is encrypted for its new number.</param>
    /// <param name="level">The Flate level.</param>
    /// <returns><see langword="true"/> when the stream was written; <see langword="false"/> when the caller copies it.</returns>
    private static bool TryRecompress(ref PdfObjectWriter writer, PdfStream stream, PdfObjectId id, bool encrypt, PdfCompressionLevel level)
    {
        var unfiltered = IsUnfiltered(stream.Dictionary);
        if (stream.Dictionary.ContainsKey(KnownName.F) || (!unfiltered && !IsByteFilterChain(stream.Dictionary)))
        {
            return false;
        }

        using var raw = stream.LeaseRawData();
        var original = PdfObjectWriter.PlainData(stream, raw.Span);
        var decoded = default(PooledBuffer);
        var compressed = default(PooledBuffer);
        try
        {
            var probe = new PdfOpenContext(null, null, CancellationToken.None);
            var data = original;
            if (!unfiltered)
            {
                _ = PdfStreamDecoder.Decode(stream, probe, ref decoded);
                data = decoded.WrittenSpan;
            }

            FlateFilter.Encode(data, level, ref compressed);
            if (compressed.Length >= original.Length && !probe.HasRepairs)
            {
                return false;
            }

            WriteFlate(ref writer, stream, id, encrypt, data, level, ref compressed);
            return true;
        }
        finally
        {
            compressed.Dispose();
            decoded.Dispose();
        }
    }

    /// <summary>Writes a stream's decoded data as Flate, or plainly when Flate is no smaller, dropping the old filter and its parameters.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="id">The stream's new object id.</param>
    /// <param name="encrypt">Whether the stream is encrypted for its new number.</param>
    /// <param name="data">The decoded data.</param>
    /// <param name="level">The Flate level.</param>
    /// <param name="compressed">Receives the compressed data, if it is not already there.</param>
    private static void WriteFlate(ref PdfObjectWriter writer, PdfStream stream, PdfObjectId id, bool encrypt, ReadOnlySpan<byte> data, PdfCompressionLevel level, ref PooledBuffer compressed)
    {
        if (compressed.Length == 0)
        {
            FlateFilter.Encode(data, level, ref compressed);
        }

        var dictionary = WithoutFilters(stream.Dictionary);
        var smaller = compressed.Length < data.Length;
        writer.WriteObjectHeader(id);
        writer.WriteStream(dictionary, smaller ? compressed.WrittenSpan : data, encrypt ? id : default, smaller);
        writer.WriteRaw(EndObject);
    }

    /// <summary>Writes a stream filtered by byte filters as its decoded data, with no filter.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="id">The stream's new object id.</param>
    /// <param name="encrypt">Whether the stream is encrypted for its new number.</param>
    /// <returns><see langword="true"/> when the stream was written; <see langword="false"/> when a filter is not a byte filter and nothing was written.</returns>
    private static bool TryWritePlain(ref PdfObjectWriter writer, PdfStream stream, PdfObjectId id, bool encrypt)
    {
        if (stream.Dictionary.ContainsKey(KnownName.F) || !IsByteFilterChain(stream.Dictionary))
        {
            return false;
        }

        var decoded = default(PooledBuffer);
        try
        {
            var context = new PdfOpenContext(null, null, CancellationToken.None);
            _ = PdfStreamDecoder.Decode(stream, context, ref decoded);
            writer.WriteObjectHeader(id);
            writer.WriteStream(WithoutFilters(stream.Dictionary), decoded.WrittenSpan, encrypt ? id : default, false);
            writer.WriteRaw(EndObject);
            return true;
        }
        finally
        {
            decoded.Dispose();
        }
    }

    /// <summary>Copies a stream dictionary without its filters, their parameters and the decoded-length hint.</summary>
    /// <param name="source">The stream dictionary.</param>
    /// <returns>The copy.</returns>
    private static PdfDictionary WithoutFilters(PdfDictionary source)
    {
        var dictionary = source.Clone();
        _ = dictionary.Remove(KnownName.Filter);
        _ = dictionary.Remove(KnownName.DecodeParms);
        _ = dictionary.Remove(KnownName.DL);
        return dictionary;
    }

    /// <summary>Determines whether every filter of a stream is a byte filter that decodes to the stream's data (no image codec, no crypt).</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <returns><see langword="true"/> when the filters are Flate, LZW, ASCIIHex, ASCII85 or RunLength only.</returns>
    private static bool IsByteFilterChain(PdfDictionary dictionary)
    {
        var filter = dictionary.Get(KnownName.Filter);
        if (filter.AsArray() is not { } array)
        {
            return IsByteFilter(filter.AsName());
        }

        for (var i = 0; i < array.Count; i++)
        {
            if (!IsByteFilter(array.GetName(i)))
            {
                return false;
            }
        }

        return array.Count > 0;
    }

    /// <summary>Determines whether a filter name is a byte filter.</summary>
    /// <param name="name">The filter name.</param>
    /// <returns><see langword="true"/> for Flate, LZW, ASCIIHex, ASCII85 and RunLength.</returns>
    private static bool IsByteFilter(PdfName name) => name.ToKnownName() is KnownName.FlateDecode or KnownName.Fl or KnownName.LZWDecode or KnownName.LZW
        or KnownName.ASCIIHexDecode or KnownName.AHx or KnownName.ASCII85Decode or KnownName.A85 or KnownName.RunLengthDecode or KnownName.RL;

    /// <summary>Determines whether a stream has no filter, so compressing it is lossless and safe.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <returns><see langword="true"/> when it has neither /Filter nor /DecodeParms.</returns>
    private static bool IsUnfiltered(PdfDictionary dictionary) =>
        dictionary.GetRaw(KnownName.Filter).IsNull && dictionary.GetRaw(KnownName.DecodeParms).IsNull;

    /// <summary>Writes an unfiltered stream, Flate-compressed when that is smaller.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="encryptionId">The id to encrypt for, or an invalid id.</param>
    /// <param name="level">The Flate level.</param>
    private static void WriteCompressed(ref PdfObjectWriter writer, PdfStream stream, PdfObjectId encryptionId, PdfCompressionLevel level)
    {
        using var raw = stream.LeaseRawData();
        var data = PdfObjectWriter.PlainData(stream, raw.Span);
        var compressed = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(data, level, ref compressed);
            var smaller = compressed.Length < data.Length;
            writer.WriteStream(stream.Dictionary, smaller ? compressed.WrittenSpan : data, encryptionId, smaller);
        }
        finally
        {
            compressed.Dispose();
        }
    }
}
