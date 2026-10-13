// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>Finds the bidirectional type of a character from its script range and general category (Unicode Standard Annex #9, 3.2).</summary>
internal static class BidiClasses
{
    /// <summary>The number of characters in the Basic Latin block.</summary>
    private const int AsciiLength = 128;

    /// <summary>The types of the Basic Latin characters.</summary>
    private static readonly BidiClass[] Ascii = BuildAscii();

    /// <summary>The types of characters beyond ASCII by general category.</summary>
    private static readonly BidiClass[] CategoryTypes = BuildCategoryTypes();

    /// <summary>Characters beyond ASCII whose type is not what their block or category suggests, tried first.</summary>
    private static readonly BidiRange[] Exceptions =
    [
        new(0x00A0, 0x00A0, BidiClass.CS),
        new(0x060C, 0x060C, BidiClass.CS),
        new(0x061C, 0x061C, BidiClass.AL),
        new(0x0660, 0x0669, BidiClass.AN),
        new(0x066B, 0x066C, BidiClass.AN),
        new(0x06F0, 0x06F9, BidiClass.EN),
        new(0x200E, 0x200E, BidiClass.L),
        new(0x200F, 0x200F, BidiClass.R),
    ];

    /// <summary>Blocks of letters that read right to left as <see cref="BidiClass.R"/>.</summary>
    private static readonly BidiRange[] HebrewBlocks =
    [
        new(0x0590, 0x05FF, BidiClass.R),
        new(0x07C0, 0x085F, BidiClass.R),
        new(0xFB1D, 0xFB4F, BidiClass.R),
        new(0x10800, 0x10FFF, BidiClass.R),
    ];

    /// <summary>Blocks of letters that read right to left as <see cref="BidiClass.AL"/>.</summary>
    private static readonly BidiRange[] ArabicBlocks =
    [
        new(0x0600, 0x07BF, BidiClass.AL),
        new(0x0860, 0x08FF, BidiClass.AL),
        new(0xFB50, 0xFDFF, BidiClass.AL),
        new(0xFE70, 0xFEFF, BidiClass.AL),
    ];

    /// <summary>Gets the type of a character.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns>The type.</returns>
    internal static BidiClass Of(int codePoint) =>
        codePoint < AsciiLength ? Ascii[codePoint] : Find(Exceptions, codePoint) ?? OfBlockOrCategory(codePoint);

    /// <summary>Gets the type of a character beyond ASCII that is not an exception: marks are non-spacing, Hebrew blocks read as R, and Arabic blocks give AL to letters.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns>The type.</returns>
    private static BidiClass OfBlockOrCategory(int codePoint)
    {
        var category = CharUnicodeInfo.GetUnicodeCategory(codePoint);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark)
        {
            return BidiClass.NSM;
        }

        var arabic = Find(ArabicBlocks, codePoint);
        var letterType = IsLetter(category) ? BidiClass.AL : BidiClass.ON;
        return Find(HebrewBlocks, codePoint) ?? (arabic is null ? CategoryTypes[(int)category] : letterType);
    }

    /// <summary>Builds the types of the Basic Latin characters.</summary>
    /// <returns>The types, by character.</returns>
    private static BidiClass[] BuildAscii()
    {
        var table = new BidiClass[AsciiLength];
        Array.Fill(table, BidiClass.ON);
        for (var c = 0; c < AsciiLength; c++)
        {
            table[c] = char.IsControl((char)c) ? BidiClass.BN : table[c];
        }

        SetRange(table, '0', '9', BidiClass.EN);
        SetRange(table, 'A', 'Z', BidiClass.L);
        SetRange(table, 'a', 'z', BidiClass.L);
        SetAll(table, "+-", BidiClass.ES);
        SetAll(table, "#$%", BidiClass.ET);
        SetAll(table, ",./:", BidiClass.CS);
        SetAll(table, "\t\u000B\u001F", BidiClass.S);
        SetAll(table, "\n\r\u001C\u001D\u001E", BidiClass.B);
        SetAll(table, " \f", BidiClass.WS);
        return table;
    }

    /// <summary>Gives a run of ASCII characters a type.</summary>
    /// <param name="table">The table.</param>
    /// <param name="first">The first character.</param>
    /// <param name="last">The last character.</param>
    /// <param name="type">The type.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetRange(BidiClass[] table, char first, char last, BidiClass type) => Array.Fill(table, type, first, last - first + 1);

    /// <summary>Gives some ASCII characters a type.</summary>
    /// <param name="table">The table.</param>
    /// <param name="characters">The characters.</param>
    /// <param name="type">The type.</param>
    private static void SetAll(BidiClass[] table, string characters, BidiClass type)
    {
        foreach (var c in characters)
        {
            table[c] = type;
        }
    }

    /// <summary>Finds the type a table gives a character.</summary>
    /// <param name="ranges">The table.</param>
    /// <param name="codePoint">The character.</param>
    /// <returns>The type, or <see langword="null"/> when no range holds the character.</returns>
    private static BidiClass? Find(BidiRange[] ranges, int codePoint)
    {
        foreach (var range in ranges)
        {
            if (codePoint >= range.Start && codePoint <= range.End)
            {
                return range.Type;
            }
        }

        return null;
    }

    /// <summary>Determines whether a general category is a kind of letter.</summary>
    /// <param name="category">The category.</param>
    /// <returns><see langword="true"/> for letters.</returns>
    private static bool IsLetter(UnicodeCategory category) =>
        category is UnicodeCategory.OtherLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.UppercaseLetter;

    /// <summary>
    /// Builds the type a character beyond ASCII has by default for each general category: letters and digits of other scripts
    /// read left to right, and punctuation and symbols are neutral.
    /// </summary>
    /// <returns>The types, by category.</returns>
    private static BidiClass[] BuildCategoryTypes()
    {
        var table = new BidiClass[Enum.GetValues<UnicodeCategory>().Length];
        UnicodeCategory[] neutral =
        [
            UnicodeCategory.OtherPunctuation,
            UnicodeCategory.MathSymbol,
            UnicodeCategory.OtherSymbol,
            UnicodeCategory.ModifierSymbol,
            UnicodeCategory.OpenPunctuation,
            UnicodeCategory.ClosePunctuation,
            UnicodeCategory.InitialQuotePunctuation,
            UnicodeCategory.FinalQuotePunctuation,
            UnicodeCategory.DashPunctuation,
            UnicodeCategory.ConnectorPunctuation,
        ];
        foreach (var category in neutral)
        {
            table[(int)category] = BidiClass.ON;
        }

        table[(int)UnicodeCategory.SpaceSeparator] = BidiClass.WS;
        table[(int)UnicodeCategory.LineSeparator] = BidiClass.B;
        table[(int)UnicodeCategory.ParagraphSeparator] = BidiClass.B;
        table[(int)UnicodeCategory.Format] = BidiClass.BN;
        table[(int)UnicodeCategory.Control] = BidiClass.BN;
        table[(int)UnicodeCategory.CurrencySymbol] = BidiClass.ET;
        return table;
    }

    /// <summary>A range of characters that share a type.</summary>
    /// <param name="Start">The first character.</param>
    /// <param name="End">The last character.</param>
    /// <param name="Type">The type.</param>
    private readonly record struct BidiRange(int Start, int End, BidiClass Type);
}
