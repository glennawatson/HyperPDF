// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A paragraph cut into lines and ready to be placed on pages, wherever they turn out to fall.</summary>
[DebuggerDisplay("LaidOutParagraph: {Lines.Count} lines")]
internal sealed class LaidOutParagraph
{
    /// <summary>Initializes a new instance of the <see cref="LaidOutParagraph"/> class.</summary>
    /// <param name="style">The paragraph formatting.</param>
    /// <param name="format">The character formatting of the paragraph.</param>
    /// <param name="atoms">The atoms.</param>
    /// <param name="lines">The lines.</param>
    /// <param name="notes">The footnotes in the paragraph.</param>
    internal LaidOutParagraph(ResolvedParagraph style, ResolvedCharacter format, List<Atom> atoms, List<LineBox> lines, List<NoteRef> notes)
    {
        Style = style;
        Format = format;
        Atoms = atoms;
        Lines = lines;
        Notes = notes;
    }

    /// <summary>Gets the paragraph formatting.</summary>
    internal ResolvedParagraph Style { get; }

    /// <summary>Gets the character formatting of the paragraph.</summary>
    internal ResolvedCharacter Format { get; }

    /// <summary>Gets the atoms.</summary>
    internal List<Atom> Atoms { get; }

    /// <summary>Gets the lines.</summary>
    internal List<LineBox> Lines { get; }

    /// <summary>Gets the footnotes in the paragraph, in order.</summary>
    internal List<NoteRef> Notes { get; }

    /// <summary>Gets or sets the structure node the paragraph's text belongs to, or <see langword="null"/> when the document is not tagged.</summary>
    internal StructNode? Node { get; set; }

    /// <summary>Gets the height of all the lines, without spacing, padding or borders.</summary>
    internal float LinesHeight
    {
        get
        {
            var height = 0F;
            foreach (var line in Lines)
            {
                height += line.Height;
            }

            return height;
        }
    }

    /// <summary>Gets the text of the paragraph without formatting, as the title of an outline entry.</summary>
    internal string PlainText
    {
        get
        {
            var text = new System.Text.StringBuilder();
            foreach (var atom in Atoms)
            {
                _ = atom.Kind switch
                {
                    AtomKind.Word => text.Append(atom.Text),
                    AtomKind.Space or AtomKind.Tab or AtomKind.Break => text.Append(' '),
                    _ => text,
                };
            }

            return text.ToString().Trim();
        }
    }
}
