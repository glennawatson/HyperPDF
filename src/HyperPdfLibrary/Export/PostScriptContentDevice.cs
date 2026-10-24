// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Export;

/// <summary>
/// Draws what the content interpreter shows as PostScript level 3: paths, strokes, clips, glyph outlines, images and axial and
/// radial gradients. PostScript has no transparency, so a translucent colour is lightened towards white, and a blend mode, soft
/// mask or group alpha is not applied; those count as approximated. Tiling patterns, mesh shadings and text used as a clip count
/// as skipped.
/// </summary>
/// <param name="writer">The PostScript writer the page is written with.</param>
[DebuggerDisplay("PostScriptContentDevice: {Skipped} skipped, {Approximated} approximated")]
internal sealed class PostScriptContentDevice(PostScriptWriter writer) : IContentDevice
{
    /// <summary>The operator that saves the graphics state.</summary>
    private const string SaveState = "gsave";

    /// <summary>The operator that restores the graphics state.</summary>
    private const string RestoreState = "grestore";

    /// <summary>The render modes that fill: 0, 2, 4 and 6.</summary>
    private const int FillModes = 0x55;

    /// <summary>The render modes that stroke: 1, 2, 5 and 6.</summary>
    private const int StrokeModes = 0x66;

    /// <summary>The first render mode that also adds the text to the clip.</summary>
    private const int FirstClipMode = 4;

    /// <summary>The render modes that draw nothing: 3 and 7.</summary>
    private const int HiddenModes = 0x88;

    /// <inheritdoc/>
    public bool RetainsPaths => false;

    /// <summary>Gets how many things were left out.</summary>
    internal int Skipped { get; private set; }

    /// <summary>Gets how many things were drawn differently from the page.</summary>
    internal int Approximated { get; private set; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Save() => writer.Line(SaveState);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Restore() => writer.Line(RestoreState);

    /// <inheritdoc/>
    public void Clip(PdfPath path, bool evenOdd, Matrix3x2 ctm)
    {
        // The matrix is put back after the clip, because the clip stays in the graphics state and the matrix would too.
        writer.Word("matrix currentmatrix");
        writer.Concat(ctm);
        writer.Path(path);
        writer.Word(evenOdd ? "eoclip" : "clip");
        writer.Word("newpath");
        writer.Line("setmatrix");
    }

    /// <inheritdoc/>
    public void Fill(PdfPath path, bool evenOdd, ref GraphicsState state)
    {
        if (state.Fill.PaintsNothing || path.IsEmpty)
        {
            return;
        }

        NoteTransparency(ref state, state.FillAlpha);
        if (state.Fill.Pattern is { } pattern)
        {
            FillWithPattern(path, evenOdd, pattern, ref state);
            return;
        }

        writer.Word(SaveState);
        writer.Concat(state.Ctm);
        writer.Color(state.Fill.Red, state.Fill.Green, state.Fill.Blue, state.FillAlpha);
        writer.Path(path);
        writer.Word(evenOdd ? "eofill" : "fill");
        writer.Line(RestoreState);
    }

    /// <inheritdoc/>
    public void Stroke(PdfPath path, ref GraphicsState state)
    {
        if (state.Stroke.PaintsNothing || path.IsEmpty)
        {
            return;
        }

        if (state.Stroke.Pattern is not null)
        {
            Skipped++;
            return;
        }

        NoteTransparency(ref state, state.StrokeAlpha);
        writer.Word(SaveState);
        writer.Concat(state.Ctm);
        StrokeLook(ref state);
        writer.Path(path);
        writer.Word("stroke");
        writer.Line(RestoreState);
    }

    /// <inheritdoc/>
    public void DrawImage(IPdfRenderImage image, bool isMask, bool smooth, ref GraphicsState state)
    {
        NoteTransparency(ref state, state.FillAlpha);
        if (!PostScriptImages.TryWrite(writer, image, isMask, smooth, ref state))
        {
            Skipped++;
        }
    }

    /// <inheritdoc/>
    public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
    {
        var mode = state.RenderMode;
        if (IsMode(HiddenModes, mode) || glyph.Font.GetOutline(glyph.Code) is not { Commands.IsEmpty: false } outline)
        {
            return;
        }

        if (mode >= FirstClipMode)
        {
            Approximated++;
        }

        NoteTransparency(ref state, state.FillAlpha);
        if (IsMode(FillModes, mode) && !state.Fill.PaintsNothing)
        {
            FillGlyph(outline, glyph.GlyphMatrix, ref state);
        }

        if (IsMode(StrokeModes, mode) && !state.Stroke.PaintsNothing)
        {
            StrokeGlyph(outline, glyph.GlyphMatrix, ref state);
        }
    }

    /// <inheritdoc/>
    public void PaintShading(PdfShading shading, ref GraphicsState state)
    {
        NoteTransparency(ref state, state.FillAlpha);
        writer.Word(SaveState);
        writer.Concat(state.Ctm);
        if (shading.BBox is { } box)
        {
            writer.Rectangle(box.Left, box.Bottom, box.Right, box.Top);
            writer.Word("clip newpath");
        }

        if (!PostScriptShadings.TryWrite(writer, shading))
        {
            Skipped++;
        }

        writer.Line(RestoreState);
    }

    /// <inheritdoc/>
    public void BeginGroup(in GroupInfo group)
    {
        if (group.Alpha < 1 || group.SoftMask is not null || group.Blend != PdfBlendMode.Normal)
        {
            Approximated++;
        }

        writer.Line(SaveState);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndGroup(in GroupInfo group) => writer.Line(RestoreState);

    /// <inheritdoc/>
    public void BeginMarkedContent(ReadOnlySpan<byte> tag, PdfDictionary? properties)
    {
        // Marked content has no visual effect; optional content is resolved by the interpreter.
    }

    /// <inheritdoc/>
    public void EndMarkedContent()
    {
        // See BeginMarkedContent.
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPictureDevice CreatePictureDevice(PdfRect cull) => new DiscardingContentDevice();

    /// <summary>Determines whether a render mode is in a set.</summary>
    /// <param name="modes">A bit set with bit n set for render mode n.</param>
    /// <param name="mode">The render mode, 0 to 7.</param>
    /// <returns><see langword="true"/> when the mode is in the set.</returns>
    private static bool IsMode(int modes, int mode) => ((modes >> mode) & 1) != 0;

    /// <summary>Counts a blend mode or soft mask that PostScript cannot apply.</summary>
    /// <param name="state">The graphics state.</param>
    /// <param name="alpha">The opacity in use, which is lightened towards white rather than composited.</param>
    private void NoteTransparency(ref GraphicsState state, float alpha)
    {
        if (state.SoftMask is not null || state.BlendMode != PdfBlendMode.Normal || alpha < 1)
        {
            Approximated++;
        }
    }

    /// <summary>Sets the line width, ends, joins, miter limit, dash and colour of a stroke.</summary>
    /// <param name="state">The graphics state.</param>
    private void StrokeLook(ref GraphicsState state)
    {
        writer.Color(state.Stroke.Red, state.Stroke.Green, state.Stroke.Blue, state.StrokeAlpha);
        writer.Number(state.LineWidth);
        writer.Word("setlinewidth");
        writer.Number(state.LineCap);
        writer.Word("setlinecap");
        writer.Number(state.LineJoin);
        writer.Word("setlinejoin");
        writer.Number(state.MiterLimit);
        writer.Word("setmiterlimit");
        writer.Word("[");
        foreach (var length in state.Dash.Span)
        {
            writer.Number(length);
        }

        writer.Word("]");
        writer.Number(state.DashPhase);
        writer.Word("setdash");
    }

    /// <summary>Fills a path with a shading pattern; a tiling pattern is left out.</summary>
    /// <param name="path">The path in user space.</param>
    /// <param name="evenOdd">Whether the even-odd rule applies.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="state">The graphics state.</param>
    private void FillWithPattern(PdfPath path, bool evenOdd, PdfPatternPaint pattern, ref GraphicsState state)
    {
        if (pattern.Shading is not { } shading)
        {
            Skipped++;
            return;
        }

        // The shading is placed by the pattern matrix in the page's space, so the clip is cut in user space and then the
        // base matrix is restored before the pattern matrix is applied.
        writer.Word(SaveState);
        writer.Concat(state.Ctm);
        writer.Path(path);
        writer.Word(evenOdd ? "eoclip" : "clip");
        writer.Word("newpath");
        writer.Word("BaseMatrix setmatrix");
        writer.Concat(pattern.Matrix);
        if (!PostScriptShadings.TryWrite(writer, shading))
        {
            Skipped++;
        }

        writer.Line(RestoreState);
    }

    /// <summary>Fills a glyph outline.</summary>
    /// <param name="outline">The outline in glyph space.</param>
    /// <param name="glyphMatrix">The matrix from glyph space to user space.</param>
    /// <param name="state">The graphics state.</param>
    private void FillGlyph(PdfPath outline, Matrix3x2 glyphMatrix, ref GraphicsState state)
    {
        if (state.Fill.Pattern is not null)
        {
            Skipped++;
            return;
        }

        writer.Word(SaveState);
        writer.Concat(glyphMatrix * state.Ctm);
        writer.Color(state.Fill.Red, state.Fill.Green, state.Fill.Blue, state.FillAlpha);
        writer.Path(outline);
        writer.Word(outline.FillRule == PdfPathFillRule.EvenOdd ? "eofill" : "fill");
        writer.Line(RestoreState);
    }

    /// <summary>Strokes a glyph outline with the line width in user space, not glyph space.</summary>
    /// <param name="outline">The outline in glyph space.</param>
    /// <param name="glyphMatrix">The matrix from glyph space to user space.</param>
    /// <param name="state">The graphics state.</param>
    private void StrokeGlyph(PdfPath outline, Matrix3x2 glyphMatrix, ref GraphicsState state)
    {
        if (state.Stroke.Pattern is not null)
        {
            Skipped++;
            return;
        }

        writer.Word(SaveState);
        writer.Concat(state.Ctm);
        StrokeLook(ref state);
        writer.Path(outline.Transform(glyphMatrix));
        writer.Word("stroke");
        writer.Line(RestoreState);
    }
}
