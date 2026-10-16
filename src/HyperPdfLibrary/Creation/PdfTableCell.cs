// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// One cell of a table row. A cell may contain plain text, styled <see cref="Runs"/>, an inline <see cref="Image"/>, a
/// <see cref="CodeBlock"/>, or a list of <see cref="Blocks"/>, and may span multiple columns or rows.
/// </summary>
/// <param name="Text">The cell's plain text.</param>
[DebuggerDisplay("PdfTableCell: {Text} ({ColumnSpan}x{RowSpan})")]
public sealed record PdfTableCell(string Text)
{
    /// <summary>Initializes a new instance of the <see cref="PdfTableCell"/> class from styled text runs.</summary>
    /// <param name="runs">The styled text runs.</param>
    public PdfTableCell(IReadOnlyList<PdfTextRun> runs)
        : this(string.Empty) =>
        Runs = runs;

    /// <summary>Initializes a new instance of the <see cref="PdfTableCell"/> class from an inline image.</summary>
    /// <param name="image">The image to draw in the cell.</param>
    public PdfTableCell(PdfFlowImage image)
        : this(string.Empty) =>
        Image = image;

    /// <summary>Initializes a new instance of the <see cref="PdfTableCell"/> class from an inline image and accompanying text.</summary>
    /// <param name="image">The image to draw in the cell.</param>
    /// <param name="text">The accompanying text drawn below the image.</param>
    public PdfTableCell(PdfFlowImage image, string text)
        : this(text) =>
        Image = image;

    /// <summary>Initializes a new instance of the <see cref="PdfTableCell"/> class from a preformatted code block.</summary>
    /// <param name="codeBlock">The code block to draw in the cell.</param>
    public PdfTableCell(PdfFlowCodeBlock codeBlock)
        : this(string.Empty) =>
        CodeBlock = codeBlock;

    /// <summary>Initializes a new instance of the <see cref="PdfTableCell"/> class from a sequence of flow blocks.</summary>
    /// <param name="blocks">The flow blocks (paragraphs, headings, images or code blocks) to draw in the cell.</param>
    public PdfTableCell(IReadOnlyList<PdfFlowBlock> blocks)
        : this(string.Empty) =>
        Blocks = blocks;

    /// <summary>Gets how content lines up horizontally in this cell, or <see langword="null"/> to use the column's alignment.</summary>
    public PdfTableAlignment? Alignment { get; init; }

    /// <summary>Gets the number of columns this cell spans; the default is 1.</summary>
    public int ColumnSpan { get; init; } = 1;

    /// <summary>Gets the number of rows this cell spans; the default is 1.</summary>
    public int RowSpan { get; init; } = 1;

    /// <summary>Gets the font for this cell, or <see langword="null"/> to inherit the table's header or body font.</summary>
    public PdfCanvasFont? Font { get; init; }

    /// <summary>Gets the ordered fallback fonts for this cell, or <see langword="null"/> to inherit the table's or flow's fallback fonts.</summary>
    public IReadOnlyList<PdfCanvasFont>? FallbackFonts { get; init; }

    /// <summary>Gets the font size in points for this cell, or <see langword="null"/> to inherit the table's font size.</summary>
    public float? FontSize { get; init; }

    /// <summary>Gets the text colour for this cell, or <see langword="null"/> to inherit the table's text colour.</summary>
    public PdfRgbColor? TextColor { get; init; }

    /// <summary>Gets the background fill colour for this cell, or <see langword="null"/> for a transparent background.</summary>
    public PdfRgbColor? BackgroundColor { get; init; }

    /// <summary>Gets the styled text runs of this cell. When non-empty, the runs are laid out in order before any <see cref="Text"/>.</summary>
    public IReadOnlyList<PdfTextRun> Runs { get; init; } = [];

    /// <summary>Gets an inline image drawn inside this cell, or <see langword="null"/> when the cell has no standalone image property.</summary>
    public PdfFlowImage? Image { get; init; }

    /// <summary>Gets a preformatted code block drawn inside this cell, or <see langword="null"/>.</summary>
    public PdfFlowCodeBlock? CodeBlock { get; init; }

    /// <summary>Gets flow elements (such as images, paragraphs, headings and code blocks) drawn inside this cell.</summary>
    public IReadOnlyList<PdfFlowBlock> Blocks { get; init; } = [];

    /// <summary>Gets a value indicating whether this cell uses spans, custom font sizing, rich text runs, images, code blocks or nested flow blocks.</summary>
    internal bool UsesRichLayout =>
        ColumnSpan != 1
        || RowSpan != 1
        || FontSize is not null
        || Runs.Count > 0
        || Image is not null
        || CodeBlock is not null
        || Blocks.Count > 0;

    /// <summary>Creates a cell from styled text runs.</summary>
    /// <param name="runs">The styled runs.</param>
    /// <returns>The cell.</returns>
    public static PdfTableCell FromRuns(params ReadOnlySpan<PdfTextRun> runs) => new(runs.ToArray());
}
