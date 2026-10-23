// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Runs bytes through an encoder and back through the library's stream decoder.</summary>
internal static class FilterRoundTrip
{
    /// <summary>Encodes bytes.</summary>
    /// <param name="encode">The encoder.</param>
    /// <param name="input">The bytes.</param>
    /// <returns>The encoded bytes.</returns>
    internal static byte[] Encode(FilterEncode encode, ReadOnlySpan<byte> input)
    {
        var output = default(PooledBuffer);
        try
        {
            encode(input, ref output);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Decodes bytes through a filter that takes no parameters.</summary>
    /// <param name="filter">The filter name.</param>
    /// <param name="encoded">The encoded bytes.</param>
    /// <returns>The decoded bytes.</returns>
    internal static byte[] Decode(KnownName filter, byte[] encoded) => Decode(PdfValue.FromName(filter), default, encoded);

    /// <summary>Decodes bytes through a filter with decode parameters.</summary>
    /// <param name="filter">The filter name.</param>
    /// <param name="parameters">The decode parameters.</param>
    /// <param name="encoded">The encoded bytes.</param>
    /// <returns>The decoded bytes.</returns>
    internal static byte[] Decode(KnownName filter, PdfDictionary parameters, byte[] encoded) =>
        Decode(PdfValue.FromName(filter), PdfValue.FromDictionary(parameters), encoded);

    /// <summary>Decodes bytes through a /Filter and /DecodeParms pair of any shape.</summary>
    /// <param name="filters">The /Filter value.</param>
    /// <param name="parameters">The /DecodeParms value, or a null value.</param>
    /// <param name="encoded">The encoded bytes.</param>
    /// <returns>The decoded bytes.</returns>
    internal static byte[] Decode(PdfValue filters, PdfValue parameters, byte[] encoded)
    {
        var output = default(PooledBuffer);
        try
        {
            _ = PdfStreamDecoder.Apply(encoded, filters, parameters, ref output);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }
}
