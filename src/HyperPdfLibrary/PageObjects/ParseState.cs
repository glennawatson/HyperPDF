// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>The graphics and text state the parser tracks; <c>q</c> and <c>Q</c> save and restore it whole.</summary>
[DebuggerDisplay("ParseState: font size {FontSize}, render mode {RenderMode}")]
internal record struct ParseState
{
    /// <summary>Gets or sets the matrix from the current space to user space.</summary>
    internal Matrix3x2 Ctm { get; set; }

    /// <summary>Gets or sets the fill colour.</summary>
    internal PdfPaint Fill { get; set; }

    /// <summary>Gets or sets the stroke colour.</summary>
    internal PdfPaint Stroke { get; set; }

    /// <summary>Gets or sets the line width.</summary>
    internal float LineWidth { get; set; }

    /// <summary>Gets or sets the font, or null.</summary>
    internal PdfFont? Font { get; set; }

    /// <summary>Gets or sets the font's resource name.</summary>
    internal PdfName FontName { get; set; }

    /// <summary>Gets or sets the font size.</summary>
    internal float FontSize { get; set; }

    /// <summary>Gets or sets the character spacing.</summary>
    internal float CharacterSpacing { get; set; }

    /// <summary>Gets or sets the word spacing.</summary>
    internal float WordSpacing { get; set; }

    /// <summary>Gets or sets the horizontal scaling as a ratio.</summary>
    internal float HorizontalScaling { get; set; }

    /// <summary>Gets or sets the leading.</summary>
    internal float Leading { get; set; }

    /// <summary>Gets or sets the text rise.</summary>
    internal float Rise { get; set; }

    /// <summary>Gets or sets the text render mode.</summary>
    internal int RenderMode { get; set; }

    /// <summary>Gets or sets the innermost clip.</summary>
    internal ClipNode? Clip { get; set; }

    /// <summary>Gets or sets the opacity of filling, from the last graphics state that set it.</summary>
    internal float FillAlpha { get; set; }

    /// <summary>Gets or sets the opacity of stroking, from the last graphics state that set it.</summary>
    internal float StrokeAlpha { get; set; }

    /// <summary>Gets or sets the blend mode, from the last graphics state that set it; none means normal.</summary>
    internal PdfName BlendMode { get; set; }

    /// <summary>Creates the state a content stream starts in.</summary>
    /// <param name="ctm">The starting matrix.</param>
    /// <returns>The state.</returns>
    internal static ParseState Start(Matrix3x2 ctm) => new() { Ctm = ctm, Fill = PdfPaint.Black, Stroke = PdfPaint.Black, LineWidth = 1, HorizontalScaling = 1, FillAlpha = 1, StrokeAlpha = 1 };
}
