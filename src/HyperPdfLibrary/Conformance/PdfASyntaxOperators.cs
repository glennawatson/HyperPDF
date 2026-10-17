// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Conformance;

/// <summary>Classifies content stream operators by the operands veraPDF checks (ISO 32000-1, Annex A).</summary>
internal static class PdfASyntaxOperators
{
    /// <summary>The longest operator.</summary>
    private const int MaxLength = 3;

    /// <summary>The bits of one byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Gets the operands an operator links to the implementation limits.</summary>
    /// <param name="word">The operator.</param>
    /// <returns>The link.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfASyntaxLink LinkOf(ReadOnlySpan<byte> word)
    {
        ReadOnlySpan<int> keys =
        [
            0x127, 0x14A, 0x14D, 0x15C, 0x163, 0x164, 0x169, 0x16A,
            0x16C, 0x16D, 0x176, 0x177, 0x179, 0x24353, 0x24450, 0x24D50,
            0x25343, 0x25444, 0x2544A, 0x2544C, 0x25463, 0x25464, 0x25466, 0x2546A,
            0x2546D, 0x25473, 0x25477, 0x2547A, 0x2636D, 0x26373, 0x26430, 0x26431,
            0x27265, 0x27363, 0x3424443, 0x3424D43, 0x353434E, 0x373636E,
        ];
        ReadOnlySpan<PdfASyntaxLink> values =
        [
            PdfASyntaxLink.LastString, PdfASyntaxLink.LastInteger, PdfASyntaxLink.LastNumber,
            PdfASyntaxLink.LastString, PdfASyntaxLink.AllNumbers, PdfASyntaxLink.Dash,
            PdfASyntaxLink.LastNumber, PdfASyntaxLink.LastInteger, PdfASyntaxLink.AllNumbers,
            PdfASyntaxLink.AllNumbers, PdfASyntaxLink.AllNumbers, PdfASyntaxLink.LastNumber,
            PdfASyntaxLink.AllNumbers, PdfASyntaxLink.LastName, PdfASyntaxLink.TagAndProperties,
            PdfASyntaxLink.LastName, PdfASyntaxLink.LastName, PdfASyntaxLink.LastTwoNumbers,
            PdfASyntaxLink.LastArray, PdfASyntaxLink.LastNumber, PdfASyntaxLink.LastNumber,
            PdfASyntaxLink.LastTwoNumbers, PdfASyntaxLink.Font, PdfASyntaxLink.LastString,
            PdfASyntaxLink.AllNumbers, PdfASyntaxLink.LastNumber, PdfASyntaxLink.LastNumber,
            PdfASyntaxLink.LastNumber, PdfASyntaxLink.AllNumbers, PdfASyntaxLink.LastName,
            PdfASyntaxLink.LastTwoNumbers, PdfASyntaxLink.AllNumbers, PdfASyntaxLink.AllNumbers,
            PdfASyntaxLink.LastName, PdfASyntaxLink.TagAndProperties, PdfASyntaxLink.LastName,
            PdfASyntaxLink.LastName, PdfASyntaxLink.LastName,
        ];
        var index = keys.BinarySearch(Key(word));
        return index < 0 ? default : values[index];
    }

    /// <summary>Determines whether a word is a standard operator.</summary>
    /// <param name="word">The word.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsStandard(ReadOnlySpan<byte> word)
    {
        ReadOnlySpan<int> keys =
        [
            0x122, 0x127, 0x142, 0x146, 0x147, 0x14A, 0x14B, 0x14D,
            0x151, 0x153, 0x157, 0x162, 0x163, 0x164, 0x166, 0x167,
            0x168, 0x169, 0x16A, 0x16B, 0x16C, 0x16D, 0x16E, 0x171,
            0x173, 0x176, 0x177, 0x179, 0x2422A, 0x24249, 0x24254, 0x24258,
            0x24353, 0x24450, 0x2446F, 0x24549, 0x24554, 0x24558, 0x24944, 0x24D50,
            0x25247, 0x25343, 0x2542A, 0x25444, 0x2544A, 0x2544C, 0x25463, 0x25464,
            0x25466, 0x2546A, 0x2546D, 0x25472, 0x25473, 0x25477, 0x2547A, 0x2572A,
            0x2622A, 0x2636D, 0x26373, 0x26430, 0x26431, 0x2662A, 0x26773, 0x27265,
            0x27267, 0x27269, 0x27363, 0x27368, 0x3424443, 0x3424D43, 0x3454D43, 0x353434E,
            0x373636E,
        ];
        return keys.BinarySearch(Key(word)) >= 0;
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
