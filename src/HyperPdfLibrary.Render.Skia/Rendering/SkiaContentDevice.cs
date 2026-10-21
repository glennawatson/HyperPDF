// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Render.Skia.Images;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// The Skia device: records what the interpreter draws into an <see cref = "SKPicture"/>. The picture is in the space the
/// interpreter started in (viewer space for pages), so replaying it only needs a matrix.
/// </summary>
[DebuggerDisplay("SkiaContentDevice")]
internal sealed class SkiaContentDevice : IPictureDevice, IType3GlyphScope
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

    /// <summary>The most converted glyph outlines retained while recording one picture.</summary>
    private const int MaxCachedGlyphPaths = 512;

    /// <summary>The most pattern shaders kept for one recording; further ones are made for the one draw.</summary>
    private const int MaxCachedShaders = 64;

    /// <summary>The image masks one Type 3 glyph procedure can draw and have kept; later ones share the last place.</summary>
    private const int MaxType3Images = 8;

    /// <summary>The colour channel table that leaves a channel unchanged.</summary>
    private static readonly byte[] IdentityTable = CreateIdentityTable();

    /// <summary>The alpha table that keeps zero and raises every other value to full.</summary>
    private static readonly byte[] ShapeTable = CreateShapeTable();

    /// <summary>The cull rectangle of a group recording that has no bounds.</summary>
    private static readonly SKRect UnboundedCull = new(-UnboundedExtent, -UnboundedExtent, UnboundedExtent, UnboundedExtent);

    /// <summary>The style of a picture shown at any scale with the default quality: no page matrix.</summary>
    private static readonly PdfPictureStyle Plain = new(PdfRenderQuality.Default, null);

    /// <summary>The recorder, ended by <see cref="Finish"/>.</summary>
    private readonly SKPictureRecorder _recorder = new();

    /// <summary>The native builder reused only while this device records path operations.</summary>
    private readonly SKPathBuilder _pathBuilder = new();

    /// <summary>The native path a stroked glyph outline is transformed into.</summary>
    private readonly SKPath _strokedGlyphPath = new();

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

    /// <summary>The Type 3 glyphs whose procedures are running, innermost last.</summary>
    private readonly List<Type3Scope> _type3 = [];

    /// <summary>The pattern shaders made for this recording, by pattern and matrix.</summary>
    private readonly Dictionary<ShaderKey, SKShader> _shaders = [];

    /// <summary>The canvas of the page or picture recording itself.</summary>
    private SKCanvas _rootCanvas;

    /// <summary>The count of the images drawn, including those drawn into nested pictures.</summary>
    private PictureWeight _weight;

    /// <summary>How edges, thin lines and scaled images are drawn into this recording.</summary>
    private PdfRenderQuality _quality = PdfRenderQuality.Default;

    /// <summary>The matrix from the recording's space to device pixels, without the tile offset; valid when <see cref="_hasDevice"/>.</summary>
    private Matrix3x2 _device = Matrix3x2.Identity;

    /// <summary>The matrix from device pixels back to the recording's space; valid when <see cref="_hasDevice"/>.</summary>
    private SKMatrix _deviceInverse = SKMatrix.Identity;

    /// <summary>Whether the recording serves one page matrix, so sizes in device pixels are known.</summary>
    private bool _hasDevice;

    /// <summary>Whether paths, strokes and clips have smoothed edges.</summary>
    private bool _smoothGraphics = true;

    /// <summary>The line width the next stroke uses instead of the graphics state's, or NaN for the state's.</summary>
    private float _strokeWidth = float.NaN;

    /// <summary>The canvas the interpreter draws on now: the recording canvas, or a knockout child group's.</summary>
    private SKCanvas _canvas;

    /// <summary>Turns any non-zero alpha into full alpha, which marks the pixels a group painted.</summary>
    private SKColorFilter? _shapeFilter;

    /// <summary>The device's reusable luminosity-to-alpha filter.</summary>
    private SKColorFilter? _lumaFilter;

    /// <summary>Whether <see cref="Finish"/> has run.</summary>
    private bool _finished;

    /// <summary>The registered backend's pool that owns this rental, or null for a directly constructed device.</summary>
    private ReusableSlots<SkiaContentDevice>? _pool;

    /// <summary>The glyph masks every device of the backend shares, or null when small text is always filled from its outline.</summary>
    private SkiaGlyphMasks? _masks;

    /// <summary>Draws small text from glyph masks; made when the first mask is drawn.</summary>
    private GlyphMaskPainter? _maskPainter;

    /// <summary>One once <see cref="Dispose"/> has run since the recording began.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="SkiaContentDevice"/> class.</summary>
    /// <param name="cull">The area the picture covers.</param>
    internal SkiaContentDevice(SKRect cull)
        : this(cull, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SkiaContentDevice"/> class.</summary>
    /// <param name="cull">The area the picture covers.</param>
    /// <param name="weight">Counts the images drawn; pictures nested in another recording share its count. Null counts them in the device itself.</param>
    private SkiaContentDevice(SKRect cull, PictureWeight? weight) => Begin(cull, weight);

    /// <summary>Gets the count of the pixel memory of the images drawn, including those drawn into nested pictures.</summary>
    public PictureWeight Weight => _weight;

    /// <inheritdoc/>
    public bool RetainsPaths => false;

    /// <summary>Gets or sets a value indicating whether scaled images are smoothed; when false they use the nearest source pixel and unsmoothed edges.</summary>
    internal bool SmoothImages { get; set; } = true;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Save() => _ = _canvas.Save();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Restore() => RestoreCanvas();

    /// <inheritdoc/>
    public void Clip(PdfPath path, bool evenOdd, Matrix3x2 ctm)
    {
        SetCanvasMatrix(SkiaConversions.ToSkMatrix(ctm));
        if (SkiaPathConversion.TryGetRectangle(path, out var rectangle))
        {
            // Skia clips to a rectangle path exactly as it clips to the rectangle, and this needs no native path.
            _canvas.ClipRect(rectangle, SKClipOperation.Intersect, _smoothGraphics);
            return;
        }

        using var native = SkiaPathConversion.Create(_pathBuilder, path, evenOdd);
        _canvas.ClipPath(native, SKClipOperation.Intersect, _smoothGraphics);
    }

    /// <inheritdoc/>
    public void Fill(PdfPath path, bool evenOdd, ref GraphicsState state)
    {
        // A plain colour fill of an axis-aligned rectangle (re f) draws the rectangle, which needs no native path.
        if (state.Fill.Pattern is null && SkiaPathConversion.TryGetRectangle(path, out var rectangle))
        {
            FillRectangle(rectangle, ref state);
            return;
        }

        using var native = SkiaPathConversion.Create(_pathBuilder, path, evenOdd);
        var masked = BeginMask(ref state, state.SoftMask is null ? SKRect.Empty : native.Bounds, state.Ctm);
        FillShape(native, state.Ctm, state.Fill, state.FillAlpha, state.BlendMode);
        EndMask(ref state, masked);
    }

    /// <inheritdoc/>
    public void Stroke(PdfPath path, ref GraphicsState state)
    {
        if (_hasDevice && TryStrokeFitted(path, ref state))
        {
            return;
        }

        // One straight segment in a plain colour is drawn as a line, which needs no native path.
        if (state.Stroke.Pattern is null && SkiaPathConversion.TryGetLine(path, out var from, out var to))
        {
            StrokeMasked(new(from, to), ref state);
            return;
        }

        using var native = SkiaPathConversion.Create(_pathBuilder, path);
        StrokeMasked(new(native), ref state);
    }

    /// <inheritdoc/>
    public void DrawImage(IPdfRenderImage image, bool isMask, bool smooth, ref GraphicsState state)
    {
        Weight.Add(image);
        var native = SkiaResources.Image(image);
        var matrix = new Matrix3x2(1F / image.Width, 0, 0, -1F / image.Height, 0, 1) * state.Ctm;
        var masked = BeginMask(ref state, new(0, 0, 1, 1), state.Ctm);
        if (isMask && _type3.Count > 0 && CanDrawMask(ref state, masked.Active) && TryDrawImageMask(native, matrix, ref state, smooth))
        {
            EndMask(ref state, masked);
            return;
        }

        SetCanvasMatrix(SkiaConversions.ToSkMatrix(matrix));
        _paint.Reset();
        _paint.IsAntialias = SmoothImages;
        _paint.BlendMode = ElementBlend(state.BlendMode);
        _paint.Color = isMask ? SkiaConversions.ToSkColor(state.Fill, state.FillAlpha) : WhiteWithAlpha(state.FillAlpha);
        var sampling = SkiaImageSampling.Choose(native.Width, state.Ctm, smooth, SmoothImages);
        _canvas.DrawImage(native, new SKRect(0, 0, image.Width, image.Height), sampling, _paint);
        EndMask(ref state, masked);
    }

    /// <inheritdoc/>
    public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
    {
        var mode = state.RenderMode;
        if (IsMode(HiddenModes, mode) || !TryGetOutline(glyph, out var outline))
        {
            return;
        }

        var matrix = glyph.GlyphMatrix * state.Ctm;
        var masked = BeginMask(ref state, state.SoftMask is null ? SKRect.Empty : OutlineBounds(glyph.Font, glyph.Code, outline), matrix);
        if (IsMode(FillModes, mode) && !state.Fill.PaintsNothing && !(CanDrawMask(ref state, masked.Active) && TryDrawMask(glyph, outline, matrix, ref state)))
        {
            using var fillLease = GetGlyphPath(glyph.Font, glyph.Code, outline);
            FillTextShape(fillLease.Path, matrix, state.Fill, state.FillAlpha, state.BlendMode);
        }

        if (IsMode(StrokeModes, mode) && !state.Stroke.PaintsNothing)
        {
            using var strokeLease = GetGlyphPath(glyph.Font, glyph.Code, outline);
            StrokeGlyph(strokeLease.Path, glyph.GlyphMatrix, ref state);
        }

        EndMask(ref state, masked);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BeginType3Glyph(PdfFont font, int code) => _type3.Add(new(font, code, 0));

    /// <inheritdoc/>
    public void EndType3Glyph()
    {
        if (_type3.Count > 0)
        {
            _type3.RemoveAt(_type3.Count - 1);
        }
    }

    /// <inheritdoc/>
    public void PaintShading(PdfShading shading, ref GraphicsState state)
    {
        var masked = BeginMask(ref state, SKRect.Empty, state.Ctm);
        _ = _canvas.Save();
        SetCanvasMatrix(SkiaConversions.ToSkMatrix(state.Ctm));
        if (shading.BBox is { } box)
        {
            _canvas.ClipRect(new(box.Left, box.Bottom, box.Right, box.Top), SKClipOperation.Intersect, _smoothGraphics);
        }

        DrawShadingPaint(shading, state.FillAlpha, state.BlendMode, MatrixScale(state.Ctm));
        RestoreCanvas();
        EndMask(ref state, masked);
    }

    /// <inheritdoc/>
    public void BeginGroup(in GroupInfo group)
    {
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
        SetCanvasMatrix(SKMatrix.Identity);

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

            RestoreCanvas();
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
    public IPictureDevice CreatePictureDevice(PdfRect cull)
    {
        var bounds = SkiaConversions.ToSkRect(cull);
        var style = new PdfPictureStyle(_quality, null);
        if (_pool is { } pool)
        {
            return Rent(pool, bounds, Weight, style, _masks);
        }

        var created = new SkiaContentDevice(bounds, Weight);
        created.Configure(style);
        created._masks = _masks;
        return created;
    }

    /// <inheritdoc/>
    public IPdfRenderPicture Finish()
    {
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

        if (_pool is not null && Recycle())
        {
            return;
        }

        DisposeShaders();
        _shapeFilter?.Dispose();
        _lumaFilter?.Dispose();
        _maskPainter?.Dispose();
        _paint.Dispose();
        foreach (var path in _glyphPaths.Values)
        {
            path.Dispose();
        }

        _pathBuilder.Dispose();
        _strokedGlyphPath.Dispose();
        _layerPaint.Dispose();
        _rootCanvas.Dispose();
        _canvas.Dispose();
        _recorder.Dispose();
    }

    /// <summary>Hands out an idle device, or makes one when none is idle. Dispose it to give it back.</summary>
    /// <param name="pool">The registered backend's idle devices.</param>
    /// <param name="cull">The area the picture covers.</param>
    /// <param name="weight">Counts the images drawn; null counts them in the device itself.</param>
    /// <returns>A device that is recording, owned by the caller until it is disposed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SkiaContentDevice Rent(ReusableSlots<SkiaContentDevice> pool, SKRect cull, PictureWeight? weight) => Rent(pool, cull, weight, Plain, null);

    /// <summary>Hands out an idle device, or makes one when none is idle. Dispose it to give it back.</summary>
    /// <param name="pool">The registered backend's idle devices.</param>
    /// <param name="cull">The area the picture covers.</param>
    /// <param name="weight">Counts the images drawn; null counts them in the device itself.</param>
    /// <param name="masks">The glyph masks the backend's devices share.</param>
    /// <returns>A device that is recording, owned by the caller until it is disposed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SkiaContentDevice Rent(ReusableSlots<SkiaContentDevice> pool, SKRect cull, PictureWeight? weight, SkiaGlyphMasks masks) => Rent(pool, cull, weight, Plain, masks);

    /// <summary>Hands out an idle device, or makes one when none is idle, for a picture shown in a known way. Dispose it to give it back.</summary>
    /// <param name="pool">The registered backend's idle devices.</param>
    /// <param name="cull">The area the picture covers.</param>
    /// <param name="weight">Counts the images drawn; null counts them in the device itself.</param>
    /// <param name="style">How the picture is shown: the quality settings and the page matrix.</param>
    /// <returns>A device that is recording, owned by the caller until it is disposed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SkiaContentDevice Rent(ReusableSlots<SkiaContentDevice> pool, SKRect cull, PictureWeight? weight, in PdfPictureStyle style) => Rent(pool, cull, weight, style, null);

    /// <summary>Hands out an idle device, or makes one when none is idle, for a picture shown in a known way. Dispose it to give it back.</summary>
    /// <param name="pool">The registered backend's idle devices.</param>
    /// <param name="cull">The area the picture covers.</param>
    /// <param name="weight">Counts the images drawn; null counts them in the device itself.</param>
    /// <param name="style">How the picture is shown: the quality settings and the page matrix.</param>
    /// <param name="masks">The glyph masks the backend's devices share, or null to fill every glyph from its outline.</param>
    /// <returns>A device that is recording, owned by the caller until it is disposed.</returns>
    internal static SkiaContentDevice Rent(ReusableSlots<SkiaContentDevice> pool, SKRect cull, PictureWeight? weight, in PdfPictureStyle style, SkiaGlyphMasks? masks)
    {
        var device = pool.Take();
        if (device is null)
        {
            device = new(cull, weight);
        }
        else
        {
            device.Begin(cull, weight);
        }

        device.Configure(style);
        device._pool = pool;
        device._masks = masks;
        return device;
    }

    /// <summary>Gets the area of a glyph's native outline, converting it only if it is not kept already.</summary>
    /// <param name="font">The font that owns the outline.</param>
    /// <param name="code">The character code.</param>
    /// <param name="outline">The managed outline.</param>
    /// <returns>The area in the glyph's own space.</returns>
    internal SKRect OutlineBounds(PdfFont font, int code, PdfPath outline)
    {
        using var lease = GetGlyphPath(font, code, outline);
        return lease.Path.Bounds;
    }

    /// <summary>Draws a glyph's native outline, converting it only if it is not kept already.</summary>
    /// <param name="font">The font that owns the outline.</param>
    /// <param name="code">The character code.</param>
    /// <param name="outline">The managed outline.</param>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="paint">The paint.</param>
    internal void DrawOutline(PdfFont font, int code, PdfPath outline, SKCanvas canvas, SKPaint paint)
    {
        using var lease = GetGlyphPath(font, code, outline);
        canvas.DrawPath(lease.Path, paint);
    }

    /// <summary>Gets the outline of a glyph that has something to draw.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <param name="outline">Receives the outline.</param>
    /// <returns><see langword="false"/> when the glyph has no outline or no commands, as a space has.</returns>
    private static bool TryGetOutline(in GlyphEvent glyph, [NotNullWhen(true)] out PdfPath? outline)
    {
        outline = glyph.Font.GetOutline(glyph.Code);
        return outline is { Commands.IsEmpty: false };
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
    /// <param name="luma">The device-owned luminosity filter.</param>
    /// <param name="transfer">The transfer table.</param>
    /// <returns>The filter.</returns>
    private static SKColorFilter CreateLumaFilter(SKColorFilter luma, byte[] transfer)
    {
        // SkiaSharp rejects null channel tables, so the colour channels get the identity table.
        using var table = SKColorFilter.CreateTable(transfer, IdentityTable, IdentityTable, IdentityTable);
        return SKColorFilter.CreateCompose(table, luma);
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

    /// <summary>Makes the shader of a shading pattern.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="local">The matrix from the shape's space to the pattern's.</param>
    /// <returns>The shader, or null.</returns>
    private static SKShader? MakeShadingShader(PdfPatternPaint pattern, SKMatrix local) =>
        pattern.Shading?.GetShader() is { } source ? SkiaResources.Shader(source).WithLocalMatrix(local) : null;

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
        _maskPainter?.ForgetMatrix();
        _canvas = nested;
        SetCanvasMatrix(SKMatrix.Identity);
        _knockout.Add(group.Knockout);
    }

    /// <summary>Ends a group begun by <see cref="BeginKnockoutChild"/>: clears the parent where the group painted, then composites it.</summary>
    /// <param name="group">The group.</param>
    /// <param name="frame">The group's recording.</param>
    private void EndKnockoutChild(in GroupInfo group, PictureFrame frame)
    {
        using var picture = frame.Recorder.EndRecording();
        frame.Canvas.Dispose();
        _maskPainter?.ForgetMatrix();
        _canvas = frame.Parent;
        frame.Recorder.Dispose();
        SetCanvasMatrix(SKMatrix.Identity);

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
        RestoreCanvas();
    }

    /// <summary>Starts a recording on the device's own recorder, which can record again once the previous picture has been taken.</summary>
    /// <param name="cull">The area the picture covers.</param>
    /// <param name="weight">Counts the images drawn; null counts them in the device itself.</param>
    [MemberNotNull(nameof(_rootCanvas), nameof(_canvas), nameof(_weight))]
    private void Begin(SKRect cull, PictureWeight? weight)
    {
        if (weight is null)
        {
            _ownWeight.Reset();
        }

        _weight = weight ?? _ownWeight;
        _finished = false;
        Volatile.Write(ref _disposed, 0);
        _rootCanvas = _recorder.BeginRecording(cull, true);
        _maskPainter?.ForgetMatrix();
        _canvas = _rootCanvas;
    }

    /// <summary>Sets how this recording draws edges, thin lines and scaled images.</summary>
    /// <param name="style">How the picture will be shown.</param>
    private void Configure(in PdfPictureStyle style)
    {
        _quality = style.Quality;
        _smoothGraphics = _quality.SmoothGraphics;
        SmoothImages = _quality.SmoothImages;
        _strokeWidth = float.NaN;
        _hasDevice = style.Device is { } device && Matrix3x2.Invert(device, out _);
        if (!_hasDevice)
        {
            _device = Matrix3x2.Identity;
            _deviceInverse = SKMatrix.Identity;
            return;
        }

        _device = style.Device!.Value;
        _ = SkiaConversions.ToSkMatrix(_device).TryInvert(out _deviceInverse);
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
        _type3.Clear();
        DisposeShaders();
        _pathBuilder.Reset();
        _paint.Reset();
        _paint.IsAntialias = true;
        _layerPaint.Reset();
        Configure(Plain);
        _weight = _ownWeight;
        _ownWeight.Reset();
        SmoothImages = true;
        var pool = _pool;
        _pool = null;
        return pool!.Offer(this);
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

    /// <summary>Sets the canvas matrix, which the glyph mask painter can no longer assume it set last.</summary>
    /// <param name="matrix">The matrix.</param>
    private void SetCanvasMatrix(SKMatrix matrix)
    {
        _maskPainter?.ForgetMatrix();
        _canvas.SetMatrix(matrix);
    }

    /// <summary>Restores the canvas, which brings back a matrix the glyph mask painter did not set.</summary>
    private void RestoreCanvas()
    {
        _maskPainter?.ForgetMatrix();
        _canvas.Restore();
    }

    /// <summary>Moves the canvas, which changes the matrix the glyph mask painter set last.</summary>
    /// <param name="dx">The horizontal distance.</param>
    /// <param name="dy">The vertical distance.</param>
    private void TranslateCanvas(float dx, float dy)
    {
        _maskPainter?.ForgetMatrix();
        _canvas.Translate(dx, dy);
    }

    /// <summary>Starts a layer, limited to a rectangle unless it is empty.</summary>
    /// <param name="bounds">The area in page space, or empty for no limit.</param>
    /// <param name="paint">The layer paint.</param>
    private void SaveLayer(SKRect bounds, SKPaint paint)
    {
        _maskPainter?.ForgetMatrix();
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
        _maskPainter?.ForgetMatrix();
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
        outline.Transform(SkiaConversions.ToSkMatrix(glyphMatrix), _strokedGlyphPath);
        StrokeShape(new(_strokedGlyphPath), state.Ctm, ref state);
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
        SetCanvasMatrix(SKMatrix.Identity);
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
        RestoreCanvas();
    }

    /// <summary>Multiplies the open layer by a soft mask's coverage.</summary>
    /// <param name="mask">The mask.</param>
    /// <param name="bounds">The layer's area in page space, or empty.</param>
    private void ApplyMask(PdfSoftMask mask, SKRect bounds)
    {
        SetCanvasMatrix(SKMatrix.Identity);
        var luma = mask.IsLuminosity ? _lumaFilter ??= SKColorFilter.CreateLumaColor() : null;
        using var filter = luma is not null && mask.Transfer is { } transfer ? CreateLumaFilter(luma, transfer) : null;
        _layerPaint.Reset();
        _layerPaint.BlendMode = SKBlendMode.DstIn;
        _layerPaint.ColorFilter = filter ?? luma;
        SaveLayer(bounds, _layerPaint);
        if (mask.IsLuminosity && SkiaConversions.ToSkColor(mask.Backdrop) != SKColors.Black)
        {
            _paint.Reset();
            _paint.Color = SkiaConversions.ToSkColor(mask.Backdrop);
            _canvas.DrawPaint(_paint);
        }

        SkiaResources.Picture(mask.Picture).Playback(_canvas);
        RestoreCanvas();
    }

    /// <summary>Sets the paint's colour or shader for a fill or stroke colour.</summary>
    /// <param name="color">The colour.</param>
    /// <param name="alpha">The alpha.</param>
    /// <param name="matrix">The matrix the shape is drawn with, from shape space to the page.</param>
    /// <returns>The shader's lease, which is disposed after drawing and releases the shader only when it was not kept for reuse.</returns>
    private ShaderLease ApplyColor(in ColorState color, float alpha, Matrix3x2 matrix)
    {
        if (color.Pattern is not { } pattern)
        {
            _paint.Color = SkiaConversions.ToSkColor(color, alpha);
            return default;
        }

        _paint.Color = WhiteWithAlpha(alpha);

        // Skia cannot filter a shading function over each pixel's area, so /AntiAlias maps to dithering: it spreads the
        // quantisation steps of the colour ramp, which is the aliasing a smooth shading shows.
        _paint.IsDither = pattern.Shading?.AntiAlias == true;
        if (!Matrix3x2.Invert(matrix, out var inverse))
        {
            return default;
        }

        var lease = GetPatternShader(pattern, SkiaConversions.ToSkMatrix(pattern.Matrix * inverse));
        _paint.Shader = lease.Shader;
        return lease;
    }

    /// <summary>
    /// Gets the shader of a pattern for a matrix. A recording fills many shapes with one pattern at one matrix, so the shader is
    /// made once and kept until the recording is recycled; past <see cref="MaxCachedShaders"/> it is made for the one draw.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="local">The matrix from the shape's space to the pattern's.</param>
    /// <returns>The shader's lease.</returns>
    private ShaderLease GetPatternShader(PdfPatternPaint pattern, SKMatrix local)
    {
        object? source = pattern.Tile;
        source ??= pattern.Shading;
        if (source is null)
        {
            return default;
        }

        var key = new ShaderKey(source, local);
        if (_shaders.TryGetValue(key, out var existing))
        {
            return new(existing, false);
        }

        var created = pattern.Tile is { } tile ? CreateTileShader(tile, local) : MakeShadingShader(pattern, local);
        if (created is null)
        {
            return default;
        }

        if (_shaders.Count >= MaxCachedShaders)
        {
            return new(created, true);
        }

        _shaders.Add(key, created);
        return new(created, false);
    }

    /// <summary>Releases the pattern shaders kept for the recording. A picture that drew with one keeps its own reference.</summary>
    private void DisposeShaders()
    {
        foreach (var shader in _shaders.Values)
        {
            shader.Dispose();
        }

        _shaders.Clear();
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
            _paint.IsAntialias = _smoothGraphics;
            _paint.BlendMode = ElementBlend(blend);
            _paint.Color = SkiaConversions.ToSkColor(new(PdfColorSpace.DeviceRgb, background, null, false), alpha);
            SetCanvasMatrix(SkiaConversions.ToSkMatrix(matrix));
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
        _paint.IsAntialias = _smoothGraphics;
        _paint.BlendMode = ElementBlend(blend);
        var clipped = ClipShadingBox(color);
        using var lease = ApplyColor(color, alpha, matrix);
        SetCanvasMatrix(SkiaConversions.ToSkMatrix(matrix));
        _canvas.DrawPath(path, _paint);
        if (clipped)
        {
            RestoreCanvas();
        }
    }

    /// <summary>Fills an axis-aligned rectangle with the state's plain fill colour, as <see cref="FillShape"/> fills a path of the same outline.</summary>
    /// <param name="rectangle">The rectangle in user space.</param>
    /// <param name="state">The graphics state.</param>
    private void FillRectangle(SKRect rectangle, ref GraphicsState state)
    {
        var masked = BeginMask(ref state, rectangle, state.Ctm);
        _paint.Reset();
        _paint.IsAntialias = _smoothGraphics;
        _paint.BlendMode = ElementBlend(state.BlendMode);
        _paint.Color = SkiaConversions.ToSkColor(state.Fill, state.FillAlpha);
        SetCanvasMatrix(SkiaConversions.ToSkMatrix(state.Ctm));
        _canvas.DrawRect(rectangle, _paint);
        EndMask(ref state, masked);
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
        SetCanvasMatrix(SkiaConversions.ToSkMatrix(matrix));
        _canvas.ClipPath(path, SKClipOperation.Intersect, _smoothGraphics);
        var clip = _canvas.DeviceClipBounds;
        var reach = inverse.MapRect(SKRect.Create(clip.Left, clip.Top, clip.Width, clip.Height));
        var range = TileRange.Create(reach, tile);
        if (range is not { } copies || copies.Count > MaxTileCopies)
        {
            RestoreCanvas();
            return false;
        }

        _layerPaint.Reset();
        _layerPaint.Color = WhiteWithAlpha(alpha);
        _layerPaint.BlendMode = ElementBlend(blend);
        DrawTileCopies(SkiaResources.Picture(pattern.Tile.Picture!), patternMatrix, tile, copies);
        RestoreCanvas();
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
                SetCanvasMatrix(patternMatrix);
                TranslateCanvas(column * tile.Width, row * tile.Height);
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
        using var dash = SkiaDash.Create(state.Dash, state.DashPhase, scale);
        _paint.PathEffect = dash;
        _pathBuilder.Reset();
        if (!_paint.GetFillPath(path, _pathBuilder))
        {
            return false;
        }

        using var outline = _pathBuilder.Detach();
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
        SetCanvasMatrix(SkiaConversions.ToSkMatrix(pattern.Matrix));
        _canvas.ClipRect(new(box.Left, box.Bottom, box.Right, box.Top), SKClipOperation.Intersect, _smoothGraphics);
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
        // A caller that fitted a line to the pixel grid has already set the width, in the space the shape is drawn in.
        var width = float.IsNaN(_strokeWidth) ? ResolveWidth(matrix, ref state) : new(_strokeWidth, true);
        _strokeWidth = width.Fitted ? width.Width : float.NaN;
        StrokeSized(shape, matrix, width.Fitted, ref state);
        _strokeWidth = float.NaN;
    }

    /// <summary>Gets the width a stroke is drawn with after stroke adjustment and the thinnest line setting.</summary>
    /// <param name="matrix">The matrix from the shape's space to the page.</param>
    /// <param name="state">The graphics state.</param>
    /// <returns>The width in the shape's space, and whether it was fitted.</returns>
    private SkiaStrokeFit.FittedWidth ResolveWidth(Matrix3x2 matrix, ref GraphicsState state) =>
        _hasDevice
            ? SkiaStrokeFit.Resolve(_quality, state.StrokeAdjustment, state.LineWidth, SkiaStrokeFit.DeviceScale(matrix * _device))
            : new(state.LineWidth, false);

    /// <summary>
    /// Fits a horizontal or vertical line, or a rectangle, to whole pixels when stroke adjustment applies (ISO 32000-2
    /// 10.7.5), and strokes it. Dashed, patterned, masked and turned shapes keep the general path.
    /// </summary>
    /// <param name="path">The path in user space.</param>
    /// <param name="state">The graphics state.</param>
    /// <returns><see langword="true"/> when the stroke was drawn.</returns>
    private bool TryStrokeFitted(PdfPath path, ref GraphicsState state)
    {
        if (!SkiaStrokeFit.IsAdjusting(_quality, state.StrokeAdjustment) || state.Stroke.Pattern is not null || state.SoftMask is not null || !state.Dash.IsEmpty)
        {
            return false;
        }

        var toDevice = state.Ctm * _device;
        var userScale = SkiaStrokeFit.DeviceScale(toDevice);
        var fit = SkiaStrokeFit.Resolve(_quality, state.StrokeAdjustment, state.LineWidth, userScale);
        var pixels = (int)MathF.Round(fit.Width * userScale);
        if (SkiaPathConversion.TryGetLine(path, out var from, out var to))
        {
            return SkiaStrokeFit.TryFitLine(from, to, toDevice, _deviceInverse, pixels, out var line)
                && StrokeFittedShape(new(line.From, line.To), pixels, ref state);
        }

        if (!SkiaPathConversion.TryGetRectangle(path, out var rectangle) || !SkiaStrokeFit.TryFitRectangle(rectangle, toDevice, _deviceInverse, pixels, out var fitted))
        {
            return false;
        }

        return StrokeFittedShape(StrokeGeometry.Rectangle(fitted), pixels, ref state);
    }

    /// <summary>Strokes a shape that is already in the space the picture is recorded in, at a whole-pixel width.</summary>
    /// <param name="shape">The fitted shape.</param>
    /// <param name="pixels">The line width in device pixels.</param>
    /// <param name="state">The graphics state.</param>
    /// <returns><see langword="true"/>, always.</returns>
    private bool StrokeFittedShape(in StrokeGeometry shape, int pixels, ref GraphicsState state)
    {
        _strokeWidth = pixels / SkiaStrokeFit.DeviceScale(_device);
        StrokeShape(shape, Matrix3x2.Identity, ref state);
        return true;
    }

    /// <summary>Strokes a shape with a width already resolved.</summary>
    /// <param name="shape">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page.</param>
    /// <param name="fitted">Whether the width was fitted to the pixel grid or the thinnest line setting, so it needs no hairline.</param>
    /// <param name="state">The graphics state.</param>
    private void StrokeSized(in StrokeGeometry shape, Matrix3x2 matrix, bool fitted, ref GraphicsState state)
    {
        var scale = MatrixScale(matrix);
        if (shape.Path is { } path && TryStrokeOutline(path, matrix, scale, ref state))
        {
            return;
        }

        // Lines stay visible at low zoom: ISO 32000-2 8.4.3.2 draws a zero width as the thinnest line the device can make,
        // and a very thin line is kept as readable. The picture is replayed at any scale, so a line that could fall below
        // a pixel is drawn twice: at its width, and as a hairline, which Skia keeps one pixel wide.
        if (fitted || state.LineWidth <= 0 || state.LineWidth * scale >= ThinLineLimit)
        {
            DrawStroke(shape, matrix, scale, ref state, new(state.StrokeAlpha, ElementBlend(state.BlendMode), false));
            return;
        }

        DrawThinStroke(shape, matrix, scale, ref state);
    }

    /// <summary>Strokes a line that could fall below a pixel at its width and again as a hairline, in a layer when it is translucent or blended.</summary>
    /// <param name="shape">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page.</param>
    /// <param name="scale">The matrix's average scale.</param>
    /// <param name="state">The graphics state.</param>
    private void DrawThinStroke(in StrokeGeometry shape, Matrix3x2 matrix, float scale, ref GraphicsState state)
    {
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
            RestoreCanvas();
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
        using var dash = SkiaDash.Create(state.Dash, state.DashPhase, scale);
        _paint.PathEffect = dash;
        var clipped = ClipShadingBox(state.Stroke);
        using var lease = ApplyColor(state.Stroke, pass.Alpha, matrix);
        SetCanvasMatrix(SkiaConversions.ToSkMatrix(matrix));
        shape.Draw(_canvas, _paint);
        if (pass.Hairline)
        {
            _paint.StrokeWidth = 0;
            shape.Draw(_canvas, _paint);
        }

        if (clipped)
        {
            RestoreCanvas();
        }
    }

    /// <summary>Resets the paint to stroke with the state's width, caps, joins and miter limit.</summary>
    /// <param name="state">The graphics state.</param>
    private void ConfigureStroke(ref GraphicsState state)
    {
        _paint.Reset();
        _paint.IsAntialias = _smoothGraphics;
        _paint.Style = SKPaintStyle.Stroke;
        _paint.StrokeWidth = float.IsNaN(_strokeWidth) ? state.LineWidth : _strokeWidth;

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
        SetCanvasMatrix(SKMatrix.Identity);
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
        _paint.StrokeWidth = Math.Max(state.LineWidth, 1 / Math.Max(scale, SkiaDash.MinScale));
        using var dash = SkiaDash.Create(state.Dash, state.DashPhase, scale);
        _paint.PathEffect = dash;
        _pathBuilder.Reset();
        if (!_paint.GetFillPath(path, _pathBuilder))
        {
            return;
        }

        using var outline = _pathBuilder.Detach();
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
        SetCanvasMatrix(SkiaConversions.ToSkMatrix(matrix));
        _canvas.ClipPath(path, SKClipOperation.Intersect, _smoothGraphics);
        SetCanvasMatrix(SkiaConversions.ToSkMatrix(pattern.Matrix));
        if (pattern.Shading!.BBox is { } box)
        {
            _canvas.ClipRect(new(box.Left, box.Bottom, box.Right, box.Top), SKClipOperation.Intersect, _smoothGraphics);
        }

        DrawMesh(mesh, alpha, blend, pattern.Shading.AntiAlias);
        RestoreCanvas();
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

    /// <summary>Determines whether a glyph may be drawn from a mask: the recording serves one page matrix, the fill is a plain colour and no soft mask or knockout group shapes it.</summary>
    /// <param name="state">The graphics state.</param>
    /// <param name="masked">Whether a soft mask is in effect for the glyph.</param>
    /// <returns><see langword="true"/> when a mask can draw the glyph.</returns>
    private bool CanDrawMask(ref GraphicsState state, bool masked) =>
        _masks is not null && _hasDevice && _quality.UsesGlyphMasks && !masked && state.Fill.Pattern is null && !(_knockout.Count > 0 && _knockout[^1]);

    /// <summary>Draws a glyph from its mask.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <param name="outline">The glyph's managed outline, converted only when a mask must be made.</param>
    /// <param name="matrix">The matrix from the outline to the recording's space.</param>
    /// <param name="state">The graphics state.</param>
    /// <returns><see langword="true"/> when the glyph was drawn; <see langword="false"/> when it must be filled from its outline.</returns>
    private bool TryDrawMask(in GlyphEvent glyph, PdfPath outline, Matrix3x2 matrix, ref GraphicsState state)
    {
        var plain = _frames.Count == 0 && state.BlendMode == PdfBlendMode.Normal && state.FillAlpha >= 1;
        var style = new GlyphMaskStyle(_quality, SkiaConversions.ToSkColor(state.Fill, state.FillAlpha), ElementBlend(state.BlendMode), plain);
        _maskPainter ??= new(_masks!);
        return _maskPainter.TryDraw(_canvas, _deviceInverse, new GlyphMaskGlyph<OutlineMaskSource>(glyph.Font, glyph.Code, new(this, glyph.Font, glyph.Code, outline), matrix * _device), style);
    }

    /// <summary>Draws the image mask of a Type 3 glyph from a mask made once for the glyph's size and position.</summary>
    /// <param name="native">The image.</param>
    /// <param name="imageToRecording">The matrix from the image's pixels to the recording's space.</param>
    /// <param name="state">The graphics state.</param>
    /// <param name="smooth">Whether the image asks for smoothing.</param>
    /// <returns><see langword="true"/> when the mask was drawn.</returns>
    private bool TryDrawImageMask(SKImage native, Matrix3x2 imageToRecording, ref GraphicsState state, bool smooth)
    {
        // The mask is kept under the glyph and the place of the image in its procedure, not under the image, which an inline image makes anew each time.
        var scope = _type3[^1];
        _type3[^1] = scope with { Images = scope.Images + 1 };
        var plain = _frames.Count == 0 && state.BlendMode == PdfBlendMode.Normal && state.FillAlpha >= 1;
        var style = new GlyphMaskStyle(_quality, SkiaConversions.ToSkColor(state.Fill, state.FillAlpha), ElementBlend(state.BlendMode), plain);
        var toDevice = imageToRecording * _device;
        var sampling = SkiaImageSampling.Choose(native.Width, toDevice, smooth, SmoothImages);
        var key = (scope.Code * MaxType3Images) + Math.Min(scope.Images, MaxType3Images - 1);
        _maskPainter ??= new(_masks!);
        return _maskPainter.TryDraw(_canvas, _deviceInverse, new GlyphMaskGlyph<ImageMaskSource>(scope.Font, key, new(native, sampling), toDevice), style);
    }

    /// <summary>Fills glyph outlines with the text edge setting rather than the graphics one.</summary>
    /// <param name="path">The outlines.</param>
    /// <param name="matrix">The matrix from the outlines' space to the page.</param>
    /// <param name="color">The colour.</param>
    /// <param name="alpha">The alpha.</param>
    /// <param name="blend">The blend mode.</param>
    private void FillTextShape(SKPath path, Matrix3x2 matrix, in ColorState color, float alpha, PdfBlendMode blend)
    {
        var smooth = _smoothGraphics;
        _smoothGraphics = _quality.TextEdges != PdfTextEdges.Sharp;
        FillShape(path, matrix, color, alpha, blend);
        _smoothGraphics = smooth;
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

    /// <summary>What a stroke draws: a native path, or, with no path, one straight segment or a rectangle.</summary>
    /// <param name="Path">The native path, or <see langword="null"/> for a segment or rectangle.</param>
    /// <param name="From">The start of the segment, or one corner of the rectangle.</param>
    /// <param name="To">The end of the segment, or the opposite corner of the rectangle.</param>
    /// <param name="IsRectangle">Whether the points are the corners of a rectangle rather than the ends of a segment.</param>
    private readonly record struct StrokeGeometry(SKPath? Path, SKPoint From, SKPoint To, bool IsRectangle)
    {
        /// <summary>Initializes a new instance of the <see cref="StrokeGeometry"/> struct for a path.</summary>
        /// <param name="path">The native path.</param>
        internal StrokeGeometry(SKPath path)
            : this(path, default, default, false)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="StrokeGeometry"/> struct for a straight segment.</summary>
        /// <param name="from">The start of the segment.</param>
        /// <param name="to">The end of the segment.</param>
        internal StrokeGeometry(SKPoint from, SKPoint to)
            : this(null, from, to, false)
        {
        }

        /// <summary>Gets the area the shape's points cover.</summary>
        internal SKRect Bounds => Path is { } path
            ? path.Bounds
            : new(MathF.Min(From.X, To.X), MathF.Min(From.Y, To.Y), MathF.Max(From.X, To.X), MathF.Max(From.Y, To.Y));

        /// <summary>Gets the geometry of a rectangle.</summary>
        /// <param name="rectangle">The rectangle.</param>
        /// <returns>The geometry.</returns>
        internal static StrokeGeometry Rectangle(SKRect rectangle) => new(null, new(rectangle.Left, rectangle.Top), new(rectangle.Right, rectangle.Bottom), true);

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

            if (IsRectangle)
            {
                canvas.DrawRect(Bounds, paint);
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
