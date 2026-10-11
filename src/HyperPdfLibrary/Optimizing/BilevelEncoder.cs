// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Re-encodes 1-bit images losslessly: as CCITT Group 4 or Flate, whichever is smaller. The bits are kept exactly, so
/// /Decode, /ImageMask and colour key semantics are unchanged. Greyscale images that hold only black and white become
/// 1-bit first, which keeps their look as 0 maps to 0 and 255 to 1 under the same /Decode.
/// </summary>
internal static class BilevelEncoder
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The mask of a pixel's bit index within its byte.</summary>
    private const int BitMask = 7;

    /// <summary>The shift from a pixel index to its byte.</summary>
    private const int ByteShift = 3;

    /// <summary>The sample value from which a pixel is white.</summary>
    private const byte WhiteFrom = 128;

    /// <summary>The value of a white 8-bit sample.</summary>
    private const byte White = 0xFF;

    /// <summary>The /K value of Group 4 coding.</summary>
    private const int Group4 = -1;

    /// <summary>The entries of the CCITT parameters.</summary>
    private const int ParameterCount = 4;

    /// <summary>Determines whether 8-bit samples are all 0 or 255.</summary>
    /// <param name="samples">The samples.</param>
    /// <returns><see langword="true"/> when the image is black and white only.</returns>
    internal static bool IsBlackAndWhite(ReadOnlySpan<byte> samples) => samples.IndexOfAnyExcept((byte)0, White) < 0;

    /// <summary>Packs 8-bit samples into 1-bit rows: 0 stays 0, 255 becomes 1.</summary>
    /// <param name="samples">The samples, one byte a pixel.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="packed">Receives the packed rows.</param>
    internal static void Pack(ReadOnlySpan<byte> samples, int width, int height, ref PooledBuffer packed)
    {
        var stride = (width + BitMask) >> ByteShift;
        var target = packed.GetSpan(stride * height);
        target[..(stride * height)].Clear();
        for (var y = 0; y < height; y++)
        {
            var row = samples.Slice(y * width, width);
            var line = target.Slice(y * stride, stride);
            for (var x = 0; x < width; x++)
            {
                if (row[x] >= WhiteFrom)
                {
                    line[x >> ByteShift] |= (byte)(1 << (BitMask - (x & BitMask)));
                }
            }
        }

        packed.Advance(stride * height);
    }

    /// <summary>Encodes 1-bit rows as Group 4 or Flate, whichever is smaller, when that beats the stored size.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <param name="rows">The packed rows.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="storedLength">The image's stored size, which the result must beat.</param>
    /// <returns>The new image, or <see langword="null"/> when nothing is smaller.</returns>
    internal static PdfStream? Encode(PdfDictionary dictionary, ReadOnlySpan<byte> rows, int width, int height, long storedLength)
    {
        var fax = default(PooledBuffer);
        var flate = default(PooledBuffer);
        try
        {
            // The more common bit value is coded as white, as fax codes favour long white runs.
            var blackIs1 = CountOnes(rows) << 1 < (long)rows.Length * ByteBits;
            CcittG4Encoder.Encode(rows, width, height, blackIs1, ref fax, dictionary.Owner?.ScratchPools ?? new ScratchPools());
            StreamRecompressor.Deflate(rows, ref flate);
            var useFax = fax.Length <= flate.Length;
            var best = useFax ? fax.WrittenSpan : flate.WrittenSpan;
            if (best.Length >= storedLength)
            {
                return null;
            }

            var copy = Prepare(dictionary);
            if (useFax)
            {
                copy.Set(KnownName.Filter, PdfValue.FromName(KnownName.CCITTFaxDecode));
                copy.Set(KnownName.DecodeParms, PdfValue.FromDictionary(FaxParameters(dictionary.Owner, width, height, blackIs1)));
            }
            else
            {
                copy.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            }

            return new(copy, best.ToArray());
        }
        finally
        {
            fax.Dispose();
            flate.Dispose();
        }
    }

    /// <summary>Copies an image dictionary without its filter, parameters and lengths, as a 1-bit image.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <returns>The copy.</returns>
    private static PdfDictionary Prepare(PdfDictionary dictionary)
    {
        var copy = dictionary.Clone();
        _ = copy.Remove(KnownName.Length);
        _ = copy.Remove(KnownName.DL);
        _ = copy.Remove(KnownName.Filter);
        _ = copy.Remove(KnownName.DecodeParms);
        copy.Set(KnownName.BitsPerComponent, PdfValue.FromInteger(1));
        return copy;
    }

    /// <summary>Builds the Group 4 parameters.</summary>
    /// <param name="owner">The store the dictionary belongs to.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="blackIs1">Whether 1 bits are black.</param>
    /// <returns>The parameters.</returns>
    private static PdfDictionary FaxParameters(PdfObjectStore? owner, int width, int height, bool blackIs1)
    {
        var parameters = new PdfDictionary(owner, ParameterCount);
        parameters.Set(KnownName.K, PdfValue.FromInteger(Group4));
        parameters.Set(KnownName.Columns, PdfValue.FromInteger(width));
        parameters.Set(KnownName.Rows, PdfValue.FromInteger(height));
        if (blackIs1)
        {
            parameters.Set(KnownName.BlackIs1, PdfValue.FromBoolean(true));
        }

        return parameters;
    }

    /// <summary>Counts the set bits.</summary>
    /// <param name="rows">The bytes.</param>
    /// <returns>The count.</returns>
    private static long CountOnes(ReadOnlySpan<byte> rows)
    {
        long count = 0;
        var words = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, ulong>(rows);
        foreach (var word in words)
        {
            count += BitOperations.PopCount(word);
        }

        for (var i = words.Length * sizeof(ulong); i < rows.Length; i++)
        {
            count += BitOperations.PopCount(rows[i]);
        }

        return count;
    }
}
