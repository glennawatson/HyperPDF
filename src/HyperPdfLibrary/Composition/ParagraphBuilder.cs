// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>Turns a <see cref="PdfParagraph"/> into a <see cref="LaidOutParagraph"/>: resolves its styles, measures its text and breaks it into lines.</summary>
internal static class ParagraphBuilder
{
    /// <summary>The inlines put in front of a list item's paragraph: the label and a tab.</summary>
    private const int PrefixInlines = 2;

    /// <summary>Lays a paragraph out in a width.</summary>
    /// <param name="env">The document and its styles.</param>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="textWidth">The width of the text area.</param>
    /// <param name="adjust">What the container changes in the paragraph.</param>
    /// <param name="top">The y of the top of the paragraph, which only matters to <paramref name="bounds"/>.</param>
    /// <param name="bounds">How much room each line has.</param>
    /// <returns>The laid out paragraph.</returns>
    /// <exception cref="InvalidOperationException">A style is missing or has no typeface.</exception>
    internal static LaidOutParagraph Build(LayoutEnv env, PdfParagraph paragraph, float textWidth, ParagraphAdjust adjust, float top, ILineBounds bounds)
    {
        var resolved = env.Styles.Resolve(paragraph.Style);
        var format = ParagraphBox.ApplyInsets(Adjust(resolved.Paragraph.Apply(paragraph.Format), adjust));
        var character = resolved.Character.Apply(paragraph.CharacterFormat);
        var builder = new AtomBuilder(env.Styles, env.Fields, env.Notes);
        var atoms = builder.Build(adjust.Label is null ? paragraph.Inlines : Prefix(adjust.Label, paragraph.Inlines), character);
        if (adjust.Label is not null && adjust.LabelNode is not null)
        {
            MarkLabel(atoms, adjust.LabelNode);
        }

        var lines = new LineBreaker(atoms, format, textWidth, character).Break(top, bounds);
        return new(format, character, atoms, lines, builder.Notes);
    }

    /// <summary>Applies a container's changes to a paragraph's formatting.</summary>
    /// <param name="format">The paragraph formatting.</param>
    /// <param name="adjust">The changes.</param>
    /// <returns>The adjusted formatting.</returns>
    private static ResolvedParagraph Adjust(ResolvedParagraph format, ParagraphAdjust adjust)
    {
        if (adjust.LeftIndent == 0F && adjust.Hang == 0F)
        {
            return format;
        }

        var left = format.LeftIndent + adjust.LeftIndent;
        var stops = new PdfTabStop[format.TabStops.Length + 1];
        stops[0] = PdfTabStop.At(left);
        format.TabStops.CopyTo(stops, 1);
        Array.Sort(stops, static (a, b) => a.Position.CompareTo(b.Position));
        return format with { LeftIndent = left, FirstLineIndent = format.FirstLineIndent - adjust.Hang, TabStops = stops };
    }

    /// <summary>Puts a label and a tab in front of a paragraph's inlines.</summary>
    /// <param name="label">The label.</param>
    /// <param name="inlines">The paragraph's inlines.</param>
    /// <returns>The inlines with the label first.</returns>
    private static List<PdfInline> Prefix(string label, IReadOnlyList<PdfInline> inlines)
    {
        var all = new List<PdfInline>(inlines.Count + PrefixInlines) { new PdfRun(label), PdfTab.Instance };
        all.AddRange(inlines);
        return all;
    }

    /// <summary>Gives the label atoms, up to and including the tab after them, their own structure node.</summary>
    /// <param name="atoms">The atoms.</param>
    /// <param name="node">The node.</param>
    private static void MarkLabel(List<Atom> atoms, StructNode node)
    {
        foreach (var atom in atoms)
        {
            atom.Node = node;
            if (atom.Kind == AtomKind.Tab)
            {
                break;
            }
        }
    }
}
