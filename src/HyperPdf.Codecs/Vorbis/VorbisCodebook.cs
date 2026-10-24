// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Vorbis;

/// <summary>
/// A Vorbis codebook: a prefix code for entry numbers, and for some books a list of vectors the entries stand for. Codewords are
/// handed out in entry order to the lowest free place in the code tree. Codes of up to ten bits are decoded by one table lookup, and
/// longer ones continue from the tree node the table names.
/// </summary>
[DebuggerDisplay("VorbisCodebook: {Entries} entries of {Dimensions}")]
internal sealed class VorbisCodebook
{
    /// <summary>The bits of the first lookup.</summary>
    private const int TableBits = 10;

    /// <summary>The flag on a table entry that leads to the tree.</summary>
    private const uint TreeFlag = 0x8000_0000;

    /// <summary>The mask of the length in a leaf entry of the table.</summary>
    private const uint LengthMask = 0x1F;

    /// <summary>The shift of the entry number in a leaf entry of the table.</summary>
    private const int EntryShift = 5;

    /// <summary>The bits in a codeword.</summary>
    private const int CodeBits = 32;

    /// <summary>The synchronisation pattern that starts every codebook.</summary>
    private const uint SyncPattern = 0x564342;

    /// <summary>The bits of the synchronisation pattern.</summary>
    private const int SyncBits = 24;

    /// <summary>The bits of the number of dimensions.</summary>
    private const int DimensionBits = 16;

    /// <summary>The bits of the number of entries.</summary>
    private const int EntryBits = 24;

    /// <summary>The bits of a codeword length.</summary>
    private const int LengthBits = 5;

    /// <summary>The bits of the type of vector lookup.</summary>
    private const int LookupTypeBits = 4;

    /// <summary>The bits of the number of bits of a vector value.</summary>
    private const int ValueBitCountBits = 4;

    /// <summary>The mask of the mantissa of a packed float.</summary>
    private const uint MantissaMask = 0x1F_FFFF;

    /// <summary>The mask of the exponent of a packed float, before it is shifted down.</summary>
    private const uint ExponentMask = 0x7FE0_0000;

    /// <summary>The shift that brings the exponent of a packed float down.</summary>
    private const int ExponentShift = 21;

    /// <summary>The sign bit of a packed float.</summary>
    private const uint SignBit = 0x8000_0000;

    /// <summary>The bias of the exponent of a packed float.</summary>
    private const int ExponentBias = 788;

    /// <summary>The most entries a codebook can have.</summary>
    private const int MostEntries = 1 << EntryBits;

    /// <summary>The entry number that marks a pattern no codeword starts with.</summary>
    private const uint InvalidEntry = 0x7FF_FFFF;

    /// <summary>The children of a tree node.</summary>
    private const int Children = 2;

    /// <summary>The lookup type that builds a vector from a grid of values.</summary>
    private const int GridLookup = 1;

    /// <summary>The lookup type that lists every vector in full.</summary>
    private const int ListLookup = 2;

    /// <summary>The first lookup: for each pattern of the next ten bits, a leaf or the tree node reached.</summary>
    private readonly uint[] _table = new uint[1 << TableBits];

    /// <summary>The tree being built.</summary>
    private readonly List<int> _tree = [0, 0];

    /// <summary>The tree: for each node two children; a positive child is a node, a negative child is the complement of an entry, 0 is none.</summary>
    private int[] _children = [];

    /// <summary>The values of the vectors, one run of <see cref="Dimensions"/> for each entry, or empty when the book has no vectors.</summary>
    private float[] _vectors = [];

    /// <summary>Initializes a new instance of the <see cref="VorbisCodebook"/> class.</summary>
    private VorbisCodebook()
    {
    }

    /// <summary>Gets the number of values in a vector.</summary>
    internal int Dimensions { get; private set; }

    /// <summary>Gets the number of entries.</summary>
    internal int Entries { get; private set; }

    /// <summary>Gets a value indicating whether entries stand for vectors of values.</summary>
    internal bool HasVectors => _vectors.Length != 0;

    /// <summary>Works out how many bits count a number: the position of its highest set bit.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The bit count, 0 for 0.</returns>
    internal static int Ilog(int value) => value <= 0 ? 0 : CodeBits - BitOperations.LeadingZeroCount((uint)value);

    /// <summary>Reads a codebook from the setup header.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The codebook.</returns>
    /// <exception cref="InvalidDataException">The codebook is damaged.</exception>
    internal static VorbisCodebook Read(ref LsbBitReader reader)
    {
        if (reader.Read(SyncBits) != SyncPattern)
        {
            throw new InvalidDataException("A Vorbis codebook does not start with its marker.");
        }

        var book = new VorbisCodebook { Dimensions = (int)reader.Read(DimensionBits), Entries = (int)reader.Read(EntryBits) };
        var lengths = ReadLengths(ref reader, book.Entries);
        book.BuildCode(lengths);
        book.ReadVectors(ref reader);
        return book;
    }

    /// <summary>Reads an entry number.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The entry, or -1 when the bits are not a code of this book.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Decode(ref LsbBitReader reader)
    {
        var entry = _table[reader.Peek(TableBits)];
        if ((entry & TreeFlag) == 0)
        {
            reader.Skip((int)(entry & LengthMask));
            var value = (int)(entry >> EntryShift);
            return value >= MostEntries ? -1 : value;
        }

        reader.Skip(TableBits);
        return WalkTree(ref reader, (int)(entry & ~TreeFlag));
    }

    /// <summary>Gets the vector of an entry.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<float> Vector(int entry) => _vectors.AsSpan(entry * Dimensions, Dimensions);

    /// <summary>Reads the codeword length of every entry.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="entries">The number of entries.</param>
    /// <returns>The lengths; 0 for an entry that is not used.</returns>
    /// <exception cref="InvalidDataException">The lengths do not fit the number of entries.</exception>
    private static byte[] ReadLengths(ref LsbBitReader reader, int entries)
    {
        var lengths = new byte[entries];
        if (!reader.ReadBit())
        {
            var sparse = reader.ReadBit();
            for (var entry = 0; entry < entries; entry++)
            {
                var used = !sparse || reader.ReadBit();
                lengths[entry] = used ? (byte)(reader.Read(LengthBits) + 1) : (byte)0;
            }

            return lengths;
        }

        var current = 0;
        var length = (int)reader.Read(LengthBits) + 1;
        while (current < entries)
        {
            var count = (int)reader.Read(Ilog(entries - current));
            if (current + count > entries || length > CodeBits)
            {
                throw new InvalidDataException("A Vorbis codebook has too many entries of one length.");
            }

            lengths.AsSpan(current, count).Fill((byte)length);
            current += count;
            length++;
        }

        return lengths;
    }

    /// <summary>Reads a packed 32 bit float.</summary>
    /// <param name="packed">The packed bits.</param>
    /// <returns>The value.</returns>
    private static float Unpack(uint packed)
    {
        double mantissa = packed & MantissaMask;
        if ((packed & SignBit) != 0)
        {
            mantissa = -mantissa;
        }

        var exponent = (int)((packed & ExponentMask) >> ExponentShift) - ExponentBias;
        return (float)(mantissa * Math.Pow(Children, exponent));
    }

    /// <summary>Takes the lowest free place in the tree at or above a depth and splits what is left of it.</summary>
    /// <param name="available">The free place at each depth, as a codeword aligned to the top of 32 bits, or 0.</param>
    /// <param name="length">The depth the entry needs.</param>
    /// <returns>The codeword, aligned to the top of 32 bits.</returns>
    /// <exception cref="InvalidDataException">The code has no room for the entry.</exception>
    private static uint TakeCode(Span<uint> available, int length)
    {
        var depth = length;
        while (depth > 0 && available[depth] == 0)
        {
            depth--;
        }

        if (depth == 0)
        {
            throw new InvalidDataException("A Vorbis codebook has more entries than its code lengths allow.");
        }

        var code = available[depth];
        available[depth] = 0;
        for (var split = length; split > depth; split--)
        {
            available[split] = code + (1U << (CodeBits - split));
        }

        return code;
    }

    /// <summary>Finds the largest whole number whose power of the dimensions does not pass the number of entries.</summary>
    /// <param name="entries">The number of entries.</param>
    /// <param name="dimensions">The dimensions.</param>
    /// <returns>The number of multiplicands.</returns>
    private static int Lookup1Values(int entries, int dimensions)
    {
        var values = (int)Math.Floor(Math.Pow(entries, 1.0 / dimensions));
        while (Power(values + 1, dimensions) <= entries)
        {
            values++;
        }

        while (values > 1 && Power(values, dimensions) > entries)
        {
            values--;
        }

        return values;
    }

    /// <summary>Raises a number to a power, stopping early when it passes the most entries a codebook has.</summary>
    /// <param name="value">The number.</param>
    /// <param name="exponent">The power.</param>
    /// <returns>The result, or a number past the limit.</returns>
    private static long Power(int value, int exponent)
    {
        long result = 1;
        for (var index = 0; index < exponent && result <= MostEntries; index++)
        {
            result *= value;
        }

        return result;
    }

    /// <summary>Follows the tree from a node, a bit at a time.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="start">The node to start from.</param>
    /// <returns>The entry, or -1 when the bits lead nowhere.</returns>
    private int WalkTree(ref LsbBitReader reader, int start)
    {
        var node = start;
        while (true)
        {
            node = _children[(node * Children) + (reader.ReadBit() ? 1 : 0)];
            if (node < 0)
            {
                return ~node;
            }

            if (node == 0)
            {
                return -1;
            }
        }
    }

    /// <summary>Gives each used entry its codeword and builds the lookups.</summary>
    /// <param name="lengths">The codeword length of every entry.</param>
    private void BuildCode(byte[] lengths)
    {
        Span<uint> available = stackalloc uint[CodeBits + 1];
        var first = Array.FindIndex(lengths, static length => length != 0);
        if (first >= 0)
        {
            Insert(0, lengths[first], first);
            for (var depth = 1; depth <= lengths[first]; depth++)
            {
                available[depth] = 1U << (CodeBits - depth);
            }

            for (var entry = first + 1; entry < lengths.Length; entry++)
            {
                if (lengths[entry] != 0)
                {
                    Insert(TakeCode(available, lengths[entry]), lengths[entry], entry);
                }
            }
        }

        _children = [.. _tree];
        FillTable();
    }

    /// <summary>Adds an entry to the tree.</summary>
    /// <param name="code">The codeword, aligned to the top of 32 bits.</param>
    /// <param name="length">The length of the codeword.</param>
    /// <param name="entry">The entry.</param>
    private void Insert(uint code, int length, int entry)
    {
        var node = 0;
        for (var depth = 0; depth < length; depth++)
        {
            var slot = (node * Children) + (int)((code >> (CodeBits - 1 - depth)) & 1);
            if (depth == length - 1)
            {
                _tree[slot] = ~entry;
                return;
            }

            if (_tree[slot] <= 0)
            {
                _tree[slot] = _tree.Count / Children;
                _tree.Add(0);
                _tree.Add(0);
            }

            node = _tree[slot];
        }
    }

    /// <summary>Fills the lookup table by walking the tree for every pattern of the first bits.</summary>
    private void FillTable()
    {
        for (var pattern = 0; pattern < _table.Length; pattern++)
        {
            var node = 0;
            var depth = 0;
            var leaf = false;
            while (depth < TableBits && !leaf)
            {
                var child = _children[(node * Children) + ((pattern >> depth) & 1)];
                depth++;
                leaf = child <= 0;
                if (leaf)
                {
                    _table[pattern] = ((child < 0 ? (uint)~child : InvalidEntry) << EntryShift) | (uint)depth;
                }
                else
                {
                    node = child;
                }
            }

            if (!leaf)
            {
                _table[pattern] = TreeFlag | (uint)node;
            }
        }
    }

    /// <summary>Reads the vector lookup, if the book has one.</summary>
    /// <param name="reader">The bits.</param>
    /// <exception cref="InvalidDataException">The lookup is damaged.</exception>
    private void ReadVectors(ref LsbBitReader reader)
    {
        var type = (int)reader.Read(LookupTypeBits);
        if (type == 0)
        {
            return;
        }

        if (type > ListLookup || Entries > MostEntries)
        {
            throw new InvalidDataException("A Vorbis codebook has an unknown lookup type.");
        }

        var minimum = Unpack(reader.Read(CodeBits));
        var delta = Unpack(reader.Read(CodeBits));
        var valueBits = (int)reader.Read(ValueBitCountBits) + 1;
        var sequence = reader.ReadBit();
        var values = type == GridLookup ? Lookup1Values(Entries, Dimensions) : Entries * Dimensions;
        var multiplicands = new float[values];
        for (var index = 0; index < values; index++)
        {
            multiplicands[index] = reader.Read(valueBits);
        }

        _vectors = new float[Entries * Dimensions];
        for (var entry = 0; entry < Entries; entry++)
        {
            FillVector(entry, type == GridLookup, multiplicands, new(minimum, delta, sequence));
        }
    }

    /// <summary>Works out one vector from the multiplicands.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="grid">Whether the multiplicands are a grid that the entry number indexes in a mixed radix, rather than a full list.</param>
    /// <param name="multiplicands">The multiplicands.</param>
    /// <param name="scale">How the multiplicands become values.</param>
    private void FillVector(int entry, bool grid, float[] multiplicands, VectorScale scale)
    {
        var last = 0F;
        var divisor = 1;
        var span = _vectors.AsSpan(entry * Dimensions, Dimensions);
        for (var dimension = 0; dimension < Dimensions; dimension++)
        {
            var index = grid ? (entry / divisor) % multiplicands.Length : (entry * Dimensions) + dimension;
            var value = (multiplicands[index] * scale.Delta) + scale.Minimum + last;
            span[dimension] = value;
            last = scale.Sequence ? value : 0F;
            divisor = grid ? divisor * multiplicands.Length : divisor;
        }
    }

    /// <summary>How the integer multiplicands of a lookup become values.</summary>
    /// <param name="Minimum">The smallest value.</param>
    /// <param name="Delta">The step.</param>
    /// <param name="Sequence">Whether each value adds to the one before.</param>
    private readonly record struct VectorScale(float Minimum, float Delta, bool Sequence);
}
