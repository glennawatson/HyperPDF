// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>One measured piece of a paragraph: a word, a space, a tab, a break, a footnote number or a bookmark.</summary>
[DebuggerDisplay("Atom: {Kind} '{Text}' {Width}")]
internal sealed class Atom
{
    /// <summary>Initializes a new instance of the <see cref="Atom"/> class.</summary>
    /// <param name="kind">The kind of piece.</param>
    /// <param name="text">The text; a space for a <see cref="AtomKind.Space"/>, and empty for pieces without text.</param>
    /// <param name="format">The character formatting.</param>
    internal Atom(AtomKind kind, string text, ResolvedCharacter format)
    {
        Kind = kind;
        Text = text;
        Format = format;
    }

    /// <summary>Gets the kind of piece.</summary>
    internal AtomKind Kind { get; }

    /// <summary>Gets the text.</summary>
    internal string Text { get; }

    /// <summary>Gets the character formatting.</summary>
    internal ResolvedCharacter Format { get; }

    /// <summary>Gets or sets the font the text is drawn in.</summary>
    internal PdfCanvasFont? Font { get; set; }

    /// <summary>Gets or sets the size in points the text is drawn at, after small capitals and scripts shrink it.</summary>
    internal float Size { get; set; }

    /// <summary>Gets or sets how far in points the baseline is raised, or lowered when negative.</summary>
    internal float Shift { get; set; }

    /// <summary>Gets or sets the width in points. A tab's width is worked out for each line.</summary>
    internal float Width { get; set; }

    /// <summary>Gets or sets how far the text rises above the baseline, counting the shift.</summary>
    internal float Ascent { get; set; }

    /// <summary>Gets or sets how far the text falls below the baseline, counting the shift.</summary>
    internal float Descent { get; set; }

    /// <summary>Gets or sets a value indicating whether the piece joins the one before it with no space, so no line breaks between them.</summary>
    internal bool JoinsPrevious { get; set; }

    /// <summary>Gets or sets the link the piece is part of, or <see langword="null"/>.</summary>
    internal PdfLinkTarget? Link { get; set; }

    /// <summary>Gets or sets the description of the link the piece is part of.</summary>
    internal string? LinkDescription { get; set; }

    /// <summary>Gets or sets the place of the footnote a <see cref="AtomKind.Note"/> stands for in the paragraph's notes, or -1.</summary>
    internal int NoteIndex { get; set; } = -1;

    /// <summary>Gets or sets the name of the bookmark a <see cref="AtomKind.Mark"/> stands for, or <see langword="null"/>.</summary>
    internal string? Bookmark { get; set; }

    /// <summary>Gets or sets a value indicating whether the text is drawn after a lower case letter was made capital, which only changes its size.</summary>
    internal bool IsSmallCapital { get; set; }

    /// <summary>Gets or sets the structure node the atom belongs to when it is not the paragraph's, such as a list label's, or <see langword="null"/>.</summary>
    internal StructNode? Node { get; set; }

    /// <summary>Makes a copy that holds different text, with the same formatting and measuring.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The copy, which still needs measuring.</returns>
    internal Atom WithText(string text) => new(Kind, text, Format)
    {
        Font = Font,
        Size = Size,
        Shift = Shift,
        Ascent = Ascent,
        Descent = Descent,
        JoinsPrevious = JoinsPrevious,
        Link = Link,
        LinkDescription = LinkDescription,
        NoteIndex = NoteIndex,
        Bookmark = Bookmark,
        IsSmallCapital = IsSmallCapital,
        Node = Node,
    };
}
