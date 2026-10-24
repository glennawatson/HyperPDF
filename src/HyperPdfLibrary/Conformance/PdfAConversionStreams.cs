// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Makes stream filters meet PDF/A: LZW-compressed streams are compressed again with Flate, which keeps the data exactly
/// (ISO 19005-1 §6.1.10; ISO 19005-2 and -3 §6.1.7.2; ISO 19005-4:2020 §6.1.8). JPEG 2000 in PDF/A-1, crypt filters and
/// streams whose data lives in another file stop the conversion.
/// </summary>
internal static class PdfAConversionStreams
{
    /// <summary>Checks cancellation after each 64 objects.</summary>
    private const int CancellationCheckMask = 63;

    /// <summary>Applies the fixes.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Apply(PdfAConversionSession session)
    {
        var graph = PdfObjectGraph.Collect(session.Store, false, session.CancellationToken);
        var tally = new PdfAConversionStreamTally();
        for (var number = 1; number <= graph.Count; number++)
        {
            if ((number & CancellationCheckMask) == 0)
            {
                session.CancellationToken.ThrowIfCancellationRequested();
            }

            if (graph.GetValue(number).AsStream() is { } stream)
            {
                FixStream(session, new(graph.GetOldNumber(number), 0), stream, tally);
            }
        }

        Report(session, tally);
    }

    /// <summary>Checks one stream's filters and compresses an LZW stream again.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="id">The stream object.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="tally">The counts.</param>
    private static void FixStream(PdfAConversionSession session, PdfObjectId id, PdfStream stream, PdfAConversionStreamTally tally)
    {
        var dictionary = stream.Dictionary;
        if (dictionary.ContainsKey(KnownName.F) || dictionary.ContainsKey(KnownName.FFilter) || dictionary.ContainsKey(KnownName.FDecodeParms))
        {
            tally.External++;
            return;
        }

        var filters = Filters(dictionary.Get(KnownName.Filter));
        tally.Crypt += filters.Contains(KnownName.Crypt) ? 1 : 0;
        tally.Jpx += session.Part == PdfAClaim.Part1 && filters.Contains(KnownName.JPXDecode) ? 1 : 0;
        if (!filters.Contains(KnownName.LZWDecode) && !filters.Contains(KnownName.LZW))
        {
            return;
        }

        if (Recompress(session, stream) is not { } fixedStream)
        {
            return;
        }

        StoreEditing.Replace(session.Store, id, PdfValue.FromStream(fixedStream));
        tally.Recompressed++;
    }

    /// <summary>Lists a /Filter value's filters.</summary>
    /// <param name="filter">A name or an array of names.</param>
    /// <returns>The filters.</returns>
    private static KnownName[] Filters(PdfValue filter)
    {
        if (filter.AsArray() is { } chain)
        {
            var names = new KnownName[chain.Count];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = chain.GetName(i).ToKnownName();
            }

            return names;
        }

        // Most streams have no filter or one, so the common cases allocate no list.
        return filter.Kind == PdfKind.Name ? [filter.AsName().ToKnownName()] : [];
    }

    /// <summary>Decodes the general filters of a stream and compresses the result with Flate, keeping any image codec.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="stream">The stream.</param>
    /// <returns>The new stream, or <see langword="null"/> when it could not be decoded.</returns>
    private static PdfStream? Recompress(PdfAConversionSession session, PdfStream stream)
    {
        var decoded = default(PooledBuffer);
        var compressed = default(PooledBuffer);
        try
        {
            PdfImageCodec codec;
            try
            {
                codec = stream.Decode(ref decoded, session.CancellationToken);
            }
            catch (Exception ex) when (ex is InvalidDataException or PdfException or NotSupportedException)
            {
                session.Refuse("A compressed stream could not be read, so it could not be compressed again without LZW.", PdfARule.FilterLzwForbidden, PdfARule.FilterAllowed);
                return null;
            }

            var dictionary = stream.Dictionary.Clone();
            _ = dictionary.Remove(KnownName.Length);
            _ = dictionary.Remove(KnownName.DL);
            var parms = stream.Dictionary.Get(KnownName.DecodeParms);
            _ = dictionary.Remove(KnownName.DecodeParms);
            if (codec != PdfImageCodec.None)
            {
                // The image codec's data is kept as it is, with its own parameters.
                dictionary.Set(KnownName.Filter, PdfValue.FromName(CodecFilter(codec)));
                var last = parms.AsArray() is { Count: > 0 } array ? array.GetRaw(array.Count - 1) : parms;
                if (!last.IsNull)
                {
                    dictionary.Set(KnownName.DecodeParms, last);
                }

                return new(dictionary, decoded.ToArray());
            }

            StreamRecompressor.Deflate(decoded.WrittenSpan, ref compressed);
            dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            return new(dictionary, compressed.ToArray());
        }
        finally
        {
            decoded.Dispose();
            compressed.Dispose();
        }
    }

    /// <summary>Gets the filter name of an image codec.</summary>
    /// <param name="codec">The codec.</param>
    /// <returns>The filter.</returns>
    private static KnownName CodecFilter(PdfImageCodec codec) => codec switch
    {
        PdfImageCodec.Jpeg => KnownName.DCTDecode,
        PdfImageCodec.Jpeg2000 => KnownName.JPXDecode,
        PdfImageCodec.Jbig2 => KnownName.JBIG2Decode,
        _ => KnownName.CCITTFaxDecode,
    };

    /// <summary>Records what was changed and what stops the conversion.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="tally">The counts.</param>
    private static void Report(PdfAConversionSession session, PdfAConversionStreamTally tally)
    {
        if (tally.Recompressed > 0)
        {
            session.Changed("Compressed LZW streams again with Flate; the content is unchanged.", PdfARule.FilterLzwForbidden, PdfARule.FilterAllowed);
        }

        if (tally.External > 0)
        {
            session.RefuseIfBroken("Some content is stored in another file, and PDF/A needs everything inside the document.", PdfARule.StreamExternalFile);
        }

        if (tally.Crypt > 0)
        {
            session.RefuseIfBroken("Some streams use a crypt filter, which PDF/A does not allow.", PdfARule.FilterAllowed);
        }

        if (tally.Jpx > 0)
        {
            session.RefuseIfBroken("The document has JPEG 2000 images, which PDF/A-1 does not allow. Choose PDF/A-2 or later.", PdfARule.FilterAllowed, PdfARule.FilterLzwForbidden);
        }
    }
}
