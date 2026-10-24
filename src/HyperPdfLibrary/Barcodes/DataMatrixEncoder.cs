// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace HyperPdfLibrary.Barcodes;

/// <summary>Encodes text as a square Data Matrix ECC 200 symbol in ASCII mode.</summary>
internal static class DataMatrixEncoder
{
    /// <summary>The polynomial of the field.</summary>
    private const int FieldPolynomial = 0x12D;

    /// <summary>The first error correction root's power.</summary>
    private const int FirstRoot = 1;

    /// <summary>The most error correction codewords a block holds, which sizes the buffer kept on the stack.</summary>
    private const int MaximumEccLength = 68;

    /// <summary>The codeword that starts the pad bytes.</summary>
    private const int PadCodeword = 129;

    /// <summary>The codeword added to a digit pair's value.</summary>
    private const int DigitPairBase = 130;

    /// <summary>The codeword that makes the next byte one above 127.</summary>
    private const int UpperShift = 235;

    /// <summary>The highest ASCII value that is a single codeword.</summary>
    private const int HighestAscii = 127;

    /// <summary>The ten in a pair of digits.</summary>
    private const int Ten = 10;

    /// <summary>The multiplier of the pad byte scrambling.</summary>
    private const int ScrambleMultiplier = 149;

    /// <summary>The modulus of the pad byte scrambling.</summary>
    private const int ScrambleModulus = 253;

    /// <summary>The number the scrambled pad value wraps at.</summary>
    private const int PadWrap = 254;

    /// <summary>What a module of a region is.</summary>
    private enum Edge
    {
        /// <summary>A module of the data.</summary>
        Data = 0,

        /// <summary>A dark module of the finder or clock edge.</summary>
        Dark = 1,

        /// <summary>A light module of the clock edge.</summary>
        Light = 2,
    }

    /// <summary>Encodes text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The barcode.</returns>
    /// <exception cref="ArgumentException">The text is empty or too long.</exception>
    internal static PdfBarcode Encode(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        var data = GetCodewords(Encoding.UTF8.GetBytes(text));
        var size = DataMatrixSizes.Find(data.Count);
        var padded = Pad(data, size.DataBytes);
        var codewords = AddErrorCorrection(padded, size);
        var mapping = DataMatrixPlacement.Place(size.MappingSide, codewords);
        var modules = Assemble(size, mapping);
        return new(PdfBarcodeSymbology.DataMatrix, text, size.Side, modules);
    }

    /// <summary>Turns bytes into ASCII-mode codewords, packing digit pairs.</summary>
    /// <param name="bytes">The text as UTF-8.</param>
    /// <returns>The codewords.</returns>
    private static List<byte> GetCodewords(byte[] bytes)
    {
        var codewords = new List<byte>(bytes.Length);
        for (var i = 0; i < bytes.Length; i++)
        {
            var value = bytes[i];
            if (IsDigit(value) && i + 1 < bytes.Length && IsDigit(bytes[i + 1]))
            {
                codewords.Add((byte)(DigitPairBase + (((value - '0') * Ten) + (bytes[i + 1] - '0'))));
                i++;
            }
            else if (value <= HighestAscii)
            {
                codewords.Add((byte)(value + 1));
            }
            else
            {
                codewords.Add(UpperShift);
                codewords.Add((byte)(value - HighestAscii));
            }
        }

        return codewords;
    }

    /// <summary>Determines whether a byte is an ASCII digit.</summary>
    /// <param name="value">The byte.</param>
    /// <returns><see langword="true"/> for 0 to 9.</returns>
    private static bool IsDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';

    /// <summary>Fills the unused capacity with the pad codeword and scrambled pad codewords.</summary>
    /// <param name="data">The data codewords.</param>
    /// <param name="capacity">The data codewords the size holds.</param>
    /// <returns>Exactly <paramref name="capacity"/> codewords.</returns>
    private static byte[] Pad(List<byte> data, int capacity)
    {
        var result = new byte[capacity];
        data.CopyTo(result);
        for (var i = data.Count; i < capacity; i++)
        {
            if (i == data.Count)
            {
                result[i] = PadCodeword;
                continue;
            }

            var scrambled = PadCodeword + (((ScrambleMultiplier * (i + 1)) % ScrambleModulus) + 1);
            result[i] = (byte)(scrambled > PadWrap ? scrambled - PadWrap : scrambled);
        }

        return result;
    }

    /// <summary>Splits the data into interleaved blocks and adds error correction to each.</summary>
    /// <param name="data">The data codewords.</param>
    /// <param name="size">The symbol size.</param>
    /// <returns>The data codewords followed by the error correction codewords, as they are placed.</returns>
    private static byte[] AddErrorCorrection(byte[] data, DataMatrixSize size)
    {
        var blocks = size.Blocks;
        var eccPerBlock = size.EccBytes / blocks;
        var field = new ReedSolomon(FieldPolynomial, FirstRoot);
        var result = new byte[size.DataBytes + size.EccBytes];
        data.CopyTo(result, 0);

        // Every codeword, data then error correction, goes to block (position mod blocks), so when the data does not fill
        // a whole round (the 144 by 144 symbol) the error correction codewords carry on from the next block.
        var shift = size.DataBytes % blocks;
        var part = ArrayPool<byte>.Shared.Rent((data.Length / blocks) + 1);
        Span<byte> ecc = stackalloc byte[MaximumEccLength];
        ecc = ecc[..eccPerBlock];
        try
        {
            for (var block = 0; block < blocks; block++)
            {
                var length = CopyBlockData(data, block, blocks, part);
                field.GetRemainder(part.AsSpan(0, length), ecc);
                var first = size.DataBytes + ((block - shift + blocks) % blocks);
                for (var i = 0; i < eccPerBlock; i++)
                {
                    result[first + (i * blocks)] = ecc[i];
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(part);
        }

        return result;
    }

    /// <summary>Copies the data codewords of one block: every <paramref name="blocks"/>th codeword from the block's number.</summary>
    /// <param name="data">The data codewords.</param>
    /// <param name="block">The block.</param>
    /// <param name="blocks">The number of blocks.</param>
    /// <param name="part">Receives the block's data.</param>
    /// <returns>How many codewords the block has.</returns>
    private static int CopyBlockData(byte[] data, int block, int blocks, byte[] part)
    {
        var length = 0;
        for (var i = block; i < data.Length; i += blocks)
        {
            part[length] = data[i];
            length++;
        }

        return length;
    }

    /// <summary>Surrounds the mapping area with the finder and clock edges of each data region.</summary>
    /// <param name="size">The symbol size.</param>
    /// <param name="mapping">The mapping area's modules row by row.</param>
    /// <returns>The symbol's modules row by row.</returns>
    private static bool[] Assemble(DataMatrixSize size, bool[] mapping)
    {
        var side = size.Side;
        var region = size.RegionSide;
        var stride = region + DataMatrixSize.EdgeModules;
        var modules = new bool[side * side];
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var edge = GetEdge(x % stride, y % stride, region);
                modules[(y * side) + x] = edge == Edge.Data
                    ? mapping[(GetMappingIndex(y, region) * size.MappingSide) + GetMappingIndex(x, region)]
                    : edge == Edge.Dark;
            }
        }

        return modules;
    }

    /// <summary>Tells whether a module of a region is dark when it is on the region's edge.</summary>
    /// <param name="x">The column within the region including its edges.</param>
    /// <param name="y">The row within the region including its edges.</param>
    /// <param name="region">The modules across a region's data.</param>
    /// <returns>What the module is.</returns>
    private static Edge GetEdge(int x, int y, int region)
    {
        if (x == 0 || y == region + 1)
        {
            return Edge.Dark;
        }

        if (y == 0)
        {
            return (x & 1) == 0 ? Edge.Dark : Edge.Light;
        }

        if (x != region + 1)
        {
            return Edge.Data;
        }

        return (y & 1) == 1 ? Edge.Dark : Edge.Light;
    }

    /// <summary>Maps a symbol row or column to the matching row or column of the mapping area.</summary>
    /// <param name="position">The row or column in the symbol.</param>
    /// <param name="region">The modules across a region's data.</param>
    /// <returns>The index in the mapping area.</returns>
    private static int GetMappingIndex(int position, int region)
    {
        var stride = region + DataMatrixSize.EdgeModules;
        return (position / stride * region) + (position % stride) - 1;
    }
}
