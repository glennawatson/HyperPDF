// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// The PDFium engine's annotation, shape and reply tests, run against the native editor: each kind is added and read
/// back, changed, removed, saved and read again by PDFium.
/// </summary>
public sealed class NativeAnnotationTests
{
    /// <summary>The number of pages in the generated document.</summary>
    private const int PageCount = 2;

    /// <summary>The page shapes go on.</summary>
    private const int ShapePage = 1;

    /// <summary>Tolerance for positions, in points.</summary>
    private const float Tolerance = 2F;

    /// <summary>The text size in points.</summary>
    private const float FontSize = 12;

    /// <summary>The typed signature size in points.</summary>
    private const float SignatureSize = 24;

    /// <summary>The ink line width in points.</summary>
    private const float InkWidth = 2;

    /// <summary>The shape line width in points.</summary>
    private const float ShapeWidth = 3;

    /// <summary>Two comments, or two replies.</summary>
    private const int Two = 2;

    /// <summary>The marked line.</summary>
    private static readonly PageRect Line = new(72, 100, 200, 14);

    /// <summary>A drawn stroke.</summary>
    private static readonly PagePoint[] Stroke = [new(100, 300), new(140, 320), new(180, 300), new(220, 330)];

    /// <summary>The stroke's length.</summary>
    private static readonly int[] Strokes = [Stroke.Length];

    /// <summary>Where the sticky note goes.</summary>
    private static readonly PagePoint NoteAt = new(400, 100);

    /// <summary>Where the unrelated comment goes.</summary>
    private static readonly PagePoint OtherAt = new(300, 300);

    /// <summary>Where the text box goes.</summary>
    private static readonly PagePoint TextAt = new(72, 500);

    /// <summary>Where the typed signature goes.</summary>
    private static readonly PagePoint SignatureAt = new(72, 600);

    /// <summary>The rectangle's top-left corner.</summary>
    private static readonly PagePoint BoxStart = new(72, 100);

    /// <summary>The rectangle's bottom-right corner.</summary>
    private static readonly PagePoint BoxEnd = new(200, 180);

    /// <summary>The ellipse's top-left corner.</summary>
    private static readonly PagePoint OvalStart = new(300, 100);

    /// <summary>The ellipse's bottom-right corner.</summary>
    private static readonly PagePoint OvalEnd = new(420, 180);

    /// <summary>The arrow's tail.</summary>
    private static readonly PagePoint Tail = new(72, 300);

    /// <summary>The arrow's point.</summary>
    private static readonly PagePoint Point = new(272, 300);

    /// <summary>The line's start.</summary>
    private static readonly PagePoint LineStart = new(72, 400);

    /// <summary>The line's end.</summary>
    private static readonly PagePoint LineEnd = new(272, 440);

    /// <summary>Where the stamp goes.</summary>
    private static readonly PagePoint StampAt = new(300, 500);

    /// <summary>The kinds added by <see cref="AddsAndReadsEachKind"/>, in order.</summary>
    private static readonly AnnotationKind[] EachKind =
    [
        AnnotationKind.Highlight, AnnotationKind.Underline, AnnotationKind.StrikeOut, AnnotationKind.Squiggly, AnnotationKind.Ink,
        AnnotationKind.Note, AnnotationKind.TextBox, AnnotationKind.Signature, AnnotationKind.Signature,
    ];

    /// <summary>The shapes added by <see cref="AddAllShapes"/>, in order.</summary>
    private static readonly AnnotationKind[] EachShape = [AnnotationKind.Rectangle, AnnotationKind.Ellipse, AnnotationKind.Arrow, AnnotationKind.Line, AnnotationKind.Stamp];

    /// <summary>Each kind can be added and is read back with its kind, colour, author and note.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddsAndReadsEachKind()
    {
        using var test = new HyperPdfTestDocument(PageCount);
        var editor = test.Editor;
        var highlight = HyperPdfAnnotationShapes.AddMarkup(editor, 0, AnnotationKind.Highlight, [Line], AnnotationColors.Sage, "Check this");
        _ = HyperPdfAnnotationShapes.AddMarkup(editor, 0, AnnotationKind.Underline, [Line], AnnotationColors.Slate, string.Empty);
        _ = HyperPdfAnnotationShapes.AddMarkup(editor, 0, AnnotationKind.StrikeOut, [Line], AnnotationColors.Clay, string.Empty);
        _ = HyperPdfAnnotationShapes.AddMarkup(editor, 0, AnnotationKind.Squiggly, [Line], AnnotationColors.Sand, string.Empty);
        _ = HyperPdfAnnotationShapes.AddInk(editor, 0, Stroke, Strokes, AnnotationColors.Ink, InkWidth, AnnotationKind.Ink);
        _ = HyperPdfAnnotationShapes.AddNote(editor, 0, NoteAt, "A sticky note", AnnotationColors.Sand);
        _ = HyperPdfAnnotationText.AddText(editor, 0, TextAt, "Written on the page\nSecond line", FontSize, AnnotationColors.Ink, AnnotationKind.TextBox);
        _ = HyperPdfAnnotationText.AddText(editor, 0, SignatureAt, "Glenn Watson", SignatureSize, AnnotationColors.Ink, AnnotationKind.Signature);
        _ = HyperPdfAnnotationShapes.AddInk(editor, 0, Stroke, Strokes, AnnotationColors.Ink, InkWidth, AnnotationKind.Signature);

        var annotations = HyperPdfTestDocument.Read(editor, 0);
        var first = annotations.First(a => a.Index == highlight);

        await Assert.That(HyperPdfAnnotationReading.GetHasUnsavedChanges(editor)).IsTrue();
        await Assert.That(highlight).IsGreaterThanOrEqualTo(0);
        await Assert.That(annotations.Select(static a => a.Kind).ToArray()).IsEquivalentTo(EachKind);
        await Assert.That(first.Color).IsEqualTo(AnnotationColors.Sage);
        await Assert.That(first.Contents).IsEqualTo("Check this");
        await Assert.That(first.Author).IsEqualTo(Environment.UserName);
        await Assert.That(first.Modified).IsNotNull();
        await Assert.That(first.Bounds.Left).IsEqualTo(Line.Left).Within(Tolerance);
        await Assert.That(first.Bounds.Top).IsEqualTo(Line.Top).Within(Tolerance);
        await Assert.That(annotations.Single(static a => a.Kind == AnnotationKind.TextBox).Bounds.Width).IsGreaterThan(0);
        await Assert.That(annotations.Single(static a => a.Kind == AnnotationKind.Ink).LineWidth).IsEqualTo(InkWidth);
    }

    /// <summary>An annotation can be recoloured, given a note and removed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangesAndRemoves()
    {
        using var test = new HyperPdfTestDocument(PageCount);
        var editor = test.Editor;
        var index = HyperPdfAnnotationShapes.AddMarkup(editor, 0, AnnotationKind.Highlight, [Line], AnnotationColors.Sand, string.Empty);
        var text = HyperPdfAnnotationText.AddText(editor, 0, TextAt, "Text", FontSize, AnnotationColors.Ink, AnnotationKind.TextBox);

        var recoloured = HyperPdfAnnotationEditing.SetColor(editor, 0, index, AnnotationColors.Clay);
        var recolouredText = HyperPdfAnnotationEditing.SetColor(editor, 0, text, AnnotationColors.Slate);
        var noted = HyperPdfAnnotationReading.SetContents(editor, 0, index, "Changed");
        var changed = HyperPdfTestDocument.Read(editor, 0).Single(a => a.Index == index);
        var textColor = HyperPdfTestDocument.Read(editor, 0).Single(a => a.Index == text).Color;
        var removed = HyperPdfAnnotationReading.Remove(editor, 0, index);
        var after = HyperPdfTestDocument.Read(editor, 0);

        await Assert.That(recoloured && recolouredText && noted && removed).IsTrue();
        await Assert.That(changed.Color).IsEqualTo(AnnotationColors.Clay);
        await Assert.That(textColor).IsEqualTo(AnnotationColors.Slate);
        await Assert.That(changed.Contents).IsEqualTo("Changed");
        await Assert.That(after.Count).IsEqualTo(1);
    }

    /// <summary>Annotations survive saving, and PDFium reads them back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesAndReopens()
    {
        using var test = new HyperPdfTestDocument(PageCount);
        var editor = test.Editor;
        _ = HyperPdfAnnotationShapes.AddMarkup(editor, 1, AnnotationKind.Highlight, [Line], AnnotationColors.Sand, "Saved note");
        var saved = HyperPdfTestDocument.Save(editor);
        var annotations = HyperPdfTestDocument.ReadSaved(saved, 1);

        await Assert.That(HyperPdfAnnotationReading.GetHasUnsavedChanges(editor)).IsFalse();
        await Assert.That(annotations.Count).IsEqualTo(1);
        await Assert.That(annotations[0].Contents).IsEqualTo("Saved note");
        await Assert.That(annotations[0].Color).IsEqualTo(AnnotationColors.Sand);
    }

    /// <summary>Each shape and the stamp is read back as what it is, with its colour, and survives saving.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddsReadsAndSavesEachShape()
    {
        using var test = new HyperPdfTestDocument(PageCount);
        var editor = test.Editor;
        var indexes = AddAllShapes(editor);
        var recoloured = HyperPdfAnnotationEditing.SetColor(editor, ShapePage, indexes[^1], AnnotationColors.Slate);
        var annotations = HyperPdfTestDocument.Read(editor, ShapePage);
        var reopened = HyperPdfTestDocument.ReadSaved(HyperPdfTestDocument.Save(editor), ShapePage);

        await Assert.That(Array.TrueForAll(indexes, static i => i >= 0)).IsTrue();
        await Assert.That(recoloured).IsTrue();
        await Assert.That(annotations.Select(static a => a.Kind).ToArray()).IsEquivalentTo(EachShape);
        await Assert.That(annotations.Single(static a => a.Kind == AnnotationKind.Rectangle).Color).IsEqualTo(AnnotationColors.Clay);
        await Assert.That(annotations.Single(static a => a.Kind == AnnotationKind.Stamp).Contents).IsEqualTo("APPROVED");
        await Assert.That(annotations.Single(static a => a.Kind == AnnotationKind.Stamp).Color).IsEqualTo(AnnotationColors.Slate);
        await Assert.That(annotations.Single(static a => a.Kind == AnnotationKind.Rectangle).Bounds.Left).IsLessThanOrEqualTo(BoxStart.X);
        await Assert.That(reopened.Select(static a => a.Kind).ToArray()).IsEquivalentTo(EachShape);
        await Assert.That(reopened.Single(static a => a.Kind == AnnotationKind.Stamp).Color).IsEqualTo(AnnotationColors.Slate);
    }

    /// <summary>A zero-length arrow, an empty stamp and an unknown shape are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesEmptyShapes()
    {
        using var test = new HyperPdfTestDocument(PageCount);
        var editor = test.Editor;

        await Assert.That(HyperPdfAnnotationShapes.AddShape(editor, ShapePage, AnnotationKind.Arrow, Tail, Tail, AnnotationColors.Ink, ShapeWidth)).IsEqualTo(-1);
        await Assert.That(HyperPdfAnnotationText.AddStamp(editor, ShapePage, StampAt, " ", AnnotationColors.Clay)).IsEqualTo(-1);
        await Assert.That(HyperPdfAnnotationShapes.AddShape(editor, ShapePage, AnnotationKind.Highlight, Tail, Point, AnnotationColors.Ink, ShapeWidth)).IsEqualTo(-1);
        await Assert.That(HyperPdfAnnotationReading.GetHasUnsavedChanges(editor)).IsFalse();
    }

    /// <summary>Replies and a status are read back under their comment, oldest first, and not listed on their own.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThreadsRepliesUnderTheirComment()
    {
        using var test = new HyperPdfTestDocument(1);
        var editor = test.Editor;
        var note = HyperPdfAnnotationShapes.AddNote(editor, 0, NoteAt, "Is this figure right?", AnnotationColors.Sand);
        var other = HyperPdfAnnotationShapes.AddNote(editor, 0, OtherAt, "Unrelated", AnnotationColors.Sage);
        var first = HyperPdfAnnotationReplies.AddReply(editor, 0, note, "Yes, checked against the source.", ReviewState.None);
        var status = HyperPdfAnnotationReplies.AddReply(editor, 0, note, string.Empty, ReviewState.Accepted);
        var empty = HyperPdfAnnotationReplies.AddReply(editor, 0, note, " ", ReviewState.None);
        var annotations = HyperPdfTestDocument.Read(editor, 0);
        var replies = new List<AnnotationReply>();
        HyperPdfAnnotationReplies.GetReplies(editor, 0, note, replies);
        var otherReplies = new List<AnnotationReply>();
        HyperPdfAnnotationReplies.GetReplies(editor, 0, other, otherReplies);

        await Assert.That(first >= 0 && status >= 0).IsTrue();
        await Assert.That(empty).IsEqualTo(-1);
        await Assert.That(annotations.Count).IsEqualTo(Two);
        await Assert.That(replies.Count).IsEqualTo(Two);
        await Assert.That(replies[0].Contents).IsEqualTo("Yes, checked against the source.");
        await Assert.That(replies[0].Author).IsEqualTo(Environment.UserName);
        await Assert.That(replies[1].State).IsEqualTo(ReviewState.Accepted);
        await Assert.That(otherReplies.Count).IsEqualTo(0);
    }

    /// <summary>Saved replies are standard <c>/IRT</c> replies, which PDFium reads back as the same thread.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesStandardReplies()
    {
        using var test = new HyperPdfTestDocument(1);
        var editor = test.Editor;
        var note = HyperPdfAnnotationShapes.AddNote(editor, 0, NoteAt, "Please confirm.", AnnotationColors.Sand);
        _ = HyperPdfAnnotationReplies.AddReply(editor, 0, note, "Confirmed.", ReviewState.None);
        _ = HyperPdfAnnotationReplies.AddReply(editor, 0, note, string.Empty, ReviewState.Completed);
        var saved = HyperPdfTestDocument.Save(editor);
        var dictionaries = HyperPdfTestDocument.Dictionaries(saved, 0);
        using var reopened = new HyperPdfTestDocument(saved);
        var reopenedEditor = Core.Documents.DocumentFeature<IAnnotationEditor>.Cast(reopened.Document)!;
        var annotations = HyperPdfTestDocument.Read(reopenedEditor, 0);
        var replies = new List<AnnotationReply>();
        reopenedEditor.GetReplies(0, annotations[0].Index, replies);

        await Assert.That(dictionaries[1].GetRaw(KnownName.IRT).IsReference).IsTrue();
        await Assert.That(dictionaries[1].IsName(KnownName.RT, KnownName.R)).IsTrue();
        await Assert.That(annotations.Count).IsEqualTo(1);
        await Assert.That(replies.Select(static r => r.Contents).ToArray()).IsEquivalentTo(["Confirmed.", string.Empty]);
        await Assert.That(replies[1].State).IsEqualTo(ReviewState.Completed);
    }

    /// <summary>Adds every shape and a stamp.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The new annotations' indexes.</returns>
    private static int[] AddAllShapes(HyperPdfAnnotations editor) =>
    [
        HyperPdfAnnotationShapes.AddShape(editor, ShapePage, AnnotationKind.Rectangle, BoxStart, BoxEnd, AnnotationColors.Clay, ShapeWidth),
        HyperPdfAnnotationShapes.AddShape(editor, ShapePage, AnnotationKind.Ellipse, OvalStart, OvalEnd, AnnotationColors.Slate, ShapeWidth),
        HyperPdfAnnotationShapes.AddShape(editor, ShapePage, AnnotationKind.Arrow, Tail, Point, AnnotationColors.Ink, ShapeWidth),
        HyperPdfAnnotationShapes.AddShape(editor, ShapePage, AnnotationKind.Line, LineStart, LineEnd, AnnotationColors.Sage, ShapeWidth),
        HyperPdfAnnotationText.AddStamp(editor, ShapePage, StampAt, "APPROVED", AnnotationColors.Clay),
    ];
}
