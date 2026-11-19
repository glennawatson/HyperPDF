// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Render.Managed.Images;
using HyperPdfLibrary.Render.Managed.Pictures;
using HyperPdfLibrary.Render.Managed.Raster;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Render.Managed;

/// <summary>
/// Records what the content interpreter draws as the steps of a picture. Nothing is rasterised here: the picture is drawn later, at
/// the scale of each tile.
/// </summary>
[DebuggerDisplay("ManagedContentDevice: {_ops.Count} steps")]
internal sealed class ManagedContentDevice : IPictureDevice, IType3GlyphScope
{
    /// <summary>The text render modes that fill: 0, 2, 4 and 6.</summary>
    private const int FillModes = 0x55;

    /// <summary>The text render modes that stroke: 1, 2, 5 and 6.</summary>
    private const int StrokeModes = 0x66;

    /// <summary>Half, the mean of two lengths.</summary>
    private const float Half = 0.5F;

    /// <summary>The alpha of an opaque pixel, in its place in the pixel.</summary>
    private const uint Opaque = 0xFF000000;

    /// <summary>The shift of the red byte of a pixel.</summary>
    private const int RedShift = 16;

    /// <summary>The shift of the green byte of a pixel.</summary>
    private const int GreenShift = 8;

    /// <summary>The steps recorded so far.</summary>
    private readonly PictureOpList _ops = new();

    /// <summary>How the picture is drawn for the screen: its text placement, its edges and its line widths.</summary>
    private readonly PdfRenderQuality _quality;

    /// <summary>Builds the rectangles shadings are clipped to.</summary>
    private readonly PdfPathBuilder _boxes = new();

    /// <summary>Whether each group being recorded is a knockout group, outermost first.</summary>
    private readonly List<bool> _knockout = [];

    /// <summary>The soft mask of the painting call being recorded, or <see langword="null"/> for none.</summary>
    private PdfSoftMask? _mask;

    /// <summary>Initializes a new instance of the <see cref="ManagedContentDevice"/> class.</summary>
    /// <param name="weight">The images the picture and its parents draw, or <see langword="null"/> to start a new count.</param>
    /// <param name="quality">How the picture is drawn for the screen.</param>
    internal ManagedContentDevice(PictureWeight? weight, PdfRenderQuality quality)
    {
        Weight = weight ?? new();
        _quality = quality;
    }

    /// <inheritdoc/>
    public PictureWeight Weight { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Save() => _ops.Add(new(PictureOpKind.Save, Matrix3x2.Identity));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Restore() => _ops.Add(new(PictureOpKind.Restore, Matrix3x2.Identity));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clip(PdfPath path, bool evenOdd, Matrix3x2 ctm) => _ops.Add(new(PictureOpKind.Clip, ctm) { Path = path, Flag = evenOdd });

    /// <inheritdoc/>
    public void Fill(PdfPath path, bool evenOdd, ref GraphicsState state)
    {
        _mask = state.SoftMask;
        if (state.Fill.PaintsNothing)
        {
            return;
        }

        var shape = new PictureOp(PictureOpKind.Fill, state.Ctm) { Path = path, Flag = evenOdd, Blend = state.BlendMode };
        if (state.Fill.Pattern is { } pattern)
        {
            AddPatterned(shape, pattern, state.FillAlpha);
            return;
        }

        Paint(shape with { Color = RasterPaint.Solid(state.Fill.Rgb, state.FillAlpha).Color });
    }

    /// <inheritdoc/>
    public void Stroke(PdfPath path, ref GraphicsState state)
    {
        _mask = state.SoftMask;
        if (state.Stroke.PaintsNothing)
        {
            return;
        }

        var shape = new PictureOp(PictureOpKind.Stroke, state.Ctm) { Path = path, Stroke = StyleOf(ref state), Blend = state.BlendMode, Flag = state.StrokeAdjustment };
        if (state.Stroke.Pattern is { } pattern)
        {
            AddPatterned(shape, pattern, state.StrokeAlpha);
            return;
        }

        Paint(shape with { Color = RasterPaint.Solid(state.Stroke.Rgb, state.StrokeAlpha).Color });
    }

    /// <inheritdoc/>
    public void DrawImage(IPdfRenderImage image, bool isMask, bool smooth, ref GraphicsState state)
    {
        _mask = state.SoftMask;
        if (image is not ManagedRenderImage managed)
        {
            return;
        }

        Weight.Add(image);
        var tint = isMask ? RasterPaint.Solid(state.Fill.Rgb, state.FillAlpha).Color : 0;
        Paint(new(PictureOpKind.Image, state.Ctm) { Image = managed, Color = tint, Alpha = state.FillAlpha, Flag = smooth, Blend = state.BlendMode });
    }

    /// <inheritdoc/>
    public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
    {
        _mask = state.SoftMask;
        var mode = state.RenderMode;
        if (glyph.Font.GetOutline(glyph.Code) is not { Commands.IsEmpty: false } outline)
        {
            return;
        }

        if (IsMode(FillModes, mode) && !state.Fill.PaintsNothing)
        {
            var fill = RasterPaint.Solid(state.Fill.Rgb, state.FillAlpha).Color;
            Paint(new(PictureOpKind.Fill, glyph.GlyphMatrix * state.Ctm) { Path = outline, Color = fill, Blend = state.BlendMode, IsText = true });
        }

        if (!IsMode(StrokeModes, mode) || state.Stroke.PaintsNothing)
        {
            return;
        }

        // The line width is in user space, so the outline is moved there before it is stroked.
        var color = RasterPaint.Solid(state.Stroke.Rgb, state.StrokeAlpha).Color;
        Paint(new(PictureOpKind.Stroke, state.Ctm) { Path = outline.Transform(glyph.GlyphMatrix), Stroke = StyleOf(ref state), Color = color, Blend = state.BlendMode, Flag = state.StrokeAdjustment });
    }

    /// <inheritdoc/>
    public void BeginType3Glyph(PdfFont font, int code)
    {
        // A Type 3 glyph is drawn as ordinary content, so its scope needs no step.
    }

    /// <inheritdoc/>
    public void EndType3Glyph()
    {
        // See BeginType3Glyph.
    }

    /// <inheritdoc/>
    public void PaintShading(PdfShading shading, ref GraphicsState state)
    {
        _mask = state.SoftMask;
        if (shading.IsMesh)
        {
            Save();
            ClipToBox(shading, state.Ctm);
            AddMesh(shading, state.Ctm, state.FillAlpha, state.BlendMode);
            Restore();
            return;
        }

        if (shading.GetShader() is not ManagedRenderShader gradient)
        {
            return;
        }

        // The shading operator paints the whole clip, inside the shading's own box when it has one.
        Save();
        ClipToBox(shading, state.Ctm);
        Paint(new(PictureOpKind.Paint, Matrix3x2.Identity) { Shader = gradient, ShaderMatrix = state.Ctm, Alpha = state.FillAlpha, Blend = state.BlendMode });
        Restore();
    }

    /// <inheritdoc/>
    public void BeginGroup(in GroupInfo group)
    {
        // A group that is one object of a knockout group replaces what its parent drew where it paints (ISO 32000-2, 11.6.6), so it
        // starts empty rather than from its backdrop.
        var replaces = _knockout.Count > 0 && _knockout[^1] && group.Blend == PdfBlendMode.Normal;
        _knockout.Add(group.Knockout);
        var backdrop = !replaces && !group.Isolated && group.Blend == PdfBlendMode.Normal;
        _ops.Add(new(PictureOpKind.BeginLayer, Matrix3x2.Identity) { Alpha = group.Alpha, Flag = backdrop, Blend = group.Blend, Replaces = replaces });
    }

    /// <inheritdoc/>
    public void EndGroup(in GroupInfo group)
    {
        if (_knockout.Count > 0)
        {
            _knockout.RemoveAt(_knockout.Count - 1);
        }

        AddMask(group.SoftMask);
        _ops.Add(new(PictureOpKind.EndLayer, Matrix3x2.Identity));
    }

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
    public IPictureDevice CreatePictureDevice(PdfRect cull) => new ManagedContentDevice(Weight, _quality);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPdfRenderPicture Finish() => new ManagedPicture(_ops.ToArray(), RasterStyle.From(_quality));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _ops.Dispose();

    /// <summary>Determines whether a text render mode is in a set.</summary>
    /// <param name="modes">A bit set with bit n set for render mode n.</param>
    /// <param name="mode">The render mode, 0 to 7.</param>
    /// <returns><see langword="true"/> when the mode is in the set.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsMode(int modes, int mode) => ((modes >> mode) & 1) != 0;

    /// <summary>Reads how lines are stroked from the graphics state.</summary>
    /// <param name="state">The graphics state.</param>
    /// <returns>The stroke style, with its own copy of the dash pattern.</returns>
    private static StrokeStyle StyleOf(ref GraphicsState state) =>
        new(state.LineWidth, state.LineCap, state.LineJoin, state.MiterLimit, state.Dash.IsEmpty ? default : state.Dash.ToArray(), state.DashPhase);

    /// <summary>Records a step that paints. Under a soft mask the step goes into a layer of its own, which the mask then fades.</summary>
    /// <param name="op">The step.</param>
    private void Paint(in PictureOp op)
    {
        if (_mask is not { Picture: ManagedPicture } mask)
        {
            _ops.Add(op);
            return;
        }

        _ops.Add(new(PictureOpKind.BeginLayer, Matrix3x2.Identity) { Alpha = 1F });
        _ops.Add(op);
        AddMask(mask);
        _ops.Add(new(PictureOpKind.EndLayer, Matrix3x2.Identity));
    }

    /// <summary>Records the fading of the layer started last by a soft mask.</summary>
    /// <param name="mask">The soft mask, or <see langword="null"/> for none.</param>
    private void AddMask(PdfSoftMask? mask)
    {
        if (mask is not { Picture: ManagedPicture picture })
        {
            return;
        }

        // A luminosity mask is drawn over its backdrop colour; an alpha mask starts clear.
        var backdrop = mask.IsLuminosity ? Opaque | ((uint)mask.Backdrop.Red << RedShift) | ((uint)mask.Backdrop.Green << GreenShift) | mask.Backdrop.Blue : 0;
        _ops.Add(new(PictureOpKind.Mask, Matrix3x2.Identity) { Picture = picture, Flag = mask.IsLuminosity, Color = backdrop, Table = mask.Transfer });
    }

    /// <summary>Records a shape painted with a pattern: the shading's background first, then the shading inside its box.</summary>
    /// <param name="shape">The fill or stroke step, without its paint.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="alpha">How opaque the paint is.</param>
    private void AddPatterned(in PictureOp shape, PdfPatternPaint pattern, float alpha)
    {
        if (pattern.Tile is { Picture: ManagedPicture period } tile)
        {
            Paint(shape with { Picture = period, Tile = tile.Tile, ShaderMatrix = pattern.Matrix, Alpha = alpha });
            return;
        }

        if (pattern.Shading is not { } shading)
        {
            return;
        }

        if (shading.Background is { } background)
        {
            // ISO 32000-2 Table 77: the background fills the whole shape, and the shading paints over it inside its box.
            Paint(shape with { Color = RasterPaint.Solid(background, alpha).Color });
        }

        if (shading.IsMesh)
        {
            AddMeshFill(shape, shading, pattern.Matrix, alpha);
            return;
        }

        if (shading.GetShader() is not ManagedRenderShader gradient)
        {
            return;
        }

        Save();
        ClipToBox(shading, pattern.Matrix);
        Paint(shape with { Shader = gradient, ShaderMatrix = pattern.Matrix, Alpha = alpha });
        Restore();
    }

    /// <summary>Records a shape filled with a mesh shading: the mesh is drawn clipped to the shape and to the shading's box.</summary>
    /// <param name="shape">The fill step. A stroke is not painted with a mesh.</param>
    /// <param name="shading">The mesh shading.</param>
    /// <param name="matrix">The matrix from the shading's space to the picture's space.</param>
    /// <param name="alpha">How opaque the paint is.</param>
    private void AddMeshFill(in PictureOp shape, PdfShading shading, Matrix3x2 matrix, float alpha)
    {
        if (shape.Kind != PictureOpKind.Fill || shape.Path is not { } path)
        {
            return;
        }

        Save();
        Clip(path, shape.Flag, shape.Matrix);
        ClipToBox(shading, matrix);
        AddMesh(shading, matrix, alpha, shape.Blend);
        Restore();
    }

    /// <summary>Records the triangles of a mesh shading.</summary>
    /// <param name="shading">The mesh shading.</param>
    /// <param name="matrix">The matrix from the shading's space to the picture's space.</param>
    /// <param name="alpha">How opaque the mesh is.</param>
    /// <param name="blend">How the mesh mixes with what is under it.</param>
    private void AddMesh(PdfShading shading, Matrix3x2 matrix, float alpha, PdfBlendMode blend)
    {
        // The mesh is cut finely enough for the scale it is drawn at: the mean length of the matrix's two axes.
        var scale = (MathF.Sqrt((matrix.M11 * matrix.M11) + (matrix.M12 * matrix.M12)) + MathF.Sqrt((matrix.M21 * matrix.M21) + (matrix.M22 * matrix.M22))) * Half;
        if (shading.GetMesh(scale) is not { } mesh)
        {
            return;
        }

        var chunks = new List<ManagedRenderVertices>(mesh.Chunks.Length);
        foreach (var chunk in mesh.Chunks)
        {
            if (chunk is ManagedRenderVertices triangles)
            {
                chunks.Add(triangles);
            }
        }

        Paint(new(PictureOpKind.Mesh, matrix) { Vertices = [.. chunks], Shader = mesh.Ramp as ManagedRenderShader, Alpha = alpha, Blend = blend });
    }

    /// <summary>Clips to the box of a shading, when it has one.</summary>
    /// <param name="shading">The shading.</param>
    /// <param name="matrix">The matrix from the shading's space to the picture's space.</param>
    private void ClipToBox(PdfShading shading, Matrix3x2 matrix)
    {
        if (shading.BBox is not { } box)
        {
            return;
        }

        _boxes.Reset();
        _boxes.AddRect(new(box.Left, box.Bottom, box.Right, box.Top));
        Clip(_boxes.Detach(), false, matrix);
    }
}
