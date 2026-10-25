// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Paragraph formatting with every property settled, after styles and direct formatting are combined.</summary>
/// <param name="Alignment">How lines sit between the left and right edges.</param>
/// <param name="LeftIndent">The left indent in points.</param>
/// <param name="RightIndent">The right indent in points.</param>
/// <param name="FirstLineIndent">The first line's extra indent in points.</param>
/// <param name="SpaceBefore">The space above in points.</param>
/// <param name="SpaceAfter">The space below in points.</param>
/// <param name="LineSpacing">The line spacing, read by <paramref name="LineSpacingRule"/>.</param>
/// <param name="LineSpacingRule">How the line spacing is read.</param>
/// <param name="KeepTogether">Whether the paragraph is kept on one page.</param>
/// <param name="KeepWithNext">Whether the paragraph stays with the next one.</param>
/// <param name="PageBreakBefore">Whether the paragraph starts a page.</param>
/// <param name="WidowControl">Whether single lines are kept from a page's edges.</param>
/// <param name="Borders">The lines round the paragraph.</param>
/// <param name="Padding">The space between the border and the text.</param>
/// <param name="Shading">The colour behind the paragraph, or <see langword="null"/> for none.</param>
/// <param name="TabStops">The tab stops, ordered by position.</param>
/// <param name="OutlineLevel">The outline level, 0 when the paragraph is not in the outline.</param>
[DebuggerDisplay("ResolvedParagraph: {Alignment}, indent {LeftIndent}")]
internal sealed record ResolvedParagraph(
    PdfTextAlignment Alignment,
    float LeftIndent,
    float RightIndent,
    float FirstLineIndent,
    float SpaceBefore,
    float SpaceAfter,
    float LineSpacing,
    PdfLineSpacingRule LineSpacingRule,
    bool KeepTogether,
    bool KeepWithNext,
    bool PageBreakBefore,
    bool WidowControl,
    PdfBorders Borders,
    PdfInsets Padding,
    PdfCanvasColor? Shading,
    PdfTabStop[] TabStops,
    int OutlineLevel)
{
    /// <summary>Gets the formatting that nothing has changed: left aligned single spaced text with widow control.</summary>
    internal static ResolvedParagraph Empty { get; } =
        new(PdfTextAlignment.Left, 0F, 0F, 0F, 0F, 0F, 1F, PdfLineSpacingRule.Multiple, false, false, false, true, PdfBorders.None, PdfInsets.None, null, [], 0);

    /// <summary>Lays a format over this one.</summary>
    /// <param name="over">The format whose set properties win, or <see langword="null"/> for none.</param>
    /// <returns>The combined formatting.</returns>
    internal ResolvedParagraph Apply(PdfParagraphFormat? over) => over is null ? this : ApplySpacing(over).ApplyLines(over).ApplyBox(over);

    /// <summary>Orders tab stops by position.</summary>
    /// <param name="stops">The stops.</param>
    /// <returns>A new array of the stops, nearest the left edge first.</returns>
    private static PdfTabStop[] Sort(IReadOnlyList<PdfTabStop> stops)
    {
        var sorted = new PdfTabStop[stops.Count];
        for (var i = 0; i < sorted.Length; i++)
        {
            sorted[i] = stops[i];
        }

        Array.Sort(sorted, static (a, b) => a.Position.CompareTo(b.Position));
        return sorted;
    }

    /// <summary>Lays the alignment, indents and spacing of a format over this one.</summary>
    /// <param name="over">The format.</param>
    /// <returns>The combined formatting.</returns>
    private ResolvedParagraph ApplySpacing(PdfParagraphFormat over) => this with
    {
        Alignment = over.Alignment ?? Alignment,
        LeftIndent = over.LeftIndent ?? LeftIndent,
        RightIndent = over.RightIndent ?? RightIndent,
        FirstLineIndent = over.FirstLineIndent ?? FirstLineIndent,
        SpaceBefore = over.SpaceBefore ?? SpaceBefore,
        SpaceAfter = over.SpaceAfter ?? SpaceAfter,
    };

    /// <summary>Lays the line spacing and page flow rules of a format over this one.</summary>
    /// <param name="over">The format.</param>
    /// <returns>The combined formatting.</returns>
    private ResolvedParagraph ApplyLines(PdfParagraphFormat over) => this with
    {
        LineSpacing = over.LineSpacing ?? LineSpacing,
        LineSpacingRule = over.LineSpacingRule ?? LineSpacingRule,
        KeepTogether = over.KeepTogether ?? KeepTogether,
        KeepWithNext = over.KeepWithNext ?? KeepWithNext,
        PageBreakBefore = over.PageBreakBefore ?? PageBreakBefore,
        WidowControl = over.WidowControl ?? WidowControl,
    };

    /// <summary>Lays the borders, shading, tab stops and outline level of a format over this one.</summary>
    /// <param name="over">The format.</param>
    /// <returns>The combined formatting.</returns>
    private ResolvedParagraph ApplyBox(PdfParagraphFormat over) => this with
    {
        Borders = over.Borders ?? Borders,
        Padding = over.Padding ?? Padding,
        Shading = over.Shading ?? Shading,
        TabStops = over.TabStops is { } stops ? Sort(stops) : TabStops,
        OutlineLevel = over.OutlineLevel ?? OutlineLevel,
    };
}
