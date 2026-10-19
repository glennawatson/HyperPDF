// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Looks up standard structure types by spelling and says what each one means.</summary>
public static class PdfStructureTypes
{
    /// <summary>The standard structure namespace of PDF 1.7.</summary>
    internal const string Pdf17Uri = "http://iso.org/pdf/ssn";

    /// <summary>The standard structure namespace of PDF 2.0.</summary>
    internal const string Pdf20Uri = "http://iso.org/pdf2/ssn";

    /// <summary>The MathML 3 namespace, whose element names a PDF 2.0 file may use as structure types (ISO 32000-2 14.8.6).</summary>
    internal const string MathMlUri = "http://www.w3.org/1998/Math/MathML";

    /// <summary>The flag of a type that is standard in PDF 1.7.</summary>
    internal const int Pdf17Flag = 1;

    /// <summary>The flag of a type that is standard in PDF 2.0.</summary>
    internal const int Pdf20Flag = 2;

    /// <summary>Both standard namespaces, used for elements that name no namespace.</summary>
    internal const int AnyStandardFlag = Pdf17Flag | Pdf20Flag;

    /// <summary>The flag of a type that only groups other elements.</summary>
    private const int GroupingFlag = 4;

    /// <summary>The longest fixed spelling looked up; Hn names have no fixed length limit.</summary>
    private const int MaxSpelling = 32;

    /// <summary>The byte length of a standard spelling group.</summary>
    private const int SpellingLength2 = 2;

    /// <summary>The byte length of a standard spelling group.</summary>
    private const int SpellingLength3 = 3;

    /// <summary>The byte length of a standard spelling group.</summary>
    private const int SpellingLength4 = 4;

    /// <summary>The byte length of a standard spelling group.</summary>
    private const int SpellingLength5 = 5;

    /// <summary>The byte length of a standard spelling group.</summary>
    private const int SpellingLength6 = 6;

    /// <summary>The byte length of a standard spelling group.</summary>
    private const int SpellingLength7 = 7;

    /// <summary>The byte length of a standard spelling group.</summary>
    private const int SpellingLength8 = 8;

    /// <summary>The byte length of a standard spelling group.</summary>
    private const int SpellingLength9 = 9;

    /// <summary>The byte length of a standard spelling group.</summary>
    private const int SpellingLength10 = 10;

    /// <summary>The byte length of a standard spelling group.</summary>
    private const int SpellingLength16 = 16;

    /// <summary>The standard type names of the PDF 1.7 namespace, in the order of <see cref="PdfStructureType"/>.</summary>
    private static readonly string[] Pdf17Names = BuildNames(Pdf17Flag);

    /// <summary>The standard type names of the PDF 2.0 namespace, in the order of <see cref="PdfStructureType"/>.</summary>
    private static readonly string[] Pdf20Names = BuildNames(Pdf20Flag);

    /// <summary>Gets the standard structure namespace of PDF 1.7.</summary>
    public static string Pdf17Namespace => Pdf17Uri;

    /// <summary>Gets the standard structure namespace of PDF 2.0.</summary>
    public static string Pdf20Namespace => Pdf20Uri;

    /// <summary>Gets each type's flags, indexed by <see cref="PdfStructureType"/>: PDF 1.7 (1), PDF 2.0 (2) and grouping (4).</summary>
    private static ReadOnlySpan<byte> Flags =>
    [
        0x00, 0x07, 0x06, 0x07, 0x05, 0x07, 0x07, 0x06, 0x07, 0x05, 0x01, 0x03,
        0x05, 0x01, 0x05, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x02,
        0x07, 0x03, 0x03, 0x03, 0x07, 0x07, 0x03, 0x03, 0x07, 0x07, 0x07, 0x03,
        0x01, 0x01, 0x02, 0x01, 0x01, 0x01, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03,
        0x03, 0x03, 0x03, 0x03, 0x03, 0x07, 0x02, 0x02, 0x02, 0x02, 0x02,
    ];

    /// <summary>Gets each type's <see cref="PdfSemanticRole"/>, indexed by <see cref="PdfStructureType"/>.</summary>
    private static ReadOnlySpan<byte> Roles =>
    [
        0x00, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x12, 0x14,
        0x15, 0x16, 0x15, 0x03, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02,
        0x04, 0x05, 0x06, 0x07, 0x08, 0x0A, 0x0B, 0x0C, 0x09, 0x09, 0x09, 0x17,
        0x12, 0x11, 0x11, 0x17, 0x03, 0x13, 0x0F, 0x18, 0x17, 0x17, 0x17, 0x17,
        0x17, 0x17, 0x17, 0x0D, 0x0E, 0x10, 0x17, 0x17, 0x17, 0x00, 0x02,
    ];

    /// <summary>Finds the standard type a spelling names, in either standard namespace.</summary>
    /// <param name="spelling">The type's bytes, as in a <c>/S</c> name.</param>
    /// <returns>The type, or <see cref="PdfStructureType.Unknown"/>.</returns>
    public static PdfStructureType Find(ReadOnlySpan<byte> spelling)
    {
        if (spelling.IsEmpty)
        {
            return PdfStructureType.Unknown;
        }

        var type = spelling.Length <= MaxSpelling ? FindExact(spelling) : PdfStructureType.Unknown;
        return type == PdfStructureType.Unknown ? FindNumberedHeading(spelling) : type;
    }

    /// <summary>Gets what a type means to a reader.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The role.</returns>
    public static PdfSemanticRole GetRole(PdfStructureType type) => (uint)type < (uint)Roles.Length ? (PdfSemanticRole)Roles[(int)type] : PdfSemanticRole.Unknown;

    /// <summary>Gets a heading type's level, 1 to 6; <c>H</c> and <c>Title</c> count as level 1, the top heading.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The level, or zero when the enum alone does not specify a level. NumberedHeading needs its spelling.</returns>
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

    /// <summary>Gets the standard type names a namespace defines, such as the choices for a tag type picker.</summary>
    /// <param name="namespaceUri">The namespace's <c>/NS</c> text; <see langword="null"/> or the PDF 1.7 namespace gives the PDF 1.7 types, and the PDF 2.0 namespace gives the PDF 2.0 types.</param>
    /// <returns>The names, shared and never changing; empty for a namespace that is not a standard one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<string> GetStandardNames(string? namespaceUri) => GetStandardNameArray(namespaceUri);

    /// <summary>Gets the shared array of standard type names a namespace defines. The caller must not change it.</summary>
    /// <param name="namespaceUri">The namespace's <c>/NS</c> text.</param>
    /// <returns>The names; empty for a namespace that is not a standard one.</returns>
    internal static string[] GetStandardNameArray(string? namespaceUri) => namespaceUri switch
    {
        null or Pdf17Uri => Pdf17Names,
        Pdf20Uri => Pdf20Names,
        _ => [],
    };

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

    /// <summary>Reuses the exact standard spelling or decodes an unrecognised name for a retained result.</summary>
    /// <param name="spelling">The type's UTF-8 bytes.</param>
    /// <returns>The type name.</returns>
    internal static string GetSpelling(ReadOnlySpan<byte> spelling) => GetKnownSpelling(FindExact(spelling)) ?? Encoding.UTF8.GetString(spelling);

    /// <summary>Lists the spellings of the types a namespace defines.</summary>
    /// <param name="flag">The namespace flag.</param>
    /// <returns>The spellings.</returns>
    private static string[] BuildNames(int flag)
    {
        var names = new List<string>();
        for (var type = PdfStructureType.Document; type <= PdfStructureType.Subdivision; type++)
        {
            if ((GetFlags(type) & flag) != 0 && GetKnownSpelling(type) is { } spelling)
            {
                names.Add(spelling);
            }
        }

        return [.. names];
    }

    /// <summary>Gets a type's flags.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The flags.</returns>
    private static int GetFlags(PdfStructureType type) => (uint)type < (uint)Flags.Length ? Flags[(int)type] : 0;

    /// <summary>Recognises numbered headings without imposing a numeric or spelling-length limit.</summary>
    /// <param name="spelling">The type's bytes.</param>
    /// <returns>A heading type, or <see cref="PdfStructureType.Unknown"/>.</returns>
    private static PdfStructureType FindNumberedHeading(ReadOnlySpan<byte> spelling)
    {
        if (!NumberedHeadings.IsName(spelling))
        {
            return PdfStructureType.Unknown;
        }

        return spelling.Length == SpellingLength2 && spelling[1] <= (byte)'6'
            ? PdfStructureType.Heading1 + (spelling[1] - '1')
            : PdfStructureType.NumberedHeading;
    }

    /// <summary>Finds an exact standard spelling directly in its UTF-8 bytes.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindExact(ReadOnlySpan<byte> spelling) => spelling.Length <= SpellingLength6 ? FindShort(spelling) : FindLong(spelling);

    /// <summary>Matches standard spellings in this byte-length group.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindShort(ReadOnlySpan<byte> spelling) => spelling.Length switch
    {
        1 => FindLength1Part0(spelling),
        SpellingLength2 => FindLength2Part0(spelling),
        SpellingLength3 => FindLength3Part0(spelling),
        SpellingLength4 => FindLength4Part0(spelling),
        SpellingLength5 => FindLength5Part0(spelling),
        SpellingLength6 => FindLength6Part0(spelling),
        _ => PdfStructureType.Unknown,
    };

    /// <summary>Matches standard spellings in this byte-length group.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindLong(ReadOnlySpan<byte> spelling) => spelling.Length switch
    {
        SpellingLength7 => FindLength7Part0(spelling),
        SpellingLength8 => FindLength8Part0(spelling),
        SpellingLength9 => FindLength9Part0(spelling),
        SpellingLength10 => FindLength10Part0(spelling),
        SpellingLength16 => FindLength16Part0(spelling),
        _ => PdfStructureType.Unknown,
    };

    /// <summary>Matches exact UTF-8 names without constructing a lookup table.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindLength1Part0(ReadOnlySpan<byte> spelling) => spelling switch
    {
        var name when name.SequenceEqual("P"u8) => PdfStructureType.Paragraph,
        var name when name.SequenceEqual("H"u8) => PdfStructureType.Heading,
        var name when name.SequenceEqual("L"u8) => PdfStructureType.List,
        _ => PdfStructureType.Unknown,
    };

    /// <summary>Matches exact UTF-8 names without constructing a lookup table.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindLength2Part0(ReadOnlySpan<byte> spelling) => spelling switch
    {
        var name when name.SequenceEqual("LI"u8) => PdfStructureType.ListItem,
        var name when name.SequenceEqual("TR"u8) => PdfStructureType.TableRow,
        var name when name.SequenceEqual("TH"u8) => PdfStructureType.TableHeaderCell,
        var name when name.SequenceEqual("TD"u8) => PdfStructureType.TableDataCell,
        var name when name.SequenceEqual("RB"u8) => PdfStructureType.RubyBase,
        var name when name.SequenceEqual("RT"u8) => PdfStructureType.RubyText,
        var name when name.SequenceEqual("RP"u8) => PdfStructureType.RubyPunctuation,
        var name when name.SequenceEqual("WT"u8) => PdfStructureType.WarichuText,
        _ => FindLength2Part1(spelling),
    };

    /// <summary>Matches exact UTF-8 names without constructing a lookup table.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindLength2Part1(ReadOnlySpan<byte> spelling) => spelling switch
    {
        var name when name.SequenceEqual("WP"u8) => PdfStructureType.WarichuPunctuation,
        var name when name.SequenceEqual("Em"u8) => PdfStructureType.Emphasis,
        var name when name.SequenceEqual("H1"u8) => PdfStructureType.Heading1,
        var name when name.SequenceEqual("H2"u8) => PdfStructureType.Heading2,
        var name when name.SequenceEqual("H3"u8) => PdfStructureType.Heading3,
        var name when name.SequenceEqual("H4"u8) => PdfStructureType.Heading4,
        var name when name.SequenceEqual("H5"u8) => PdfStructureType.Heading5,
        var name when name.SequenceEqual("H6"u8) => PdfStructureType.Heading6,
        _ => PdfStructureType.Unknown,
    };

    /// <summary>Matches exact UTF-8 names without constructing a lookup table.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindLength3Part0(ReadOnlySpan<byte> spelling) => spelling switch
    {
        var name when name.SequenceEqual("Art"u8) => PdfStructureType.Article,
        var name when name.SequenceEqual("Div"u8) => PdfStructureType.Division,
        var name when name.SequenceEqual("TOC"u8) => PdfStructureType.TableOfContents,
        var name when name.SequenceEqual("Lbl"u8) => PdfStructureType.Label,
        var name when name.SequenceEqual("Sub"u8) => PdfStructureType.Subdivision,
        _ => PdfStructureType.Unknown,
    };

    /// <summary>Matches exact UTF-8 names without constructing a lookup table.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindLength4Part0(ReadOnlySpan<byte> spelling) => spelling switch
    {
        var name when name.SequenceEqual("Part"u8) => PdfStructureType.Part,
        var name when name.SequenceEqual("Sect"u8) => PdfStructureType.Section,
        var name when name.SequenceEqual("TOCI"u8) => PdfStructureType.TableOfContentsItem,
        var name when name.SequenceEqual("Span"u8) => PdfStructureType.Span,
        var name when name.SequenceEqual("Note"u8) => PdfStructureType.Note,
        var name when name.SequenceEqual("Code"u8) => PdfStructureType.Code,
        var name when name.SequenceEqual("Link"u8) => PdfStructureType.Link,
        var name when name.SequenceEqual("Ruby"u8) => PdfStructureType.Ruby,
        _ => FindLength4Part1(spelling),
    };

    /// <summary>Matches exact UTF-8 names without constructing a lookup table.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindLength4Part1(ReadOnlySpan<byte> spelling) => spelling switch
    {
        var name when name.SequenceEqual("Form"u8) => PdfStructureType.Form,
        _ => PdfStructureType.Unknown,
    };

    /// <summary>Matches exact UTF-8 names without constructing a lookup table.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindLength5Part0(ReadOnlySpan<byte> spelling) => spelling switch
    {
        var name when name.SequenceEqual("Aside"u8) => PdfStructureType.Aside,
        var name when name.SequenceEqual("Index"u8) => PdfStructureType.Index,
        var name when name.SequenceEqual("Title"u8) => PdfStructureType.Title,
        var name when name.SequenceEqual("LBody"u8) => PdfStructureType.ListBody,
        var name when name.SequenceEqual("Table"u8) => PdfStructureType.Table,
        var name when name.SequenceEqual("THead"u8) => PdfStructureType.TableHead,
        var name when name.SequenceEqual("TBody"u8) => PdfStructureType.TableBody,
        var name when name.SequenceEqual("TFoot"u8) => PdfStructureType.TableFoot,
        _ => FindLength5Part1(spelling),
    };

    /// <summary>Matches exact UTF-8 names without constructing a lookup table.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindLength5Part1(ReadOnlySpan<byte> spelling) => spelling switch
    {
        var name when name.SequenceEqual("Quote"u8) => PdfStructureType.Quote,
        var name when name.SequenceEqual("Annot"u8) => PdfStructureType.Annotation,
        _ => PdfStructureType.Unknown,
    };

    /// <summary>Matches exact UTF-8 names without constructing a lookup table.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindLength6Part0(ReadOnlySpan<byte> spelling) => spelling switch
    {
        var name when name.SequenceEqual("FENote"u8) => PdfStructureType.FootnoteOrEndnote,
        var name when name.SequenceEqual("Figure"u8) => PdfStructureType.Figure,
        var name when name.SequenceEqual("Strong"u8) => PdfStructureType.Strong,
        _ => PdfStructureType.Unknown,
    };

    /// <summary>Matches exact UTF-8 names without constructing a lookup table.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindLength7Part0(ReadOnlySpan<byte> spelling) => spelling switch
    {
        var name when name.SequenceEqual("Private"u8) => PdfStructureType.Private,
        var name when name.SequenceEqual("Caption"u8) => PdfStructureType.Caption,
        var name when name.SequenceEqual("Warichu"u8) => PdfStructureType.Warichu,
        var name when name.SequenceEqual("Formula"u8) => PdfStructureType.Formula,
        _ => PdfStructureType.Unknown,
    };

    /// <summary>Matches exact UTF-8 names without constructing a lookup table.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindLength8Part0(ReadOnlySpan<byte> spelling) => spelling switch
    {
        var name when name.SequenceEqual("Document"u8) => PdfStructureType.Document,
        var name when name.SequenceEqual("BibEntry"u8) => PdfStructureType.BibliographyEntry,
        var name when name.SequenceEqual("Artifact"u8) => PdfStructureType.Artifact,
        _ => PdfStructureType.Unknown,
    };

    /// <summary>Matches exact UTF-8 names without constructing a lookup table.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindLength9Part0(ReadOnlySpan<byte> spelling) => spelling switch
    {
        var name when name.SequenceEqual("NonStruct"u8) => PdfStructureType.NonStructural,
        var name when name.SequenceEqual("Reference"u8) => PdfStructureType.Reference,
        _ => PdfStructureType.Unknown,
    };

    /// <summary>Matches exact UTF-8 names without constructing a lookup table.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindLength10Part0(ReadOnlySpan<byte> spelling) => spelling switch
    {
        var name when name.SequenceEqual("BlockQuote"u8) => PdfStructureType.BlockQuote,
        _ => PdfStructureType.Unknown,
    };

    /// <summary>Matches exact UTF-8 names without constructing a lookup table.</summary>
    /// <param name="spelling">The structure type name.</param>
    /// <returns>The exact standard type, or Unknown.</returns>
    private static PdfStructureType FindLength16Part0(ReadOnlySpan<byte> spelling) => spelling switch
    {
        var name when name.SequenceEqual("DocumentFragment"u8) => PdfStructureType.DocumentFragment,
        _ => PdfStructureType.Unknown,
    };

    /// <summary>Returns a shared literal for an exact standard type.</summary>
    /// <param name="type">The exact standard type.</param>
    /// <returns>The canonical spelling, or null for an unknown type.</returns>
    private static string? GetKnownSpelling(PdfStructureType type) => type switch
    {
        PdfStructureType.Document => "Document",
        PdfStructureType.DocumentFragment => "DocumentFragment",
        PdfStructureType.Part => "Part",
        PdfStructureType.Article => "Art",
        PdfStructureType.Section => "Sect",
        PdfStructureType.Division => "Div",
        PdfStructureType.Aside => "Aside",
        PdfStructureType.NonStructural => "NonStruct",
        PdfStructureType.Private => "Private",
        PdfStructureType.BlockQuote => "BlockQuote",
        PdfStructureType.Caption => "Caption",
        PdfStructureType.TableOfContents => "TOC",
        PdfStructureType.TableOfContentsItem => "TOCI",
        PdfStructureType.Index => "Index",
        PdfStructureType.Paragraph => "P",
        PdfStructureType.Heading => "H",
        PdfStructureType.Title => "Title",
        PdfStructureType.List => "L",
        PdfStructureType.ListItem => "LI",
        PdfStructureType.Label => "Lbl",
        PdfStructureType.ListBody => "LBody",
        PdfStructureType.Table => "Table",
        PdfStructureType.TableRow => "TR",
        PdfStructureType.TableHeaderCell => "TH",
        PdfStructureType.TableDataCell => "TD",
        PdfStructureType.TableHead => "THead",
        PdfStructureType.TableBody => "TBody",
        PdfStructureType.TableFoot => "TFoot",
        PdfStructureType.Span => "Span",
        PdfStructureType.Quote => "Quote",
        PdfStructureType.Note => "Note",
        PdfStructureType.FootnoteOrEndnote => "FENote",
        PdfStructureType.Reference => "Reference",
        PdfStructureType.BibliographyEntry => "BibEntry",
        PdfStructureType.Code => "Code",
        PdfStructureType.Link => "Link",
        PdfStructureType.Annotation => "Annot",
        PdfStructureType.Ruby => "Ruby",
        PdfStructureType.RubyBase => "RB",
        PdfStructureType.RubyText => "RT",
        PdfStructureType.RubyPunctuation => "RP",
        PdfStructureType.Warichu => "Warichu",
        PdfStructureType.WarichuText => "WT",
        PdfStructureType.WarichuPunctuation => "WP",
        PdfStructureType.Figure => "Figure",
        PdfStructureType.Formula => "Formula",
        PdfStructureType.Form => "Form",
        PdfStructureType.Emphasis => "Em",
        PdfStructureType.Strong => "Strong",
        PdfStructureType.Subdivision => "Sub",
        PdfStructureType.Artifact => "Artifact",
        _ => GetHeadingSpelling(type),
    };

    /// <summary>Returns the exact numbered heading spelling.</summary>
    /// <param name="type">The exact standard type.</param>
    /// <returns>The heading spelling, or null.</returns>
    private static string? GetHeadingSpelling(PdfStructureType type) => type switch
    {
        PdfStructureType.Heading1 => "H1",
        PdfStructureType.Heading2 => "H2",
        PdfStructureType.Heading3 => "H3",
        PdfStructureType.Heading4 => "H4",
        PdfStructureType.Heading5 => "H5",
        PdfStructureType.Heading6 => "H6",
        _ => null,
    };
}
