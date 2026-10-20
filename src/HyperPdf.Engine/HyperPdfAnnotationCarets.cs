// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Engine;

/// <summary>Adds caret annotations, which mark where text should be inserted (ISO 32000-2 §12.5.6.11).</summary>
internal static class HyperPdfAnnotationCarets
{
    /// <summary>How far in from the caret's sides its curves swing at the base, as a share of its width.</summary>
    private const float ShoulderWidth = 0.15F;

    /// <summary>How far above the base those curves start, as a share of its height.</summary>
    private const float ShoulderHeight = 0.05F;

    /// <summary>How far in from the tip its curves meet it, as a share of the caret's width.</summary>
    private const float TipWidth = 0.05F;

    /// <summary>How far below the tip its curves meet it, as a share of the caret's height.</summary>
    private const float TipHeight = 0.35F;

    /// <summary>Adds a caret whose tip points at a place on the page.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="location">The place the text goes: the caret hangs below this point with its tip on it.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddCaret(HyperPdfAnnotations annotationState, int pageIndex, PagePoint location, uint color)
    {
        lock (annotationState.Gate)
        {
            if (HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is not { } page)
            {
                return -1;
            }

            const float halfWidth = HyperPdfAnnotationShapes.CaretWidth * HyperPdfAnnotationShapes.Half;
            var bounds = default(UserBounds);
            bounds.Add(HyperPdfAnnotationReading.ToUser(page, new(location.X - halfWidth, location.Y)));
            bounds.Add(HyperPdfAnnotationReading.ToUser(page, new(location.X + halfWidth, location.Y + HyperPdfAnnotationShapes.CaretHeight)));
            var rectangle = bounds.ToRectangle(0);
            var caret = PdfAnnotations.Create(annotationState.Store, KnownName.Caret, rectangle);
            Draw(annotationState, caret, rectangle, color);
            return HyperPdfAnnotationReading.Add(annotationState, pageIndex, page, caret, color, string.Empty, []);
        }
    }

    /// <summary>Changes a caret's colour, drawing its appearance again so it still prints.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="caret">The annotation.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    internal static bool Recolor(HyperPdfAnnotations annotationState, PdfDictionary caret, uint color)
    {
        PdfAnnotations.SetColor(caret, KnownName.C, color);
        Draw(annotationState, caret, PdfAnnotations.GetRectangle(caret), color);
        HyperPdfAnnotationReading.SetModified(annotationState, caret);
        return true;
    }

    /// <summary>Gives a caret an appearance: a filled arch with its tip at the top.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="caret">The annotation.</param>
    /// <param name="rectangle">Its rectangle, which the appearance fills.</param>
    /// <param name="color">The colour.</param>
    private static void Draw(HyperPdfAnnotations annotationState, PdfDictionary caret, PdfRectangle rectangle, uint color)
    {
        var width = rectangle.Right - rectangle.Left;
        var height = rectangle.Top - rectangle.Bottom;
        var middle = (rectangle.Left + rectangle.Right) * HyperPdfAnnotationShapes.Half;
        var shoulderY = rectangle.Bottom + (height * ShoulderHeight);
        var tipY = rectangle.Top - (height * TipHeight);
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            PdfAppearances.SetColors(ref builder, color);
            builder.MoveTo(rectangle.Left, rectangle.Bottom);
            builder.CurveTo(middle - (width * ShoulderWidth), shoulderY, middle - (width * TipWidth), tipY, middle, rectangle.Top);
            builder.CurveTo(middle + (width * TipWidth), tipY, middle + (width * ShoulderWidth), shoulderY, rectangle.Right, rectangle.Bottom);
            builder.ClosePath();
            builder.Fill();
            builder.RestoreState();
            _ = PdfAnnotations.SetNormalAppearance(annotationState.Store, caret, builder.ToFormXObject(annotationState.Store, rectangle, null));
        }
        finally
        {
            builder.Dispose();
        }
    }
}
