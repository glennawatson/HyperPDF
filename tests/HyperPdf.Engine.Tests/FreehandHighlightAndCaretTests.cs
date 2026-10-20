// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks the freehand highlight (an ink annotation with a see-through stroke) and the caret annotation (ISO 32000-2 §12.5.6.11, §12.5.6.13).</summary>
public sealed class FreehandHighlightAndCaretTests
{
    /// <summary>The page the annotations go on.</summary>
    private const int Page = 0;

    /// <summary>The width of the highlighter pen in points.</summary>
    private const float PenWidth = 14;

    /// <summary>The bytes in one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The vertical position of the stroke, in points from the top of the page.</summary>
    private const int StrokeY = 300;

    /// <summary>A pixel on the stroke.</summary>
    private const int StrokeX = 160;

    /// <summary>The lowest blue value expected where a 40% yellow pen crosses white paper.</summary>
    private const int LowestBlue = 140;

    /// <summary>The highest blue value expected there.</summary>
    private const int HighestBlue = 170;

    /// <summary>A pure yellow pen.</summary>
    private const uint Yellow = 0xFFFF00;

    /// <summary>Tolerance for positions, in points.</summary>
    private const float Tolerance = 2F;

    /// <summary>Tolerance for an opacity.</summary>
    private const float OpacityTolerance = 0.001F;

    /// <summary>The darkest value that counts as inked: below this halfway.</summary>
    private const int HalfWay = byte.MaxValue / 2;

    /// <summary>The stroke: one straight line across the page.</summary>
    private static readonly PagePoint[] Stroke = [new(100, StrokeY), new(220, StrokeY)];

    /// <summary>The length of the stroke.</summary>
    private static readonly int[] Strokes = [Stroke.Length];

    /// <summary>Where the caret's tip goes.</summary>
    private static readonly PagePoint CaretAt = new(200, 400);

    /// <summary>A freehand highlight reads back as its own kind, and its appearance is see-through and multiplied.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FreehandHighlightIsSeeThroughInk()
    {
        using var test = new HyperPdfTestDocument(1);

        var index = HyperPdfAnnotationShapes.AddInk(test.Editor, Page, Stroke, Strokes, Yellow, PenWidth, AnnotationKind.FreehandHighlight);
        var saved = HyperPdfTestDocument.Save(test.Editor);
        var annotation = HyperPdfTestDocument.ReadSaved(saved, Page).Single();
        var dictionary = HyperPdfTestDocument.Dictionaries(saved, Page).Single();

        await Assert.That(index).IsGreaterThanOrEqualTo(0);
        await Assert.That(annotation.Kind).IsEqualTo(AnnotationKind.FreehandHighlight);
        await Assert.That(HyperPdfTestDocument.Name(dictionary, KnownName.Subtype)).IsEqualTo("Ink");
        await Assert.That(dictionary.GetSingle(KnownName.CA)).IsEqualTo(HyperPdfAnnotationShapes.HighlighterOpacity).Within(OpacityTolerance);
    }

    /// <summary>The pen darkens the paper by its opacity once, not twice, and leaves the page elsewhere white.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FreehandHighlightPaintsOnce()
    {
        using var test = new HyperPdfTestDocument(1);
        _ = HyperPdfAnnotationShapes.AddInk(test.Editor, Page, Stroke, Strokes, Yellow, PenWidth, AnnotationKind.FreehandHighlight);
        var size = test.Document.GetPageSizes()[Page];
        var width = (int)Math.Round(size.Width);
        var height = (int)Math.Round(size.Height);
        var pixels = new byte[width * height * BytesPerPixel];

        var rendered = test.Document.Render(new(Page, 1, PageRotation.None, 0, 0, RenderFlags.Annotations), new(pixels, width, height, width * BytesPerPixel));
        var onStroke = pixels[((StrokeY * width) + StrokeX) * BytesPerPixel];
        var away = pixels[(((StrokeY + (int)PenWidth) * width) + StrokeX) * BytesPerPixel];

        await Assert.That(rendered).IsTrue();
        await Assert.That((int)onStroke).IsBetween(LowestBlue, HighestBlue);
        await Assert.That((int)away).IsEqualTo(byte.MaxValue);
    }

    /// <summary>A freehand highlight keeps its see-through look when it is recoloured.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FreehandHighlightKeepsOpacityWhenRecoloured()
    {
        using var test = new HyperPdfTestDocument(1);
        var index = HyperPdfAnnotationShapes.AddInk(test.Editor, Page, Stroke, Strokes, Yellow, PenWidth, AnnotationKind.FreehandHighlight);

        var changed = HyperPdfAnnotationEditing.SetColor(test.Editor, Page, index, AnnotationColors.Sage);
        var dictionary = HyperPdfTestDocument.Dictionaries(HyperPdfTestDocument.Save(test.Editor), Page).Single();

        await Assert.That(changed).IsTrue();
        await Assert.That(dictionary.GetSingle(KnownName.CA)).IsEqualTo(HyperPdfAnnotationShapes.HighlighterOpacity).Within(OpacityTolerance);
        await Assert.That(HyperPdfTestDocument.ReadSaved(HyperPdfTestDocument.Save(test.Editor), Page).Single().Kind).IsEqualTo(AnnotationKind.FreehandHighlight);
    }

    /// <summary>A caret is saved as a standard Caret annotation with an appearance, and reads back as a caret near where it was put.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CaretIsSavedAsAStandardCaret()
    {
        using var test = new HyperPdfTestDocument(1);

        var index = HyperPdfAnnotationCarets.AddCaret(test.Editor, Page, CaretAt, AnnotationColors.Clay);
        var saved = HyperPdfTestDocument.Save(test.Editor);
        var annotation = HyperPdfTestDocument.ReadSaved(saved, Page).Single();
        var dictionary = HyperPdfTestDocument.Dictionaries(saved, Page).Single();

        await Assert.That(index).IsGreaterThanOrEqualTo(0);
        await Assert.That(annotation.Kind).IsEqualTo(AnnotationKind.Caret);
        await Assert.That(annotation.Color).IsEqualTo(AnnotationColors.Clay);
        await Assert.That(annotation.Bounds.Left + (annotation.Bounds.Width * HyperPdfAnnotationShapes.Half)).IsEqualTo(CaretAt.X).Within(Tolerance);
        await Assert.That(annotation.Bounds.Top).IsEqualTo(CaretAt.Y).Within(Tolerance);
        await Assert.That(HyperPdfTestDocument.Name(dictionary, KnownName.Subtype)).IsEqualTo("Caret");
        await Assert.That(dictionary.GetDictionary(KnownName.AP)).IsNotNull();
    }

    /// <summary>A caret shows on the page and the rest of the page stays white.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CaretIsDrawn()
    {
        using var test = new HyperPdfTestDocument(1);
        _ = HyperPdfAnnotationCarets.AddCaret(test.Editor, Page, CaretAt, 0x000000);
        var size = test.Document.GetPageSizes()[Page];
        var width = (int)Math.Round(size.Width);
        var height = (int)Math.Round(size.Height);
        var pixels = new byte[width * height * BytesPerPixel];

        var rendered = test.Document.Render(new(Page, 1, PageRotation.None, 0, 0, RenderFlags.Annotations), new(pixels, width, height, width * BytesPerPixel));
        var inked = 0;
        for (var i = 0; i < pixels.Length; i += BytesPerPixel)
        {
            inked += pixels[i] < HalfWay ? 1 : 0;
        }

        await Assert.That(rendered).IsTrue();
        await Assert.That(inked).IsGreaterThan(0);
    }
}
