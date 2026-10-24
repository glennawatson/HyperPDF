// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H265;

/// <summary>The quantisation matrices of a sequence or picture parameter set (7.3.4 and 7.4.5), expanded to a factor for every coefficient.</summary>
internal sealed class H265ScalingLists
{
    /// <summary>The block sizes with a matrix: 4x4, 8x8, 16x16 and 32x32.</summary>
    private const int Sizes = 4;

    /// <summary>The matrices of each size: intra and inter for each of the three colour components.</summary>
    private const int Matrices = 6;

    /// <summary>The first matrix of the inter prediction modes.</summary>
    private const int FirstInter = 3;

    /// <summary>The size number of 8x8 blocks.</summary>
    private const int Size8x8 = 1;

    /// <summary>The size number of 16x16 blocks.</summary>
    private const int Size16x16 = 2;

    /// <summary>The size number of 32x32 blocks, which only have two matrices.</summary>
    private const int Size32x32 = 3;

    /// <summary>The coefficients of an 8x8 list.</summary>
    private const int Coefficients8x8 = 64;

    /// <summary>The coefficients of a 4x4 list.</summary>
    private const int Coefficients4x4 = 16;

    /// <summary>How far apart the coded matrices of 32x32 blocks are: only the intra and inter luma matrices are coded.</summary>
    private const int Size32Step = 3;

    /// <summary>The shift that turns a size number into the base 2 logarithm of the coefficient count step.</summary>
    private const int CoefficientShift = 1;

    /// <summary>The side of the smallest block.</summary>
    private const int SmallestSide = 4;

    /// <summary>The base 2 logarithm of the side of a 4x4 block.</summary>
    private const int Log2Side4x4 = 2;

    /// <summary>The base 2 logarithm of the side of an 8x8 block.</summary>
    private const int Log2Side8x8 = 3;

    /// <summary>The value of the flat 4x4 lists and of the coefficient at the corner when nothing else is coded.</summary>
    private const int FlatValue = 16;

    /// <summary>The value the running coefficient starts from.</summary>
    private const int StartValue = 8;

    /// <summary>The modulus of the running coefficient.</summary>
    private const int ValueRange = 256;

    /// <summary>The default 8x8 list for intra blocks (Table 7-6).</summary>
    private static readonly byte[] DefaultIntra =
    [
        16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 17, 16, 17, 16, 17, 18,
        17, 18, 18, 17, 18, 21, 19, 20, 21, 20, 19, 21, 24, 22, 22, 24,
        24, 22, 22, 24, 25, 25, 27, 30, 27, 25, 25, 29, 31, 35, 35, 31,
        29, 36, 41, 44, 41, 36, 47, 54, 54, 47, 65, 70, 65, 88, 88, 115,
    ];

    /// <summary>The default 8x8 list for inter blocks (Table 7-6).</summary>
    private static readonly byte[] DefaultInter =
    [
        16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 17, 17, 17, 17, 17, 18,
        18, 18, 18, 18, 18, 20, 20, 20, 20, 20, 20, 20, 24, 24, 24, 24,
        24, 24, 24, 24, 25, 25, 25, 25, 25, 25, 25, 28, 28, 28, 28, 28,
        28, 33, 33, 33, 33, 33, 41, 41, 41, 41, 54, 54, 54, 71, 71, 91,
    ];

    /// <summary>The factor of every coefficient of each matrix, in raster order, indexed by size number times six plus matrix number.</summary>
    private readonly byte[][] _factors;

    /// <summary>Initializes a new instance of the <see cref="H265ScalingLists"/> class.</summary>
    /// <param name="lists">The coded list of each matrix: 16 or 64 values.</param>
    /// <param name="dc">The coefficient at the corner of each 16x16 and 32x32 matrix.</param>
    private H265ScalingLists(byte[][] lists, byte[] dc)
    {
        _factors = new byte[Sizes * Matrices][];
        for (var size = 0; size < Sizes; size++)
        {
            for (var matrix = 0; matrix < Matrices; matrix++)
            {
                var index = (size * Matrices) + matrix;
                _factors[index] = Expand(lists[index], size, dc[index]);
            }
        }
    }

    /// <summary>Gets the lists a stream that enables scaling lists without coding any uses.</summary>
    internal static H265ScalingLists Default { get; } = CreateDefault();

    /// <summary>Reads the lists from a parameter set.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The lists.</returns>
    /// <exception cref="InvalidDataException">The lists are damaged.</exception>
    internal static H265ScalingLists Read(ref MsbBitReader reader)
    {
        var lists = new byte[Sizes * Matrices][];
        var dc = new byte[Sizes * Matrices];
        for (var size = 0; size < Sizes; size++)
        {
            var step = size == Size32x32 ? Size32Step : 1;
            for (var matrix = 0; matrix < Matrices; matrix += step)
            {
                ReadOne(ref reader, size, matrix, step, lists, dc);
            }
        }

        for (var matrix = 0; matrix < Matrices; matrix++)
        {
            // 32x32 chroma matrices exist for 4:4:4 video and come from the 16x16 lists (7-50 and 7-51).
            var index = (Size32x32 * Matrices) + matrix;
            lists[index] ??= lists[(Size16x16 * Matrices) + matrix];
            dc[index] = dc[index] != 0 ? dc[index] : dc[(Size16x16 * Matrices) + matrix];
        }

        return new(lists, dc);
    }

    /// <summary>Gets the factors of a matrix.</summary>
    /// <param name="size">The size number: 0 for 4x4 to 3 for 32x32.</param>
    /// <param name="matrix">The matrix number: 0 to 2 for intra Y, Cb, Cr and 3 to 5 for inter.</param>
    /// <returns>The factor of each coefficient, in raster order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<byte> Factors(int size, int matrix) => _factors[(size * Matrices) + matrix];

    /// <summary>Builds the default lists.</summary>
    /// <returns>The lists.</returns>
    private static H265ScalingLists CreateDefault()
    {
        var lists = new byte[Sizes * Matrices][];
        var dc = new byte[Sizes * Matrices];
        for (var size = 0; size < Sizes; size++)
        {
            for (var matrix = 0; matrix < Matrices; matrix++)
            {
                lists[(size * Matrices) + matrix] = DefaultList(size, matrix);
                dc[(size * Matrices) + matrix] = FlatValue;
            }
        }

        return new(lists, dc);
    }

    /// <summary>Gets the default list of a matrix.</summary>
    /// <param name="size">The size number.</param>
    /// <param name="matrix">The matrix number.</param>
    /// <returns>The list.</returns>
    private static byte[] DefaultList(int size, int matrix)
    {
        if (size == 0)
        {
            var flat = new byte[Coefficients4x4];
            Array.Fill(flat, (byte)FlatValue);
            return flat;
        }

        return matrix < FirstInter ? DefaultIntra : DefaultInter;
    }

    /// <summary>Reads the list of one matrix.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="size">The size number.</param>
    /// <param name="matrix">The matrix number.</param>
    /// <param name="step">How far apart the coded matrices of this size are.</param>
    /// <param name="lists">Receives the list.</param>
    /// <param name="dc">Receives the corner coefficient.</param>
    /// <exception cref="InvalidDataException">The list refers to a list that does not exist.</exception>
    private static void ReadOne(ref MsbBitReader reader, int size, int matrix, int step, byte[][] lists, byte[] dc)
    {
        var index = (size * Matrices) + matrix;
        if (!reader.ReadBit())
        {
            var delta = (int)ExpGolomb.ReadUe(ref reader);
            var reference = matrix - (delta * step);
            if (delta == 0)
            {
                lists[index] = DefaultList(size, matrix);
                dc[index] = FlatValue;
            }
            else if (reference < 0)
            {
                throw new InvalidDataException("A scaling list refers to a list that does not exist.");
            }
            else
            {
                lists[index] = lists[(size * Matrices) + reference];
                dc[index] = dc[(size * Matrices) + reference];
            }

            return;
        }

        var count = Math.Min(Coefficients8x8, Coefficients4x4 << (size << CoefficientShift));
        var next = StartValue;
        if (size > Size8x8)
        {
            next = ExpGolomb.ReadSe(ref reader) + StartValue;
            dc[index] = (byte)next;
        }

        var list = new byte[count];
        for (var i = 0; i < count; i++)
        {
            next = (next + ExpGolomb.ReadSe(ref reader) + ValueRange) % ValueRange;
            list[i] = (byte)next;
        }

        lists[index] = list;
    }

    /// <summary>Expands a coded list to a factor for every coefficient of the block (7-44 to 7-51).</summary>
    /// <param name="list">The coded list.</param>
    /// <param name="size">The size number.</param>
    /// <param name="dc">The corner coefficient of a 16x16 or 32x32 matrix.</param>
    /// <returns>The factors in raster order.</returns>
    private static byte[] Expand(byte[] list, int size, byte dc)
    {
        var side = SmallestSide << size;
        var factors = new byte[side * side];
        var scan = H265ScanOrder.Diagonal(size == 0 ? Log2Side4x4 : Log2Side8x8);
        var ratio = size <= Size8x8 ? 1 : 1 << (size - Size8x8);
        for (var i = 0; i < list.Length; i++)
        {
            var position = scan[i];
            for (var j = 0; j < ratio; j++)
            {
                for (var k = 0; k < ratio; k++)
                {
                    factors[(((position.Y * ratio) + j) * side) + (position.X * ratio) + k] = list[i];
                }
            }
        }

        if (size >= Size16x16)
        {
            factors[0] = dc;
        }

        return factors;
    }
}
