// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// The parent-child rules of ISO/TS 32005:2023 clause 7.2, Table 5, which extend ISO 32000-2:2020 Annex L: which standard
/// structure types may be children of which. Each row lists the children the table allows. A child marked <c>ø*</c> (allowed
/// only when the parent is used as a grouping element) and a child whose rule is <c>‡</c> (see the type's description) are
/// listed as allowed, so the check never reports a use the table permits in some context. Content items and artifacts are not
/// structure types and are not listed. All heading types H1 and above are treated as <c>Hn</c>.
/// </summary>
internal static class StructureContainment
{
    /// <summary>The name that stands for numbered headings in the table.</summary>
    internal const string NumberedHeading = "Hn";

    /// <summary>The Caption type's spelling in the table.</summary>
    private const string CaptionName = "Caption";

    /// <summary>The parents that may hold a Caption once (ISO/TS 32005 Table 5, occurrence 0..1).</summary>
    private const string CaptionOnceParents = "L Form LBody Table TOC Aside BlockQuote Title Figure Formula";

    /// <summary>The parents of the heading types that may hold an Art or a Sect once.</summary>
    private const string HeadingParents = "Hn H";

    /// <summary>The only parent that holds a THead or a TFoot once.</summary>
    private const string TableParent = "Table";

    /// <summary>The spelling of the structure tree root in the table.</summary>
    private const string TreeRoot = "StructTreeRoot";

    /// <summary>The rows: a parent, a colon and the space-separated children the table allows. A line that starts with <c>+</c> continues the row above.</summary>
    private const string Rows = """
        StructTreeRoot: Document
        Document: Document DocumentFragment Part Art Div Sect TOC Aside BlockQuote NonStruct Private P Note Code Hn H Title Link Annot Form FENote
        +Index L Table Figure Formula
        DocumentFragment: Document DocumentFragment Part Art Div Sect TOC Aside BlockQuote NonStruct Private P Note Code Hn H Title Link Annot Form
        +FENote Index L Table Figure Formula
        Part: Document DocumentFragment Part Art Div Sect TOC TOCI Aside BlockQuote NonStruct Private P Note Code Hn H Title Sub Lbl Link Reference
        +Annot Form FENote Index L BibEntry Table Caption Figure Formula
        Div: Document DocumentFragment Part Art Div Sect TOC TOCI Aside BlockQuote NonStruct Private P Note Code Hn H Title Sub Lbl Em Strong Span
        +Quote Link Reference Annot Form Ruby RB RT RP Warichu WT WP FENote Index L LI LBody BibEntry Table TR TH TD THead TBody TFoot Caption Figure
        +Formula
        Art: DocumentFragment Part Div Sect TOC Aside BlockQuote NonStruct Private P Note Code Hn H Title Lbl Link Annot Form FENote Index L Table
        +Caption Figure Formula
        Sect: DocumentFragment Part Art Div Sect TOC Aside BlockQuote NonStruct Private P Note Code Hn H Title Lbl Link Annot Form FENote Index L
        +Table Caption Figure Formula
        TOC: Part TOC TOCI NonStruct Private Caption
        TOCI: Div TOC NonStruct Private P Lbl Reference
        Aside: Document DocumentFragment Part Art Div Sect TOC BlockQuote NonStruct Private P Note Code Hn H Lbl Link Reference Annot Form FENote
        +Index L Table Caption Figure Formula
        BlockQuote: Document DocumentFragment Part Art Div Sect TOC BlockQuote NonStruct Private P Note Code Hn H Title Lbl Link Reference Annot Form
        +FENote Index L Table Caption Figure Formula
        NonStruct: Document DocumentFragment Part Art Div Sect TOC TOCI Aside BlockQuote NonStruct Private P Note Code Hn H Title Sub Lbl Em Strong
        +Span Quote Link Reference Annot Form Ruby RB RT RP Warichu WT WP FENote Index L LI LBody BibEntry Table TR TH TD THead TBody TFoot Caption
        +Figure Formula
        Private: Document DocumentFragment Part Art Div Sect TOC TOCI Aside BlockQuote NonStruct Private P Note Code Hn H Title Sub Lbl Em Strong Span
        +Quote Link Reference Annot Form Ruby RB RT RP Warichu WT WP FENote Index L LI LBody BibEntry Table TR TH TD THead TBody TFoot Caption Figure
        +Formula
        Title: Part Div Aside NonStruct Private P Note Code Lbl Em Strong Span Quote Link Reference Annot Form Ruby Warichu FENote L BibEntry Table
        +Caption Figure Formula
        Sub: NonStruct Private Note Code Lbl Em Strong Span Quote Link Reference Annot Form Ruby Warichu FENote L BibEntry Figure Formula
        P: NonStruct Private Note Code Sub Lbl Em Strong Span Quote Link Reference Annot Form Ruby Warichu FENote L BibEntry Table Figure Formula
        Note: DocumentFragment Part Art Div Sect Aside BlockQuote NonStruct Private P Note Code Sub Lbl Em Strong Span Quote Link Reference Annot Form
        +Ruby Warichu FENote Index L BibEntry Table Caption Figure Formula
        Code: DocumentFragment Part Div NonStruct Private Note Em Strong Span Link Reference Annot FENote BibEntry
        Hn: Art Sect NonStruct Private Note Code Sub Lbl Em Strong Span Quote Link Reference Annot Form Ruby Warichu FENote BibEntry Figure Formula
        H: Art Sect NonStruct Private Note Code Sub Lbl Em Strong Span Quote Link Reference Annot Form Ruby Warichu FENote BibEntry Figure Formula
        Lbl: NonStruct Private Note Code Sub Em Strong Span Quote Link Reference Annot Form Ruby Warichu FENote BibEntry Figure Formula
        Em: NonStruct Private Note Code Sub Lbl Em Strong Span Quote Link Reference Annot Form Ruby Warichu FENote BibEntry Figure Formula
        Strong: NonStruct Private Note Code Sub Lbl Em Strong Span Quote Link Reference Annot Form Ruby Warichu FENote BibEntry Figure Formula
        Span: NonStruct Private Note Code Sub Lbl Em Strong Span Quote Link Reference Annot Form Ruby Warichu FENote BibEntry Figure Formula
        Quote: NonStruct Private Note Code Sub Lbl Em Strong Span Quote Link Reference Annot Form Ruby Warichu FENote BibEntry Figure Formula
        Link: DocumentFragment Part Art Div Sect Aside BlockQuote NonStruct Private P Note Code Hn H Title Sub Lbl Em Strong Span Quote Reference
        +Annot Form Ruby Warichu FENote L BibEntry Table Caption Figure Formula
        Reference: NonStruct Private Note Lbl Em Strong Span Link Annot FENote BibEntry Figure
        Annot: DocumentFragment Part Art Div Sect Aside BlockQuote NonStruct P Note Code Hn H Title Sub Lbl Em Strong Span Quote Link Reference Annot
        +Form Ruby Warichu FENote L BibEntry Table Caption Figure Formula
        Form: Part Div NonStruct Private Note Code Lbl Reference FENote L BibEntry Table Caption Figure Formula
        Ruby: NonStruct Private RB RT RP
        RB: NonStruct Private Sub Em Strong Span Quote Link Reference Annot Form
        RT: NonStruct Private Sub Em Strong Span Quote Link Reference Annot Form
        RP: NonStruct Private Sub Em Strong Span Quote Link Reference Annot Form
        Warichu: NonStruct Private WT WP
        WT: NonStruct Private Sub Em Strong Span Quote Link Reference Annot Form
        WP: NonStruct Private Sub Em Strong Span Quote Link Reference Annot Form Figure
        FENote: DocumentFragment Part Art Div Sect Aside BlockQuote NonStruct Private P Note Code Sub Lbl Em Strong Span Quote Link Reference Annot
        +Form Ruby Warichu FENote L Table Caption Figure Formula
        Index: Part Div Sect NonStruct Private P Note Hn H Reference Annot FENote L Table Caption Figure Formula
        L: NonStruct Private L LI Caption
        LI: Div NonStruct Private Lbl LBody
        LBody: Part Art Div Sect Aside BlockQuote NonStruct Private P Note Code Hn H Sub Em Strong Span Quote Link Reference Annot Form Ruby Warichu
        +FENote Index L BibEntry Table Caption Figure Formula
        BibEntry: Part Div NonStruct Private P Note Lbl Em Strong Span Link Reference Annot FENote Figure
        Table: NonStruct Private TR THead TBody TFoot Caption
        TR: NonStruct Private TH TD
        TH: Art Div Sect NonStruct Private P Note Code Hn H Lbl Em Strong Span Quote Link Reference Annot Form Ruby Warichu FENote Index L BibEntry
        +Table Figure Formula
        TD: Art Div Sect NonStruct Private P Note Code Hn H Lbl Em Strong Span Quote Link Reference Annot Form Ruby Warichu FENote Index L BibEntry
        +Table Figure Formula
        THead: NonStruct Private TR
        TBody: NonStruct Private TR
        TFoot: NonStruct Private TR
        Caption: DocumentFragment Part Art Div Sect Aside BlockQuote NonStruct Private P Note Code Hn H Sub Lbl Em Strong Span Quote Link Reference
        +Annot Form Ruby Warichu FENote Index L BibEntry Table Figure Formula
        Figure: Part Art Div Sect Aside BlockQuote NonStruct Private P Note Code Hn H Sub Lbl Em Strong Span Quote Link Reference Annot Form Ruby
        +Warichu FENote Index L BibEntry Table Caption Figure Formula
        Formula: Part Div Aside BlockQuote NonStruct Private P Note Code Hn H Sub Lbl Em Strong Span Quote Link Reference Annot Form Ruby Warichu
        +FENote Index L BibEntry Table Caption Figure Formula
        """;

    /// <summary>The rows sorted by parent, each with its children sorted, built once from <see cref="Rows"/> and never changed.</summary>
    private static readonly ContainmentRow[] Table = Build();

    /// <summary>Determines whether the table has a row for a parent type.</summary>
    /// <param name="parent">The parent's standard type spelling, such as <c>P</c>; <c>Hn</c> for any numbered heading.</param>
    /// <returns><see langword="true"/> when the table says what the parent may hold.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Knows(string parent) => Find(parent) >= 0;

    /// <summary>Determines whether a child type may be inside a parent type.</summary>
    /// <param name="parent">The parent's standard type spelling; <c>Hn</c> for any numbered heading.</param>
    /// <param name="child">The child's standard type spelling; <c>Hn</c> for any numbered heading.</param>
    /// <returns><see langword="true"/> when the table allows it, or has no row for the parent.</returns>
    internal static bool Allows(string parent, string child)
    {
        var row = Find(parent);
        return row < 0 || Array.BinarySearch(Table[row].Children, child, StringComparer.Ordinal) >= 0;
    }

    /// <summary>Determines whether a child type may be a top-level element, directly inside the structure tree root.</summary>
    /// <param name="child">The child's standard type spelling.</param>
    /// <returns><see langword="true"/> when it is <c>Document</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool AllowsAtRoot(string child) => Allows(TreeRoot, child);

    /// <summary>Gets the table's spelling of an element's standard type.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The spelling, <c>Hn</c> for a numbered heading; <see langword="null"/> for a type that is not a standard one, or an artifact.</returns>
    internal static string? NameOf(PdfStructureElement element)
    {
        if (element.Type is PdfStructureType.Unknown or PdfStructureType.Artifact || element.ResolvesToMathMl)
        {
            return null;
        }

        return element.HeadingLevel > 0 ? NumberedHeading : element.MappedType;
    }

    /// <summary>Gets the table's spelling of a type name.</summary>
    /// <param name="type">The type as a file spells it, such as <c>H2</c>.</param>
    /// <returns><c>Hn</c> for a numbered heading; otherwise the name itself.</returns>
    internal static string NameOf(string type) =>
        type.Length > 1 && type[0] == 'H' && char.IsAsciiDigit(type[1]) ? NumberedHeading : type;

    /// <summary>Gets the flag of a child type that a parent may hold at most once (occurrence 0..1 in Table 5).</summary>
    /// <param name="parent">The parent's table spelling, or <see langword="null"/>.</param>
    /// <param name="child">The child's table spelling, or <see langword="null"/>.</param>
    /// <returns>A flag for the type, or <see cref="OnceChild.None"/> when the table allows any number.</returns>
    internal static OnceChild AtMostOnce(string? parent, string? child) => child switch
    {
        CaptionName => OnceIf(CaptionOnceParents, parent, OnceChild.Caption),
        "THead" => OnceIf(TableParent, parent, OnceChild.Head),
        "TFoot" => OnceIf(TableParent, parent, OnceChild.Foot),
        "Art" => OnceIf(HeadingParents, parent, OnceChild.Article),
        "Sect" => OnceIf(HeadingParents, parent, OnceChild.Section),
        _ => OnceChild.None,
    };

    /// <summary>Gets a flag when a parent is in a list of parents, and none otherwise.</summary>
    /// <param name="parents">The space-separated parents that may hold the child once.</param>
    /// <param name="parent">The parent's table spelling, or <see langword="null"/>.</param>
    /// <param name="flag">The flag to give.</param>
    /// <returns>The flag, or <see cref="OnceChild.None"/>.</returns>
    private static OnceChild OnceIf(string parents, string? parent, OnceChild flag)
    {
        if (parent is null)
        {
            return OnceChild.None;
        }

        foreach (var range in parents.AsSpan().Split(' '))
        {
            if (parents.AsSpan(range).SequenceEqual(parent))
            {
                return flag;
            }
        }

        return OnceChild.None;
    }

    /// <summary>Finds a parent's row.</summary>
    /// <param name="parent">The parent's standard type spelling.</param>
    /// <returns>The row's position, or a negative number when the table has none.</returns>
    private static int Find(string parent)
    {
        var low = 0;
        var high = Table.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            var order = string.CompareOrdinal(Table[middle].Parent, parent);
            if (order == 0)
            {
                return middle;
            }

            if (order < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return -1;
    }

    /// <summary>Reads the rows.</summary>
    /// <returns>The rows sorted by parent.</returns>
    private static ContainmentRow[] Build()
    {
        var rows = new List<ContainmentRow>();
        var joined = Rows.Replace("\n+", " ", StringComparison.Ordinal);
        foreach (var line in joined.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            var children = line[(colon + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Array.Sort(children, StringComparer.Ordinal);
            rows.Add(new(line[..colon], children));
        }

        rows.Sort(static (a, b) => string.CompareOrdinal(a.Parent, b.Parent));
        return [.. rows];
    }

    /// <summary>One parent and the children the table allows in it.</summary>
    /// <param name="Parent">The parent's standard type spelling.</param>
    /// <param name="Children">The allowed children, sorted by ordinal comparison.</param>
    private sealed record ContainmentRow(string Parent, string[] Children);
}
