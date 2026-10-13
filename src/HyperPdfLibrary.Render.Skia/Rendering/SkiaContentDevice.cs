// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// The Skia device: records what the interpreter draws into an <see cref = "SKPicture"/>. The picture is in the space the
/// interpreter started in (viewer space for pages), so replaying it only needs a matrix.
/// </summary>
[DebuggerDisplay("SkiaContentDevice")]
internal sealed class SkiaContentDevice : IPictureDevice
{
    /// <summary>The pixels of padding added to layer bounds so anti-aliased edges and strokes are not cut off.</summary>
    private const float BoundsPadding = 2;

    /// <summary>The most copies of a large tiling period drawn directly for one shape.</summary>
    private const int MaxTileCopies = 1024;

    /// <summary>The half side of the cull rectangle used for a knockout child group without bounds.</summary>
    private const float UnboundedExtent = 1e7F;

    /// <summary>The largest alpha as a float.</summary>
    private const float AlphaMax = 255;

    /// <summary>The render modes that fill: 0, 2, 4 and 6.</summary>
    private const int FillModes = 0x55;

    /// <summary>The render modes that stroke: 1, 2, 5 and 6.</summary>
    private const int StrokeModes = 0x66;

    /// <summary>The render modes that draw nothing: 3 and 7.</summary>
    private const int HiddenModes = 0x88;

    /// <summary>The line cap value for round caps.</summary>
    private const int RoundCap = 1;

    /// <summary>The line cap value for square caps.</summary>
    private const int SquareCap = 2;

    /// <summary>The line join value for round joins.</summary>
    private const int RoundJoin = 1;

    /// <summary>The line join value for bevel joins.</summary>
    private const int BevelJoin = 2;

    /// <summary>The factor that makes an odd-length dash array even by repeating it.</summary>
    private const int PairLength = 2;

    /// <summary>
    /// The page width, in points, below which a stroke also gets a hairline: lines this thin fall under one pixel at
    /// 100% zoom and above. Wider lines get none, because a hairline under an anti-aliased one point line darkens its
    /// edges, so below 100% zoom lines of one point or more may draw thinner than a pixel.
    /// </summary>
    private const float ThinLineLimit = 1;

    /// <summary>
    /// The length, in device pixels, given to a zero-length dash. ISO 32000-2 8.4.3.6 says a zero-length dash paints a dot
    /// with round or square caps and nothing with butt caps. Skia draws a cap only on a dash of some length, and a tenth
    /// of a pixel gives the full dot (a hundredth drew visibly smaller dots in a render comparison) while a butt-capped
    /// dash of that length stays under 10% coverage.
    /// </summary>
    private const float DotLength = 0.1F;

    /// <summary>
    /// The shortest dash cycle, in device pixels, that is dashed. A pattern repeating faster than a sixteenth of a pixel
    /// reads as an even tone, so it is drawn solid instead of building an enormous number of segments.
    /// </summary>
    private const float MinDashCycle = 0.0625F;

    /// <summary>The smallest scale used when dividing by a matrix scale.</summary>
    private const float MinScale = 1e-6F;

    /// <summary>The most converted glyph outlines retained while recording one picture.</summary>
    private const int MaxCachedGlyphPaths = 512;

    /// <summary>The most separate glyphs drawn as one filled path.</summary>
    private const int MaxBatchedGlyphs = 4;

    /// <summary>The largest page-space side of a grouped path, keeping it suitable for Skia's path atlas at common zooms.</summary>
    private const float MaxGlyphBatchSide = 128;

    /// <summary>The page-space gap required so separate anti-aliased glyph edges cannot darken each other.</summary>
    private const float GlyphBatchGap = 0.5F;

    /// <summary>The colour channel table that leaves a channel unchanged.</summary>
    private static readonly byte[] IdentityTable = CreateIdentityTable();

    /// <summary>The alpha table that keeps zero and raises every other value to full.</summary>
    private static readonly byte[] ShapeTable = CreateShapeTable();

    /// <summary>The cull rectangle of a group recording that has no bounds.</summary>
    private static readonly SKRect UnboundedCull = new(-UnboundedExtent, -UnboundedExtent, UnboundedExtent, UnboundedExtent);

    /// <summary>Idle devices, kept with their native recorder, builders and paints so the next recording on a busy thread starts without creating them.</summary>
    private static readonly ReusableSlots<SkiaContentDevice> Idle = new();

    /// <summary>The recorder, ended by <see cref="Finish"/>.</summary>
    private readonly SKPictureRecorder _recorder = new();

    /// <summary>The native builder reused only while this device records path operations.</summary>
    private readonly SKPathBuilder _pathBuilder = new();

    /// <summary>Accumulates only compatible, non-overlapping solid-colour glyph paths.</summary>
    private readonly SKPathBuilder _glyphBatchBuilder = new();

    /// <summary>Repeated glyphs share one converted path during page recording.</summary>
    private readonly Dictionary<GlyphOutlineKey, SKPath> _glyphPaths = [];

    /// <summary>The image count a root recording keeps for itself; nested recordings use their parent's instead.</summary>
    private readonly PictureWeight _ownWeight = new();

    /// <summary>The open groups' own recordings, innermost last; null for a group drawn in a layer.</summary>
    private readonly List<PictureFrame?> _frames = [];

    /// <summary>The paint reused for every shape.</summary>
    private readonly SKPaint _paint = new() { IsAntialias = true };

    /// <summary>The paint reused for layers.</summary>
    private readonly SKPaint _layerPaint = new();

    /// <summary>Whether each open transparency group is a knockout group, innermost last.</summary>
    private readonly List<bool> _knockout = [];

    /// <summary>The canvas of the page or picture recording itself.</summary>
    private SKCanvas _rootCanvas;

    /// <summary>The count of the images drawn, including those drawn into nested pictures.</summary>
    private PictureWeight _weight;

    /// <summary>Whether compatible glyphs are collected into short filled paths.</summary>
    private bool _batchGlyphs;

    /// <summary>The canvas the interpreter draws on now: the recording canvas, or a knockout child group's.</summary>
    private SKCanvas _canvas;

    /// <summary>Turns any non-zero alpha into full alpha, which marks the pixels a group painted.</summary>
    private SKColorFilter? _shapeFilter;

    /// <summary>The first path held until another glyph makes a batch worthwhile.</summary>
    private SKPath? _firstBatchedGlyph;

    /// <summary>The first glyph's transform, used unchanged when a batch contains only one glyph.</summary>
    private Matrix3x2 _firstBatchedMatrix;

    /// <summary>The solid colour shared by a pending batch.</summary>
    private ColorState _batchColor;

    /// <summary>The page-space bounds of all glyphs in a pending batch.</summary>
    private SKRect _batchBounds;

    /// <summary>The number of glyphs waiting to be drawn.</summary>
    private int _batchCount;

    /// <summary>Whether <see cref="Finish"/> has run.</summary>
    private bool _finished;

    /// <summary>Whether <see cref="Rent"/> handed this device out, so <see cref="Dispose"/> keeps it for reuse.</summary>
    private bool _rented;

    /// <summary>One once <see cref="Dispose"/> has run since the recording began.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="SkiaContentDevice"/> class.</summary>
    /// <param name="cull">The area the picture covers.</param>
    internal SkiaContentDevice(SKRect cull)
        : this(cull, null, true)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SkiaContentDevice"/> class.</summary>
    /// <param name="cull">The recording bounds.</param>
    /// <param name="batchGlyphs">Whether to group compatible glyphs.</param>
    internal SkiaContentDevice(SKRect cull, bool batchGlyphs)
        : this(cull, null, batchGlyphs)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SkiaContentDevice"/> class.</summary>
    /// <param name="cull">The area the picture covers.</param>
    /// <param name="weight">Counts the images drawn; pictures nested in another recording share its count. Null counts them in the device itself.</param>
    /// <param name="batchGlyphs">Whether compatible glyphs are grouped.</param>
    private SkiaContentDevice(SKRect cull, PictureWeight? weight, bool batchGlyphs) => Begin(cull, weight, batchGlyphs);

    /// <summary>Gets the count of the pixel memory of the images drawn, including those drawn into nested pictures.</summary>
    public PictureWeight Weight => _weight;

    /// <inheritdoc/>
    public bool RetainsPaths => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Save()
    {
        FlushGlyphBatch();
        _ = _canvas.Save();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Restore()
    {
        FlushGlyphBatch();
        _canvas.Restore();
    }

    /// <inheritdoc/>
    public void Clip(PdfPath path, bool evenOdd, Matrix3x2 ctm)
    {
        FlushGlyphBatch();
        _canvas.SetMatrix(SkiaConversions.ToSkMatrix(ctm));
        if (SkiaPathConversion.TryGetRectangle(path, out var rectangle))
        {
            // Skia clips to a rectangle path exactly as it clips to the rectangle, and this needs no native path.
            _canvas.ClipRect(rectangle, SKClipOperation.Intersect, true);
            return;
        }

        using var native = SkiaPathConversion.Create(_pathBuilder, path, evenOdd);
        _canvas.ClipPath(native, SKClipOperation.Intersect, true);
    }

    /// <inheritdoc/>
    public void Fill(PdfPath path, bool evenOdd, ref GraphicsState state)
    {
        FlushGlyphBatch();
        using var native = SkiaPathConversion.Create(_pathBuilder, path, evenOdd);
        var masked = BeginMask(ref state, native.Bounds, state.Ctm);
        FillShape(native, state.Ctm, state.Fill, state.FillAlpha, state.BlendMode);
        EndMask(ref state, masked);
    }

    /// <inheritdoc/>
    public void Stroke(PdfPath path, ref GraphicsState state)
    {
        FlushGlyphBatch();

        // One straight segment in a plain colour is drawn as a line, which needs no native path.
        if (state.Stroke.Pattern is null && SkiaPathConversion.TryGetLine(path, out var from, out var to))
        {
            StrokeMasked(new(null, from, to), ref state);
            return;
        }

        using var native = SkiaPathConversion.Create(_pathBuilder, path);
        StrokeMasked(new(native), ref state);
    }

    /// <inheritdoc/>
    public void DrawImage(IPdfRenderImage image, bool isMask, bool smooth, ref GraphicsState state)
    {
        FlushGlyphBatch();
        Weight.Add(image);
        var native = SkiaResources.Image(image);
        var matrix = new Matrix3x2(1F / image.Width, 0, 0, -1F / image.Height, 0, 1) * state.Ctm;
        var masked = BeginMask(ref state, new(0, 0, 1, 1), state.Ctm);
        _canvas.SetMatrix(SkiaConversions.ToSkMatrix(matrix));
        _paint.Reset();
        _paint.IsAntialias = true;
        _paint.BlendMode = ElementBlend(state.BlendMode);
        _paint.Color = isMask ? SkiaConversions.ToSkColor(state.Fill, state.FillAlpha) : WhiteWithAlpha(state.FillAlpha);
        var sampling = ChooseSampling(native, smooth, state.Ctm);
        _canvas.DrawImage(native, new SKRect(0, 0, image.Width, image.Height), sampling, _paint);
        EndMask(ref state, masked);
    }

    /// <inheritdoc/>
    public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
    {
        var mode = state.RenderMode;
        if (IsMode(HiddenModes, mode) || glyph.Font.GetOutline(glyph.Code)is not { } outline)
        {
            return;
        }

        var matrix = glyph.GlyphMatrix * state.Ctm;
        using var pathLease = GetGlyphPath(glyph.Font, glyph.Code, outline);
        var path = pathLease.Path;
        if (CanBatchGlyph(mode, pathLease.Owns, ref state) && QueueGlyph(path, matrix, state.Fill))
        {
            return;
        }

        FlushGlyphBatch();
        var masked = BeginMask(ref state, path.Bounds, matrix);
        if (IsMode(FillModes, mode) && !state.Fill.PaintsNothing)
        {
            FillShape(path, matrix, state.Fill, state.FillAlpha, state.BlendMode);
        }

        if (IsMode(StrokeModes, mode) && !state.Stroke.PaintsNothing)
        {
            StrokeGlyph(path, glyph.GlyphMatrix, ref state);
        }

        EndMask(ref state, masked);
    }

    /// <inheritdoc/>
    public void PaintShading(PdfShading shading, ref GraphicsState state)
    {
        FlushGlyphBatch();
        var masked = BeginMask(ref state, SKRect.Empty, state.Ctm);
        _ = _canvas.Save();
        _canvas.SetMatrix(SkiaConversions.ToSkMatrix(state.Ctm));
        if (shading.BBox is { } box)
        {
            _canvas.ClipRect(new(box.Left, box.Bottom, box.Right, box.Top), SKClipOperation.Intersect, true);
        }

        DrawShadingPaint(shading, state.FillAlpha, state.BlendMode, MatrixScale(state.Ctm));
        _canvas.Restore();
        EndMask(ref state, masked);
    }

    /// <inheritdoc/>
    public void BeginGroup(in GroupInfo group)
    {
        FlushGlyphBatch();
        var parentKnocksOut = _knockout.Count > 0 && _knockout[^1];
        if (parentKnocksOut && group.Blend == PdfBlendMode.Normal)
        {
            BeginKnockoutChild(group);
            return;
        }

        _frames.Add(null);
        _layerPaint.Reset();
        _layerPaint.BlendMode = ElementBlend(group.Blend);
        _layerPaint.Color = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)MathF.Round(Math.Clamp(group.Alpha, 0, 1) * AlphaMax));
        _canvas.SetMatrix(SKMatrix.Identity);

        // A non-isolated group starts from a copy of its backdrop, so blend modes inside it mix with what lies below. That
        // copy composites back unchanged only through a Normal blend, so other blends use an isolated layer.
        // ISO 32000-2 11.6.6 differs for the other blends (it removes the backdrop's share from the group result), which a
        // Skia layer cannot do. A group inside a knockout group starts from the parent's initial backdrop, not from what the
        // parent has drawn so far; the layer starts empty, which is that backdrop when the parent is isolated.
        if (!group.Isolated && group.Blend == PdfBlendMode.Normal && !parentKnocksOut)
        {
            SaveLayerWithBackdrop(SkiaConversions.ToSkRect(group.Bounds), _layerPaint);
        }
        else
        {
            SaveLayer(SkiaConversions.ToSkRect(group.Bounds), _layerPaint);
        }

        _knockout.Add(group.Knockout);
    }

    /// <inheritdoc/>
    public void EndGroup(in GroupInfo group)
    {
        FlushGlyphBatch();
        var frame = _frames.Count > 0 ? _frames[^1] : null;
        if (_frames.Count > 0)
        {
            _frames.RemoveAt(_frames.Count - 1);
        }

        if (frame is not null)
        {
            EndKnockoutChild(group, frame);
        }
        else
        {
            if (group.SoftMask is { } mask)
            {
                ApplyMask(mask, SkiaConversions.ToSkRect(group.Bounds));
            }

            _canvas.Restore();
        }

        if (_knockout.Count > 0)
        {
            _knockout.RemoveAt(_knockout.Count - 1);
        }
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
    public IPictureDevice CreatePictureDevice(PdfRect cull) => Rent(SkiaConversions.ToSkRect(cull), Weight, _batchGlyphs);

    /// <inheritdoc/>
    public IPdfRenderPicture Finish()
    {
        FlushGlyphBatch();
        _finished = true;
        return new SkiaRenderPicture(_recorder.EndRecording());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        CloseOpenFrames();
        if (!_finished)
        {
            using var discarded = _recorder.EndRecording();
            _finished = true;
        }

        if (_rented && Recycle())
        {
            return;
        }

        _shapeFilter?.Dispose();
        _paint.Dispose();
        foreach (var path in _glyphPaths.Values)
        {
            path.Dispose();
        }

        _pathBuilder.Dispose();
        _glyphBatchBuilder.Dispose();
        _layerPaint.Dispose();
        _rootCanvas.Dispose();
        _canvas.Dispose();
        _recorder.Dispose();
    }

    /// <summary>Hands out an idle device, or makes one when none is idle. Dispose it to give it back.</summary>
    /// <param name="cull">The area the picture covers.</param>
    /// <param name="weight">Counts the images drawn; null counts them in the device itself.</param>
    /// <param name="batchGlyphs">Whether compatible glyphs are grouped.</param>
    /// <returns>A device that is recording, owned by the caller until it is disposed.</returns>
    internal static SkiaContentDevice Rent(SKRect cull, PictureWeight? weight, bool batchGlyphs)
    {
        var device = Idle.Take();
        if (device is null)
        {
            device = new(cull, weight, batchGlyphs);
        }
        else
        {
            device.Begin(cull, weight, batchGlyphs);
        }

        device._rented = true;
        return device;
    }

    /// <summary>Determines whether a text render mode is in a set.</summary>
    /// <param name="modes">A bit set with bit n set for render mode n.</param>
    /// <param name="mode">The render mode, 0 to 7.</param>
    /// <returns><see langword="true"/> when the mode is in the set.</returns>
    private static bool IsMode(int modes, int mode) => ((modes >> mode) & 1) != 0;

    /// <summary>Grows a rectangle for a stroke.</summary>
    /// <param name="bounds">The path bounds.</param>
    /// <param name="width">The line width.</param>
    /// <returns>The grown rectangle.</returns>
    private static SKRect Inflate(SKRect bounds, float width)
    {
        var grow = Math.Max(width, 1) * BoundsPadding;
        return new(bounds.Left - grow, bounds.Top - grow, bounds.Right + grow, bounds.Bottom + grow);
    }

    /// <summary>Creates the filter that turns a colour's luminosity into alpha.</summary>
    /// <param name="transfer">The transfer table, or null.</param>
    /// <returns>The filter.</returns>
    private static SKColorFilter CreateLumaFilter(byte[]? transfer)
    {
        var luma = SKColorFilter.CreateLumaColor();
        if (transfer is null)
        {
            return luma;
        }

        // SkiaSharp rejects null channel tables, so the colour channels get the identity table.
        using var table = SKColorFilter.CreateTable(transfer, IdentityTable, IdentityTable, IdentityTable);
        var composed = SKColorFilter.CreateCompose(table, luma);
        luma.Dispose();
        return composed;
    }

    /// <summary>Creates the alpha table that marks painted pixels.</summary>
    /// <returns>256 entries: 0 stays 0 and the rest are 255.</returns>
    private static byte[] CreateShapeTable()
    {
        var table = new byte[byte.MaxValue + 1];
        Array.Fill(table, byte.MaxValue, 1, byte.MaxValue);
        return table;
    }

    /// <summary>Creates the identity channel table.</summary>
    /// <returns>256 entries, each its own index.</returns>
    private static byte[] CreateIdentityTable()
    {
        var table = new byte[byte.MaxValue + 1];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = (byte)i;
        }

        return table;
    }

    /// <summary>Creates white at an alpha, which lets a shader's colours through scaled by the alpha.</summary>
    /// <param name="alpha">The alpha from 0 to 1.</param>
    /// <returns>The colour.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
private static SKColor WhiteWithAlpha(float alpha) =>
        new(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)MathF.Round(Math.Clamp(alpha, 0, 1) * AlphaMax));

    /// <summary>Chooses how an image is resampled.</summary>
    /// <param name="image">The image.</param>
    /// <param name="smooth">Whether the image asks for smoothing.</param>
    /// <param name="ctm">The matrix from the image's unit square to the page.</param>
    /// <returns>The sampling options.</returns>
    private static SKSamplingOptions ChooseSampling(SKImage image, bool smooth, Matrix3x2 ctm)
    {
        var width = MathF.Sqrt((ctm.M11 * ctm.M11) + (ctm.M12 * ctm.M12));
        return smooth || image.Width >= width
            ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
            : new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
    }

    /// <summary>
    /// Creates a dash effect (ISO 32000-2 8.4.3.6). A zero-length dash becomes a tenth of a device pixel so round or
    /// square caps still draw a dot, negative lengths (invalid) count as zero, and an array that is all zero or whose cycle
    /// is finer than a sixteenth of a pixel is drawn solid.
    /// </summary>
    /// <param name="lengths">The dash lengths; empty for a solid line.</param>
    /// <param name="phase">The phase.</param>
    /// <param name="scale">The device units one unit of the path's space spans.</param>
    /// <returns>The effect, or null for a solid line.</returns>
    private static SKPathEffect? CreateDash(ImmutableArray<float> lengths, float phase, float scale)
    {
        if (lengths.IsDefaultOrEmpty)
        {
            return null;
        }

        var source = ImmutableCollectionsMarshal.AsArray(lengths)!;
        var cycle = 0F;
        foreach (var length in source)
        {
            if (!float.IsFinite(length))
            {
                return null;
            }

            cycle += Math.Max(0, length);
        }

        scale = Math.Max(scale, MinScale);
        if (cycle <= 0 || cycle * scale < MinDashCycle)
        {
            return null;
        }

        var dot = DotLength / scale;

        var count = source.Length % PairLength == 0 ? source.Length : source.Length * PairLength;
        var array = new float[count];
        for (var i = 0; i < count; i++)
        {
            var length = source[i % source.Length];
            array[i] = length <= dot ? dot : length;
        }

        return SKPathEffect.CreateDash(array, phase);
    }

    /// <summary>Creates the repeating shader of a tiling pattern's period.</summary>
    /// <param name="tile">The period.</param>
    /// <param name="local">The matrix from pattern space to the space the shape is drawn in.</param>
    /// <returns>The shader, or null.</returns>
    private static SKShader? CreateTileShader(PatternCell tile, SKMatrix local)
    {
        if (tile.Image is { } image)
        {
            var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            return SkiaResources.Image(image).ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, sampling, SkiaConversions.ToSkMatrix(tile.ImageToPattern).PostConcat(local));
        }

        return tile.Picture is { } picture
            ? SkiaResources.Picture(picture).ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, SKFilterMode.Linear, local, SkiaConversions.ToSkRect(tile.Tile))
            : null;
    }

    /// <summary>Gets the average scale of a matrix: how many page units one unit of its space spans.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <returns>The scale.</returns>
    private static float MatrixScale(Matrix3x2 matrix) =>
        (MathF.Sqrt((matrix.M11 * matrix.M11) + (matrix.M12 * matrix.M12)) + MathF.Sqrt((matrix.M21 * matrix.M21) + (matrix.M22 * matrix.M22))) / PairLength;

    /// <summary>Rejects glyph bounds that cannot fit in a modest page-space path batch.</summary>
    /// <param name="bounds">The transformed glyph bounds.</param>
    /// <returns>True when the bounds can be batched.</returns>
    private static bool AreBatchBoundsUsable(SKRect bounds) =>
        !bounds.IsEmpty && float.IsFinite(bounds.Left) && float.IsFinite(bounds.Top)
        && float.IsFinite(bounds.Right) && float.IsFinite(bounds.Bottom)
        && bounds.Width <= MaxGlyphBatchSide && bounds.Height <= MaxGlyphBatchSide;

    /// <summary>Keeps the anti-aliased edges of separate glyphs from blending differently as one path.</summary>
    /// <param name="first">The pending group.</param>
    /// <param name="next">The next glyph.</param>
    /// <returns>True when their padded bounds do not overlap.</returns>
    private static bool AreGlyphsSeparated(SKRect first, SKRect next) =>
        next.Left >= first.Right + GlyphBatchGap || next.Right <= first.Left - GlyphBatchGap
        || next.Top >= first.Bottom + GlyphBatchGap || next.Bottom <= first.Top - GlyphBatchGap;

    /// <summary>
    /// Starts a group that is one object of a knockout group. The group records into its own picture, so that when it
    /// ends it can replace the parent's pixels only where the group painted (its shape, ISO 32000-2 11.6.6), not across its
    /// whole bounding box, which a layer drawn with the Source blend would do.
    /// </summary>
    /// <param name="group">The group.</param>
    private void BeginKnockoutChild(in GroupInfo group)
    {
        var recorder = new SKPictureRecorder();
        var bounds = SkiaConversions.ToSkRect(group.Bounds);
        var nested = recorder.BeginRecording(bounds.IsEmpty ? UnboundedCull : Inflate(bounds, 0), true);
        _frames.Add(new(recorder, _canvas, nested));
        _canvas = nested;
        _canvas.SetMatrix(SKMatrix.Identity);
        _knockout.Add(group.Knockout);
    }

    /// <summary>Ends a group begun by <see cref="BeginKnockoutChild"/>: clears the parent where the group painted, then composites it.</summary>
    /// <param name="group">The group.</param>
    /// <param name="frame">The group's recording.</param>
    private void EndKnockoutChild(in GroupInfo group, PictureFrame frame)
    {
        using var picture = frame.Recorder.EndRecording();
        frame.Canvas.Dispose();
        _canvas = frame.Parent;
        frame.Recorder.Dispose();
        _canvas.SetMatrix(SKMatrix.Identity);

        // Any painted pixel counts as shape, so the parent is cleared there before the group's own colour and alpha go on.
        _shapeFilter ??= SKColorFilter.CreateTable(ShapeTable, IdentityTable, IdentityTable, IdentityTable);
        _layerPaint.Reset();
        _layerPaint.BlendMode = SKBlendMode.DstOut;
        _layerPaint.ColorFilter = _shapeFilter;
        _canvas.DrawPicture(picture, _layerPaint);

        _layerPaint.Reset();
        _layerPaint.Color = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)MathF.Round(Math.Clamp(group.Alpha, 0, 1) * AlphaMax));
        if (group.SoftMask is not { } mask)
        {
            _canvas.DrawPicture(picture, _layerPaint);
            return;
        }

        var layerBounds = SkiaConversions.ToSkRect(group.Bounds);
        SaveLayer(layerBounds, _layerPaint);
        _canvas.DrawPicture(picture);
        ApplyMask(mask, layerBounds);
        _canvas.Restore();
    }

    /// <summary>Starts a recording on the device's own recorder, which can record again once the previous picture has been taken.</summary>
    /// <param name="cull">The area the picture covers.</param>
    /// <param name="weight">Counts the images drawn; null counts them in the device itself.</param>
    /// <param name="batchGlyphs">Whether compatible glyphs are grouped.</param>
    [MemberNotNull(nameof(_rootCanvas), nameof(_canvas), nameof(_weight))]
    private void Begin(SKRect cull, PictureWeight? weight, bool batchGlyphs)
    {
        if (weight is null)
        {
            _ownWeight.Reset();
        }

        _weight = weight ?? _ownWeight;
        _batchGlyphs = batchGlyphs;
        _finished = false;
        Volatile.Write(ref _disposed, 0);
        _rootCanvas = _recorder.BeginRecording(cull, true);
        _canvas = _rootCanvas;
    }

    /// <summary>
    /// Releases what the finished recording holds, so an idle device pins no font, image or document, and offers the device
    /// for reuse. The native recorder, builders and paints stay.
    /// </summary>
    /// <returns><see langword="true"/> when the device was kept; <see langword="false"/> when the idle slots are full.</returns>
    private bool Recycle()
    {
        _rootCanvas.Dispose();
        _canvas.Dispose();
        foreach (var path in _glyphPaths.Values)
        {
            path.Dispose();
        }

        _glyphPaths.Clear();
        _knockout.Clear();
        _pathBuilder.Reset();
        _glyphBatchBuilder.Reset();
        _paint.Reset();
        _paint.IsAntialias = true;
        _layerPaint.Reset();
        _firstBatchedGlyph = null;
        _batchCount = 0;
        _batchColor = default;
        _weight = _ownWeight;
        _ownWeight.Reset();
        _rented = false;
        return Idle.Offer(this);
    }

    /// <summary>Closes the pictures of groups still open when the device is disposed.</summary>
    private void CloseOpenFrames()
    {
        for (var i = _frames.Count - 1; i >= 0; i--)
        {
            if (_frames[i] is not { } frame)
            {
                continue;
            }

            using var discarded = frame.Recorder.EndRecording();
            frame.Canvas.Dispose();
            frame.Recorder.Dispose();
        }

        _frames.Clear();
    }

    /// <summary>Starts a layer, limited to a rectangle unless it is empty.</summary>
    /// <param name="bounds">The area in page space, or empty for no limit.</param>
    /// <param name="paint">The layer paint.</param>
    private void SaveLayer(SKRect bounds, SKPaint paint)
    {
        if (bounds.IsEmpty)
        {
            _ = _canvas.SaveLayer(paint);
        }
        else
        {
            _ = _canvas.SaveLayer(Inflate(bounds, 0), paint);
        }
    }

    /// <summary>Starts a layer that begins as a copy of what lies beneath it, limited to a rectangle unless it is empty.</summary>
    /// <param name="bounds">The area in page space, or empty for no limit.</param>
    /// <param name="paint">The layer paint.</param>
    private void SaveLayerWithBackdrop(SKRect bounds, SKPaint paint)
    {
        var record = new SKCanvasSaveLayerRec { Paint = paint, Flags = SKCanvasSaveLayerRecFlags.InitializeWithPrevious };
        if (!bounds.IsEmpty)
        {
            record.Bounds = Inflate(bounds, 0);
        }

        _ = _canvas.SaveLayer(record);
    }

    /// <summary>
    /// Gets the Skia blend mode for an object. Inside a knockout group a Normal object replaces what the group drew before
    /// it, which is the Source mode weighted by the object's coverage.
    /// </summary>
    /// <param name="blend">The object's blend mode.</param>
    /// <returns>The Skia blend mode.</returns>
    private SKBlendMode ElementBlend(PdfBlendMode blend) =>
        blend == PdfBlendMode.Normal && _knockout.Count > 0 && _knockout[^1] ? SKBlendMode.Src : SkiaConversions.ToSkBlend(blend);

    /// <summary>Strokes a glyph outline in user space, so the line width follows the page transform rather than the font size.</summary>
    /// <param name="outline">The outline in glyph space.</param>
    /// <param name="glyphMatrix">The matrix from glyph space to user space.</param>
    /// <param name="state">The graphics state.</param>
    private void StrokeGlyph(SKPath outline, Matrix3x2 glyphMatrix, ref GraphicsState state)
    {
        using var transformed = new SKPath();
        outline.Transform(SkiaConversions.ToSkMatrix(glyphMatrix), transformed);
        StrokeShape(new(transformed), state.Ctm, ref state);
    }

    /// <summary>Opens a layer when the state has a soft mask.</summary>
    /// <param name="state">The graphics state.</param>
    /// <param name="localBounds">The area to be painted in the space of <paramref name="matrix"/>, or empty if unknown.</param>
    /// <param name="matrix">The matrix from that space to the page.</param>
    /// <returns>The scope to give to <see cref="EndMask"/>.</returns>
    private SkiaMaskScope BeginMask(ref GraphicsState state, SKRect localBounds, Matrix3x2 matrix)
    {
        if (state.SoftMask is null)
        {
            return default;
        }

        var bounds = localBounds.IsEmpty ? SKRect.Empty : SkiaConversions.ToSkMatrix(matrix).MapRect(localBounds);
        _canvas.SetMatrix(SKMatrix.Identity);
        _layerPaint.Reset();
        SaveLayer(bounds, _layerPaint);
        return new(true, bounds);
    }

    /// <summary>Applies the soft mask to what was painted since <see cref="BeginMask"/>, and closes the layer.</summary>
    /// <param name="state">The graphics state.</param>
    /// <param name="scope">The scope from <see cref="BeginMask"/>.</param>
    private void EndMask(ref GraphicsState state, in SkiaMaskScope scope)
    {
        if (!scope.Active || state.SoftMask is not { } mask)
        {
            return;
        }

        ApplyMask(mask, scope.Bounds);
        _canvas.Restore();
    }

    /// <summary>Multiplies the open layer by a soft mask's coverage.</summary>
    /// <param name="mask">The mask.</param>
    /// <param name="bounds">The layer's area in page space, or empty.</param>
    private void ApplyMask(PdfSoftMask mask, SKRect bounds)
    {
        _canvas.SetMatrix(SKMatrix.Identity);
        using var filter = mask.IsLuminosity ? CreateLumaFilter(mask.Transfer) : null;
        _layerPaint.Reset();
        _layerPaint.BlendMode = SKBlendMode.DstIn;
        _layerPaint.ColorFilter = filter;
        SaveLayer(bounds, _layerPaint);
        if (mask.IsLuminosity && SkiaConversions.ToSkColor(mask.Backdrop) != SKColors.Black)
        {
            _paint.Reset();
            _paint.Color = SkiaConversions.ToSkColor(mask.Backdrop);
            _canvas.DrawPaint(_paint);
        }

        SkiaResources.Picture(mask.Picture).Playback(_canvas);
        _canvas.Restore();
    }

    /// <summary>Sets the paint's colour or shader for a fill or stroke colour.</summary>
    /// <param name="color">The colour.</param>
    /// <param name="alpha">The alpha.</param>
    /// <param name="matrix">The matrix the shape is drawn with, from shape space to the page.</param>
    /// <returns>A shader to dispose after drawing, or null.</returns>
    private SKShader? ApplyColor(in ColorState color, float alpha, Matrix3x2 matrix)
    {
        if (color.Pattern is not { } pattern)
        {
            _paint.Color = SkiaConversions.ToSkColor(color, alpha);
            return null;
        }

        _paint.Color = WhiteWithAlpha(alpha);

        // Skia cannot filter a shading function over each pixel's area, so /AntiAlias maps to dithering: it spreads the
        // quantisation steps of the colour ramp, which is the aliasing a smooth shading shows.
        _paint.IsDither = pattern.Shading?.AntiAlias == true;
        if (!Matrix3x2.Invert(matrix, out var inverse))
        {
            return null;
        }

        var local = SkiaConversions.ToSkMatrix(pattern.Matrix * inverse);
        SKShader? shader = null;
        if (pattern.Tile is { } tile)
        {
            shader = CreateTileShader(tile, local);
        }
        else if (pattern.Shading?.GetShader()is { } source)
        {
            shader = SkiaResources.Shader(source).WithLocalMatrix(local);
        }

        _paint.Shader = shader;
        return shader;
    }

    /// <summary>Fills a shape.</summary>
    /// <param name="path">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page.</param>
    /// <param name="color">The colour.</param>
    /// <param name="alpha">The alpha.</param>
    /// <param name="blend">The blend mode.</param>
    private void FillShape(SKPath path, Matrix3x2 matrix, in ColorState color, float alpha, PdfBlendMode blend)
    {
        // ISO 32000-2 Table 77: the background fills the whole shape first, then the shading paints over it inside its /BBox.
        if (color.Pattern?.Shading?.Background is { } background)
        {
            _paint.Reset();
            _paint.IsAntialias = true;
            _paint.BlendMode = ElementBlend(blend);
            _paint.Color = SkiaConversions.ToSkColor(new(PdfColorSpace.DeviceRgb, background, null, false), alpha);
            _canvas.SetMatrix(SkiaConversions.ToSkMatrix(matrix));
            _canvas.DrawPath(path, _paint);
        }

        if (color.Pattern is { Shading.IsMesh: true } meshPattern)
        {
            FillMesh(path, matrix, meshPattern, alpha, blend);
            return;
        }

        if (color.Pattern is { Tile.Picture: not null } tilePattern && TryFillTileCopies(path, matrix, tilePattern, alpha, blend))
        {
            return;
        }

        _paint.Reset();
        _paint.IsAntialias = true;
        _paint.BlendMode = ElementBlend(blend);
        var clipped = ClipShadingBox(color);
        using var shader = ApplyColor(color, alpha, matrix);
        _canvas.SetMatrix(SkiaConversions.ToSkMatrix(matrix));
        _canvas.DrawPath(path, _paint);
        if (clipped)
        {
            _canvas.Restore();
        }
    }

    /// <summary>
    /// Fills a shape with a tiling pattern whose period is too large to rasterise by drawing the period picture once for
    /// each lattice position that reaches the shape. Skia's picture shader shrinks a very large tile until a small cell
    /// disappears, which happens when a step is far larger than the page.
    /// </summary>
    /// <param name="path">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page.</param>
    /// <param name="pattern">The tiling pattern, whose period is a picture.</param>
    /// <param name="alpha">The alpha.</param>
    /// <param name="blend">The blend mode.</param>
    /// <returns><see langword="false"/> when too many copies would be needed and the shader should draw instead.</returns>
    private bool TryFillTileCopies(SKPath path, Matrix3x2 matrix, PdfPatternPaint pattern, float alpha, PdfBlendMode blend)
    {
        var tile = SkiaConversions.ToSkRect(pattern.Tile!.Tile);
        var patternMatrix = SkiaConversions.ToSkMatrix(pattern.Matrix);
        if (!patternMatrix.TryInvert(out var inverse) || tile.Width <= 0 || tile.Height <= 0)
        {
            return false;
        }

        _ = _canvas.Save();
        _canvas.SetMatrix(SkiaConversions.ToSkMatrix(matrix));
        _canvas.ClipPath(path, SKClipOperation.Intersect, true);
        var clip = _canvas.DeviceClipBounds;
        var reach = inverse.MapRect(SKRect.Create(clip.Left, clip.Top, clip.Width, clip.Height));
        var range = TileRange.Create(reach, tile);
        if (range is not { } copies || copies.Count > MaxTileCopies)
        {
            _canvas.Restore();
            return false;
        }

        _layerPaint.Reset();
        _layerPaint.Color = WhiteWithAlpha(alpha);
        _layerPaint.BlendMode = ElementBlend(blend);
        DrawTileCopies(SkiaResources.Picture(pattern.Tile.Picture!), patternMatrix, tile, copies);
        _canvas.Restore();
        return true;
    }

    /// <summary>Draws the period picture at each lattice position with the layer paint.</summary>
    /// <param name="picture">The period.</param>
    /// <param name="patternMatrix">The matrix from pattern space to the page.</param>
    /// <param name="tile">The period's rectangle in pattern space.</param>
    /// <param name="copies">The positions to draw.</param>
    private void DrawTileCopies(SKPicture picture, SKMatrix patternMatrix, SKRect tile, in TileRange copies)
    {
        for (var column = copies.FirstColumn; column <= copies.LastColumn; column++)
        {
            for (var row = copies.FirstRow; row <= copies.LastRow; row++)
            {
                _canvas.SetMatrix(patternMatrix);
                _canvas.Translate(column * tile.Width, row * tile.Height);
                _canvas.DrawPicture(picture, _layerPaint);
            }
        }
    }

    /// <summary>Strokes a path with a large-period tiling pattern: the stroke's outline is filled with copies of the period.</summary>
    /// <param name="path">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page.</param>
    /// <param name="pattern">The tiling pattern, whose period is a picture.</param>
    /// <param name="scale">The matrix's average scale.</param>
    /// <param name="state">The graphics state.</param>
    /// <returns><see langword="false"/> when the shader should draw instead.</returns>
    private bool TryStrokeTileCopies(SKPath path, Matrix3x2 matrix, PdfPatternPaint pattern, float scale, ref GraphicsState state)
    {
        ConfigureStroke(ref state);
        using var dash = CreateDash(state.Dash, state.DashPhase, scale);
        _paint.PathEffect = dash;
        using var builder = new SKPathBuilder();
        if (!_paint.GetFillPath(path, builder))
        {
            return false;
        }

        using var outline = builder.Detach();
        return TryFillTileCopies(outline, matrix, pattern, state.StrokeAlpha, state.BlendMode);
    }

    /// <summary>Clips to a shading pattern's /BBox, which lies in pattern space; the caller restores the canvas when this returns true.</summary>
    /// <param name="color">The paint colour.</param>
    /// <returns><see langword="true"/> when the canvas was saved and clipped.</returns>
    private bool ClipShadingBox(in ColorState color)
    {
        if (color.Pattern is not { Shading.BBox: { } box } pattern)
        {
            return false;
        }

        _ = _canvas.Save();
        _canvas.SetMatrix(SkiaConversions.ToSkMatrix(pattern.Matrix));
        _canvas.ClipRect(new(box.Left, box.Bottom, box.Right, box.Top), SKClipOperation.Intersect, true);
        return true;
    }

    /// <summary>Strokes a shape inside the layer a soft mask asks for.</summary>
    /// <param name="shape">The shape.</param>
    /// <param name="state">The graphics state.</param>
    private void StrokeMasked(in StrokeGeometry shape, ref GraphicsState state)
    {
        // The bounds are only needed to size the soft mask's layer, so a stroke without one does not read them.
        var masked = state.SoftMask is null ? default : BeginMask(ref state, Inflate(shape.Bounds, state.LineWidth), state.Ctm);
        StrokeShape(shape, state.Ctm, ref state);
        EndMask(ref state, masked);
    }

    /// <summary>Strokes a shape with the state's line style.</summary>
    /// <param name="shape">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page; the line width is scaled by it.</param>
    /// <param name="state">The graphics state.</param>
    private void StrokeShape(in StrokeGeometry shape, Matrix3x2 matrix, ref GraphicsState state)
    {
        var scale = MatrixScale(matrix);
        if (shape.Path is { } path && TryStrokeOutline(path, matrix, scale, ref state))
        {
            return;
        }

        // Lines stay visible at low zoom: ISO 32000-2 8.4.3.2 draws a zero width as the thinnest line the device can make,
        // and a very thin line is kept as readable. The picture is replayed at any scale, so a line that could fall below
        // a pixel is drawn twice: at its width, and as a hairline, which Skia keeps one pixel wide.
        var thin = state.LineWidth > 0 && state.LineWidth * scale < ThinLineLimit;
        if (!thin)
        {
            DrawStroke(shape, matrix, scale, ref state, new(state.StrokeAlpha, ElementBlend(state.BlendMode), false));
            return;
        }

        var layered = state.StrokeAlpha < 1 || state.BlendMode != PdfBlendMode.Normal;
        if (layered)
        {
            BeginStrokeLayer(shape, matrix, ref state);
        }

        var alpha = layered ? 1 : state.StrokeAlpha;
        var blend = layered ? SKBlendMode.SrcOver : ElementBlend(state.BlendMode);
        DrawStroke(shape, matrix, scale, ref state, new(alpha, blend, true));
        if (layered)
        {
            _canvas.Restore();
        }
    }

    /// <summary>Strokes with a mesh shading or a large-period tiling pattern by filling the stroke's outline.</summary>
    /// <param name="path">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page.</param>
    /// <param name="scale">The matrix's average scale.</param>
    /// <param name="state">The graphics state.</param>
    /// <returns><see langword="true"/> when the stroke was drawn.</returns>
    private bool TryStrokeOutline(SKPath path, Matrix3x2 matrix, float scale, ref GraphicsState state)
    {
        if (state.Stroke.Pattern is { Shading.IsMesh: true } meshPattern)
        {
            StrokeMesh(path, matrix, meshPattern, scale, ref state);
            return true;
        }

        return state.Stroke.Pattern is { Tile.Picture: not null } tilePattern && TryStrokeTileCopies(path, matrix, tilePattern, scale, ref state);
    }

    /// <summary>Sets up the stroke paint and draws a shape, and then its hairline when asked.</summary>
    /// <param name="shape">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page.</param>
    /// <param name="scale">The matrix's average scale.</param>
    /// <param name="state">The graphics state.</param>
    /// <param name="pass">The alpha and blend to stroke with, and whether to add a hairline.</param>
    private void DrawStroke(in StrokeGeometry shape, Matrix3x2 matrix, float scale, ref GraphicsState state, in StrokePass pass)
    {
        ConfigureStroke(ref state);
        _paint.BlendMode = pass.Blend;
        using var dash = CreateDash(state.Dash, state.DashPhase, scale);
        _paint.PathEffect = dash;
        var clipped = ClipShadingBox(state.Stroke);
        using var shader = ApplyColor(state.Stroke, pass.Alpha, matrix);
        _canvas.SetMatrix(SkiaConversions.ToSkMatrix(matrix));
        shape.Draw(_canvas, _paint);
        if (pass.Hairline)
        {
            _paint.StrokeWidth = 0;
            shape.Draw(_canvas, _paint);
        }

        if (clipped)
        {
            _canvas.Restore();
        }
    }

    /// <summary>Resets the paint to stroke with the state's width, caps, joins and miter limit.</summary>
    /// <param name="state">The graphics state.</param>
    private void ConfigureStroke(ref GraphicsState state)
    {
        _paint.Reset();
        _paint.IsAntialias = true;
        _paint.Style = SKPaintStyle.Stroke;
        _paint.StrokeWidth = state.LineWidth;

        // Skia ignores a negative limit and keeps its default, so clamp. ISO 32000-2 8.4.3.5 defines the miter length over the
        // line width as 1 / sin(angle / 2), which is at least 1, so a limit below 1 bevels every corner.
        _paint.StrokeMiter = Math.Max(0, state.MiterLimit);
        _paint.StrokeCap = state.LineCap switch
        {
            RoundCap => SKStrokeCap.Round,
            SquareCap => SKStrokeCap.Square,
            _ => SKStrokeCap.Butt,
        };
        _paint.StrokeJoin = state.LineJoin switch
        {
            RoundJoin => SKStrokeJoin.Round,
            BevelJoin => SKStrokeJoin.Bevel,
            _ => SKStrokeJoin.Miter,
        };
    }

    /// <summary>Opens a layer that composites a thin translucent stroke once, so its hairline and body do not double up.</summary>
    /// <param name="shape">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page.</param>
    /// <param name="state">The graphics state.</param>
    private void BeginStrokeLayer(in StrokeGeometry shape, Matrix3x2 matrix, ref GraphicsState state)
    {
        var bounds = SkiaConversions.ToSkMatrix(matrix).MapRect(Inflate(shape.Bounds, state.LineWidth * Math.Max(1, state.MiterLimit)));
        _canvas.SetMatrix(SKMatrix.Identity);
        _layerPaint.Reset();
        _layerPaint.BlendMode = ElementBlend(state.BlendMode);
        _layerPaint.Color = WhiteWithAlpha(state.StrokeAlpha);
        SaveLayer(bounds, _layerPaint);
    }

    /// <summary>Strokes a path with a mesh shading: the stroke's outline is filled with the mesh.</summary>
    /// <param name="path">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page.</param>
    /// <param name="pattern">The mesh pattern.</param>
    /// <param name="scale">The matrix's average scale.</param>
    /// <param name="state">The graphics state.</param>
    private void StrokeMesh(SKPath path, Matrix3x2 matrix, PdfPatternPaint pattern, float scale, ref GraphicsState state)
    {
        ConfigureStroke(ref state);
        _paint.StrokeWidth = Math.Max(state.LineWidth, 1 / Math.Max(scale, MinScale));
        using var dash = CreateDash(state.Dash, state.DashPhase, scale);
        _paint.PathEffect = dash;
        using var builder = new SKPathBuilder();
        if (!_paint.GetFillPath(path, builder))
        {
            return;
        }

        using var outline = builder.Detach();
        FillMesh(outline, matrix, pattern, state.StrokeAlpha, state.BlendMode);
    }

    /// <summary>Fills a shape with a mesh shading pattern.</summary>
    /// <param name="path">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="alpha">The alpha.</param>
    /// <param name="blend">The blend mode.</param>
    private void FillMesh(SKPath path, Matrix3x2 matrix, PdfPatternPaint pattern, float alpha, PdfBlendMode blend)
    {
        var mesh = pattern.Shading!.GetMesh(MatrixScale(pattern.Matrix));
        if (mesh is null)
        {
            return;
        }

        _ = _canvas.Save();
        _canvas.SetMatrix(SkiaConversions.ToSkMatrix(matrix));
        _canvas.ClipPath(path, SKClipOperation.Intersect, true);
        _canvas.SetMatrix(SkiaConversions.ToSkMatrix(pattern.Matrix));
        if (pattern.Shading!.BBox is { } box)
        {
            _canvas.ClipRect(new(box.Left, box.Bottom, box.Right, box.Top), SKClipOperation.Intersect, true);
        }

        DrawMesh(mesh, alpha, blend, pattern.Shading.AntiAlias);
        _canvas.Restore();
    }

    /// <summary>Draws the triangles of a mesh with the current canvas matrix.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="alpha">The alpha.</param>
    /// <param name="blend">The blend mode.</param>
    /// <param name="dither">Whether the shading asks for /AntiAlias.</param>
    private void DrawMesh(ShadingMesh mesh, float alpha, PdfBlendMode blend, bool dither)
    {
        _paint.Reset();
        _paint.IsAntialias = true;
        _paint.IsDither = dither;
        _paint.Color = WhiteWithAlpha(alpha);
        _paint.BlendMode = ElementBlend(blend);
        _paint.Shader = mesh.Ramp is { } ramp ? SkiaResources.Shader(ramp) : null;
        foreach (var chunk in mesh.Chunks)
        {
            _canvas.DrawVertices(SkiaResources.Vertices(chunk), SKBlendMode.Dst, _paint);
        }
    }

    /// <summary>Paints a shading over the whole clip with the current canvas matrix.</summary>
    /// <param name="shading">The shading.</param>
    /// <param name="alpha">The alpha.</param>
    /// <param name="blend">The blend mode.</param>
    /// <param name="scale">The page units one unit of shading space spans, which sets how finely patch meshes are cut.</param>
    private void DrawShadingPaint(PdfShading shading, float alpha, PdfBlendMode blend, float scale)
    {
        if (shading.IsMesh)
        {
            if (shading.GetMesh(scale)is { } mesh)
            {
                DrawMesh(mesh, alpha, blend, shading.AntiAlias);
            }

            return;
        }

        if (shading.GetShader()is not { } shader)
        {
            return;
        }

        _paint.Reset();
        _paint.IsAntialias = true;
        _paint.IsDither = shading.AntiAlias;
        _paint.Shader = SkiaResources.Shader(shader);
        _paint.Color = WhiteWithAlpha(alpha);
        _paint.BlendMode = ElementBlend(blend);
        _canvas.DrawPaint(_paint);
    }

    /// <summary>Groups a solid glyph with earlier separate glyphs of the same colour.</summary>
    /// <param name="path">The cached glyph outline.</param>
    /// <param name="matrix">The glyph-to-page transform.</param>
    /// <param name="color">The fill colour.</param>
    /// <returns>True when drawing has been deferred into the batch.</returns>
    private bool QueueGlyph(SKPath path, Matrix3x2 matrix, in ColorState color)
    {
        var nativeMatrix = SkiaConversions.ToSkMatrix(matrix);
        var bounds = nativeMatrix.MapRect(path.Bounds);
        if (!AreBatchBoundsUsable(bounds))
        {
            return false;
        }

        if (_batchCount > 0 && !CanJoinGlyphBatch(bounds, color))
        {
            FlushGlyphBatch();
        }

        if (_batchCount == 0)
        {
            _firstBatchedGlyph = path;
            _firstBatchedMatrix = matrix;
            _batchColor = color;
            _batchBounds = bounds;
            _batchCount = 1;
            return true;
        }

        if (_batchCount == 1)
        {
            var firstMatrix = SkiaConversions.ToSkMatrix(_firstBatchedMatrix);
            _glyphBatchBuilder.AddPath(_firstBatchedGlyph!, in firstMatrix);
        }

        _glyphBatchBuilder.AddPath(path, in nativeMatrix);
        _batchBounds = new(
            MathF.Min(_batchBounds.Left, bounds.Left),
            MathF.Min(_batchBounds.Top, bounds.Top),
            MathF.Max(_batchBounds.Right, bounds.Right),
            MathF.Max(_batchBounds.Bottom, bounds.Bottom));
        _batchCount++;
        return true;
    }

    /// <summary>Checks whether a glyph can use the same page-space path as a pending group.</summary>
    /// <param name="bounds">The candidate bounds.</param>
    /// <param name="color">The candidate colour.</param>
    /// <returns>True when it can join the group.</returns>
    private bool CanJoinGlyphBatch(SKRect bounds, in ColorState color) =>
        _batchCount < MaxBatchedGlyphs && color == _batchColor && AreGlyphsSeparated(_batchBounds, bounds)
        && MathF.Max(bounds.Right, _batchBounds.Right) - MathF.Min(bounds.Left, _batchBounds.Left) <= MaxGlyphBatchSide
        && MathF.Max(bounds.Bottom, _batchBounds.Bottom) - MathF.Min(bounds.Top, _batchBounds.Top) <= MaxGlyphBatchSide;

    /// <summary>Uses grouped drawing only when per-glyph transparency, effects and strokes are absent.</summary>
    /// <param name="mode">The PDF text mode.</param>
    /// <param name="ownsPath">Whether the path ends with this glyph's call.</param>
    /// <param name="state">The graphics state.</param>
    /// <returns>True when the glyph may enter a batch.</returns>
    private bool CanBatchGlyph(int mode, bool ownsPath, ref GraphicsState state) =>
        _batchGlyphs && mode == 0 && !ownsPath && !state.Fill.PaintsNothing && state.Fill.Pattern is null
        && state.FillAlpha >= 1F && state.BlendMode == PdfBlendMode.Normal && state.SoftMask is null
        && _knockout.Count == 0;

    /// <summary>Records a pending glyph group before any later drawing or clip change.</summary>
    private void FlushGlyphBatch()
    {
        var count = _batchCount;
        if (count == 0)
        {
            return;
        }

        _batchCount = 0;
        var first = _firstBatchedGlyph;
        _firstBatchedGlyph = null;
        if (count == 1)
        {
            FillShape(first!, _firstBatchedMatrix, _batchColor, 1, PdfBlendMode.Normal);
            return;
        }

        using var combined = _glyphBatchBuilder.Detach();
        FillShape(combined, Matrix3x2.Identity, _batchColor, 1, PdfBlendMode.Normal);
    }

    /// <summary>Converts a glyph once per recording when it repeats.</summary>
    /// <param name="font">The font that owns the outline.</param>
    /// <param name="code">The character code.</param>
    /// <param name="outline">The managed outline.</param>
    /// <returns>The path and its disposal ownership.</returns>
    private GlyphPathLease GetGlyphPath(PdfFont font, int code, PdfPath outline)
    {
        var key = new GlyphOutlineKey(font, code);
        if (_glyphPaths.TryGetValue(key, out var existing))
        {
            return new(existing, false);
        }

        var created = SkiaPathConversion.Create(_pathBuilder, outline);
        if (_glyphPaths.Count >= MaxCachedGlyphPaths)
        {
            return new(created, true);
        }

        _glyphPaths.Add(key, created);
        return new(created, false);
    }

    /// <summary>What a stroke draws: a native path, or, with no path, one straight segment.</summary>
    /// <param name="Path">The native path, or <see langword="null"/> for a segment.</param>
    /// <param name="From">The start of the segment.</param>
    /// <param name="To">The end of the segment.</param>
    private readonly record struct StrokeGeometry(SKPath? Path, SKPoint From, SKPoint To)
    {
        /// <summary>Initializes a new instance of the <see cref="StrokeGeometry"/> struct for a path.</summary>
        /// <param name="path">The native path.</param>
        internal StrokeGeometry(SKPath path)
            : this(path, default, default)
        {
        }

        /// <summary>Gets the area the shape's points cover.</summary>
        internal SKRect Bounds => Path is { } path
            ? path.Bounds
            : new(MathF.Min(From.X, To.X), MathF.Min(From.Y, To.Y), MathF.Max(From.X, To.X), MathF.Max(From.Y, To.Y));

        /// <summary>Draws the shape with a paint.</summary>
        /// <param name="canvas">The canvas.</param>
        /// <param name="paint">The paint.</param>
        internal void Draw(SKCanvas canvas, SKPaint paint)
        {
            if (Path is { } path)
            {
                canvas.DrawPath(path, paint);
                return;
            }

            canvas.DrawLine(From, To, paint);
        }
    }

    /// <summary>Identifies an outline in one page recording.</summary>
    /// <param name="Font">The owning font.</param>
    /// <param name="Code">The font's character code.</param>
    private readonly record struct GlyphOutlineKey(PdfFont Font, int Code);

    /// <summary>Releases only a path that exceeded the recording cache.</summary>
    /// <param name="Path">The native glyph outline.</param>
    /// <param name="Owns">Whether this draw must dispose it.</param>
    private readonly record struct GlyphPathLease(SKPath Path, bool Owns) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose()
        {
            if (Owns)
            {
                Path.Dispose();
            }
        }
    }

    /// <summary>The lattice positions of a tiling period that reach an area.</summary>
    /// <param name="FirstColumn">The first column.</param>
    /// <param name="LastColumn">The last column.</param>
    /// <param name="FirstRow">The first row.</param>
    /// <param name="LastRow">The last row.</param>
    private readonly record struct TileRange(int FirstColumn, int LastColumn, int FirstRow, int LastRow)
    {
        /// <summary>Gets the number of positions.</summary>
        internal long Count => (long)(LastColumn - FirstColumn + 1) * (LastRow - FirstRow + 1);

        /// <summary>Finds the positions of a period that reach an area.</summary>
        /// <param name="reach">The area in pattern space.</param>
        /// <param name="tile">One period in pattern space.</param>
        /// <returns>The positions, or null when the area is not finite.</returns>
        internal static TileRange? Create(SKRect reach, SKRect tile) =>
            !float.IsFinite(reach.Width) || !float.IsFinite(reach.Height)
                ? null
                : new(
                    (int)MathF.Floor((reach.Left - tile.Left) / tile.Width),
                    (int)MathF.Floor((reach.Right - tile.Left) / tile.Width),
                    (int)MathF.Floor((reach.Top - tile.Top) / tile.Height),
                    (int)MathF.Floor((reach.Bottom - tile.Top) / tile.Height));
    }

    /// <summary>A knockout child group's own recording.</summary>
    /// <param name="Recorder">The recorder the group's content goes to.</param>
    /// <param name="Parent">The canvas to draw on when the group ends.</param>
    /// <param name="Canvas">The recorder's canvas.</param>
    private sealed record PictureFrame(SKPictureRecorder Recorder, SKCanvas Parent, SKCanvas Canvas);
}
