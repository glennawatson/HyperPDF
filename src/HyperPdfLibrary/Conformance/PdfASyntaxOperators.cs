// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Conformance;

/// <summary>Classifies content stream operators by the operands veraPDF checks (ISO 32000-1, Annex A).</summary>
internal static class PdfASyntaxOperators
{
    /// <summary>The longest operator.</summary>
    private const int MaxLength = 3;

    /// <summary>The bits of one byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The operators whose operands are checked, keyed by <see cref="Key"/>.</summary>
    private static readonly FrozenDictionary<int, PdfASyntaxLink> Links = new Dictionary<int, PdfASyntaxLink>
    {
        [Key("i"u8)] = PdfASyntaxLink.LastNumber,
        [Key("M"u8)] = PdfASyntaxLink.LastNumber,
        [Key("w"u8)] = PdfASyntaxLink.LastNumber,
        [Key("Tc"u8)] = PdfASyntaxLink.LastNumber,
        [Key("Tw"u8)] = PdfASyntaxLink.LastNumber,
        [Key("TL"u8)] = PdfASyntaxLink.LastNumber,
        [Key("Ts"u8)] = PdfASyntaxLink.LastNumber,
        [Key("Tz"u8)] = PdfASyntaxLink.LastNumber,
        [Key("Td"u8)] = PdfASyntaxLink.LastTwoNumbers,
        [Key("TD"u8)] = PdfASyntaxLink.LastTwoNumbers,
        [Key("d0"u8)] = PdfASyntaxLink.LastTwoNumbers,
        [Key("J"u8)] = PdfASyntaxLink.LastInteger,
        [Key("j"u8)] = PdfASyntaxLink.LastInteger,
        [Key("c"u8)] = PdfASyntaxLink.AllNumbers,
        [Key("l"u8)] = PdfASyntaxLink.AllNumbers,
        [Key("m"u8)] = PdfASyntaxLink.AllNumbers,
        [Key("re"u8)] = PdfASyntaxLink.AllNumbers,
        [Key("v"u8)] = PdfASyntaxLink.AllNumbers,
        [Key("y"u8)] = PdfASyntaxLink.AllNumbers,
        [Key("cm"u8)] = PdfASyntaxLink.AllNumbers,
        [Key("Tm"u8)] = PdfASyntaxLink.AllNumbers,
        [Key("d1"u8)] = PdfASyntaxLink.AllNumbers,
        [Key("d"u8)] = PdfASyntaxLink.Dash,
        [Key("Tj"u8)] = PdfASyntaxLink.LastString,
        [Key("'"u8)] = PdfASyntaxLink.LastString,
        [Key("\""u8)] = PdfASyntaxLink.LastString,
        [Key("TJ"u8)] = PdfASyntaxLink.LastArray,
        [Key("Tf"u8)] = PdfASyntaxLink.Font,
        [Key("cs"u8)] = PdfASyntaxLink.LastName,
        [Key("CS"u8)] = PdfASyntaxLink.LastName,
        [Key("sc"u8)] = PdfASyntaxLink.LastName,
        [Key("SC"u8)] = PdfASyntaxLink.LastName,
        [Key("scn"u8)] = PdfASyntaxLink.LastName,
        [Key("SCN"u8)] = PdfASyntaxLink.LastName,
        [Key("BMC"u8)] = PdfASyntaxLink.LastName,
        [Key("MP"u8)] = PdfASyntaxLink.LastName,
        [Key("BDC"u8)] = PdfASyntaxLink.TagAndProperties,
        [Key("DP"u8)] = PdfASyntaxLink.TagAndProperties,
    }.ToFrozenDictionary();

    /// <summary>Every standard operator, keyed by <see cref="Key"/>.</summary>
    private static readonly FrozenSet<int> Standard = StandardKeys(
    [
        "b", "B", "b*", "B*", "BDC", "BI", "BMC", "BT", "BX", "c", "cm", "CS", "cs", "d", "d0", "d1", "Do", "DP", "EI", "EMC",
        "ET", "EX", "f", "F", "f*", "G", "g", "gs", "h", "i", "ID", "j", "J", "K", "k", "l", "m", "M", "MP", "n", "q", "Q",
        "re", "RG", "rg", "ri", "s", "S", "SC", "sc", "SCN", "scn", "sh", "T*", "Tc", "Td", "TD", "Tf", "Tj", "TJ", "TL",
        "Tm", "Tr", "Ts", "Tw", "Tz", "v", "w", "W", "W*", "y", "'", "\"",
    ]);

    /// <summary>Gets the operands an operator links to the implementation limits.</summary>
    /// <param name="word">The operator.</param>
    /// <returns>The link.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfASyntaxLink LinkOf(ReadOnlySpan<byte> word) => Links.GetValueOrDefault(Key(word));

    /// <summary>Determines whether a word is a standard operator.</summary>
    /// <param name="word">The word.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsStandard(ReadOnlySpan<byte> word) => Standard.Contains(Key(word));

    /// <summary>Builds the keys of the standard operators.</summary>
    /// <param name="names">The operators, which are ASCII.</param>
    /// <returns>The keys.</returns>
    private static FrozenSet<int> StandardKeys(string[] names)
    {
        var keys = new HashSet<int>(names.Length);
        Span<byte> bytes = stackalloc byte[MaxLength];
        foreach (var name in names)
        {
            var length = Encoding.ASCII.GetBytes(name, bytes);
            _ = keys.Add(Key(bytes[..length]));
        }

        return keys.ToFrozenSet();
    }

    /// <summary>Packs an operator of up to three bytes and its length into one integer.</summary>
    /// <param name="word">The operator.</param>
    /// <returns>The key, or -1 for a longer word.</returns>
    private static int Key(ReadOnlySpan<byte> word)
    {
        if (word.IsEmpty || word.Length > MaxLength)
        {
            return -1;
        }

        // The length leads, so a shorter operator never packs to the same key as a longer one.
        var key = word.Length;
        foreach (var b in word)
        {
            key = (key << ByteBits) | b;
        }

        return key;
    }
}
