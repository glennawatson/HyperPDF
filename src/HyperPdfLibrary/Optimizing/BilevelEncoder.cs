// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.Intrinsics;
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

    /// <summary>Reverses the order of the first eight lanes and of the last eight.</summary>
    private static readonly Vector128<byte> ReverseEachEight =
        Vector128.Create((byte)0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00, 0x0F, 0x0E, 0x0D, 0x0C, 0x0B, 0x0A, 0x09, 0x08);

    /// <summary>Copies the first byte into the first eight lanes and the second into the last eight.</summary>
    private static readonly Vector128<byte> SpreadTwoBytes =
        Vector128.Create((byte)0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01);

    /// <summary>The bit of a byte that each lane of a group of eight stands for, from pixel 0 down to pixel 7.</summary>
    private static readonly Vector128<byte> EachPixelsBit =
        Vector128.Create((byte)0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01, 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01);

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
            PackRow(samples.Slice(y * width, width), target.Slice(y * stride, stride));
        }

        packed.Advance(stride * height);
    }

    /// <summary>Unpacks 1-bit rows into 8-bit samples: 0 stays 0, 1 becomes 255.</summary>
    /// <param name="rows">The packed rows, each padded to a whole byte.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="samples">Receives one byte a pixel.</param>
    internal static void Unpack(ReadOnlySpan<byte> rows, int width, int height, ref PooledBuffer samples)
    {
        var stride = (width + BitMask) >> ByteShift;
        var target = samples.GetSpan(width * height);
        for (var y = 0; y < height; y++)
        {
            UnpackRow(rows.Slice(y * stride, stride), target.Slice(y * width, width));
        }

        samples.Advance(width * height);
    }

    /// <summary>Encodes 1-bit rows as Group 4, JBIG2 or Flate, whichever is smallest, when that beats the stored size.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <param name="rows">The packed rows.</param>
    /// <param name="size">The image dimensions.</param>
    /// <param name="storedLength">The image's stored size, which the result must beat.</param>
    /// <param name="codings">The codings besides Flate that may be tried.</param>
    /// <param name="scratchPools">The document or registered service's compression owner.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The new image and the coding used, or <see langword="null"/> when nothing is smaller.</returns>
    internal static BilevelResult? Encode(
        PdfDictionary dictionary,
        ReadOnlySpan<byte> rows,
        PixelSize size,
        long storedLength,
        in BilevelCodings codings,
        ScratchPools scratchPools,
        CancellationToken cancellationToken)
    {
        var (width, height, _) = size;
        var fax = default(PooledBuffer);
        var flate = default(PooledBuffer);
        try
        {
            StreamRecompressor.Deflate(rows, ref flate, scratchPools);

            // The more common bit value is coded as white, as fax codes favour long white runs.
            var blackIs1 = CountOnes(rows) << 1 < (long)rows.Length * ByteBits;
            var faxWins = codings.UseFax && TryFax(rows, size, blackIs1, flate.Length, ref fax, scratchPools, cancellationToken);
            var best = faxWins ? fax.Length : flate.Length;
            var limit = Math.Min(best, storedLength);
            var jbig2 = codings.Jbig2 == PdfJbig2Mode.Off ? null : BilevelJbig2.Choose(rows, width, height, codings.Jbig2, limit, scratchPools, cancellationToken);
            if (jbig2 is not null)
            {
                var coding = jbig2.IsLossless ? "JBIG2" : "JBIG2 with lossy symbol merging";
                return new(WithData(Prepare(dictionary), KnownName.JBIG2Decode, jbig2.Data), coding, jbig2.IsLossless);
            }

            if (best >= storedLength)
            {
                return null;
            }

            var copy = Prepare(dictionary);
            if (!faxWins)
            {
                return new(WithData(copy, KnownName.FlateDecode, flate.WrittenSpan.ToArray()), "Flate", true);
            }

            copy.Set(KnownName.DecodeParms, PdfValue.FromDictionary(FaxParameters(dictionary.Owner, width, height, blackIs1)));
            return new(WithData(copy, KnownName.CCITTFaxDecode, fax.WrittenSpan.ToArray()), "CCITT Group 4", true);
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
    internal static PdfDictionary Prepare(PdfDictionary dictionary)
    {
        var copy = dictionary.Clone();
        _ = copy.Remove(KnownName.Length);
        _ = copy.Remove(KnownName.DL);
        _ = copy.Remove(KnownName.Filter);
        _ = copy.Remove(KnownName.DecodeParms);
        copy.Set(KnownName.BitsPerComponent, PdfValue.FromInteger(1));
        return copy;
    }

    /// <summary>Encodes Group 4 and compares it with the Flate size.</summary>
    /// <param name="rows">The packed rows.</param>
    /// <param name="size">The image dimensions.</param>
    /// <param name="blackIs1">Whether 1 bits are black.</param>
    /// <param name="flateLength">The Flate size.</param>
    /// <param name="fax">Receives the Group 4 data.</param>
    /// <param name="scratchPools">The original caller's line-buffer owner.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns><see langword="true"/> when Group 4 is no larger than Flate.</returns>
    private static bool TryFax(ReadOnlySpan<byte> rows, PixelSize size, bool blackIs1, int flateLength, ref PooledBuffer fax, ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        CcittEncoder.EncodeGroup4(rows, size.Width, size.Height, blackIs1, ref fax, scratchPools, cancellationToken);
        return fax.Length <= flateLength;
    }

    /// <summary>Sets the filter of a prepared image dictionary and pairs it with its data.</summary>
    /// <param name="copy">The prepared dictionary.</param>
    /// <param name="filter">The filter name.</param>
    /// <param name="data">The encoded data.</param>
    /// <returns>The new image.</returns>
    private static PdfStream WithData(PdfDictionary copy, KnownName filter, byte[] data)
    {
        copy.Set(KnownName.Filter, PdfValue.FromName(filter));
        return new(copy, data);
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

    /// <summary>Packs one row. A sample is white when its top bit is set, so the top bits of 16 samples are the mask of 16 pixels once the first eight are put in reverse order.</summary>
    /// <param name="row">The samples of the row.</param>
    /// <param name="line">The cleared bytes of the packed row.</param>
    private static void PackRow(ReadOnlySpan<byte> row, Span<byte> line)
    {
        var x = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            for (; x + Vector128<byte>.Count <= row.Length; x += Vector128<byte>.Count)
            {
                // Pixel 0 is the top bit of its byte, so each group of eight lanes is reversed before the top bits are gathered.
                var reversed = Vector128.Shuffle(Vector128.Create(row.Slice(x, Vector128<byte>.Count)), ReverseEachEight);
                BinaryPrimitives.WriteUInt16LittleEndian(line[(x >> ByteShift)..], (ushort)reversed.ExtractMostSignificantBits());
            }
        }

        for (; x < row.Length; x++)
        {
            if (row[x] >= WhiteFrom)
            {
                line[x >> ByteShift] |= (byte)(1 << (BitMask - (x & BitMask)));
            }
        }
    }

    /// <summary>Unpacks one row, 16 pixels from each pair of bytes: each byte is spread over eight lanes and each lane keeps only its own bit.</summary>
    /// <param name="line">The packed row.</param>
    /// <param name="row">Receives one byte a pixel.</param>
    private static void UnpackRow(ReadOnlySpan<byte> line, Span<byte> row)
    {
        var x = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            for (; x + Vector128<byte>.Count <= row.Length; x += Vector128<byte>.Count)
            {
                var pair = Vector128.CreateScalar(BinaryPrimitives.ReadUInt16LittleEndian(line[(x >> ByteShift)..])).AsByte();
                var bits = Vector128.Shuffle(pair, SpreadTwoBytes) & EachPixelsBit;
                Vector128.Equals(bits, EachPixelsBit).CopyTo(row[x..]);
            }
        }

        for (; x < row.Length; x++)
        {
            row[x] = (line[x >> ByteShift] & (1 << (BitMask - (x & BitMask)))) != 0 ? White : (byte)0;
        }
    }
}
