// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that every kind of annotation the viewer can add can be moved, recoloured and removed, and that each change survives saving and opening the file again.</summary>
public sealed class AnnotationKindEditTests
{
    /// <summary>The first colour an annotation gets.</summary>
    private const uint FirstColor = 0xFF2020;

    /// <summary>The colour an annotation is changed to.</summary>
    private const uint SecondColor = 0x2060FF;

    /// <summary>The line width of drawn annotations.</summary>
    private const float Width = 2;

    /// <summary>The font size of text annotations.</summary>
    private const float FontSize = 12;

    /// <summary>The left of the place an annotation is moved to.</summary>
    private const float TargetLeft = 120;

    /// <summary>The top of the place an annotation is moved to.</summary>
    private const float TargetTop = 140;

    /// <summary>The width of the place an annotation is moved to.</summary>
    private const float TargetWidth = 90;

    /// <summary>The height of the place an annotation is moved to.</summary>
    private const float TargetHeight = 50;

    /// <summary>How far a saved annotation's edges may be from where it was put, in points; shapes with a line width are a little larger than the box they were given.</summary>
    private const float EdgeTolerance = 6;

    /// <summary>The start of a drawn annotation.</summary>
    private const float Start = 30;

    /// <summary>The end of a drawn annotation.</summary>
    private const float End = 90;

    /// <summary>The middle of the page's top left, for annotations made from one point.</summary>
    private const float Point = 40;

    /// <summary>The text of the annotations that hold text.</summary>
    private const string Words = "Some words";

    /// <summary>The mask of the red, green and blue bits of a colour, without the alpha.</summary>
    private const uint ColorMask = 0x00FFFFFF;

    /// <summary>Every kind that can be moved can be given a new place, a new colour and be removed, and the saved file says so.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(AnnotationKind.Ink)]
    [Arguments(AnnotationKind.FreehandHighlight)]
    [Arguments(AnnotationKind.Note)]
    [Arguments(AnnotationKind.TextBox)]
    [Arguments(AnnotationKind.Rectangle)]
    [Arguments(AnnotationKind.Ellipse)]
    [Arguments(AnnotationKind.Arrow)]
    [Arguments(AnnotationKind.Line)]
    [Arguments(AnnotationKind.Stamp)]
    [Arguments(AnnotationKind.Callout)]
    [Arguments(AnnotationKind.Polygon)]
    [Arguments(AnnotationKind.Cloud)]
    [Arguments(AnnotationKind.PolyLine)]
    [Arguments(AnnotationKind.Caret)]
    public async Task EveryMovableKindCanBeMovedRecolouredAndRemoved(AnnotationKind kind)
    {
        using var test = new HyperPdfTestDocument(TestPdf.Create(1));
        var index = Add(test, kind);
        var added = HyperPdfTestDocument.Read(test.Editor, 0);

        var moved = HyperPdfDocumentAnnotationEditing.SetBounds(test.Document, 0, index, new(TargetLeft, TargetTop, TargetWidth, TargetHeight));
        var afterMove = HyperPdfTestDocument.ReadSaved(HyperPdfTestDocument.Save(test.Editor), 0);

        var recoloured = HyperPdfDocumentAnnotationEditing.SetColor(test.Document, 0, index, SecondColor);
        var afterColour = HyperPdfTestDocument.ReadSaved(HyperPdfTestDocument.Save(test.Editor), 0);

        var removed = HyperPdfDocumentAnnotationEditing.Remove(test.Document, 0, index);
        var afterRemove = HyperPdfTestDocument.ReadSaved(HyperPdfTestDocument.Save(test.Editor), 0);

        await Assert.That(added.Count).IsEqualTo(1);
        await Assert.That(added[0].Kind).IsEqualTo(kind);
        await Assert.That(added[0].IsMovable).IsTrue();
        await Assert.That(moved).IsTrue();
        await Assert.That(afterMove.Count).IsEqualTo(1);
        await Assert.That(afterMove[0].Bounds.Left).IsEqualTo(TargetLeft).Within(EdgeTolerance);
        await Assert.That(afterMove[0].Bounds.Top).IsEqualTo(TargetTop).Within(EdgeTolerance);
        await Assert.That(afterMove[0].Bounds.Right).IsEqualTo(TargetLeft + TargetWidth).Within(EdgeTolerance);
        await Assert.That(afterMove[0].Bounds.Bottom).IsEqualTo(TargetTop + TargetHeight).Within(EdgeTolerance);
        await Assert.That(recoloured).IsTrue();
        await Assert.That(afterColour[0].Color & ColorMask).IsEqualTo(SecondColor);
        await Assert.That(removed).IsTrue();
        await Assert.That(afterRemove.Count).IsEqualTo(0);
    }

    /// <summary>Adds an annotation of a kind with the call that makes that kind.</summary>
    /// <param name="test">The document.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The annotation's index.</returns>
    private static int Add(HyperPdfTestDocument test, AnnotationKind kind)
    {
        var document = test.Document;
        PagePoint[] triangle = [new(Start, Start), new(End, Start), new(Start, End)];
        return kind switch
        {
            AnnotationKind.Ink or AnnotationKind.FreehandHighlight => HyperPdfDocumentAnnotationEditing.AddInk(document, 0, triangle, [triangle.Length], FirstColor, Width, kind),
            AnnotationKind.Note => HyperPdfDocumentAnnotationEditing.AddNote(document, 0, new(Point, Point), Words, FirstColor),
            AnnotationKind.TextBox => HyperPdfDocumentAnnotationEditing.AddText(document, 0, new(Point, Point), Words, FontSize, FirstColor, kind),
            AnnotationKind.Stamp => HyperPdfDocumentAnnotationEditing.AddStamp(document, 0, new(Point, Point), Words, FirstColor),
            AnnotationKind.Callout => HyperPdfDocumentAnnotationEditing.AddCallout(document, 0, new(Start, Start), new(End, End), Words, FontSize, FirstColor),
            AnnotationKind.Caret => HyperPdfDocumentAnnotationEditing.AddCaret(document, 0, new(Point, Point), FirstColor),
            AnnotationKind.Polygon or AnnotationKind.Cloud or AnnotationKind.PolyLine => HyperPdfDocumentAnnotationEditing.AddPolygon(document, 0, kind, triangle, FirstColor, Width),
            _ => HyperPdfDocumentAnnotationEditing.AddShape(document, 0, kind, new(Start, Start), new(End, End), FirstColor, Width),
        };
    }
}
