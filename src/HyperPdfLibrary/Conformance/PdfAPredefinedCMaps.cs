// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The predefined CMaps PDF/A allows a composite font to name, with the character collection each uses in PDF 1.4, PDF
/// 1.7 and PDF 2.0 (ISO 32000-1, 9.7.5.2, Table 118; ISO 32000-2, Table 116). PDF 1.4 does not define the UTF-16 CMaps.
/// </summary>
internal static class PdfAPredefinedCMaps
{
    /// <summary>The registry of every predefined character collection.</summary>
    internal const string Registry = "Adobe";

    /// <summary>The length of a 1-character CMap name.</summary>
    private const int NameLength1 = 1;

    /// <summary>The length of a 5-character CMap name.</summary>
    private const int NameLength5 = 5;

    /// <summary>The length of a 6-character CMap name.</summary>
    private const int NameLength6 = 6;

    /// <summary>The length of a 7-character CMap name.</summary>
    private const int NameLength7 = 7;

    /// <summary>The length of a 8-character CMap name.</summary>
    private const int NameLength8 = 8;

    /// <summary>The length of a 9-character CMap name.</summary>
    private const int NameLength9 = 9;

    /// <summary>The length of a 10-character CMap name.</summary>
    private const int NameLength10 = 10;

    /// <summary>The length of a 11-character CMap name.</summary>
    private const int NameLength11 = 11;

    /// <summary>The length of a 12-character CMap name.</summary>
    private const int NameLength12 = 12;

    /// <summary>The length of a 13-character CMap name.</summary>
    private const int NameLength13 = 13;

    /// <summary>The length of a 14-character CMap name.</summary>
    private const int NameLength14 = 14;

    /// <summary>The length of a 16-character CMap name.</summary>
    private const int NameLength16 = 16;

    /// <summary>The SimplifiedChinese character collection.</summary>
    private const string SimplifiedChinese = "GB1";

    /// <summary>Character collection supplement 5.</summary>
    private const int Supplement5 = 5;

    /// <summary>Character collection supplement 2.</summary>
    private const int Supplement2 = 2;

    /// <summary>Character collection supplement 4.</summary>
    private const int Supplement4 = 4;

    /// <summary>The TraditionalChinese character collection.</summary>
    private const string TraditionalChinese = "CNS1";

    /// <summary>Character collection supplement 7.</summary>
    private const int Supplement7 = 7;

    /// <summary>Character collection supplement 3.</summary>
    private const int Supplement3 = 3;

    /// <summary>The Japanese character collection.</summary>
    private const string Japanese = "Japan1";

    /// <summary>Character collection supplement 9.</summary>
    private const int Supplement9 = 9;

    /// <summary>The PDF/A part based on PDF 1.4.</summary>
    private const int Part1 = 1;

    /// <summary>The PDF/A part based on PDF 2.0.</summary>
    private const int Part4 = 4;

    /// <summary>The PDF 2.0 ordering of the Korean collection, which replaced Korea1.</summary>
    private const string Pdf20Korean = "KR";

    /// <summary>The ordering of the Korean collection before PDF 2.0.</summary>
    private const string Korean = "Korea1";

    /// <summary>Determines whether a name is a predefined CMap.</summary>
    /// <param name="name">The CMap name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsPredefined(string name) => FindRow(name) is not null;

    /// <summary>Gets the character collection a predefined CMap uses for a PDF/A part.</summary>
    /// <param name="name">The CMap name.</param>
    /// <param name="part">The PDF/A part: 1 reads PDF 1.4, 4 reads PDF 2.0, the others PDF 1.7.</param>
    /// <returns>The collection, or null when the name is not predefined in that version.</returns>
    internal static PdfACMapCollection? Collection(string name, int part)
    {
        if (FindRow(name) is not { } row)
        {
            return null;
        }

        var supplement = part switch
        {
            Part1 => row.Pdf14,
            Part4 => row.Pdf20,
            _ => row.Pdf17,
        };
        if (supplement < 0)
        {
            return null;
        }

        var ordering = part == Part4 && row.Ordering == Korean ? Pdf20Korean : row.Ordering;
        return new(ordering, supplement);
    }

    /// <summary>Gets the fixed collection row for a CMap.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRow(string name) => name.Length <= NameLength10 ? FindShortRow(name) : FindLongRow(name);

    /// <summary>Selects a CMap row by exact name length.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindShortRow(string name) => name.Length switch
    {
        NameLength1 => FindRowLength1(name),
        NameLength5 => FindRowLength5(name),
        NameLength6 => FindRowLength6(name),
        NameLength7 => FindRowLength7(name),
        NameLength8 => FindRowLength8(name),
        NameLength9 => FindRowLength9(name),
        NameLength10 => FindRowLength10(name),
        _ => null,
    };

    /// <summary>Selects a CMap row by exact name length.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindLongRow(string name) => name.Length switch
    {
        NameLength11 => FindRowLength11(name),
        NameLength12 => FindRowLength12(name),
        NameLength13 => FindRowLength13(name),
        NameLength14 => FindRowLength14(name),
        NameLength16 => FindRowLength16(name),
        _ => null,
    };

    /// <summary>Matches an exact CMap name.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRowLength1(string name) => name switch
    {
        "H" or "V" => new(Japanese, 1, 1, Supplement7),
        _ => null,
    };

    /// <summary>Matches an exact CMap name.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRowLength5(string name) => name switch
    {
        "EUC-H" or "EUC-V" => new(Japanese, 1, 1, Supplement7),
        _ => null,
    };

    /// <summary>Matches an exact CMap name.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRowLength6(string name) => name switch
    {
        "B5pc-H" or "B5pc-V" => new(TraditionalChinese, 0, 0, Supplement7),
        _ => null,
    };

    /// <summary>Matches an exact CMap name.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRowLength7(string name) => name switch
    {
        "GBK2K-H" or "GBK2K-V" => new(SimplifiedChinese, Supplement4, Supplement4, Supplement5),
        _ => null,
    };

    /// <summary>Matches an exact CMap name.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRowLength8(string name) => name switch
    {
        "GB-EUC-H" or "GB-EUC-V" => new(SimplifiedChinese, 0, 0, Supplement5),
        _ => null,
    };

    /// <summary>Matches an exact CMap name.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRowLength9(string name) => name switch
    {
        "GBK-EUC-H" or "GBK-EUC-V" => new(SimplifiedChinese, Supplement2, Supplement2, Supplement5),
        "ETen-B5-H" or "ETen-B5-V" or "CNS-EUC-H" or "CNS-EUC-V" => new(TraditionalChinese, 0, 0, Supplement7),
        "KSC-EUC-H" or "KSC-EUC-V" => new(Korean, 0, 0, Supplement9),
        _ => null,
    };

    /// <summary>Matches a CMap name within its length group.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRowLength10(string name) => FindRowLength10Part1(name) ?? FindRowLength10Part2(name);

    /// <summary>Matches an exact CMap name.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRowLength10Part1(string name) => name switch
    {
        "GBpc-EUC-H" or "GBpc-EUC-V" => new(SimplifiedChinese, 0, 0, Supplement5),
        "GBKp-EUC-H" or "GBKp-EUC-V" => new(SimplifiedChinese, Supplement2, Supplement2, Supplement5),
        "HKscs-B5-H" or "HKscs-B5-V" => new(TraditionalChinese, Supplement3, Supplement3, Supplement7),
        "Add-RKSJ-H" or "Add-RKSJ-V" => new(Japanese, 1, 1, Supplement7),
        _ => null,
    };

    /// <summary>Matches an exact CMap name.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRowLength10Part2(string name) => name switch
    {
        "Ext-RKSJ-H" or "Ext-RKSJ-V" => new(Japanese, Supplement2, Supplement2, Supplement7),
        "Identity-H" or "Identity-V" => new("Identity", 0, 0, 0),
        _ => null,
    };

    /// <summary>Matches a CMap name within its length group.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRowLength11(string name) => FindRowLength11Part1(name) ?? FindRowLength11Part2(name);

    /// <summary>Matches an exact CMap name.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRowLength11Part1(string name) => name switch
    {
        "ETenms-B5-H" or "ETenms-B5-V" => new(TraditionalChinese, 0, 0, Supplement7),
        "83pv-RKSJ-H" or "90pv-RKSJ-H" => new(Japanese, 1, 1, Supplement7),
        "90ms-RKSJ-H" or "90ms-RKSJ-V" => new(Japanese, Supplement2, Supplement2, Supplement7),
        "KSCpc-EUC-H" => new(Korean, 0, 0, Supplement9),
        "KSCms-UHC-H" => new(Korean, 1, 1, Supplement9),
        _ => null,
    };

    /// <summary>Matches an exact CMap name.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRowLength11Part2(string name) => name switch
    {
        "KSCms-UHC-V" => new(Korean, 1, 1, Supplement9),
        _ => null,
    };

    /// <summary>Matches an exact CMap name.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRowLength12(string name) => name switch
    {
        "UniGB-UCS2-H" or "UniGB-UCS2-V" => new(SimplifiedChinese, Supplement4, Supplement4, Supplement5),
        "90msp-RKSJ-H" or "90msp-RKSJ-V" => new(Japanese, Supplement2, Supplement2, Supplement7),
        "UniKS-UCS2-H" or "UniKS-UCS2-V" => new(Korean, 1, 1, Supplement9),
        _ => null,
    };

    /// <summary>Matches an exact CMap name.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRowLength13(string name) => name switch
    {
        "UniGB-UTF16-H" or "UniGB-UTF16-V" => new(SimplifiedChinese, -1, Supplement4, Supplement5),
        "UniCNS-UCS2-H" or "UniCNS-UCS2-V" => new(TraditionalChinese, Supplement3, Supplement3, Supplement7),
        "UniJIS-UCS2-H" or "UniJIS-UCS2-V" => new(Japanese, Supplement4, Supplement4, Supplement7),
        "UniKS-UTF16-H" or "UniKS-UTF16-V" => new(Korean, -1, Supplement2, Supplement9),
        _ => null,
    };

    /// <summary>Matches an exact CMap name.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRowLength14(string name) => name switch
    {
        "UniCNS-UTF16-H" or "UniCNS-UTF16-V" => new(TraditionalChinese, -1, Supplement4, Supplement7),
        "UniJIS-UTF16-H" or "UniJIS-UTF16-V" => new(Japanese, -1, Supplement5, Supplement7),
        "KSCms-UHC-HW-H" or "KSCms-UHC-HW-V" => new(Korean, 1, 1, Supplement9),
        _ => null,
    };

    /// <summary>Matches an exact CMap name.</summary>
    /// <param name="name">The case-sensitive CMap name.</param>
    /// <returns>The row, or null for an unknown name.</returns>
    private static CollectionRow? FindRowLength16(string name) => name switch
    {
        "UniJIS-UCS2-HW-H" or "UniJIS-UCS2-HW-V" => new(Japanese, Supplement4, Supplement4, Supplement7),
        _ => null,
    };

    /// <summary>A predefined collection's supplements in the three PDF editions.</summary>
    /// <param name="Ordering">The collection name.</param>
    /// <param name="Pdf14">The PDF 1.4 supplement, or -1 when absent.</param>
    /// <param name="Pdf17">The PDF 1.7 supplement.</param>
    /// <param name="Pdf20">The PDF 2.0 supplement.</param>
    private readonly record struct CollectionRow(string Ordering, int Pdf14, int Pdf17, int Pdf20);
}
