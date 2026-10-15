// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Layers;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>Records a page's content and its annotation appearances to pictures in viewer space.</summary>
internal static class PageRecorder
{
    /// <summary>The degrees in a half turn, to turn page rotation into radians.</summary>
    private const float HalfTurnDegrees = 180;

    /// <summary>Records the page content.</summary>
    /// <param name = "cache">The document's caches.</param>
    /// <param name = "page">The page.</param>
    /// <param name = "printing">Whether optional content follows print usage.</param>
    /// <param name = "bytes">Receives the memory the picture holds for itself: its operations, without the images it drew.</param>
    /// <param name = "images">Receives the distinct images the picture drew.</param>
    /// <returns>The picture, which the caller owns.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IPdfRenderPicture RecordContent(PdfRenderCache cache, PdfPage page, bool printing, out long bytes, out ImageWeight[] images) =>
        RecordContent(cache, page, printing, new(float.PositiveInfinity, OptionalContentZoom.NoZoom), out bytes, out images);

    /// <summary>Records page content for the upper scale of a display zoom band.</summary>
    /// <param name="cache">The document caches.</param>
    /// <param name="page">The page.</param>
    /// <param name="printing">Whether print layers are used.</param>
    /// <param name="scale">The zoom band's upper scale and the layer zoom class.</param>
    /// <param name="bytes">Receives picture bytes.</param>
    /// <param name="images">Receives image weights.</param>
    /// <returns>The recorded picture.</returns>
    internal static IPdfRenderPicture RecordContent(PdfRenderCache cache, PdfPage page, bool printing, in PictureScale scale, out long bytes, out ImageWeight[] images)
    {
        using var device = Begin(cache, page, scale.Device);
        using var interpreter = ContentInterpreter.Rent(cache, device, 0, scale.UpperScale, CancellationToken.None);
        interpreter.Printing = printing;
        interpreter.ZoomClass = scale.ZoomClass;
        ContentExecution.RunPage(interpreter, page);
        return Finish(device, out bytes, out images);
    }

    /// <summary>Ends a recording and measures what the picture keeps alive.</summary>
    /// <param name = "device">The recording device.</param>
    /// <param name = "bytes">Receives the picture's own bytes, without the images it drew.</param>
    /// <param name = "images">Receives the distinct images the picture drew, which the picture keeps alive.</param>
    /// <returns>The picture, which the caller owns.</returns>
    internal static IPdfRenderPicture Finish(IPictureDevice device, out long bytes, out ImageWeight[] images)
    {
        var picture = device.Finish();
        bytes = picture.ApproximateBytesUsed;
        images = device.Weight.ToArray();
        return picture;
    }

    /// <summary>
    /// Records the annotation appearances (ISO 32000-2 12.5) in /Annots order; annotations
    /// without a normal appearance get one generated; Hidden annotations, and NoView ones on screen or ones without
    /// the Print flag when printing, are left out, as are annotations in hidden optional content and popups.
    /// </summary>
    /// <param name = "cache">The document's caches.</param>
    /// <param name = "page">The page.</param>
    /// <param name = "printing">Whether the page is rendered for printing.</param>
    /// <param name = "bytes">Receives the memory the picture holds for itself: its operations, without the images it drew.</param>
    /// <param name = "images">Receives the distinct images the picture drew.</param>
    /// <returns>The picture, which the caller owns.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IPdfRenderPicture RecordAnnotations(PdfRenderCache cache, PdfPage page, bool printing, out long bytes, out ImageWeight[] images) =>
        RecordAnnotations(cache, page, printing, new(float.PositiveInfinity, OptionalContentZoom.NoZoom), out bytes, out images);

    /// <summary>Records annotation appearances for the upper scale of a display zoom band.</summary>
    /// <param name="cache">The document caches.</param>
    /// <param name="page">The page.</param>
    /// <param name="printing">Whether print layers are used.</param>
    /// <param name="scale">The zoom band's upper scale and the layer zoom class.</param>
    /// <param name="bytes">Receives picture bytes.</param>
    /// <param name="images">Receives image weights.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The recorded picture.</returns>
    internal static IPdfRenderPicture RecordAnnotations(
        PdfRenderCache cache,
        PdfPage page,
        bool printing,
        in PictureScale scale,
        out long bytes,
        out ImageWeight[] images,
        CancellationToken cancellationToken = default)
    {
        using var device = Begin(cache, page, scale.Device);
        using var interpreter = ContentInterpreter.Rent(cache, device, 0, scale.UpperScale, cancellationToken);
        interpreter.Printing = printing;
        interpreter.ZoomClass = scale.ZoomClass;
        var annotations = page.Dictionary.GetArray(KnownName.Annots);
        DrawPass(new(cache, interpreter, page, printing), annotations);
        return Finish(device, out bytes, out images);
    }

    /// <summary>
    /// Gets an annotation's normal appearance stream. With several states it follows /AS; without /AS it uses the state
    /// named by the field value (/V, or the parent's /V) when there is one, else /Off (ISO 32000-2 12.5.5).
    /// </summary>
    /// <param name = "annotation">The annotation dictionary.</param>
    /// <param name = "names">The document's name table.</param>
    /// <returns>The stream, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfStream? GetAppearance(PdfDictionary annotation, PdfNameTable names) => GetAppearance(annotation, names, out _);

    /// <summary>Gets an annotation's normal appearance stream, and the value that refers to it as stored.</summary>
    /// <param name = "annotation">The annotation dictionary.</param>
    /// <param name = "names">The document's name table.</param>
    /// <param name = "raw">The appearance as stored: normally a reference, so a shared stream can be used by reference.</param>
    /// <returns>The stream, or null.</returns>
    internal static PdfStream? GetAppearance(PdfDictionary annotation, PdfNameTable names, out PdfValue raw)
    {
        raw = default;
        var normalRaw = annotation.GetDictionary(KnownName.AP)?.GetRaw(KnownName.N) ?? default;
        var normal = annotation.GetDictionary(KnownName.AP)?.Get(KnownName.N) ?? default;
        if (normal.AsStream() is { } stream)
        {
            raw = normalRaw;
            return stream;
        }

        if (normal.AsDictionary() is not { } states)
        {
            return null;
        }

        var selected = annotation.GetName(KnownName.AS);
        if (!selected.IsNone)
        {
            raw = states.GetRaw(selected);
            return states.GetStream(selected);
        }

        var state = FieldState(annotation, names);
        var chosen = !state.IsNone && states.ContainsKey(state) ? state : (PdfName)KnownName.Off;
        raw = states.GetRaw(chosen);
        return states.GetStream(chosen);
    }

    /// <summary>Computes the matrix that fits an appearance's transformed box to the annotation rectangle (PDF 32000 §12.5.5).</summary>
    /// <param name = "appearance">The appearance stream's dictionary.</param>
    /// <param name = "rect">The annotation rectangle.</param>
    /// <param name = "mapping">Receives the matrix from the appearance's user space to page user space.</param>
    /// <returns><see langword="false"/> when the appearance has no usable box.</returns>
    internal static bool TryMapAppearance(PdfDictionary appearance, PdfRectangle rect, out Matrix3x2 mapping)
    {
        mapping = Matrix3x2.Identity;
        if (!appearance.TryGetRectangle(KnownName.BBox, out var box))
        {
            return false;
        }

        var matrix = ReadMatrix(appearance);
        var a = Vector2.Transform(new(box.Left, box.Bottom), matrix);
        var b = Vector2.Transform(new(box.Right, box.Bottom), matrix);
        var c = Vector2.Transform(new(box.Right, box.Top), matrix);
        var d = Vector2.Transform(new(box.Left, box.Top), matrix);
        var left = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
        var right = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
        var bottom = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
        var top = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
        var scaleX = right > left ? rect.Width / (right - left) : 1;
        var scaleY = top > bottom ? rect.Height / (top - bottom) : 1;
        mapping = new(scaleX, 0, 0, scaleY, rect.Left - (left * scaleX), rect.Bottom - (bottom * scaleY));
        return true;
    }

    /// <summary>Starts a device that records the page area and clips to it.</summary>
    /// <param name="cache">The document's caches.</param>
    /// <param name = "page">The page.</param>
    /// <returns>The device.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IPictureDevice Begin(PdfRenderCache cache, PdfPage page) => Begin(cache, page, null);

    /// <summary>Starts a device that records the page area and clips to it, for a picture shown with one page matrix or at any scale.</summary>
    /// <param name="cache">The document's caches.</param>
    /// <param name="page">The page.</param>
    /// <param name="deviceMatrix">The page-to-device matrix without the tile offset, or null when the picture is replayed at other scales.</param>
    /// <returns>The device.</returns>
    internal static IPictureDevice Begin(PdfRenderCache cache, PdfPage page, Matrix3x2? deviceMatrix)
    {
        var bounds = new PdfRect(0, 0, page.Width, page.Height);
        var style = new PdfPictureStyle(cache.Quality, deviceMatrix);
        var device = cache.Document.DrawingServices.Backend.CreatePictureDevice(bounds, style);
        device.Clip(cache.Document.DrawingServices.PageClips.Get(bounds), false, Matrix3x2.Identity);
        return device;
    }

    /// <summary>Draws a page's annotations in /Annots order, which is their stacking order, bottommost first (ISO 32000-2 11.2 and 12.5.2).</summary>
    /// <param name = "pass">The page being drawn.</param>
    /// <param name = "annotations">The /Annots array, or null.</param>
    private static void DrawPass(AnnotationPass pass, PdfArray? annotations)
    {
        for (var i = 0; i < (annotations?.Count ?? 0); i++)
        {
            if (annotations!.GetDictionary(i) is not { } annotation)
            {
                continue;
            }

            DrawAnnotation(pass, annotation);
            DrawTint(pass, annotation, annotation.IsName(KnownName.Subtype, KnownName.Widget));
        }
    }

    /// <summary>Draws the tint over a fillable widget on top of its appearance, so fields stand out.</summary>
    /// <param name = "pass">The page being drawn.</param>
    /// <param name = "annotation">The annotation dictionary.</param>
    /// <param name = "isWidget">Whether the annotation is a widget; other annotations are not tinted.</param>
    private static void DrawTint(AnnotationPass pass, PdfDictionary annotation, bool isWidget)
    {
        var tint = pass.Cache.FormHighlight;
        if (!isWidget
        || !tint.IsVisible
        || !annotation.TryGetRectangle(
            KnownName.Rect,
            out var rect) || !IsShown(
            annotation,
            pass.Printing) || !ContentXObjects.IsVisible(
            pass.Interpreter,
            annotation))
        {
            return;
        }

        var context = new AnnotationContext(pass.Cache, pass.Page, annotation);
        if (!AnnotationTintAppearance.IsTinted(context))
        {
            return;
        }

        var bounds = PdfRectangle.FromCorners(rect.Left, rect.Bottom, rect.Right, rect.Top);
        DrawForm(pass, annotation, AnnotationTintAppearance.CreateTint(context, bounds, tint), bounds);
    }

    /// <summary>Draws one annotation's normal appearance, generating one when it has none.</summary>
    /// <param name = "pass">The page being drawn.</param>
    /// <param name = "annotation">The annotation dictionary.</param>
    private static void DrawAnnotation(AnnotationPass pass, PdfDictionary annotation)
    {
        if (!IsShown(annotation, pass.Printing) || !ContentXObjects.IsVisible(pass.Interpreter, annotation))
        {
            return;
        }

        if (GetAppearance(annotation, pass.Cache.Names) is { } appearance)
        {
            if (annotation.TryGetRectangle(KnownName.Rect, out var rect))
            {
                DrawForm(pass, annotation, appearance, rect);
            }

            return;
        }

        if (HasNormalAppearance(annotation) || AnnotationAppearances.Generate(new(pass.Cache, pass.Page, annotation)) is not { } generated)
        {
            return;
        }

        DrawForm(pass, annotation, generated.Form, generated.Rect);
        if (generated.Overlay is { } overlay)
        {
            DrawForm(pass, annotation, overlay, generated.Rect);
        }
    }

    /// <summary>Draws an appearance fitted to a rectangle, turned back upright for NoRotate annotations on rotated pages.</summary>
    /// <param name = "pass">The page being drawn.</param>
    /// <param name = "annotation">The annotation dictionary.</param>
    /// <param name = "appearance">The appearance stream.</param>
    /// <param name = "rect">The rectangle it is fitted to.</param>
    private static void DrawForm(AnnotationPass pass, PdfDictionary annotation, PdfStream appearance, PdfRectangle rect)
    {
        if (!TryMapAppearance(appearance.Dictionary, rect, out var mapping))
        {
            return;
        }

        var page = pass.Page;
        if ((annotation.GetInt32(KnownName.F) & (int)PdfAnnotationFlags.NoRotate) != 0 && page.Rotation != 0)
        {
            // ISO 32000-2 12.5.3 (NoRotate): the rectangle's upper-left corner stays fixed while the appearance turns back upright.
            var corner = new Vector2(Math.Min(rect.Left, rect.Right), Math.Max(rect.Top, rect.Bottom));
            mapping = mapping * Matrix3x2.CreateTranslation(-corner) * Matrix3x2.CreateRotation(page.Rotation * MathF.PI / HalfTurnDegrees) * Matrix3x2.CreateTranslation(corner);
        }

        ContentExecution.RunForm(pass.Interpreter, appearance, mapping * page.ViewerTransform, null);
    }

    /// <summary>Determines whether an annotation's flags allow it to be drawn (ISO 32000-2 12.5.3 Hidden, NoView and Print).</summary>
    /// <param name = "annotation">The annotation dictionary.</param>
    /// <param name = "printing">Whether the page is rendered for printing.</param>
    /// <returns><see langword="true"/> when it is drawn.</returns>
    private static bool IsShown(PdfDictionary annotation, bool printing)
    {
        if (annotation.IsName(KnownName.Subtype, KnownName.Popup))
        {
            // A popup is a window shown only while open (ISO 32000-2 12.5.6.14); the viewer draws its own, so none are recorded.
            return false;
        }

        var flags = (PdfAnnotationFlags)annotation.GetInt32(KnownName.F);
        if ((flags & PdfAnnotationFlags.Hidden) != 0)
        {
            return false;
        }

        return printing ? (flags & PdfAnnotationFlags.Print) != 0 : (flags & PdfAnnotationFlags.NoView) == 0;
    }

    /// <summary>Determines whether an annotation has a normal appearance entry, even one that does not resolve to a stream.</summary>
    /// <param name = "annotation">The annotation dictionary.</param>
    /// <returns><see langword="true"/> when /AP /N is a dictionary of states, which is never regenerated.</returns>
    private static bool HasNormalAppearance(PdfDictionary annotation) => annotation.GetDictionary(KnownName.AP)?.Get(KnownName.N).AsDictionary() is not null;

    /// <summary>Reads the field value used when a widget has no /AS: its own /V, else its parent's.</summary>
    /// <param name = "annotation">The annotation dictionary.</param>
    /// <param name = "names">The document's name table.</param>
    /// <returns>The value as a name, or none.</returns>
    private static PdfName FieldState(PdfDictionary annotation, PdfNameTable names)
    {
        var value = annotation.Get(KnownName.V);
        if (value.IsNull)
        {
            value = annotation.GetDictionary(KnownName.Parent)?.Get(KnownName.V) ?? default;
        }

        var state = value.AsName();
        return state.IsNone && value.AsStringBytes() is { Length: > 0 } text ? names.Intern(text) : state;
    }

    /// <summary>Reads an appearance's /Matrix.</summary>
    /// <param name = "appearance">The appearance stream's dictionary.</param>
    /// <returns>The matrix; identity when missing.</returns>
    private static Matrix3x2 ReadMatrix(PdfDictionary appearance)
    {
        const int numbers = 6;
        Span<float> values = stackalloc float[numbers];
        return appearance.GetArray(KnownName.Matrix) is { Count: >= numbers } array
        && array.ReadNumbers(values) >= numbers ? new(
            values[0],
            values[1],
            values[2],
            values[3],
            values[4],
            values[5]) : Matrix3x2.Identity;
    }
}
