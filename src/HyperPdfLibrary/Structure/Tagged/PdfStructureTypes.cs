// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Looks up standard structure types by spelling and says what each one means.</summary>
public static class PdfStructureTypes
{
    /// <summary>The standard structure namespace of PDF 1.7.</summary>
    internal const string Pdf17Uri = "http://iso.org/pdf/ssn";

    /// <summary>The standard structure namespace of PDF 2.0.</summary>
    internal const string Pdf20Uri = "http://iso.org/pdf2/ssn";

    /// <summary>The flag of a type that is standard in PDF 1.7.</summary>
    internal const int Pdf17Flag = 1;

    /// <summary>The flag of a type that is standard in PDF 2.0.</summary>
    internal const int Pdf20Flag = 2;

    /// <summary>Both standard namespaces, used for elements that name no namespace.</summary>
    internal const int AnyStandardFlag = Pdf17Flag | Pdf20Flag;

    /// <summary>The flag of a type that only groups other elements.</summary>
    private const int GroupingFlag = 4;

    /// <summary>The longest spelling looked up; standard types are a few characters.</summary>
    private const int MaxSpelling = 32;

    /// <summary>The deepest heading level.</summary>
    private const int MaxHeadingLevel = 6;

    /// <summary>The longest <c>Hn</c> spelling read: H and up to three digits.</summary>
    private const int MaxHeadingSpelling = 4;

    /// <summary>The base of decimal digits.</summary>
    private const int Decimal = 10;

    /// <summary>The standard types by spelling.</summary>
    private static readonly FrozenDictionary<string, PdfStructureType> Spellings = new Dictionary<string, PdfStructureType>(StringComparer.Ordinal)
    {
        ["Document"] = PdfStructureType.Document,
        ["DocumentFragment"] = PdfStructureType.DocumentFragment,
        ["Part"] = PdfStructureType.Part,
        ["Art"] = PdfStructureType.Article,
        ["Sect"] = PdfStructureType.Section,
        ["Div"] = PdfStructureType.Division,
        ["Aside"] = PdfStructureType.Aside,
        ["NonStruct"] = PdfStructureType.NonStructural,
        ["Private"] = PdfStructureType.Private,
        ["BlockQuote"] = PdfStructureType.BlockQuote,
        ["Caption"] = PdfStructureType.Caption,
        ["TOC"] = PdfStructureType.TableOfContents,
        ["TOCI"] = PdfStructureType.TableOfContentsItem,
        ["Index"] = PdfStructureType.Index,
        ["P"] = PdfStructureType.Paragraph,
        ["H"] = PdfStructureType.Heading,
        ["Title"] = PdfStructureType.Title,
        ["L"] = PdfStructureType.List,
        ["LI"] = PdfStructureType.ListItem,
        ["Lbl"] = PdfStructureType.Label,
        ["LBody"] = PdfStructureType.ListBody,
        ["Table"] = PdfStructureType.Table,
        ["TR"] = PdfStructureType.TableRow,
        ["TH"] = PdfStructureType.TableHeaderCell,
        ["TD"] = PdfStructureType.TableDataCell,
        ["THead"] = PdfStructureType.TableHead,
        ["TBody"] = PdfStructureType.TableBody,
        ["TFoot"] = PdfStructureType.TableFoot,
        ["Span"] = PdfStructureType.Span,
        ["Quote"] = PdfStructureType.Quote,
        ["Note"] = PdfStructureType.Note,
        ["FENote"] = PdfStructureType.FootnoteOrEndnote,
        ["Reference"] = PdfStructureType.Reference,
        ["BibEntry"] = PdfStructureType.BibliographyEntry,
        ["Code"] = PdfStructureType.Code,
        ["Link"] = PdfStructureType.Link,
        ["Annot"] = PdfStructureType.Annotation,
        ["Ruby"] = PdfStructureType.Ruby,
        ["RB"] = PdfStructureType.RubyBase,
        ["RT"] = PdfStructureType.RubyText,
        ["RP"] = PdfStructureType.RubyPunctuation,
        ["Warichu"] = PdfStructureType.Warichu,
        ["WT"] = PdfStructureType.WarichuText,
        ["WP"] = PdfStructureType.WarichuPunctuation,
        ["Figure"] = PdfStructureType.Figure,
        ["Formula"] = PdfStructureType.Formula,
        ["Form"] = PdfStructureType.Form,
        ["Em"] = PdfStructureType.Emphasis,
        ["Strong"] = PdfStructureType.Strong,
        ["Sub"] = PdfStructureType.Subdivision,
        ["Artifact"] = PdfStructureType.Artifact,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Looks the standard types up by a span of characters, so lookups allocate nothing.</summary>
    private static readonly FrozenDictionary<string, PdfStructureType>.AlternateLookup<ReadOnlySpan<char>> Lookup = Spellings.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>Gets the standard structure namespace of PDF 1.7.</summary>
    public static string Pdf17Namespace => Pdf17Uri;

    /// <summary>Gets the standard structure namespace of PDF 2.0.</summary>
    public static string Pdf20Namespace => Pdf20Uri;

    /// <summary>Gets each type's flags, indexed by <see cref="PdfStructureType"/>: PDF 1.7 (1), PDF 2.0 (2) and grouping (4).</summary>
    private static ReadOnlySpan<byte> Flags =>
    [
        0x00, 0x07, 0x06, 0x07, 0x05, 0x07, 0x07, 0x06,
        0x07, 0x05, 0x01, 0x03, 0x05, 0x01, 0x05, 0x03,
        0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x02,
        0x07, 0x03, 0x03, 0x03, 0x07, 0x07, 0x03, 0x03,
        0x07, 0x07, 0x07, 0x03, 0x01, 0x01, 0x02, 0x01,
        0x01, 0x01, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03,
        0x03, 0x03, 0x03, 0x03, 0x03, 0x07, 0x02, 0x02,
        0x02, 0x02,
    ];

    /// <summary>Gets each type's <see cref="PdfSemanticRole"/>, indexed by <see cref="PdfStructureType"/>.</summary>
    private static ReadOnlySpan<byte> Roles =>
    [
        0x00, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
        0x01, 0x01, 0x12, 0x14, 0x15, 0x16, 0x15, 0x03,
        0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02,
        0x04, 0x05, 0x06, 0x07, 0x08, 0x0A, 0x0B, 0x0C,
        0x09, 0x09, 0x09, 0x17, 0x12, 0x11, 0x11, 0x17,
        0x03, 0x13, 0x0F, 0x18, 0x17, 0x17, 0x17, 0x17,
        0x17, 0x17, 0x17, 0x0D, 0x0E, 0x10, 0x17, 0x17,
        0x17, 0x00,
    ];

    /// <summary>Finds the standard type a spelling names, in either standard namespace.</summary>
    /// <param name="spelling">The type's bytes, as in a <c>/S</c> name.</param>
    /// <returns>The type, or <see cref="PdfStructureType.Unknown"/>.</returns>
    public static PdfStructureType Find(ReadOnlySpan<byte> spelling)
    {
        if (spelling.IsEmpty || spelling.Length > MaxSpelling)
        {
            return PdfStructureType.Unknown;
        }

        Span<char> characters = stackalloc char[MaxSpelling];
        for (var i = 0; i < spelling.Length; i++)
        {
            characters[i] = (char)spelling[i];
        }

        var text = characters[..spelling.Length];
        return Lookup.TryGetValue(text, out var type) ? type : FindNumberedHeading(spelling);
    }

    /// <summary>Gets what a type means to a reader.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The role.</returns>
    public static PdfSemanticRole GetRole(PdfStructureType type) =>
        (uint)type < (uint)Roles.Length ? (PdfSemanticRole)Roles[(int)type] : PdfSemanticRole.Unknown;

    /// <summary>Gets a heading type's level, 1 to 6; <c>H</c> and <c>Title</c> count as level 1, the top heading.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The level, or zero for a type that is not a heading.</returns>
    public static int GetHeadingLevel(PdfStructureType type) => type switch
    {
        PdfStructureType.Heading or PdfStructureType.Title => 1,
        >= PdfStructureType.Heading1 and <= PdfStructureType.Heading6 => type - PdfStructureType.Heading1 + 1,
        _ => 0,
    };

    /// <summary>Determines whether a type only groups other elements and carries no content meaning of its own.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for a grouping type.</returns>
    public static bool IsGrouping(PdfStructureType type) => (GetFlags(type) & GroupingFlag) != 0;

    /// <summary>Determines whether a type is standard in the given namespaces.</summary>
    /// <param name="type">The type.</param>
    /// <param name="namespaces">The namespace flags: PDF 1.7 (1), PDF 2.0 (2) or both.</param>
    /// <returns><see langword="true"/> when the type is standard there.</returns>
    internal static bool IsStandardIn(PdfStructureType type, int namespaces) => (GetFlags(type) & namespaces & AnyStandardFlag) != 0;

    /// <summary>Gets the flags of a namespace URI.</summary>
    /// <param name="uri">The namespace's <c>/NS</c> text.</param>
    /// <returns>The standard namespace flag, or zero for any other namespace.</returns>
    internal static int GetNamespaceFlags(string? uri) => uri switch
    {
        null => AnyStandardFlag,
        Pdf17Uri => Pdf17Flag,
        Pdf20Uri => Pdf20Flag,
        _ => 0,
    };

    /// <summary>Gets a type's flags.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The flags.</returns>
    private static int GetFlags(PdfStructureType type) => (uint)type < (uint)Flags.Length ? Flags[(int)type] : 0;

    /// <summary>Reads a PDF 2.0 heading <c>Hn</c> deeper than H6 as a level 6 heading.</summary>
    /// <param name="spelling">The type's bytes.</param>
    /// <returns>A heading type, or <see cref="PdfStructureType.Unknown"/>.</returns>
    private static PdfStructureType FindNumberedHeading(ReadOnlySpan<byte> spelling)
    {
        if (spelling.Length < 2 || spelling.Length > MaxHeadingSpelling || spelling[0] != (byte)'H')
        {
            return PdfStructureType.Unknown;
        }

        var level = 0;
        foreach (var digit in spelling[1..])
        {
            if (digit is < (byte)'0' or > (byte)'9')
            {
                return PdfStructureType.Unknown;
            }

            level = (level * Decimal) + (digit - '0');
        }

        return level <= 0 ? PdfStructureType.Unknown : PdfStructureType.Heading1 + (Math.Min(level, MaxHeadingLevel) - 1);
    }
}
