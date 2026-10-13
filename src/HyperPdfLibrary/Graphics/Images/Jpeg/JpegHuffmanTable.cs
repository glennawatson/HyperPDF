// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// A JPEG Huffman table with a direct lookup for short codes and canonical ranges for long ones. A table owns its arrays
/// and is defined again for each image, so a decoder keeps its tables between images.
/// </summary>
[DebuggerDisplay("JpegHuffmanTable: {Count} symbols")]
internal sealed class JpegHuffmanTable
{
    /// <summary>The bits looked up in one step.</summary>
    internal const int LookupBits = 9;

    /// <summary>The longest Huffman code.</summary>
    internal const int MaxCodeLength = 16;

    /// <summary>The number of code-length counts in a table definition.</summary>
    internal const int LengthCounts = 16;

    /// <summary>The most symbols a table can hold.</summary>
    internal const int MaxSymbols = 256;

    /// <summary>The bits that select the symbol in a lookup entry.</summary>
    private const int SymbolBits = 8;

    /// <summary>The mask of the symbol in a lookup entry.</summary>
    private const int SymbolMask = 0xFF;

    /// <summary>Initializes a new instance of the <see cref="JpegHuffmanTable"/> class with no codes.</summary>
    internal JpegHuffmanTable()
    {
        Values = new byte[MaxSymbols];
        Lookup = new short[1 << LookupBits];
        MaxCode = new int[MaxCodeLength + 1];
        ValueOffset = new int[MaxCodeLength + 1];
    }

    /// <summary>Gets a table with no codes, which fails every decode; it stands in for a table the file never defined.</summary>
    internal static JpegHuffmanTable Empty { get; } = CreateEmpty();

    /// <summary>Gets the symbols in code order; only the first <see cref="Count"/> belong to the definition.</summary>
    internal byte[] Values { get; }

    /// <summary>Gets the number of symbols the definition has.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets a value indicating whether a definition has been accepted for this image.</summary>
    internal bool IsDefined { get; private set; }

    /// <summary>Gets the lookup of the first <see cref="LookupBits"/> bits: code length in the high byte and symbol in the low, or zero.</summary>
    internal short[] Lookup { get; }

    /// <summary>Gets the largest code of each length, or -1 when the length has no codes.</summary>
    internal int[] MaxCode { get; }

    /// <summary>Gets the amount added to a code of each length to give its index in <see cref="Values"/>.</summary>
    internal int[] ValueOffset { get; }

    /// <summary>Forgets the definition, so the table stands for one the file never gave.</summary>
    internal void Undefine()
    {
        IsDefined = false;
        Count = 0;
    }

    /// <summary>Defines the table from a DHT definition.</summary>
    /// <param name="counts">The number of codes of each length from 1 to 16.</param>
    /// <param name="values">The symbols in code order.</param>
    /// <returns><see langword="false"/> when the definition is not a valid prefix code; the table is then undefined.</returns>
    internal bool TryDefine(ReadOnlySpan<byte> counts, ReadOnlySpan<byte> values)
    {
        Undefine();
        var total = 0;
        foreach (var count in counts)
        {
            total += count;
        }

        if (total > MaxSymbols || total > values.Length)
        {
            return false;
        }

        values[..total].CopyTo(Values);
        Values.AsSpan(total).Clear();
        Lookup.AsSpan().Clear();
        Count = total;
        IsDefined = Build(counts);
        return IsDefined;
    }

    /// <summary>Builds the table with no codes.</summary>
    /// <returns>The table.</returns>
    private static JpegHuffmanTable CreateEmpty()
    {
        var table = new JpegHuffmanTable();
        _ = table.TryDefine(new byte[LengthCounts], []);
        return table;
    }

    /// <summary>Assigns canonical codes and fills the lookup tables.</summary>
    /// <param name="counts">The number of codes of each length.</param>
    /// <returns><see langword="false"/> when the lengths oversubscribe the code space.</returns>
    private bool Build(ReadOnlySpan<byte> counts)
    {
        var code = 0;
        var index = 0;
        for (var length = 1; length <= MaxCodeLength; length++)
        {
            var count = counts[length - 1];
            ValueOffset[length] = index - code;
            if (code + count > 1 << length)
            {
                return false;
            }

            if (length <= LookupBits)
            {
                FillLookup(length, code, index, count);
            }

            code += count;
            index += count;
            MaxCode[length] = count == 0 ? -1 : code - 1;
            code <<= 1;
        }

        return true;
    }

    /// <summary>Fills the lookup entries of the codes of one length.</summary>
    /// <param name="length">The code length.</param>
    /// <param name="firstCode">The first code of this length.</param>
    /// <param name="firstIndex">The index of its symbol.</param>
    /// <param name="count">The number of codes of this length.</param>
    private void FillLookup(int length, int firstCode, int firstIndex, int count)
    {
        var spread = 1 << (LookupBits - length);
        for (var i = 0; i < count; i++)
        {
            var entry = (short)((length << SymbolBits) | (Values[firstIndex + i] & SymbolMask));
            Lookup.AsSpan((firstCode + i) * spread, spread).Fill(entry);
        }
    }
}
