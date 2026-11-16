// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.PageObjects;

/// <summary>
/// One object a content stream paints, with the graphics state it is painted in. Objects can be deleted, moved and
/// recoloured; the owning <see cref="PdfPageContent"/> writes the changes when it regenerates the content.
/// </summary>
[DebuggerDisplay("PdfPageObject: {Kind} at {Bounds}")]
public abstract class PdfPageObject
{
    /// <summary>The marked-content sequences of an object outside any.</summary>
    private static readonly PdfMark[] NoMarks = [];

    /// <summary>The line style set by <see cref="SetLineStyle"/>, or null while the content's own applies.</summary>
    private PdfLineStyle? _lineStyle;

    /// <summary>The opacity set by <see cref="SetOpacity"/>, or null while the content's own applies.</summary>
    private float? _opacity;

    /// <summary>The gradient set by <see cref="SetGradientFill"/>, or null while a plain colour applies.</summary>
    private PdfGradient? _gradient;

    /// <summary>The blend mode set by <see cref="SetBlendMode"/>, or null while the content's own applies.</summary>
    private PdfBlendMode? _blendMode;

    /// <summary>The crop set by <see cref="SetCrop"/> as the shares of the object's frame cut off, or null while the whole object shows.</summary>
    private CropShares? _crop;

    /// <summary>The paint set by <see cref="SetFillPaint"/>, or null while the content's own fill applies.</summary>
    private PdfPaint? _fill;

    /// <summary>The paint set by <see cref="SetStrokePaint"/>, or null while the content's own stroke applies.</summary>
    private PdfPaint? _stroke;

    /// <summary>The clipping paths, made on first use.</summary>
    private PdfClipPath[]? _clipPaths;

    /// <summary>Initializes a new instance of the <see cref="PdfPageObject"/> class.</summary>
    private protected PdfPageObject()
    {
    }

    /// <summary>Gets the kind of object.</summary>
    public abstract PdfPageObjectKind Kind { get; }

    /// <summary>Gets the object's position among the objects of its content, in painting order.</summary>
    public int Index { get; internal set; }

    /// <summary>Gets the matrix from the object's own coordinates to user space, as the content stream set it.</summary>
    public Matrix3x2 Matrix { get; internal set; } = Matrix3x2.Identity;

    /// <summary>Gets the transformation added by <see cref="Transform"/>, in user space; the identity until then.</summary>
    public Matrix3x2 Transformation { get; private set; } = Matrix3x2.Identity;

    /// <summary>Gets the object's bounds in user space, moved by <see cref="Transformation"/>; strokes include their line width.</summary>
    public PdfRectangle Bounds => IsTransformed && PageGeometry.IsFinite(OriginalBounds) ? TextGeometry.TransformRect(Transformation, OriginalBounds) : OriginalBounds;

    /// <summary>Gets the object's bounds in user space as the content stream paints it.</summary>
    public PdfRectangle OriginalBounds { get; internal set; }

    /// <summary>Gets the intersection of the bounds of the clipping paths in force, or an unbounded rectangle when none is.</summary>
    public PdfRectangle ClipBounds { get; internal set; } = Unbounded;

    /// <summary>Gets a value indicating whether a clipping path was in force when the object was painted.</summary>
    public bool IsClipped => ClipChain is not null;

    /// <summary>Gets the clipping paths in force, outermost first.</summary>
    public IReadOnlyList<PdfClipPath> ClipPaths => _clipPaths ??= ClipNode.ToArray(ClipChain);

    /// <summary>Gets the marked-content sequences the object sits inside, outermost first.</summary>
    public IReadOnlyList<PdfMark> Marks { get; internal set; } = NoMarks;

    /// <summary>Gets the line width in user space units at the time the object was painted.</summary>
    public float LineWidth { get; internal set; }

    /// <summary>Gets the colour used for filling, or for stencil images and text.</summary>
    public PdfPaint FillPaint => _fill ?? OriginalFillPaint;

    /// <summary>Gets the colour used for stroking.</summary>
    public PdfPaint StrokePaint => _stroke ?? OriginalStrokePaint;

    /// <summary>Gets a value indicating whether the object is deleted from the regenerated content.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="Transform"/> moved the object.</summary>
    public bool IsTransformed { get; private set; }

    /// <summary>Gets a value indicating whether the object's fill or stroke colour was changed.</summary>
    public bool IsRecoloured => _fill is not null || _stroke is not null || _gradient is not null;

    /// <summary>Gets the gradient the object is filled with, or null when it is filled with a plain colour.</summary>
    public PdfGradient? FillGradient => _gradient;

    /// <summary>Gets a value indicating whether the object's line style, opacity or crop was changed.</summary>
    public bool IsRestyled => _lineStyle is not null || _opacity is not null || _crop is not null || _blendMode is not null;

    /// <summary>Gets a value indicating whether the object has any change to write.</summary>
    public virtual bool IsModified => IsDeleted || IsTransformed || IsRecoloured || IsRestyled;

    /// <summary>Gets the unbounded rectangle used for objects with no clip.</summary>
    internal static PdfRectangle Unbounded { get; } = new(float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, float.PositiveInfinity);

    /// <summary>Gets a value indicating whether a clip can be written around the object; text sits inside a text object, which takes none.</summary>
    internal virtual bool CanCrop => true;

    /// <summary>Gets or sets the content that owns the object.</summary>
    internal PdfPageContent? Owner { get; set; }

    /// <summary>Gets or sets the bytes of the content stream the object came from, with the operators that paint it.</summary>
    internal ByteRange Source { get; set; }

    /// <summary>Gets or sets the innermost clip in force.</summary>
    internal ClipNode? ClipChain { get; set; }

    /// <summary>Gets or sets the fill colour the content stream set.</summary>
    internal PdfPaint OriginalFillPaint { get; set; } = PdfPaint.Black;

    /// <summary>Gets or sets the stroke colour the content stream set.</summary>
    internal PdfPaint OriginalStrokePaint { get; set; } = PdfPaint.Black;

    /// <summary>Gets or sets the opacity of filling the content stream set.</summary>
    internal float OriginalFillAlpha { get; set; } = 1F;

    /// <summary>Gets or sets the opacity of stroking the content stream set.</summary>
    internal float OriginalStrokeAlpha { get; set; } = 1F;

    /// <summary>Gets or sets the blend mode name the content stream set; none means normal.</summary>
    internal PdfName OriginalBlendMode { get; set; }

    /// <summary>Gets the blend mode to write, or null to leave the content's own.</summary>
    internal PdfBlendMode? ChangedBlendMode => _blendMode;

    /// <summary>Gets the colour to write for filling, or null to leave the content's own.</summary>
    internal PdfPaint? ChangedFill => _fill;

    /// <summary>Gets the colour to write for stroking, or null to leave the content's own.</summary>
    internal PdfPaint? ChangedStroke => _stroke;

    /// <summary>Gets the line style to write, or null to leave the content's own.</summary>
    internal PdfLineStyle? ChangedLineStyle => _lineStyle;

    /// <summary>Gets the opacity to write, or null to leave the content's own.</summary>
    internal float? ChangedOpacity => _opacity;

    /// <summary>Gets the crop to write, or null when the whole object shows.</summary>
    internal CropShares? ChangedCrop => _crop;

    /// <summary>Deletes the object. The regenerated content leaves it out and keeps everything else in place.</summary>
    public void Delete() => IsDeleted = true;

    /// <summary>Brings back a deleted object.</summary>
    public void Undelete() => IsDeleted = false;

    /// <summary>Moves, scales or rotates the object. The change adds to earlier calls.</summary>
    /// <param name="matrix">The transformation in user space.</param>
    public void Transform(Matrix3x2 matrix)
    {
        Transformation = IsTransformed ? Transformation * matrix : matrix;
        IsTransformed = Transformation != Matrix3x2.Identity;
    }

    /// <summary>Moves the object.</summary>
    /// <param name="dx">The distance along x in user space units.</param>
    /// <param name="dy">The distance along y in user space units.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Translate(float dx, float dy) => Transform(Matrix3x2.CreateTranslation(dx, dy));

    /// <summary>Changes the fill colour.</summary>
    /// <param name="paint">The new colour.</param>
    /// <exception cref="ArgumentNullException"><paramref name="paint"/> is <see langword="null"/>.</exception>
    public void SetFillPaint(PdfPaint paint)
    {
        ArgumentNullException.ThrowIfNull(paint);
        _fill = ReferenceEquals(paint, OriginalFillPaint) ? null : paint;
        _gradient = null;
    }

    /// <summary>Fills the object with a gradient in place of a plain colour. Setting a plain fill colour takes the gradient away.</summary>
    /// <param name="gradient">The gradient, or <see langword="null"/> to fill with a plain colour again.</param>
    /// <exception cref="ArgumentException">A colour of the gradient is not a plain device colour.</exception>
    public void SetGradientFill(PdfGradient? gradient)
    {
        if (gradient is not null && (!gradient.Start.IsDeviceColor || !gradient.End.IsDeviceColor || !gradient.Start.Pattern.IsNone || !gradient.End.Pattern.IsNone))
        {
            throw new ArgumentException("A gradient runs between two device colours.", nameof(gradient));
        }

        _gradient = gradient;
    }

    /// <summary>Changes how the object's lines look: the shape of their ends and corners and their dashes. Text and inline images ignore it.</summary>
    /// <param name="style">The style.</param>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dash length is negative or not a finite number, or every dash length is zero.</exception>
    public void SetLineStyle(PdfLineStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(style.Dash);
        var total = 0F;
        foreach (var length in style.Dash)
        {
            if (!float.IsFinite(length) || length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(style), "A dash length must be a number that is not negative.");
            }

            total += length;
        }

        if (style.Dash.Length > 0 && total <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(style), "A dash pattern needs at least one length above zero.");
        }

        _lineStyle = style;
    }

    /// <summary>
    /// Crops the object to part of its frame, without changing what is stored: the part outside is clipped away when the page is drawn.
    /// Pictures and forms are drawn in a frame of their own; for a path or a shading the frame is the box it covers on the page. Another
    /// crop cuts the part that is left.
    /// </summary>
    /// <param name="left">The share of the width cut off the left, from 0 up to but not including 1.</param>
    /// <param name="top">The share of the height cut off the top.</param>
    /// <param name="right">The share of the width cut off the right.</param>
    /// <param name="bottom">The share of the height cut off the bottom.</param>
    /// <exception cref="ArgumentOutOfRangeException">A share is negative or not a finite number, or the shares leave nothing.</exception>
    /// <exception cref="InvalidOperationException">The object is text, which a clip cannot be written around.</exception>
    public void SetCrop(float left, float top, float right, float bottom)
    {
        if (!CanCrop)
        {
            throw new InvalidOperationException("Text cannot be cropped.");
        }

        var shares = new CropShares(left, top, right, bottom);
        shares.Validate();
        _crop = _crop is { } before ? before.Then(shares) : shares;
    }

    /// <summary>Changes how solid the object is, for its fill and its stroke alike (ISO 32000-2, 11.6.4, the <c>ca</c> and <c>CA</c> entries).</summary>
    /// <param name="opacity">1 for solid down to 0 for invisible.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="opacity"/> is not from 0 to 1.</exception>
    public void SetOpacity(float opacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(opacity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(opacity, 1F);
        _opacity = opacity;
    }

    /// <summary>Changes how the object's colours mix with what is under it (ISO 32000-2, 11.3.5, the <c>BM</c> entry).</summary>
    /// <param name="mode">The blend mode.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined blend mode.</exception>
    public void SetBlendMode(PdfBlendMode mode)
    {
        if ((uint)mode > (uint)PdfBlendMode.Luminosity)
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "That is not a blend mode.");
        }

        _blendMode = mode;
    }

    /// <summary>Changes the stroke colour.</summary>
    /// <param name="paint">The new colour.</param>
    /// <exception cref="ArgumentNullException"><paramref name="paint"/> is <see langword="null"/>.</exception>
    public void SetStrokePaint(PdfPaint paint)
    {
        ArgumentNullException.ThrowIfNull(paint);
        _stroke = ReferenceEquals(paint, OriginalStrokePaint) ? null : paint;
    }
}
