// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>
/// Draws shapes into a rectangle of pixels: it fills and strokes paths through the clip in force, and keeps the off-screen layers of
/// transparency groups. Everything it needs between shapes is reused, so drawing a page allocates nothing once it is warm.
/// </summary>
[DebuggerDisplay("RasterCanvas: {_frames.Count} layers")]
internal sealed class RasterCanvas : IDisposable
{
    /// <summary>The largest byte, which stands for one.</summary>
    private const uint Full = 255;

    /// <summary>How close to a pixel border an edge of a clip rectangle must be to count as on it.</summary>
    private const float PixelSnap = 1F / 64F;

    /// <summary>The shift of the alpha byte of a pixel.</summary>
    private const int AlphaShift = 24;

    /// <summary>The edges of a clip that is a rectangle with level edges left out: its two sides.</summary>
    private const int RectangleEdges = 2;

    /// <summary>The edges of the shape being drawn.</summary>
    private readonly EdgeList _edges = new();

    /// <summary>The coverage of the shape being drawn.</summary>
    private readonly CoverageBuffer _coverage = new();

    /// <summary>The clip in force.</summary>
    private readonly ClipStack _clip = new();

    /// <summary>The stroker.</summary>
    private readonly Stroker _stroker = new();

    /// <summary>The layers being drawn into, outermost first, with how each goes back onto the one under it.</summary>
    private readonly List<LayerFrame> _frames = [];

    /// <summary>Layers not in use, kept for the next group.</summary>
    private readonly List<RasterLayer> _spare = [];

    /// <summary>One line of coverage, as wide as the surface.</summary>
    private byte[] _coverageRow = [];

    /// <summary>One line of colours from a shader, as wide as the surface.</summary>
    private uint[] _shadeRow = [];

    /// <summary>One line of coverage with three cells a pixel, used for text smoothed with the screen's colour stripes. Empty until such text is drawn.</summary>
    private byte[] _stripeRow = [];

    /// <summary>The pixels being drawn into.</summary>
    private RasterLayer? _target;

    /// <summary>The shader that paints tiling patterns, or <see langword="null"/> until a pattern is used.</summary>
    private Pictures.TileShader? _tiles;

    /// <summary>Gets or sets how the picture being drawn places and smooths its text and fits its lines.</summary>
    internal RasterStyle Style { get; set; }

    /// <summary>Gets the shader that paints tiling patterns, made the first time a pattern is used.</summary>
    internal Pictures.TileShader Tiles => _tiles ??= new();

    /// <summary>Gets the shader that paints gradients, reused for every gradient.</summary>
    internal GradientShader Gradients { get; } = new();

    /// <summary>Gets the shader that paints images, reused for every image.</summary>
    internal Images.ImageShader Images { get; } = new();

    /// <inheritdoc/>
    public void Dispose()
    {
        _edges.Dispose();
        _coverage.Dispose();
        _clip.Dispose();
        _stroker.Dispose();
        _tiles?.Dispose();
        foreach (var layer in _spare)
        {
            layer.Dispose();
        }

        _spare.Clear();
        ReturnRows();
    }

    /// <summary>Starts drawing into a rectangle of pixels with nothing clipped.</summary>
    /// <param name="target">The pixels.</param>
    internal void Begin(RasterLayer target)
    {
        _target = target;
        _frames.Clear();
        _clip.Reset(target.Width, target.Height);
        if (_coverageRow.Length >= target.Width)
        {
            return;
        }

        ReturnRows();
        _coverageRow = ArrayPool<byte>.Shared.Rent(target.Width);
        _shadeRow = ArrayPool<uint>.Shared.Rent(target.Width);
    }

    /// <summary>Remembers the clip.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Save() => _clip.Save();

    /// <summary>Goes back to the clip last remembered.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Restore() => _clip.Restore();

    /// <summary>Narrows the clip to a path.</summary>
    /// <param name="path">The path.</param>
    /// <param name="evenOdd">Whether the even-odd rule fills the path.</param>
    /// <param name="matrix">The matrix from the path's space to device pixels.</param>
    internal void ClipPath(PdfPath path, bool evenOdd, Matrix3x2 matrix)
    {
        _edges.Clear();
        PathFlattener.AddFill(path, matrix, _edges);
        if (TryGetPixelRectangle(out var rectangle))
        {
            // A rectangle on pixel borders needs no mask, which is the common case of a page or a text block clip.
            _clip.IntersectRect(rectangle);
            return;
        }

        var bounds = ShapeBounds();
        if (bounds.IsEmpty)
        {
            _clip.IntersectRect(default);
            return;
        }

        _coverage.Begin(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        _coverage.Add(_edges);
        _clip.IntersectShape(_coverage, evenOdd, _coverageRow);
    }

    /// <summary>Fills a path.</summary>
    /// <param name="path">The path.</param>
    /// <param name="evenOdd">Whether the even-odd rule fills the path.</param>
    /// <param name="matrix">The matrix from the path's space to device pixels.</param>
    /// <param name="paint">The colour or shader.</param>
    internal void FillPath(PdfPath path, bool evenOdd, Matrix3x2 matrix, in RasterPaint paint)
    {
        _edges.Clear();
        PathFlattener.AddFill(path, matrix, _edges);
        FillEdges(evenOdd, paint, Style.ShapeTable);
    }

    /// <summary>Fills the outline of a glyph, placed and smoothed the way the style asks for text.</summary>
    /// <param name="path">The outline.</param>
    /// <param name="matrix">The matrix from the glyph's space to device pixels.</param>
    /// <param name="paint">The colour or shader.</param>
    internal void FillText(PdfPath path, Matrix3x2 matrix, in RasterPaint paint)
    {
        var placed = Style.Place(matrix);

        // The colour stripes only line up with the screen for plain text drawn straight onto the page. Text inside a group is
        // blended again later, which would turn the stripes into fringes, so it is smoothed in grey.
        if (Style.Lcd && paint.Shader == RasterShaderKind.None && paint.Blend == PdfBlendMode.Normal && _frames.Count == 0)
        {
            FillStripes(path, placed, paint.Color);
            return;
        }

        _edges.Clear();
        PathFlattener.AddFill(path, placed, _edges);
        FillEdges(false, paint, Style.TextTable);
    }

    /// <summary>Strokes a path.</summary>
    /// <param name="path">The path.</param>
    /// <param name="matrix">The matrix from the path's space to device pixels.</param>
    /// <param name="style">How the line is stroked.</param>
    /// <param name="paint">The colour or shader.</param>
    internal void StrokePath(PdfPath path, Matrix3x2 matrix, in StrokeStyle style, in RasterPaint paint)
    {
        _edges.Clear();
        _stroker.Stroke(path, matrix, style, _edges);
        FillEdges(false, paint, Style.ShapeTable);
    }

    /// <summary>Draws the triangles of a mesh shading through the clip.</summary>
    /// <param name="chunks">The triangles, in chunks.</param>
    /// <param name="job">The ramp, the matrix, the alpha and the blend mode.</param>
    internal void DrawMesh(ManagedRenderVertices[] chunks, in MeshJob job)
    {
        foreach (var chunk in chunks)
        {
            MeshRaster.Draw(_target!, _clip, chunk, job);
        }
    }

    /// <summary>Paints every pixel the clip lets through.</summary>
    /// <param name="paint">The colour or shader.</param>
    internal void FillClip(in RasterPaint paint)
    {
        var bounds = _clip.Bounds;
        _edges.Clear();
        _edges.Add(bounds.Left, bounds.Top, bounds.Left, bounds.Bottom);
        _edges.Add(bounds.Right, bounds.Bottom, bounds.Right, bounds.Top);
        FillEdges(false, paint, null);
    }

    /// <summary>Fills the unit square of a matrix, which is where an image is drawn.</summary>
    /// <param name="matrix">The matrix from the unit square to device pixels.</param>
    /// <param name="paint">The colour or shader.</param>
    internal void FillUnitSquare(Matrix3x2 matrix, in RasterPaint paint)
    {
        var corner = Vector2.Transform(Vector2.Zero, matrix);
        var right = Vector2.Transform(Vector2.UnitX, matrix);
        var far = Vector2.Transform(Vector2.One, matrix);
        var up = Vector2.Transform(Vector2.UnitY, matrix);
        _edges.Clear();
        _edges.Add(corner.X, corner.Y, right.X, right.Y);
        _edges.Add(right.X, right.Y, far.X, far.Y);
        _edges.Add(far.X, far.Y, up.X, up.Y);
        _edges.Add(up.X, up.Y, corner.X, corner.Y);
        FillEdges(false, paint, null);
    }

    /// <summary>Starts an off-screen layer. What is drawn next goes into it until <see cref="EndLayer"/> puts it back.</summary>
    /// <param name="alpha">How opaque the layer is when it goes back, where 1 is fully.</param>
    /// <param name="withBackdrop">Whether the layer starts as a copy of what is under it, as a group that is not isolated does.</param>
    /// <param name="blend">How the layer mixes with what is under it when it goes back.</param>
    /// <param name="replaces">Whether the layer replaces what is under it where it painted, rather than mixing with it.</param>
    internal void BeginLayer(float alpha, bool withBackdrop, PdfBlendMode blend, bool replaces)
    {
        var parent = _target!;
        var layer = _spare.Count > 0 ? _spare[^1] : new();
        if (_spare.Count > 0)
        {
            _spare.RemoveAt(_spare.Count - 1);
        }

        layer.Resize(parent.Width, parent.Height);
        if (withBackdrop)
        {
            layer.CopyFrom(parent);
        }
        else
        {
            layer.Fill(0);
        }

        _frames.Add(new(parent, (byte)MathF.Round(Math.Clamp(alpha, 0F, 1F) * Full), withBackdrop, _clip.Bounds, blend) { Replaces = replaces });
        _target = layer;
    }

    /// <summary>Puts the layer started last back onto what is under it.</summary>
    internal void EndLayer()
    {
        if (_frames.Count == 0)
        {
            return;
        }

        var frame = _frames[^1];
        _frames.RemoveAt(_frames.Count - 1);
        var layer = _target!;
        _target = frame.Parent;
        var bounds = frame.Bounds;
        var coverage = _coverageRow.AsSpan(0, bounds.Width);
        coverage.Fill(frame.Alpha);
        for (var y = bounds.Top; y < bounds.Bottom; y++)
        {
            var source = layer.Row(y).Slice(bounds.Left, bounds.Width);
            var pixels = frame.Parent.Row(y).Slice(bounds.Left, bounds.Width);
            if (frame.Replaces)
            {
                Replace(pixels, source, frame.Alpha);
            }
            else if (frame.HasBackdrop)
            {
                Mix(pixels, source, frame.Alpha);
            }
            else if (frame.Blend == PdfBlendMode.Normal)
            {
                SpanBlender.Shaded(pixels, coverage, source);
            }
            else
            {
                BlendModes.Apply(frame.Blend, pixels, coverage, source);
            }
        }

        _spare.Add(layer);
    }

    /// <summary>Starts drawing a soft mask. What is drawn next goes into a layer of its own until <see cref="EndMask"/> uses it.</summary>
    /// <param name="backdrop">The premultiplied colour the mask starts as.</param>
    internal void BeginMask(uint backdrop)
    {
        var parent = _target!;
        var layer = _spare.Count > 0 ? _spare[^1] : new();
        if (_spare.Count > 0)
        {
            _spare.RemoveAt(_spare.Count - 1);
        }

        layer.Resize(parent.Width, parent.Height);
        layer.Fill(backdrop);
        _frames.Add(new(parent, byte.MaxValue, false, _clip.Bounds, PdfBlendMode.Normal));
        _target = layer;
    }

    /// <summary>Fades the layer under the mask by the mask drawn since <see cref="BeginMask"/>.</summary>
    /// <param name="luminosity">Whether the mask's brightness says how much shows, rather than its alpha.</param>
    /// <param name="table">The table the mask's values are looked up in, or <see langword="null"/> to use them as they are.</param>
    internal void EndMask(bool luminosity, byte[]? table)
    {
        if (_frames.Count == 0)
        {
            return;
        }

        var frame = _frames[^1];
        _frames.RemoveAt(_frames.Count - 1);
        var mask = _target!;
        _target = frame.Parent;
        var bounds = frame.Bounds;
        for (var y = bounds.Top; y < bounds.Bottom; y++)
        {
            var values = mask.Row(y).Slice(bounds.Left, bounds.Width);
            var pixels = frame.Parent.Row(y).Slice(bounds.Left, bounds.Width);
            for (var i = 0; i < pixels.Length; i++)
            {
                var value = luminosity ? SoftMasks.Brightness(values[i]) : values[i] >> AlphaShift;
                pixels[i] = SpanBlender.Scale(pixels[i], table is null ? value : table[value]);
            }
        }

        _spare.Add(mask);
    }

    /// <summary>Puts a layer's pixels in place of the pixels under it, wherever the layer painted anything.</summary>
    /// <param name="pixels">The pixels under the layer.</param>
    /// <param name="layer">The layer's pixels.</param>
    /// <param name="alpha">How opaque the layer is, where 255 is fully.</param>
    private static void Replace(Span<uint> pixels, ReadOnlySpan<uint> layer, uint alpha)
    {
        for (var i = 0; i < pixels.Length; i++)
        {
            if (layer[i] >> AlphaShift == 0)
            {
                continue;
            }

            // Any painted pixel counts as the object's shape, so what was under it goes and the layer's own colour and alpha stay.
            pixels[i] = SpanBlender.Scale(layer[i], alpha);
        }
    }

    /// <summary>Mixes a layer that already holds its backdrop back into the pixels under it.</summary>
    /// <param name="pixels">The pixels under the layer.</param>
    /// <param name="layer">The layer's pixels.</param>
    /// <param name="alpha">How much of the layer shows, where 255 is all of it.</param>
    private static void Mix(Span<uint> pixels, ReadOnlySpan<uint> layer, uint alpha)
    {
        if (alpha == Full)
        {
            layer.CopyTo(pixels);
            return;
        }

        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = SpanBlender.Scale(layer[i], alpha) + SpanBlender.Scale(pixels[i], Full - alpha);
        }
    }

    /// <summary>Tells whether a coordinate is on a pixel border.</summary>
    /// <param name="value">The coordinate.</param>
    /// <returns><see langword="true"/> when it is within a small fraction of a whole number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOnPixel(float value) => MathF.Abs(value - MathF.Round(value)) < PixelSnap;

    /// <summary>Tells whether an edge is upright.</summary>
    /// <param name="edge">The four floats of the edge.</param>
    /// <returns><see langword="true"/> when its two ends have the same x.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsUpright(ReadOnlySpan<float> edge) => MathF.Abs(edge[0] - edge[1 + 1]) < PixelSnap;

    /// <summary>Fills the edges gathered.</summary>
    /// <param name="evenOdd">Whether the even-odd rule fills the shape.</param>
    /// <param name="paint">The colour or shader.</param>
    /// <param name="table">The table that reshapes the coverage, as sharp edges and darker text do, or <see langword="null"/>.</param>
    private void FillEdges(bool evenOdd, in RasterPaint paint, byte[]? table)
    {
        var bounds = ShapeBounds();
        if (bounds.IsEmpty)
        {
            return;
        }

        var target = _target!;
        _coverage.Begin(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        _coverage.Add(_edges);
        var coverage = _coverageRow.AsSpan(0, bounds.Width);
        for (var row = 0; row < bounds.Height; row++)
        {
            var range = _coverage.ReadRow(row, evenOdd, coverage);
            if (range.IsEmpty)
            {
                continue;
            }

            var y = bounds.Top + row;
            var x = bounds.Left + range.Start;
            var covered = coverage[range.Start..range.End];
            if (table is not null)
            {
                CoverageTables.Apply(covered, table);
            }

            if (_clip.HasMask)
            {
                SpanBlender.Multiply(covered, _clip.MaskRow(y, x, covered.Length));
            }

            Blend(target.Row(y).Slice(x, covered.Length), covered, paint, x, y);
        }
    }

    /// <summary>Fills the outline of a glyph with its coverage measured three times across each pixel, once for each colour stripe of the screen.</summary>
    /// <param name="path">The outline.</param>
    /// <param name="matrix">The matrix from the glyph's space to device pixels.</param>
    /// <param name="color">The premultiplied colour.</param>
    private void FillStripes(PdfPath path, Matrix3x2 matrix, uint color)
    {
        // The outline is drawn three times as wide, so each cell of the coverage is one stripe.
        _edges.Clear();
        PathFlattener.AddFill(path, matrix * Matrix3x2.CreateScale(LcdBlender.Stripes, 1), _edges);
        if (_edges.Count == 0)
        {
            return;
        }

        // The coverage of a stripe is spread onto its neighbours, so the glyph reaches a pixel further on each side.
        var bounds = PixelRect.Around((_edges.Left / LcdBlender.Stripes) - 1, _edges.Top, (_edges.Right / LcdBlender.Stripes) + 1, _edges.Bottom).Intersect(_clip.Bounds);
        if (bounds.IsEmpty)
        {
            return;
        }

        var target = _target!;
        var width = bounds.Width * LcdBlender.Stripes;
        if (_stripeRow.Length < width)
        {
            ReturnStripes();
            _stripeRow = ArrayPool<byte>.Shared.Rent(target.Width * LcdBlender.Stripes);
        }

        var stripes = _stripeRow.AsSpan(0, width);
        _coverage.Begin(bounds.Left * LcdBlender.Stripes, bounds.Top, width, bounds.Height);
        _coverage.Add(_edges);
        for (var row = 0; row < bounds.Height; row++)
        {
            var range = _coverage.ReadRow(row, false, stripes);
            if (range.IsEmpty)
            {
                continue;
            }

            // Only the stripes from the first one an edge touched are written, so the ones before it are cleared.
            stripes[..range.Start].Clear();
            var y = bounds.Top + row;
            var mask = _clip.HasMask ? _clip.MaskRow(y, bounds.Left, bounds.Width) : default;
            LcdBlender.Blend(target.Row(y).Slice(bounds.Left, bounds.Width), stripes, mask, color, Style.TextTable);
        }
    }

    /// <summary>Blends a run of a shape onto a line.</summary>
    /// <param name="pixels">The pixels of the run.</param>
    /// <param name="coverage">The coverage of each pixel.</param>
    /// <param name="paint">The colour or shader.</param>
    /// <param name="x">The first pixel of the run.</param>
    /// <param name="y">The line.</param>
    private void Blend(Span<uint> pixels, ReadOnlySpan<byte> coverage, in RasterPaint paint, int x, int y)
    {
        if (paint.Shader == RasterShaderKind.None && paint.Blend == PdfBlendMode.Normal)
        {
            SpanBlender.Solid(pixels, coverage, paint.Color);
            return;
        }

        var colors = _shadeRow.AsSpan(0, pixels.Length);
        Shade(paint, x, y, colors);

        if (paint.Blend == PdfBlendMode.Normal)
        {
            SpanBlender.Shaded(pixels, coverage, colors);
        }
        else
        {
            BlendModes.Apply(paint.Blend, pixels, coverage, colors);
        }
    }

    /// <summary>Works out the colours of a run of pixels from the paint: its one colour, or the shader of the canvas that the paint names.</summary>
    /// <param name="paint">The colour or shader.</param>
    /// <param name="x">The first pixel of the run.</param>
    /// <param name="y">The line.</param>
    /// <param name="colors">Receives the premultiplied colour of each pixel of the run.</param>
    private void Shade(in RasterPaint paint, int x, int y, Span<uint> colors)
    {
        switch (paint.Shader)
        {
            case RasterShaderKind.Gradient:
                {
                    Gradients.Shade(x, y, colors);
                    break;
                }

            case RasterShaderKind.Tile:
                {
                    Tiles.Shade(x, y, colors);
                    break;
                }

            case RasterShaderKind.Image:
                {
                    Images.Shade(x, y, colors);
                    break;
                }

            default:
                {
                    colors.Fill(paint.Color);
                    break;
                }
        }
    }

    /// <summary>Finds the pixels the edges gathered can touch inside the clip.</summary>
    /// <returns>The pixels, empty when the shape is outside the clip or has no edges.</returns>
    private PixelRect ShapeBounds() =>
        _edges.Count == 0 ? default : PixelRect.Around(_edges.Left, _edges.Top, _edges.Right, _edges.Bottom).Intersect(_clip.Bounds);

    /// <summary>Tells whether the edges gathered are a rectangle whose sides are on pixel borders.</summary>
    /// <param name="rectangle">Receives the rectangle.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    private bool TryGetPixelRectangle(out PixelRect rectangle)
    {
        rectangle = default;
        var values = _edges.Values;
        if (_edges.Count != RectangleEdges || !IsUpright(values[..EdgeList.Stride]) || !IsUpright(values[EdgeList.Stride..]))
        {
            return false;
        }

        // Two upright sides of the same height are a rectangle: level edges cover nothing and were left out.
        var sameHeight = MathF.Abs(MathF.Min(values[1], values[EdgeList.Stride - 1]) - _edges.Top) < PixelSnap
            && MathF.Abs(MathF.Max(values[EdgeList.Stride + 1], values[EdgeList.Stride + EdgeList.Stride - 1]) - _edges.Bottom) < PixelSnap;
        if (!sameHeight || !IsOnPixel(_edges.Left) || !IsOnPixel(_edges.Right) || !IsOnPixel(_edges.Top) || !IsOnPixel(_edges.Bottom))
        {
            return false;
        }

        rectangle = new((int)MathF.Round(_edges.Left), (int)MathF.Round(_edges.Top), (int)MathF.Round(_edges.Right), (int)MathF.Round(_edges.Bottom));
        return true;
    }

    /// <summary>Gives the line buffers back to the pool.</summary>
    private void ReturnRows()
    {
        if (_coverageRow.Length == 0)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(_coverageRow);
        ArrayPool<uint>.Shared.Return(_shadeRow);
        _coverageRow = [];
        _shadeRow = [];
        ReturnStripes();
    }

    /// <summary>Gives the line of stripe coverage back to the pool, when one was taken.</summary>
    private void ReturnStripes()
    {
        if (_stripeRow.Length == 0)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(_stripeRow);
        _stripeRow = [];
    }

    /// <summary>A layer being drawn into and how it goes back onto the layer under it.</summary>
    /// <param name="Parent">The layer under it.</param>
    /// <param name="Alpha">How opaque it is when it goes back, where 255 is fully.</param>
    /// <param name="HasBackdrop">Whether it started as a copy of the layer under it.</param>
    /// <param name="Bounds">The pixels that could be drawn to when it started.</param>
    /// <param name="Blend">How it mixes with the layer under it.</param>
    private readonly record struct LayerFrame(RasterLayer Parent, byte Alpha, bool HasBackdrop, PixelRect Bounds, PdfBlendMode Blend)
    {
        /// <summary>Gets a value indicating whether the layer replaces what is under it where it painted.</summary>
        internal bool Replaces { get; init; }
    }
}
