// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>Lays out the text of a footnote for the foot of its page.</summary>
internal static class NoteLayout
{
    /// <summary>The inlines put in front of a note's first paragraph: the number and a space.</summary>
    private const int PrefixInlines = 2;

    /// <summary>Lays a footnote out.</summary>
    /// <param name="env">The document and its styles.</param>
    /// <param name="note">The footnote and its number.</param>
    /// <param name="width">The width of the text area.</param>
    /// <returns>The laid out note.</returns>
    internal static LaidOutNote Build(LayoutEnv env, NoteRef note, float width)
    {
        var paragraphs = new List<LaidOutParagraph>();
        foreach (var block in note.Footnote.Content)
        {
            if (block is not PdfParagraph paragraph)
            {
                env.Warnings.Add("A footnote can only hold paragraphs; other blocks in it were left out.");
                continue;
            }

            var inlines = paragraphs.Count == 0 ? Prefix(note.Label, paragraph.Inlines) : paragraph.Inlines;
            var styled = paragraph with { Inlines = inlines, Style = paragraph.Style ?? PdfStyleNames.FootnoteText };
            paragraphs.Add(ParagraphBuilder.Build(env, styled, width, ParagraphAdjust.None, 0F, FullLineBounds.Instance));
        }

        return new(note.Label, paragraphs);
    }

    /// <summary>Puts a footnote's number in front of its first paragraph.</summary>
    /// <param name="label">The number as written.</param>
    /// <param name="inlines">The paragraph's inlines.</param>
    /// <returns>The inlines with the number and a space first.</returns>
    private static List<PdfInline> Prefix(string label, IReadOnlyList<PdfInline> inlines)
    {
        var all = new List<PdfInline>(inlines.Count + PrefixInlines) { new PdfRun(label) { Style = PdfStyleNames.FootnoteReference }, new PdfRun(" ") };
        all.AddRange(inlines);
        return all;
    }
}
