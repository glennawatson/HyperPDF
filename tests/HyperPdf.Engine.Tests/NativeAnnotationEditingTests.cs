// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// The PDFium engine's editing tests run against the native editor: moving, resizing, restyling and removing
/// annotations, polygons, clouds, callouts, picture stamps and the comment author, checked again after saving.
/// </summary>
public sealed class NativeAnnotationEditingTests
{
    /// <summary>The number of pages in the generated document.</summary>
    private const int PageCount = 2;

    /// <summary>The page edited.</summary>
    private const int Page = 1;

    /// <summary>The line width in points.</summary>
    private const float Width = 2;

    /// <summary>The thick line width in points.</summary>
    private const float ThickWidth = 6;

    /// <summary>How far annotations are moved, in points.</summary>
    private const float Shift = 150;

    /// <summary>How close read-back positions must be, in points.</summary>
    private const float Tolerance = 3;

    /// <summary>The text size in points.</summary>
    private const float FontSize = 12;

    /// <summary>The larger text size in points.</summary>
    private const float LargeFontSize = 24;

    /// <summary>How much larger text at the larger size must be, at least.</summary>
    private const float LargerText = 1.5F;

    /// <summary>Doubles a width.</summary>
    private const float Double = 2;

    /// <summary>Two annotations.</summary>
    private const int Two = 2;

    /// <summary>The picture stamp's side in pixels.</summary>
    private const int PictureSide = 8;

    /// <summary>Bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>A grey pixel channel.</summary>
    private const byte Grey = 0x40;

    /// <summary>The rectangle's top-left corner.</summary>
    private static readonly PagePoint BoxStart = new(72, 200);

    /// <summary>The rectangle's bottom-right corner.</summary>
    private static readonly PagePoint BoxEnd = new(172, 260);

    /// <summary>A drawing.</summary>
    private static readonly PagePoint[] Stroke = [new(80, 300), new(120, 280), new(160, 320)];

    /// <summary>The drawing's length.</summary>
    private static readonly int[] Strokes = [Stroke.Length];

    /// <summary>A triangle.</summary>
    private static readonly PagePoint[] Triangle = [new(300, 100), new(400, 100), new(350, 180)];

    /// <summary>A square.</summary>
    private static readonly PagePoint[] Square = [new(100, 400), new(200, 400), new(200, 480), new(100, 480)];

    /// <summary>The arrow's tail.</summary>
    private static readonly PagePoint ArrowTail = new(72, 400);

    /// <summary>The arrow's point.</summary>
    private static readonly PagePoint ArrowTip = new(200, 400);

    /// <summary>Where the stamp goes.</summary>
    private static readonly PagePoint StampAt = new(300, 300);

    /// <summary>Where the text box goes.</summary>
    private static readonly PagePoint TextAt = new(300, 400);

    /// <summary>Where the note goes.</summary>
    private static readonly PagePoint NoteAt = new(450, 100);

    /// <summary>What the callout points at.</summary>
    private static readonly PagePoint CalloutTarget = new(72, 500);

    /// <summary>Where the callout's text goes.</summary>
    private static readonly PagePoint CalloutText = new(150, 520);

    /// <summary>A highlighted line.</summary>
    private static readonly PageRect MarkedLine = new(72, 100, 200, 14);

    /// <summary>Bounds somewhere else on the page.</summary>
    private static readonly PageRect Elsewhere = new(300, 300, 100, 20);

    /// <summary>Where the picture stamp goes.</summary>
    private static readonly PageRect PictureAt = new(100, 100, 80, 80);

    /// <summary>Where the picture stamp is moved to.</summary>
    private static readonly PageRect PictureMoved = new(300, 300, 80, 80);

    /// <summary>The kinds of the standard types test.</summary>
    private static readonly AnnotationKind[] StandardKinds = [AnnotationKind.Polygon, AnnotationKind.Cloud, AnnotationKind.PolyLine, AnnotationKind.Callout];

    /// <summary>Each movable kind moves, a rectangle resizes, and the change is kept when saved.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesAndResizesAnnotations()
    {
        using var test = new HyperPdfTestDocument(PageCount);
        var editor = test.Editor;
        int[] indexes =
        [
            HyperPdfAnnotationShapes.AddShape(editor, Page, AnnotationKind.Rectangle, BoxStart, BoxEnd, AnnotationColors.Clay, Width),
            HyperPdfAnnotationShapes.AddInk(editor, Page, Stroke, Strokes, AnnotationColors.Ink, Width, AnnotationKind.Ink),
            HyperPdfAnnotationShapes.AddShape(editor, Page, AnnotationKind.Arrow, ArrowTail, ArrowTip, AnnotationColors.Ink, Width),
            HyperPdfAnnotationText.AddStamp(editor, Page, StampAt, "DRAFT", AnnotationColors.Clay),
            HyperPdfAnnotationText.AddText(editor, Page, TextAt, "Moved text", FontSize, AnnotationColors.Ink, AnnotationKind.TextBox),
            HyperPdfAnnotationShapes.AddNote(editor, Page, NoteAt, "Note", AnnotationColors.Sand),
            HyperPdfAnnotationShapes.AddPolygon(editor, Page, AnnotationKind.Polygon, Triangle, AnnotationColors.Slate, Width),
            HyperPdfAnnotationCallouts.AddCallout(editor, Page, CalloutTarget, CalloutText, "Look here", FontSize, AnnotationColors.Ink),
        ];
        var before = HyperPdfTestDocument.Read(editor, Page);
        foreach (var annotation in before)
        {
            _ = HyperPdfAnnotationEditing.SetBounds(editor, Page, annotation.Index, annotation.Bounds with { Top = annotation.Bounds.Top + Shift });
        }

        var moved = HyperPdfTestDocument.Read(editor, Page);
        var grown = HyperPdfAnnotationEditing.SetBounds(editor, Page, indexes[0], moved[0].Bounds with { Width = moved[0].Bounds.Width * Double });
        var current = HyperPdfTestDocument.Read(editor, Page);
        var reopened = HyperPdfTestDocument.ReadSaved(HyperPdfTestDocument.Save(editor), Page);

        await Assert.That(Array.TrueForAll(indexes, static i => i >= 0)).IsTrue();
        await Assert.That(moved.Count).IsEqualTo(indexes.Length);
        for (var i = 0; i < before.Count; i++)
        {
            await Assert.That(moved[i].Index).IsEqualTo(before[i].Index);
            await Assert.That(Math.Abs(moved[i].Bounds.Top - (before[i].Bounds.Top + Shift))).IsLessThan(Tolerance);
            await Assert.That(Math.Abs(moved[i].Bounds.Left - before[i].Bounds.Left)).IsLessThan(Tolerance);
            await Assert.That(Math.Abs(reopened[i].Bounds.Top - current[i].Bounds.Top)).IsLessThan(Tolerance);
        }

        await Assert.That(grown).IsTrue();
        await Assert.That(Math.Abs(current[0].Bounds.Width - (moved[0].Bounds.Width * Double))).IsLessThan(Tolerance);
    }

    /// <summary>Text markup follows its text, so it cannot be moved; nor can a removed annotation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesToMoveMarkupAndRemovedAnnotations()
    {
        using var test = new HyperPdfTestDocument(PageCount);
        var editor = test.Editor;
        var highlight = HyperPdfAnnotationShapes.AddMarkup(editor, Page, AnnotationKind.Highlight, [MarkedLine], AnnotationColors.Sand, string.Empty);
        var box = HyperPdfAnnotationShapes.AddShape(editor, Page, AnnotationKind.Rectangle, BoxStart, BoxEnd, AnnotationColors.Clay, Width);
        _ = HyperPdfAnnotationReading.SetRemoved(editor, Page, box, true);

        await Assert.That(HyperPdfAnnotationEditing.SetBounds(editor, Page, highlight, Elsewhere)).IsFalse();
        await Assert.That(HyperPdfAnnotationEditing.SetBounds(editor, Page, box, Elsewhere)).IsFalse();
    }

    /// <summary>A removed annotation keeps its index, is no longer listed, can be brought back, and is left out of the saved file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovesAndRestoresWithoutShiftingIndexes()
    {
        using var test = new HyperPdfTestDocument(PageCount);
        var editor = test.Editor;
        var box = HyperPdfAnnotationShapes.AddShape(editor, Page, AnnotationKind.Rectangle, BoxStart, BoxEnd, AnnotationColors.Clay, Width);
        var note = HyperPdfAnnotationShapes.AddNote(editor, Page, NoteAt, "Keep me", AnnotationColors.Sand);

        var removed = HyperPdfAnnotationReading.SetRemoved(editor, Page, box, true);
        var removedTwice = HyperPdfAnnotationReading.SetRemoved(editor, Page, box, true);
        var afterRemove = HyperPdfTestDocument.Read(editor, Page);
        var restored = HyperPdfAnnotationReading.SetRemoved(editor, Page, box, false);
        var afterRestore = HyperPdfTestDocument.Read(editor, Page);
        _ = HyperPdfAnnotationReading.SetRemoved(editor, Page, box, true);
        var reopened = HyperPdfTestDocument.ReadSaved(HyperPdfTestDocument.Save(editor), Page);
        var stillRemovable = HyperPdfAnnotationReading.SetRemoved(editor, Page, box, false);

        await Assert.That(removed && !removedTwice && restored).IsTrue();
        await Assert.That(afterRemove.Select(static a => a.Index).ToArray()).IsEquivalentTo([note]);
        await Assert.That(afterRestore.Count).IsEqualTo(Two);
        await Assert.That(afterRestore[0].Color).IsEqualTo(AnnotationColors.Clay);
        await Assert.That(reopened.Single().Contents).IsEqualTo("Keep me");
        await Assert.That(stillRemovable).IsTrue();
        await Assert.That(HyperPdfTestDocument.Read(editor, Page).Count).IsEqualTo(Two);
    }

    /// <summary>Line widths and text sizes change and are read back; recolouring a drawing keeps it drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RestylesAnnotations()
    {
        using var test = new HyperPdfTestDocument(PageCount);
        var editor = test.Editor;
        var ink = HyperPdfAnnotationShapes.AddInk(editor, Page, Stroke, Strokes, AnnotationColors.Ink, Width, AnnotationKind.Ink);
        var box = HyperPdfAnnotationShapes.AddShape(editor, Page, AnnotationKind.Rectangle, BoxStart, BoxEnd, AnnotationColors.Clay, Width);
        var text = HyperPdfAnnotationText.AddText(editor, Page, TextAt, "Bigger", FontSize, AnnotationColors.Ink, AnnotationKind.TextBox);
        var callout = HyperPdfAnnotationCallouts.AddCallout(editor, Page, CalloutTarget, CalloutText, "Look", FontSize, AnnotationColors.Ink);
        var before = HyperPdfTestDocument.Read(editor, Page);

        var thickInk = HyperPdfAnnotationEditing.SetLineWidth(editor, Page, ink, ThickWidth);
        var thickBox = HyperPdfAnnotationEditing.SetLineWidth(editor, Page, box, ThickWidth);
        var largeText = HyperPdfAnnotationText.SetFontSize(editor, Page, text, LargeFontSize);
        var largeCallout = HyperPdfAnnotationText.SetFontSize(editor, Page, callout, LargeFontSize);
        var recoloured = HyperPdfAnnotationEditing.SetColor(editor, Page, ink, AnnotationColors.Deep(AnnotationColors.Sage));
        var noWidthForText = HyperPdfAnnotationEditing.SetLineWidth(editor, Page, text, ThickWidth);
        var noSizeForBox = HyperPdfAnnotationText.SetFontSize(editor, Page, box, LargeFontSize);
        var after = HyperPdfTestDocument.Read(editor, Page);
        var reopened = HyperPdfTestDocument.ReadSaved(HyperPdfTestDocument.Save(editor), Page);

        await Assert.That(thickInk && thickBox && largeText && largeCallout && recoloured).IsTrue();
        await Assert.That(noWidthForText || noSizeForBox).IsFalse();
        await Assert.That(before[0].LineWidth).IsEqualTo(Width);
        await Assert.That(after[0].LineWidth).IsEqualTo(ThickWidth);
        await Assert.That(after[1].LineWidth).IsEqualTo(ThickWidth);
        await Assert.That(before[2].FontSize).IsEqualTo(FontSize);
        await Assert.That(after[2].FontSize).IsEqualTo(LargeFontSize);
        await Assert.That(after[2].Bounds.Height).IsGreaterThan(before[2].Bounds.Height * LargerText);
        await Assert.That(Math.Abs(after[2].Bounds.Top - before[2].Bounds.Top)).IsLessThan(Tolerance);
        await Assert.That(after[3].FontSize).IsEqualTo(LargeFontSize);
        await Assert.That(after[0].Color).IsEqualTo(AnnotationColors.Deep(AnnotationColors.Sage));
        await Assert.That(reopened[0].LineWidth).IsEqualTo(ThickWidth);
        await Assert.That(reopened[0].Color).IsEqualTo(AnnotationColors.Deep(AnnotationColors.Sage));
        await Assert.That(reopened[2].FontSize).IsEqualTo(LargeFontSize);
    }

    /// <summary>Polygons, clouds, runs of lines and callouts are written as the standard PDF types other readers know.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesPolygonsCloudsAndCalloutsAsStandardTypes()
    {
        using var test = new HyperPdfTestDocument(PageCount);
        var editor = test.Editor;
        int[] indexes =
        [
            HyperPdfAnnotationShapes.AddPolygon(editor, Page, AnnotationKind.Polygon, Triangle, AnnotationColors.Slate, Width),
            HyperPdfAnnotationShapes.AddPolygon(editor, Page, AnnotationKind.Cloud, Square, AnnotationColors.Clay, Width),
            HyperPdfAnnotationShapes.AddPolygon(editor, Page, AnnotationKind.PolyLine, Stroke, AnnotationColors.Ink, Width),
            HyperPdfAnnotationCallouts.AddCallout(editor, Page, CalloutTarget, CalloutText, "Check this", FontSize, AnnotationColors.Ink),
        ];
        var tooFew = HyperPdfAnnotationShapes.AddPolygon(editor, Page, AnnotationKind.Polygon, Stroke.AsSpan(0, Two), AnnotationColors.Ink, Width);
        var kinds = HyperPdfTestDocument.Read(editor, Page).Select(static a => a.Kind).ToArray();
        var saved = HyperPdfTestDocument.Save(editor);
        var dictionaries = HyperPdfTestDocument.Dictionaries(saved, Page);
        var reopened = HyperPdfTestDocument.ReadSaved(saved, Page);

        await Assert.That(Array.TrueForAll(indexes, static i => i >= 0)).IsTrue();
        await Assert.That(tooFew).IsEqualTo(-1);
        await Assert.That(kinds).IsEquivalentTo(StandardKinds);
        await Assert.That(reopened.Select(static a => a.Kind).ToArray()).IsEquivalentTo(StandardKinds);
        await Assert.That(reopened[^1].Contents).IsEqualTo("Check this");
        await Assert.That(HyperPdfTestDocument.Name(dictionaries[0], KnownName.Subtype)).IsEqualTo("Polygon");
        await Assert.That(HyperPdfTestDocument.Name(dictionaries[1], KnownName.IT)).IsEqualTo("PolygonCloud");
        await Assert.That(dictionaries[1].GetDictionary(KnownName.BE)!.IsName(KnownName.S, KnownName.C)).IsTrue();
        await Assert.That(HyperPdfTestDocument.Name(dictionaries[2], KnownName.Subtype)).IsEqualTo("PolyLine");
        await Assert.That(dictionaries[0].GetArray(KnownName.Vertices)!.Count).IsEqualTo(Triangle.Length * Two);
        await Assert.That(HyperPdfTestDocument.Name(dictionaries[3], KnownName.Subtype)).IsEqualTo("FreeText");
        await Assert.That(HyperPdfTestDocument.Name(dictionaries[3], KnownName.IT)).IsEqualTo("FreeTextCallout");
        await Assert.That(dictionaries[3].GetArray(KnownName.CL)!.Count).IsEqualTo(Two * Two);
        await Assert.That(PdfAnnotations.GetNormalAppearance(dictionaries[3])).IsNotNull();
    }

    /// <summary>A moved cloud from a saved file keeps its new vertices when saved again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesSavedPolygons()
    {
        using var first = new HyperPdfTestDocument(PageCount);
        _ = HyperPdfAnnotationShapes.AddPolygon(first.Editor, Page, AnnotationKind.Cloud, Triangle, AnnotationColors.Clay, Width);
        using var test = new HyperPdfTestDocument(HyperPdfTestDocument.Save(first.Editor));
        var editor = test.Editor;
        var before = HyperPdfTestDocument.Read(editor, Page)[0];
        var moved = HyperPdfAnnotationEditing.SetBounds(editor, Page, before.Index, before.Bounds with { Top = before.Bounds.Top + Shift });
        var recoloured = HyperPdfAnnotationEditing.SetColor(editor, Page, before.Index, AnnotationColors.Slate);
        var again = HyperPdfTestDocument.ReadSaved(HyperPdfTestDocument.Save(editor), Page);

        await Assert.That(moved && recoloured).IsTrue();
        await Assert.That(again[0].Kind).IsEqualTo(AnnotationKind.Cloud);
        await Assert.That(Math.Abs(again[0].Bounds.Top - (before.Bounds.Top + Shift))).IsLessThan(Tolerance);
    }

    /// <summary>A picture stamp is placed, listed as a stamp, and moved.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlacesPictureStamps()
    {
        using var test = new HyperPdfTestDocument(PageCount);
        var editor = test.Editor;
        var pixels = new byte[PictureSide * PictureSide * BytesPerPixel];
        Array.Fill(pixels, Grey);
        var stamp = HyperPdfAnnotationImages.AddImageStamp(editor, Page, PictureAt, pixels, PictureSide, PictureSide);
        var tooShort = HyperPdfAnnotationImages.AddImageStamp(editor, Page, PictureAt, pixels.AsSpan(0, BytesPerPixel), PictureSide, PictureSide);
        var moved = HyperPdfAnnotationEditing.SetBounds(editor, Page, stamp, PictureMoved);
        var annotation = HyperPdfTestDocument.Read(editor, Page).Single();
        var reopened = HyperPdfTestDocument.ReadSaved(HyperPdfTestDocument.Save(editor), Page).Single();

        await Assert.That(stamp).IsGreaterThanOrEqualTo(0);
        await Assert.That(tooShort).IsEqualTo(-1);
        await Assert.That(moved).IsTrue();
        await Assert.That(annotation.Kind).IsEqualTo(AnnotationKind.Stamp);
        await Assert.That(annotation.Bounds).IsEqualTo(PictureMoved);
        await Assert.That(reopened.Kind).IsEqualTo(AnnotationKind.Stamp);
        await Assert.That(reopened.Color).IsEqualTo(annotation.Color);
    }

    /// <summary>New annotations and replies record the chosen author; a blank author falls back to the user name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecordsTheChosenAuthor()
    {
        using var test = new HyperPdfTestDocument(PageCount);
        var editor = test.Editor;
        var defaultAuthor = HyperPdfAnnotationReading.GetAuthor(editor);
        HyperPdfAnnotationReading.SetAuthor(editor, "  Sam Reviewer ");
        var note = HyperPdfAnnotationShapes.AddNote(editor, Page, NoteAt, "Hello", AnnotationColors.Sand);
        _ = HyperPdfAnnotationReplies.AddReply(editor, Page, note, "Thanks", ReviewState.None);
        var replies = new List<AnnotationReply>();
        HyperPdfAnnotationReplies.GetReplies(editor, Page, note, replies);
        var listed = HyperPdfTestDocument.Read(editor, Page).Single();
        HyperPdfAnnotationReading.SetAuthor(editor, " ");

        await Assert.That(defaultAuthor).IsEqualTo(Environment.UserName);
        await Assert.That(listed.Author).IsEqualTo("Sam Reviewer");
        await Assert.That(listed.Modified).IsNotNull();
        await Assert.That(replies.Single().Author).IsEqualTo("Sam Reviewer");
        await Assert.That(HyperPdfAnnotationReading.GetAuthor(editor)).IsEqualTo(Environment.UserName);
    }
}
