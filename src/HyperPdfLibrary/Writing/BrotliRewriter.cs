// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Rewrites streams that use the BrotliDecode filter, which is not part of ISO 32000-2, so the library never writes it.
/// The byte filters are decoded and the result is Flate-compressed; an image codec later in the chain (for example
/// DCTDecode) is kept with its parameters, holding the bytes it decodes.
/// </summary>
internal static class BrotliRewriter
{
    /// <summary>Determines whether a stream's filter chain uses BrotliDecode.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool UsesBrotli(PdfDictionary dictionary)
    {
        var filters = dictionary.Get(KnownName.Filter);
        if (filters.AsArray() is not { } array)
        {
            return filters.AsName().Is(KnownName.BrotliDecode);
        }

        for (var i = 0; i < array.Count; i++)
        {
            if (array.GetName(i).Is(KnownName.BrotliDecode))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Decodes a Brotli stream's byte filters and builds the conforming dictionary and data to write instead.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="plain">The stream's encoded, unencrypted data.</param>
    /// <param name="output">Receives the data to write.</param>
    /// <returns>The dictionary to write.</returns>
    internal static PdfDictionary Rewrite(PdfDictionary dictionary, ReadOnlySpan<byte> plain, ref PooledBuffer output)
    {
        var filters = dictionary.Get(KnownName.Filter);
        var parameters = dictionary.Get(KnownName.DecodeParms);
        var decoded = default(PooledBuffer);
        try
        {
            var codec = PdfStreamDecoder.Apply(plain, filters, parameters, ref decoded, CancellationToken.None);
            var copy = dictionary.Clone();
            _ = copy.Remove(KnownName.Length);
            if (codec == PdfImageCodec.None)
            {
                ZLibCodec.Compress(decoded.WrittenSpan, ref output);
                copy.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
                _ = copy.Remove(KnownName.DecodeParms);
                return copy;
            }

            output.Write(decoded.WrittenSpan);
            KeepFrom(copy, filters, parameters, FirstImageCodec(filters));
            return copy;
        }
        finally
        {
            decoded.Dispose();
        }
    }

    /// <summary>Finds the first image codec in a filter chain.</summary>
    /// <param name="filters">The /Filter value.</param>
    /// <returns>Its index.</returns>
    private static int FirstImageCodec(PdfValue filters)
    {
        if (filters.AsArray() is not { } array)
        {
            return 0;
        }

        for (var i = 0; i < array.Count; i++)
        {
            if (PdfStreamDecoder.ImageCodec(array.GetName(i)) != PdfImageCodec.None)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>Keeps the filters and parameters from an index onwards.</summary>
    /// <param name="copy">The dictionary to update.</param>
    /// <param name="filters">The original /Filter value.</param>
    /// <param name="parameters">The original /DecodeParms value.</param>
    /// <param name="start">The first filter to keep.</param>
    private static void KeepFrom(PdfDictionary copy, PdfValue filters, PdfValue parameters, int start)
    {
        if (filters.AsArray() is not { } names)
        {
            return;
        }

        var keptNames = new PdfArray(copy.Owner, names.Count - start);
        for (var i = start; i < names.Count; i++)
        {
            keptNames.Add(PdfValue.FromName(names.GetName(i)));
        }

        copy.Set(KnownName.Filter, keptNames.Count == 1 ? keptNames.Get(0) : PdfValue.FromArray(keptNames));
        if (parameters.AsArray() is not { } parms)
        {
            // A single parameter dictionary belongs to a single filter, which is the codec kept here.
            return;
        }

        var keptParms = new PdfArray(copy.Owner, names.Count - start);
        for (var i = start; i < names.Count; i++)
        {
            keptParms.Add(i < parms.Count ? parms.Get(i) : PdfValue.Null);
        }

        copy.Set(KnownName.DecodeParms, keptParms.Count == 1 ? keptParms.Get(0) : PdfValue.FromArray(keptParms));
    }
}
