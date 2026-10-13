// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// A page's dictionary and the attributes it inherits from the page tree. Viewer space is the rotated crop box in points
/// with the origin at the top left, matching what viewers lay out. The library works in user space units; a page with a
/// <c>/UserUnit</c> other than 1 is physically that many times larger. Callers that show or measure the page in points
/// of 1/72 inch use the physical members (<see cref="PhysicalWidth"/>, <see cref="PhysicalViewerTransform"/>,
/// <see cref="ToPhysical"/>), which are the user space ones scaled once by <see cref="UserUnit"/>.
/// </summary>
[DebuggerDisplay("PdfPage: {Index} {Width}x{Height} rotate {Rotation}")]
public sealed class PdfPage
{
    /// <summary>A quarter turn in degrees.</summary>
    private const int QuarterTurn = 90;

    /// <summary>A half turn in degrees.</summary>
    private const int HalfTurn = 180;

    /// <summary>Three quarter turns in degrees.</summary>
    private const int ThreeQuarterTurn = 270;

    /// <summary>The quarter turns in a full turn.</summary>
    private const int QuarterTurnsPerCircle = 4;

    /// <summary>
    /// The largest coordinate magnitude and extent a page box may have, in points. The standard sets no limit, so this only
    /// stops values that would overflow later arithmetic.
    /// </summary>
    private const float MaxBoxExtent = 1_000_000F;

    /// <summary>The largest <c>/UserUnit</c> read; a larger value is ignored so sizes cannot overflow later arithmetic.</summary>
    private const float MaxUserUnit = 1_000_000F;

    /// <summary>The width of a US Letter page in points.</summary>
    private const float LetterWidth = 612;

    /// <summary>The height of a US Letter page in points.</summary>
    private const float LetterHeight = 792;

    /// <summary>Initializes a new instance of the <see cref="PdfPage"/> class.</summary>
    /// <param name="index">The zero based page index.</param>
    /// <param name="id">The page object's id, or an invalid id for a direct page dictionary.</param>
    /// <param name="dictionary">The page dictionary.</param>
    /// <param name="inherited">The attributes inherited from the page tree.</param>
    internal PdfPage(int index, PdfObjectId id, PdfDictionary dictionary, in InheritedAttributes inherited)
    {
        Index = index;
        Id = id;
        Dictionary = dictionary;
        Resources = dictionary.GetDictionary(KnownName.Resources) ?? inherited.Resources;
        MediaBox = ReadBox(dictionary, KnownName.MediaBox) ?? inherited.MediaBox ?? DefaultMediaBox;
        var crop = ReadBox(dictionary, KnownName.CropBox) ?? inherited.CropBox;
        CropBox = ClipCropBox(crop, MediaBox);
        var rotate = dictionary.Get(KnownName.Rotate);
        Rotation = NormaliseRotation(rotate.IsNumber ? rotate.AsInt32() : inherited.Rotate);
        var upright = Rotation % HalfTurn == 0;
        Width = upright ? CropBox.Width : CropBox.Height;
        Height = upright ? CropBox.Height : CropBox.Width;
        UserUnit = ReadUserUnit(dictionary);
        ViewerTransform = CreateViewerTransform(CropBox, Rotation);
    }

    /// <summary>Gets the US Letter media box used when a page has none.</summary>
    public static PdfRectangle DefaultMediaBox => new(0, 0, LetterWidth, LetterHeight);

    /// <summary>Gets the zero based page index.</summary>
    public int Index { get; }

    /// <summary>Gets the page object's id.</summary>
    public PdfObjectId Id { get; }

    /// <summary>Gets the page dictionary.</summary>
    public PdfDictionary Dictionary { get; }

    /// <summary>Gets the page's resources, inherited when the page has none of its own.</summary>
    public PdfDictionary? Resources { get; }

    /// <summary>Gets the media box.</summary>
    public PdfRectangle MediaBox { get; }

    /// <summary>Gets the crop box, clipped to the media box.</summary>
    public PdfRectangle CropBox { get; }

    /// <summary>Gets the bleed box (ISO 32000-2 14.11.2.1), clipped to the media box; the crop box when the page has none.</summary>
    public PdfRectangle BleedBox => ClipBox(ReadBox(Dictionary, KnownName.BleedBox), MediaBox, CropBox);

    /// <summary>Gets the trim box (ISO 32000-2 14.11.2.1), clipped to the media box; the crop box when the page has none.</summary>
    public PdfRectangle TrimBox => ClipBox(ReadBox(Dictionary, KnownName.TrimBox), MediaBox, CropBox);

    /// <summary>Gets the art box (ISO 32000-2 14.11.2.1), clipped to the media box; the crop box when the page has none.</summary>
    public PdfRectangle ArtBox => ClipBox(ReadBox(Dictionary, KnownName.ArtBox), MediaBox, CropBox);

    /// <summary>Gets the clockwise display rotation: 0, 90, 180 or 270.</summary>
    public int Rotation { get; }

    /// <summary>Gets the displayed width in points, after rotation.</summary>
    public float Width { get; }

    /// <summary>Gets the displayed height in points, after rotation.</summary>
    public float Height { get; }

    /// <summary>
    /// Gets the size of one user space unit in points (ISO 32000-2 7.7.3.3, <c>/UserUnit</c>): 1 unless the page holds a
    /// positive number. It belongs to the page and is not inherited. <see cref="Width"/>, <see cref="Height"/> and the
    /// transforms stay in user space units; the physical sizes apply the unit.
    /// </summary>
    public float UserUnit { get; }

    /// <summary>Gets the displayed width in points of physical size (1/72 inch): <see cref="Width"/> times <see cref="UserUnit"/>.</summary>
    public float PhysicalWidth => Width * UserUnit;

    /// <summary>Gets the displayed height in points of physical size (1/72 inch): <see cref="Height"/> times <see cref="UserUnit"/>.</summary>
    public float PhysicalHeight => Height * UserUnit;

    /// <summary>Gets the transform from user space to viewer space (top-left origin, rotation applied).</summary>
    public Matrix3x2 ViewerTransform { get; }

    /// <summary>Gets the transform from viewer space back to user space.</summary>
    public Matrix3x2 UserTransform => Invert(ViewerTransform);

    /// <summary>
    /// Gets the transform from user space to physical viewer space: <see cref="ViewerTransform"/> followed by a scale of
    /// <see cref="UserUnit"/>, so a distance of 1 is 1/72 inch on the page as displayed (ISO 32000-2 7.7.3.3).
    /// </summary>
    public Matrix3x2 PhysicalViewerTransform => ViewerTransform * Matrix3x2.CreateScale(UserUnit);

    /// <summary>Gets the transform from physical viewer space back to user space.</summary>
    public Matrix3x2 PhysicalUserTransform => Invert(PhysicalViewerTransform);

    /// <summary>Gets one of the page's five boundaries as readers use it: clipped to the media box, with the defaults of ISO 32000-2 14.11.2.1.</summary>
    /// <param name="box">Which boundary.</param>
    /// <returns>The boundary in user space.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="box"/> is not a defined boundary.</exception>
    public PdfRectangle GetBox(PdfPageBox box) => box switch
    {
        PdfPageBox.Media => MediaBox,
        PdfPageBox.Crop => CropBox,
        PdfPageBox.Bleed => BleedBox,
        PdfPageBox.Trim => TrimBox,
        PdfPageBox.Art => ArtBox,
        _ => throw new ArgumentOutOfRangeException(nameof(box), box, "The page box is not defined."),
    };

    /// <summary>Converts a user space point to viewer space.</summary>
    /// <param name="point">The point in user space.</param>
    /// <returns>The point in viewer space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 ToViewer(Vector2 point) => Vector2.Transform(point, ViewerTransform);

    /// <summary>Converts a viewer space point to user space.</summary>
    /// <param name="point">The point in viewer space.</param>
    /// <returns>The point in user space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 ToUser(Vector2 point) => Vector2.Transform(point, UserTransform);

    /// <summary>Converts a user space rectangle to viewer space.</summary>
    /// <param name="rectangle">The rectangle in user space.</param>
    /// <returns>The bounding rectangle in viewer space, as left, top, right, bottom.</returns>
    public PdfRectangle ToViewerRectangle(PdfRectangle rectangle)
    {
        var a = ToViewer(new(rectangle.Left, rectangle.Bottom));
        var b = ToViewer(new(rectangle.Right, rectangle.Top));
        return PdfRectangle.FromCorners(a.X, a.Y, b.X, b.Y);
    }

    /// <summary>Converts a user space point to physical viewer space (points of 1/72 inch, top-left origin, rotation applied).</summary>
    /// <param name="point">The point in user space.</param>
    /// <returns>The point in physical viewer space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 ToPhysical(Vector2 point) => Vector2.Transform(point, PhysicalViewerTransform);

    /// <summary>Converts a physical viewer space point to user space.</summary>
    /// <param name="point">The point in physical viewer space.</param>
    /// <returns>The point in user space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 FromPhysical(Vector2 point) => Vector2.Transform(point, PhysicalUserTransform);

    /// <summary>Converts a user space rectangle to physical viewer space.</summary>
    /// <param name="rectangle">The rectangle in user space.</param>
    /// <returns>The bounding rectangle in physical viewer space, as left, top, right, bottom.</returns>
    public PdfRectangle ToPhysicalRectangle(PdfRectangle rectangle)
    {
        var a = ToPhysical(new(rectangle.Left, rectangle.Bottom));
        var b = ToPhysical(new(rectangle.Right, rectangle.Top));
        return PdfRectangle.FromCorners(a.X, a.Y, b.X, b.Y);
    }

    /// <summary>
    /// Reads what making the page reads from its own dictionary, so every report about it is made when the page tree is
    /// read, even though the page is made later: a bad box, and any damage in the objects its entries point to.
    /// </summary>
    /// <param name="dictionary">The page dictionary.</param>
    /// <param name="id">The page object's id, or an invalid id for a direct page dictionary.</param>
    internal static void ReportFaults(PdfDictionary dictionary, PdfObjectId id)
    {
        PageBoxes.Report(dictionary, id.Number, dictionary.Owner?.Context);
        _ = dictionary.GetDictionary(KnownName.Resources);
        _ = ReadBox(dictionary, KnownName.MediaBox);
        _ = ReadBox(dictionary, KnownName.CropBox);
        _ = dictionary.Get(KnownName.Rotate);
        _ = ReadUserUnit(dictionary);
    }

    /// <summary>Normalises a rotation to 0, 90, 180 or 270; values that are not quarter turns are rounded down.</summary>
    /// <param name="rotate">The /Rotate value.</param>
    /// <returns>The rotation.</returns>
    internal static int NormaliseRotation(int rotate)
    {
        // /Rotate must be a multiple of 90 (ISO 32000-2 table 31); a damaged value is truncated toward zero: 100 becomes 90 and -100 becomes 270.
        var turns = ((rotate / QuarterTurn % QuarterTurnsPerCircle) + QuarterTurnsPerCircle) % QuarterTurnsPerCircle;
        return turns * QuarterTurn;
    }

    /// <summary>Reads <c>/UserUnit</c>, ignoring a value that is not a finite positive number no larger than <see cref="MaxUserUnit"/>.</summary>
    /// <param name="dictionary">The page dictionary.</param>
    /// <returns>The user unit, or 1.</returns>
    internal static float ReadUserUnit(PdfDictionary dictionary)
    {
        var value = dictionary.Get(KnownName.UserUnit);
        if (!value.IsNumber)
        {
            return 1;
        }

        var unit = (float)value.AsNumber();
        return float.IsFinite(unit) && unit > 0 && unit <= MaxUserUnit ? unit : 1;
    }

    /// <summary>Reads a box, ignoring malformed, empty, non-finite or absurdly large ones.</summary>
    /// <param name="dictionary">The page dictionary.</param>
    /// <param name="key">The box key.</param>
    /// <returns>The box, or <see langword="null"/>.</returns>
    internal static PdfRectangle? ReadBox(PdfDictionary dictionary, KnownName key) =>
        dictionary.TryGetRectangle(key, out var box) && IsUsableBox(box) ? box : null;

    /// <summary>Determines whether a box is finite, has an area and stays within <see cref="MaxBoxExtent"/>.</summary>
    /// <param name="box">The box.</param>
    /// <returns><see langword="true"/> when usable.</returns>
    internal static bool IsUsableBox(in PdfRectangle box) =>
        !box.IsEmpty
        && float.IsFinite(box.Width)
        && float.IsFinite(box.Height)
        && box.Width < MaxBoxExtent
        && box.Height < MaxBoxExtent
        && MathF.Abs(box.Left) < MaxBoxExtent
        && MathF.Abs(box.Bottom) < MaxBoxExtent
        && MathF.Abs(box.Right) < MaxBoxExtent
        && MathF.Abs(box.Top) < MaxBoxExtent;

    /// <summary>Clips the crop box to the media box, as viewers do; a crop box outside the media box is ignored.</summary>
    /// <param name="crop">The crop box, if any.</param>
    /// <param name="media">The media box.</param>
    /// <returns>The effective crop box.</returns>
    private static PdfRectangle ClipCropBox(PdfRectangle? crop, PdfRectangle media)
    {
        if (crop is not { } box)
        {
            return media;
        }

        var clipped = box.Intersect(media);
        return clipped.IsEmpty ? media : clipped;
    }

    /// <summary>Clips a bleed, trim or art box to the media box; a missing box, or one outside the media box, is the crop box.</summary>
    /// <param name="box">The box, if any.</param>
    /// <param name="media">The media box.</param>
    /// <param name="crop">The effective crop box.</param>
    /// <returns>The effective box.</returns>
    private static PdfRectangle ClipBox(PdfRectangle? box, PdfRectangle media, PdfRectangle crop)
    {
        if (box is not { } value)
        {
            return crop;
        }

        var clipped = value.Intersect(media);
        return clipped.IsEmpty ? crop : clipped;
    }

    /// <summary>Inverts a page transform, which is a rotation or reflection scaled by the user unit and so always has an inverse.</summary>
    /// <param name="matrix">The transform.</param>
    /// <returns>The inverse.</returns>
    private static Matrix3x2 Invert(Matrix3x2 matrix)
    {
        _ = Matrix3x2.Invert(matrix, out var inverse);
        return inverse;
    }

    /// <summary>Creates the user-to-viewer transform for a crop box and rotation.</summary>
    /// <param name="crop">The crop box.</param>
    /// <param name="rotation">The rotation.</param>
    /// <returns>The transform.</returns>
    private static Matrix3x2 CreateViewerTransform(PdfRectangle crop, int rotation) => rotation switch
    {
        QuarterTurn => new(0, 1, 1, 0, -crop.Bottom, -crop.Left),
        HalfTurn => new(-1, 0, 0, 1, crop.Right, -crop.Bottom),
        ThreeQuarterTurn => new(0, -1, -1, 0, crop.Top, crop.Right),
        _ => new(1, 0, 0, -1, -crop.Left, crop.Top),
    };
}
