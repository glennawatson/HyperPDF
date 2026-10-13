// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Fonts.CMaps;

namespace HyperPdfLibrary.Fonts;

/// <summary>A composite font's CMap, with what its codes mean and the character collection its CIDs belong to.</summary>
[DebuggerDisplay("CompositeCMap: {Coding} {Collection}")]
internal sealed class CompositeCMap
{
    /// <summary>The first high surrogate.</summary>
    private const int HighSurrogateStart = 0xD800;

    /// <summary>The bits of a two-byte code.</summary>
    private const int UnitBits = 16;

    /// <summary>The largest two-byte code.</summary>
    private const int MaxUnit = 0xFFFF;

    /// <summary>Initializes a new instance of the <see cref="CompositeCMap"/> class.</summary>
    /// <param name="map">The CMap.</param>
    /// <param name="coding">What the codes mean.</param>
    /// <param name="collection">The character collection of a predefined CMap, else <see cref="CjkScript.None"/>.</param>
    internal CompositeCMap(CMap map, CidCoding coding, CjkScript collection)
    {
        Map = map;
        Coding = coding;
        Collection = collection;
    }

    /// <summary>Gets the CMap.</summary>
    internal CMap Map { get; }

    /// <summary>Gets what the codes mean.</summary>
    internal CidCoding Coding { get; }

    /// <summary>Gets the character collection of a predefined CMap, or <see cref="CjkScript.None"/>.</summary>
    internal CjkScript Collection { get; }

    /// <summary>Gets a value indicating whether text is written vertically.</summary>
    internal bool IsVertical => Map.IsVertical;

    /// <summary>Maps a code to a CID.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The CID, or zero when unmapped.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int ToCid(int code) => Map.ToCid(code);

    /// <summary>
    /// Gets the Unicode value of a code without /ToUnicode, from what the CMap's name says its codes are
    /// (ISO 32000-2, 9.7.5.2): the code itself for the Unicode CMaps, the CID-to-Unicode value of the code for Identity CMaps, and that of
    /// the code's CID for national encodings.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="cidToUnicode">The font's CID-to-Unicode table, or <see langword="null"/>.</param>
    /// <returns>The code point, or zero.</returns>
    internal int ToUnicode(int code, CidToUnicodeTable? cidToUnicode) => Coding switch
    {
        CidCoding.Ucs2 => code is > 0 and <= MaxUnit ? code : 0,
        CidCoding.Utf16 => FromUtf16(code),
        CidCoding.Utf32 => code > 0 && Rune.IsValid(code) ? code : 0,
        CidCoding.Cid => cidToUnicode?.Lookup(code) ?? 0,
        CidCoding.Native => cidToUnicode?.Lookup(Map.ToCid(code)) ?? 0,
        _ => 0,
    };

    /// <summary>Decodes a UTF-16 code: one unit, or a surrogate pair read as one four-byte code.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The code point, or zero.</returns>
    private static int FromUtf16(int code)
    {
        if (code is > 0 and <= MaxUnit)
        {
            return char.IsSurrogate((char)code) ? 0 : code;
        }

        var high = (char)((uint)code >> UnitBits);
        var low = (char)(code & MaxUnit);
        return high >= HighSurrogateStart && char.IsSurrogatePair(high, low) ? char.ConvertToUtf32(high, low) : 0;
    }
}
