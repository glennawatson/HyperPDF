// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Reads the strips of a TIFF image: undoes their compression and predictor and unpacks their samples.</summary>
internal static class TiffStrips
{
    /// <summary>The tag for the offsets of the strips.</summary>
    private const int TagStripOffsets = 273;

    /// <summary>The tag for the sizes of the strips.</summary>
    private const int TagStripByteCounts = 279;

    /// <summary>The most strips an image may have.</summary>
    private const int MaxStrips = 1 << 20;

    /// <summary>The photometric value where zero is white.</summary>
    private const int WhiteIsZero = 0;

    /// <summary>The compression number for no compression.</summary>
    private const int NoCompression = 1;

    /// <summary>The compression number for LZW.</summary>
    private const int Lzw = 5;

    /// <summary>The compression number for zlib.</summary>
    private const int Deflate = 8;

    /// <summary>The older compression number for zlib.</summary>
    private const int AdobeDeflate = 32_946;

    /// <summary>The compression number for PackBits run-length coding.</summary>
    private const int PackBits = 32_773;

    /// <summary>The predictor number for horizontal differencing.</summary>
    private const int HorizontalDifferencing = 2;

    /// <summary>The Group 3 option bit for two-dimensional coding.</summary>
    private const int TwoDimensional = 1;

    /// <summary>The Group 3 option bit for fill bits before end-of-line codes.</summary>
    private const int FillBits = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits in a 16-bit sample.</summary>
    private const int WideBits = 16;

    /// <summary>The value of <c>K</c> for Group 4 coding.</summary>
    private const int GroupFourK = -1;

    /// <summary>The value of <c>K</c> for a Group 3 mixed coding; any positive value means mixed.</summary>
    private const int MixedK = 4;

    /// <summary>Reads the samples of every strip.</summary>
    /// <param name="directory">The directory.</param>
    /// <param name="info">What the tags say.</param>
    /// <param name="cancellationToken">Cancels between strips and rows.</param>
    /// <returns>The samples, pixel by pixel, in the picture's size.</returns>
    /// <exception cref="InvalidDataException">The strips are missing or damaged, or the compression is not one this reader handles.</exception>
    internal static ushort[] ReadSamples(TiffDirectory directory, in TiffInfo info, CancellationToken cancellationToken)
    {
        var offsets = directory.GetValues(TagStripOffsets, MaxStrips);
        var counts = directory.GetValues(TagStripByteCounts, MaxStrips);
        var perPlane = (info.Height + info.RowsPerStrip - 1) / info.RowsPerStrip;
        if (offsets.Length < perPlane * info.Planes)
        {
            throw new InvalidDataException("The TIFF lacks strips for its size.");
        }

        var samples = new ushort[(long)info.Width * info.Height * info.SamplesPerPixel];
        for (var plane = 0; plane < info.Planes; plane++)
        {
            for (var strip = 0; strip < perPlane; strip++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var index = (plane * perPlane) + strip;
                var firstRow = strip * info.RowsPerStrip;
                var rows = Math.Min(info.RowsPerStrip, info.Height - firstRow);
                var data = Decompress(Slice(directory, offsets[index], counts.Length > index ? counts[index] : 0), info, rows, cancellationToken);
                if (info.Predictor == HorizontalDifferencing)
                {
                    Differentiate(data, rows, info, directory.IsLittleEndian);
                }

                Unpack(data, new(firstRow, rows, plane), info, directory.IsLittleEndian, samples);
            }
        }

        return samples;
    }

    /// <summary>Reads a fax coded, one-bit image into packed rows where a set bit is white.</summary>
    /// <param name="directory">The directory.</param>
    /// <param name="info">What the tags say.</param>
    /// <param name="cancellationToken">Cancels between strips.</param>
    /// <returns>The packed rows.</returns>
    /// <exception cref="InvalidDataException">The strips are missing.</exception>
    internal static byte[] ReadFax(TiffDirectory directory, in TiffInfo info, CancellationToken cancellationToken)
    {
        var offsets = directory.GetValues(TagStripOffsets, MaxStrips);
        var counts = directory.GetValues(TagStripByteCounts, MaxStrips);
        var perPlane = (info.Height + info.RowsPerStrip - 1) / info.RowsPerStrip;
        if (offsets.Length < perPlane)
        {
            throw new InvalidDataException("The TIFF lacks strips for its size.");
        }

        var rowBytes = (info.Width + ByteBits - 1) / ByteBits;
        var packed = new byte[rowBytes * info.Height];
        var parameters = GetFaxParameters(info);
        for (var strip = 0; strip < perPlane; strip++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var firstRow = strip * info.RowsPerStrip;
            var rows = Math.Min(info.RowsPerStrip, info.Height - firstRow);
            var data = Slice(directory, offsets[strip], counts.Length > strip ? counts[strip] : 0).ToArray();
            if (info.ReversedBits)
            {
                ReverseBits(data);
            }

            var decoded = new byte[rowBytes * rows];
            CcittFaxDecoder.Decode(data, parameters with { Rows = rows }, info.Width, rows, decoded);

            // The decoder sets a bit for a black run. When white is zero in the file's own picture, the white bits are the others.
            // When black is zero, libtiff stores the picture's zeros as the coder's white runs, so the black runs are the white pixels.
            var invert = info.Photometric == WhiteIsZero;
            for (var i = 0; i < decoded.Length; i++)
            {
                packed[(firstRow * rowBytes) + i] = invert ? (byte)~decoded[i] : decoded[i];
            }
        }

        return packed;
    }

    /// <summary>Works out the fax decoder's parameters for an image.</summary>
    /// <param name="info">What the tags say.</param>
    /// <returns>The parameters, with the rows left for each strip to set.</returns>
    private static CcittParameters GetFaxParameters(in TiffInfo info) => new(
        GetFaxK(info),
        info.Width,
        0,
        true,
        info.Compression == TiffInfo.FaxHuffman || (info.FaxOptions & FillBits) != 0,
        info.Compression == TiffInfo.FaxGroup3,
        false);

    /// <summary>Gets the fax coding scheme as a PDF <c>K</c> value: negative for Group 4, zero for one dimension, positive for mixed.</summary>
    /// <param name="info">What the tags say.</param>
    /// <returns>The value.</returns>
    private static int GetFaxK(in TiffInfo info)
    {
        if (info.Compression == TiffInfo.FaxGroup4)
        {
            return GroupFourK;
        }

        return info.Compression == TiffInfo.FaxGroup3 && (info.FaxOptions & TwoDimensional) != 0 ? MixedK : 0;
    }

    /// <summary>Takes the bytes of a strip from the file.</summary>
    /// <param name="directory">The directory.</param>
    /// <param name="offset">Where the strip starts.</param>
    /// <param name="count">The strip's size; zero when the file does not say, which takes the rest of the file.</param>
    /// <returns>The strip's bytes, cut to the file.</returns>
    /// <exception cref="InvalidDataException">The strip starts outside the file.</exception>
    private static ReadOnlySpan<byte> Slice(TiffDirectory directory, uint offset, uint count)
    {
        var file = directory.File;
        if (offset >= file.Length)
        {
            throw new InvalidDataException("A TIFF strip starts outside the file.");
        }

        var available = file.Length - (long)offset;
        var length = count == 0 ? available : Math.Min(count, available);
        return file.AsSpan((int)offset, (int)length);
    }

    /// <summary>Undoes a strip's compression.</summary>
    /// <param name="strip">The strip's bytes.</param>
    /// <param name="info">What the tags say.</param>
    /// <param name="rows">The rows in the strip.</param>
    /// <param name="cancellationToken">Cancels the decompression.</param>
    /// <returns>The rows, padded with zeros to their full size if the data was short.</returns>
    /// <exception cref="InvalidDataException">The compression is not one this reader handles.</exception>
    private static byte[] Decompress(ReadOnlySpan<byte> strip, in TiffInfo info, int rows, CancellationToken cancellationToken)
    {
        var expected = (long)info.RowBytes * rows;
        var output = default(PooledBuffer);
        try
        {
            switch (info.Compression)
            {
                case NoCompression:
                {
                    output.Write(strip[..(int)Math.Min(strip.Length, expected)]);
                    break;
                }

                case Lzw:
                {
                    LzwFilter.Decode(strip, 1, ref output, cancellationToken);
                    break;
                }

                case Deflate or AdobeDeflate:
                {
                    _ = ZLibCodec.Decompress(strip, false, ref output, cancellationToken);
                    break;
                }

                case PackBits:
                {
                    RunLengthFilter.Decode(strip, ref output, cancellationToken);
                    break;
                }

                default:
                {
                    throw new InvalidDataException("This reader does not handle the TIFF's compression.");
                }
            }

            var data = new byte[expected];
            output.WrittenSpan[..(int)Math.Min(output.Length, expected)].CopyTo(data);
            if (info.ReversedBits)
            {
                ReverseBits(data);
            }

            return data;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Reverses the order of the bits in every byte.</summary>
    /// <param name="data">The bytes, changed in place.</param>
    private static void ReverseBits(Span<byte> data)
    {
        for (var i = 0; i < data.Length; i++)
        {
            var value = data[i];
            var reversed = 0;
            for (var bit = 0; bit < ByteBits; bit++)
            {
                reversed = (reversed << 1) | ((value >> bit) & 1);
            }

            data[i] = (byte)reversed;
        }
    }

    /// <summary>Undoes horizontal differencing: each sample is stored as its difference from the same sample of the pixel to its left.</summary>
    /// <param name="data">The rows, changed in place.</param>
    /// <param name="rows">The rows in the strip.</param>
    /// <param name="info">What the tags say.</param>
    /// <param name="littleEndian">Whether 16-bit samples are stored least significant byte first.</param>
    private static void Differentiate(byte[] data, int rows, in TiffInfo info, bool littleEndian)
    {
        var step = info.Separate ? 1 : info.SamplesPerPixel;
        for (var row = 0; row < rows; row++)
        {
            var line = data.AsSpan(row * info.RowBytes, info.RowBytes);
            if (info.BitsPerSample == ByteBits)
            {
                for (var i = step; i < line.Length; i++)
                {
                    line[i] += line[i - step];
                }
            }
            else if (info.BitsPerSample == WideBits)
            {
                DifferentiateWords(line, step, littleEndian);
            }
        }
    }

    /// <summary>Undoes horizontal differencing on a row of 16-bit samples.</summary>
    /// <param name="line">The row, changed in place.</param>
    /// <param name="step">The samples between a sample and the one to add.</param>
    /// <param name="littleEndian">Whether the samples are stored least significant byte first.</param>
    private static void DifferentiateWords(Span<byte> line, int step, bool littleEndian)
    {
        for (var i = step; i < line.Length / sizeof(ushort); i++)
        {
            var current = ReadWord(line, i, littleEndian) + ReadWord(line, i - step, littleEndian);
            if (littleEndian)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(line[(i * sizeof(ushort))..], (ushort)current);
            }
            else
            {
                BinaryPrimitives.WriteUInt16BigEndian(line[(i * sizeof(ushort))..], (ushort)current);
            }
        }
    }

    /// <summary>Reads a 16-bit sample.</summary>
    /// <param name="line">The row.</param>
    /// <param name="index">The sample's index.</param>
    /// <param name="littleEndian">Whether the samples are stored least significant byte first.</param>
    /// <returns>The sample.</returns>
    private static ushort ReadWord(ReadOnlySpan<byte> line, int index, bool littleEndian) => littleEndian
        ? BinaryPrimitives.ReadUInt16LittleEndian(line[(index * sizeof(ushort))..])
        : BinaryPrimitives.ReadUInt16BigEndian(line[(index * sizeof(ushort))..]);

    /// <summary>Unpacks the samples of a strip into the picture's sample array.</summary>
    /// <param name="data">The strip's rows.</param>
    /// <param name="place">Which rows the strip holds and which plane it belongs to.</param>
    /// <param name="info">What the tags say.</param>
    /// <param name="littleEndian">Whether 16-bit samples are stored least significant byte first.</param>
    /// <param name="samples">Receives the samples.</param>
    private static void Unpack(byte[] data, in StripPlace place, in TiffInfo info, bool littleEndian, ushort[] samples)
    {
        var perRow = info.Separate ? info.Width : info.Width * info.SamplesPerPixel;
        for (var row = 0; row < place.Rows; row++)
        {
            var line = data.AsSpan(row * info.RowBytes, info.RowBytes);
            var imageRow = place.FirstRow + row;
            for (var index = 0; index < perRow; index++)
            {
                var target = info.Separate
                    ? ((((long)imageRow * info.Width) + index) * info.SamplesPerPixel) + place.Plane
                    : ((long)imageRow * perRow) + index;
                samples[target] = ReadSample(line, info.BitsPerSample, index, littleEndian);
            }
        }
    }

    /// <summary>Reads one sample from a row.</summary>
    /// <param name="line">The row.</param>
    /// <param name="bits">The bits in a sample.</param>
    /// <param name="index">The sample's index in the row.</param>
    /// <param name="littleEndian">Whether 16-bit samples are stored least significant byte first.</param>
    /// <returns>The sample.</returns>
    private static ushort ReadSample(ReadOnlySpan<byte> line, int bits, int index, bool littleEndian)
    {
        if (bits == ByteBits)
        {
            return line[index];
        }

        if (bits == WideBits)
        {
            return ReadWord(line, index, littleEndian);
        }

        var bit = index * bits;
        return (ushort)((line[bit / ByteBits] >> (ByteBits - bits - (bit % ByteBits))) & ((1 << bits) - 1));
    }
}
