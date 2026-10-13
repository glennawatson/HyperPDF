// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Creation;

/// <summary>Builds the streams that created content stores.</summary>
internal static class CreatedStreams
{
    /// <summary>Makes a stream from data, Flate-compressed when that is smaller.</summary>
    /// <param name="dictionary">The stream dictionary; <c>/Filter</c> is set when the data is compressed.</param>
    /// <param name="data">The data.</param>
    /// <returns>The stream.</returns>
    internal static PdfStream CompressIfSmaller(PdfDictionary dictionary, ReadOnlySpan<byte> data)
    {
        var compressed = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(data, ref compressed);
            if (compressed.Length >= data.Length)
            {
                return new(dictionary, data.ToArray());
            }

            dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            return new(dictionary, compressed.ToArray());
        }
        finally
        {
            compressed.Dispose();
        }
    }
}
