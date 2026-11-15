// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Text;

/// <summary>
/// Knows which font file names are likely to have the characters of a script. Font families name their script, as
/// "NotoSansThai" does, and each system ships a few well known fonts for each script, so the right file is read first
/// instead of every font on the machine.
/// </summary>
internal static class ScriptFontNames
{
    /// <summary>A part of font file names: Nirmala.</summary>
    private const string NirmalaName = "Nirmala";

    /// <summary>A part of font file names: segoeui.</summary>
    private const string SegoeuiName = "segoeui";

    /// <summary>A part of font file names: DejaVuSans.</summary>
    private const string DejaVuSansName = "DejaVuSans";

    /// <summary>A part of font file names: Leelaw.</summary>
    private const string LeelawName = "Leelaw";

    /// <summary>A part of font file names: SansCJK.</summary>
    private const string SansCJKName = "SansCJK";

    /// <summary>A part of font file names: CJK.</summary>
    private const string CJKName = "CJK";

    /// <summary>A part of font file names: tahoma.</summary>
    private const string TahomaName = "tahoma";

    /// <summary>A part of font file names: meiryo.</summary>
    private const string MeiryoName = "meiryo";

    /// <summary>A part of font file names: malgun.</summary>
    private const string MalgunName = "malgun";

    /// <summary>A part of font file names: msyh.</summary>
    private const string MsyhName = "msyh";

    /// <summary>The first code point of each range, in rising order.</summary>
    private static readonly int[] Starts =
    [
        0x0530, 0x0590, 0x0600, 0x0700, 0x0900, 0x0980, 0x0A00, 0x0A80, 0x0B00, 0x0B80, 0x0C00, 0x0C80, 0x0D00, 0x0D80, 0x0E00, 0x0E80,
        0x0F00, 0x1000, 0x10A0, 0x1100, 0x1200, 0x1780, 0x2E80, 0x3040, 0x3100, 0x3130, 0x3190, 0xAC00, 0xD7B0, 0xFB1D, 0xFB50, 0xFE70, 0xFF00, 0x20000,
    ];

    /// <summary>The last code point of each range.</summary>
    private static readonly int[] Ends =
    [
        0x058F, 0x05FF, 0x06FF, 0x08FF, 0x097F, 0x09FF, 0x0A7F, 0x0AFF, 0x0B7F, 0x0BFF, 0x0C7F, 0x0CFF, 0x0D7F, 0x0DFF, 0x0E7F, 0x0EFF,
        0x0FFF, 0x109F, 0x10FF, 0x11FF, 0x139F, 0x17FF, 0x303F, 0x30FF, 0x312F, 0x318F, 0xABFF, 0xD7AF, 0xFB1C, 0xFB4F, 0xFDFF, 0xFEFF, 0xFFEF, 0x3FFFF,
    ];

    /// <summary>The parts of font file names to look for in each range, likeliest first.</summary>
    private static readonly string[][] Names =
    [
        ["Armenian", SegoeuiName, DejaVuSansName],
        Hebrew,
        Arabic,
        Arabic,
        ["Devanagari", NirmalaName, "Kohinoor", "Mangal"],
        ["Bengali", NirmalaName, "Vrinda", "Kohinoor"],
        ["Gurmukhi", NirmalaName, "Raavi"],
        ["Gujarati", NirmalaName, "Shruti"],
        ["Oriya", NirmalaName, "Kalinga"],
        ["Tamil", NirmalaName, "Latha"],
        ["Telugu", NirmalaName, "Gautami"],
        ["Kannada", NirmalaName, "Tunga"],
        ["Malayalam", NirmalaName, "Kartika"],
        ["Sinhala", NirmalaName, "Iskoola"],
        ["Thai", LeelawName, TahomaName, "Thonburi", "Loma", "Garuda"],
        ["Lao", LeelawName, "DokChampa"],
        ["Tibetan", "himalaya", "Kailasa"],
        ["Myanmar", "mmrtext", "Padauk"],
        ["Georgian", SegoeuiName, DejaVuSansName],
        Korean,
        ["Ethiopic", "ebrima", "Kefa"],
        ["Khmer", LeelawName, "DaunPenh"],
        Chinese,
        Japanese,
        Chinese,
        Korean,
        Chinese,
        Korean,
        Chinese,
        Hebrew,
        Arabic,
        Arabic,
        Chinese,
        Chinese,
    ];

    /// <summary>Gets the names for Hebrew.</summary>
    private static string[] Hebrew => ["Hebrew", SegoeuiName, "arial", DejaVuSansName, "Lucida"];

    /// <summary>Gets the names for Arabic and the scripts written with it.</summary>
    private static string[] Arabic => ["Arabic", "Naskh", "Kufi", SegoeuiName, TahomaName, "GeezaPro", DejaVuSansName];

    /// <summary>Gets the names for Chinese, which the fonts for Japanese and Korean also cover.</summary>
    private static string[] Chinese => [SansCJKName, CJKName, MsyhName, "PingFang", "simsun", "msjh", "Hiragino", "WenQuanYi", "wqy", MeiryoName, MalgunName];

    /// <summary>Gets the names for Japanese kana.</summary>
    private static string[] Japanese => [SansCJKName, CJKName, MeiryoName, "YuGoth", "Hiragino", "msgothic", MsyhName];

    /// <summary>Gets the names for Korean.</summary>
    private static string[] Korean => [SansCJKName, CJKName, MalgunName, "AppleSDGothic", "gulim"];

    /// <summary>Gets the parts of font file names likely to have a character.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns>The name parts, likeliest first, or none for a character outside the scripts known here.</returns>
    internal static string[] For(int codePoint)
    {
        var index = Array.BinarySearch(Starts, codePoint);
        if (index < 0)
        {
            // Not the start of a range, so the range before the place it would go is the only one that can hold it.
            index = ~index - 1;
        }

        return index >= 0 && codePoint <= Ends[index] ? Names[index] : [];
    }
}
