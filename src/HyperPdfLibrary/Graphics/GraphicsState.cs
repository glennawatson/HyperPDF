// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Functions;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics;

/// <summary>
/// The graphics state of PDF 32000 §8.4, held by value so <c>q</c> saves it with one copy onto a reused stack. Colours
/// are already resolved to RGB.
/// </summary>
[DebuggerDisplay("GraphicsState: width {LineWidth}")]
public record struct GraphicsState
{
    /// <summary>The default miter limit.</summary>
    private const float DefaultMiterLimit = 10;

    /// <summary>Initializes a new instance of the <see cref="GraphicsState"/> struct with the initial values.</summary>
    public GraphicsState()
    {
        Ctm = Matrix3x2.Identity;
        Fill = ColorState.Black;
        Stroke = ColorState.Black;
        LineWidth = 1;
        MiterLimit = DefaultMiterLimit;
        FillAlpha = 1;
        StrokeAlpha = 1;
        HorizontalScaling = 1;
        Dash = default;
        TextKnockout = true;
        Flatness = 1;
        RenderingIntent = IccIntent.RelativeColorimetric;
    }

    /// <summary>Gets or sets the current transformation matrix, from user space to the page.</summary>
    public Matrix3x2 Ctm { get; set; }

    /// <summary>Gets or sets the fill colour.</summary>
    public ColorState Fill { get; set; }

    /// <summary>Gets or sets the stroke colour.</summary>
    public ColorState Stroke { get; set; }

    /// <summary>Gets or sets the line width in user space units; 0 means the thinnest line the device can draw.</summary>
    public float LineWidth { get; set; }

    /// <summary>Gets or sets the line cap: 0 butt, 1 round, 2 projecting square.</summary>
    public int LineCap { get; set; }

    /// <summary>Gets or sets the line join: 0 miter, 1 round, 2 bevel.</summary>
    public int LineJoin { get; set; }

    /// <summary>Gets or sets the miter limit.</summary>
    public float MiterLimit { get; set; }

    /// <summary>Gets or sets the dash lengths; empty for solid lines.</summary>
    public ReadOnlyMemory<float> Dash { get; set; }

    /// <summary>Gets or sets the dash phase.</summary>
    public float DashPhase { get; set; }

    /// <summary>Gets or sets the fill (non-stroking) alpha, /ca.</summary>
    public float FillAlpha { get; set; }

    /// <summary>Gets or sets the stroke alpha, /CA.</summary>
    public float StrokeAlpha { get; set; }

    /// <summary>Gets or sets the blend mode.</summary>
    public PdfBlendMode BlendMode { get; set; }

    /// <summary>Gets or sets the soft mask, or null for none.</summary>
    public PdfSoftMask? SoftMask { get; set; }

    /// <summary>Gets or sets the character spacing, Tc.</summary>
    public float CharacterSpacing { get; set; }

    /// <summary>Gets or sets the word spacing, Tw.</summary>
    public float WordSpacing { get; set; }

    /// <summary>Gets or sets the horizontal scaling as a fraction (Tz divided by 100).</summary>
    public float HorizontalScaling { get; set; }

    /// <summary>Gets or sets the leading, TL.</summary>
    public float Leading { get; set; }

    /// <summary>Gets or sets the font, or null before Tf.</summary>
    public PdfFont? Font { get; set; }

    /// <summary>Gets or sets the font size.</summary>
    public float FontSize { get; set; }

    /// <summary>Gets or sets the text render mode, Tr: 0 fill, 1 stroke, 2 both, 3 invisible, 4-7 the same plus clip.</summary>
    public int RenderMode { get; set; }

    /// <summary>Gets or sets the text rise, Ts.</summary>
    public float Rise { get; set; }

    /// <summary>Gets or sets a value indicating whether stroking overprints, /OP.</summary>
    public bool StrokeOverprint { get; set; }

    /// <summary>Gets or sets a value indicating whether filling and images overprint, /op (or /OP when /op is absent).</summary>
    public bool FillOverprint { get; set; }

    /// <summary>Gets or sets the overprint mode, /OPM: 0 or 1.</summary>
    public int OverprintMode { get; set; }

    /// <summary>Gets or sets a value indicating whether the glyphs of one text show knock each other out, /TK.</summary>
    public bool TextKnockout { get; set; }

    /// <summary>Gets or sets the flatness tolerance, /FL, from 0 to 100; the drawing device flattens curves on its own, so it changes no output.</summary>
    public float Flatness { get; set; }

    /// <summary>Gets or sets the smoothness tolerance, /SM, from 0 to 1; the drawing device shades smoothly on its own, so it changes no output.</summary>
    public float Smoothness { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether automatic stroke adjustment is on, /SA. The drawing device draws thin lines
    /// at least one pixel wide whatever the value, so it changes no output.
    /// </summary>
    public bool StrokeAdjustment { get; set; }

    /// <summary>Gets or sets the transfer function tables, /TR2 or else /TR, or null for the identity (a name, or no function).</summary>
    public PdfTransfer? Transfer { get; set; }

    /// <summary>Gets or sets the halftone dictionary, /HT; null for the default halftone. A screen shows continuous tones, so it changes no output.</summary>
    public PdfDictionary? Halftone { get; set; }

    /// <summary>Gets or sets the black generation function, /BG2 or else /BG; null for the default. It acts on CMYK separation, which a screen does not do.</summary>
    public PdfFunction? BlackGeneration { get; set; }

    /// <summary>Gets or sets the undercolour removal function, /UCR2 or else /UCR; null for the default. It acts on CMYK separation, which a screen does not do.</summary>
    public PdfFunction? UndercolorRemoval { get; set; }

    /// <summary>Gets or sets the rendering intent, /RI or <c>ri</c>; relative colorimetric initially (ISO 32000-2 Table 51).</summary>
    internal IccIntent RenderingIntent { get; set; }

    /// <summary>Gets or sets the black point compensation choice, /UseBlackPtComp; /Default initially (ISO 32000-2 Table 51).</summary>
    internal BlackPointCompensationMode BlackPointMode { get; set; }

    /// <summary>Gets the ICC conversion the rendering intent and black point choice select.</summary>
    internal readonly IccRendering ColorRendering => IccRendering.From(RenderingIntent, BlackPointMode);
}
