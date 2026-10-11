// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>
/// Tests for moving, resizing, restyling and removing annotations, the polygon, cloud, callout and picture stamp tools,
/// and the comment author, each read back and checked again after saving and reopening.
/// </summary>
public sealed class AnnotationEditingTests
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

    /// <summary>How far inside the rectangle's left edge its side is probed.</summary>
    private const float SideProbe = 30;

    /// <summary>Halves a sum to find a midpoint.</summary>
    private const float Half = 0.5F;

    /// <summary>How far along the cloud's top side a scallop's peak is probed.</summary>
    private const float ScallopProbe = 55;

    /// <summary>How far above the cloud's top side a scallop's peak is probed.</summary>
    private const float ScallopRise = 4;

    /// <summary>How far into a picture stamp it is probed.</summary>
    private const float PictureProbe = 40;

    /// <summary>Bytes per rendered pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>A white channel.</summary>
    private const byte White = 0xFF;

    /// <summary>The picture stamp's side in pixels.</summary>
    private const int PictureSide = 8;

    /// <summary>A grey pixel channel.</summary>
    private const byte Grey = 0x40;

    /// <summary>The green channel's offset in a BGRA pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The red channel's offset in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The alpha channel's offset in a BGRA pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>How far around a probed point pixels are checked.</summary>
    private const int Reach = 2;

    /// <summary>The rectangle's top-left corner, below the page's text.</summary>
    private static readonly PagePoint BoxStart = new(72, 200);

    /// <summary>The rectangle's bottom-right corner.</summary>
    private static readonly PagePoint BoxEnd = new(172, 260);

    /// <summary>A drawing.</summary>
    private static readonly PagePoint[] Stroke = [new(80, 300), new(120, 280), new(160, 320)];

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

    /// <summary>What the saved callout points at.</summary>
    private static readonly PagePoint SavedCalloutTarget = new(400, 600);

    /// <summary>Where the saved callout's text goes.</summary>
    private static readonly PagePoint SavedCalloutText = new(300, 650);

    /// <summary>A highlighted line.</summary>
    private static readonly PageRect MarkedLine = new(72, 100, 200, 14);

    /// <summary>Bounds somewhere else on the page.</summary>
    private static readonly PageRect Elsewhere = new(300, 300, 100, 20);

    /// <summary>Where the picture stamp goes.</summary>
    private static readonly PageRect PictureAt = new(100, 100, 80, 80);

    /// <summary>Where the picture stamp is moved to.</summary>
    private static readonly PageRect PictureMoved = new(300, 300, 80, 80);

    /// <summary>Moves each movable kind and resizes a rectangle; the change is read back, drawn, and kept when saved.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesAndResizesAnnotations()
    {
        using var test = new TestDocument(PageCount);
        var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!;
        int[] indexes =
        [
            editor.AddShape(Page, AnnotationKind.Rectangle, BoxStart, BoxEnd, AnnotationColors.Clay, Width),
            editor.AddInk(Page, Stroke, [Stroke.Length], AnnotationColors.Ink, Width, AnnotationKind.Ink),
            editor.AddShape(Page, AnnotationKind.Arrow, ArrowTail, ArrowTip, AnnotationColors.Ink, Width),
            editor.AddStamp(Page, StampAt, "DRAFT", AnnotationColors.Clay),
            editor.AddText(Page, TextAt, "Moved text", FontSize, AnnotationColors.Ink, AnnotationKind.TextBox),
            editor.AddNote(Page, NoteAt, "Note", AnnotationColors.Sand),
            editor.AddPolygon(Page, AnnotationKind.Polygon, Triangle, AnnotationColors.Slate, Width),
            editor.AddCallout(Page, CalloutTarget, CalloutText, "Look here", FontSize, AnnotationColors.Ink),
        ];
        var before = Read(editor);
        foreach (var annotation in before)
        {
            var bounds = annotation.Bounds;
            _ = editor.SetBounds(Page, annotation.Index, bounds with { Top = bounds.Top + Shift });
        }

        var moved = Read(editor);
        var grown = editor.SetBounds(Page, indexes[0], moved[0].Bounds with { Width = moved[0].Bounds.Width * Double });
        var resized = Read(editor)[0].Bounds;
        var pixels = Render(test.Document, out var width);
        var reopened = await SaveAndReadAsync(editor);

        await Assert.That(Array.TrueForAll(indexes, static i => i >= 0)).IsTrue();
        await Assert.That(moved.Count).IsEqualTo(indexes.Length);
        for (var i = 0; i < before.Count; i++)
        {
            await Assert.That(moved[i].Index).IsEqualTo(before[i].Index);
            await Assert.That(Math.Abs(moved[i].Bounds.Top - (before[i].Bounds.Top + Shift))).IsLessThan(Tolerance);
            await Assert.That(Math.Abs(moved[i].Bounds.Left - before[i].Bounds.Left)).IsLessThan(Tolerance);
            await Assert.That(Math.Abs(reopened[i].Bounds.Top - Read(editor)[i].Bounds.Top)).IsLessThan(Tolerance);
        }

        await Assert.That(grown).IsTrue();
        await Assert.That(Math.Abs(resized.Width - (moved[0].Bounds.Width * Double))).IsLessThan(Tolerance);
        await Assert.That(IsInked(pixels, width, new(BoxStart.X, BoxStart.Y + Shift + SideProbe))).IsTrue();
        await Assert.That(IsInked(pixels, width, new(BoxStart.X, BoxStart.Y + SideProbe))).IsFalse();
        await Assert.That(IsInked(pixels, width, new(Stroke[1].X, Stroke[1].Y + Shift))).IsTrue();
        await Assert.That(IsInked(pixels, width, Stroke[1])).IsFalse();
    }

    /// <summary>Text markup follows its text, so it cannot be moved; nor can a removed annotation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesToMoveMarkupAndRemovedAnnotations()
    {
        using var test = new TestDocument(PageCount);
        var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!;
        var highlight = editor.AddMarkup(Page, AnnotationKind.Highlight, [MarkedLine], AnnotationColors.Sand, string.Empty);
        var box = editor.AddShape(Page, AnnotationKind.Rectangle, BoxStart, BoxEnd, AnnotationColors.Clay, Width);
        _ = editor.SetRemoved(Page, box, true);

        var movedHighlight = editor.SetBounds(Page, highlight, Elsewhere);
        var movedRemoved = editor.SetBounds(Page, box, Elsewhere);

        await Assert.That(movedHighlight).IsFalse();
        await Assert.That(movedRemoved).IsFalse();
    }

    /// <summary>
    /// A removed annotation keeps its index, is no longer listed or drawn, can be brought back, and is left out of the
    /// saved file.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovesAndRestoresWithoutShiftingIndexes()
    {
        using var test = new TestDocument(PageCount);
        var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!;
        var box = editor.AddShape(Page, AnnotationKind.Rectangle, BoxStart, BoxEnd, AnnotationColors.Clay, Width);
        var note = editor.AddNote(Page, NoteAt, "Keep me", AnnotationColors.Sand);
        var side = new PagePoint(BoxStart.X, BoxStart.Y + SideProbe);

        var removed = editor.SetRemoved(Page, box, true);
        var removedTwice = editor.SetRemoved(Page, box, true);
        var afterRemove = Read(editor);
        var hidden = !IsInked(Render(test.Document, out var width), width, side);
        var restored = editor.SetRemoved(Page, box, false);
        var afterRestore = Read(editor);
        var shown = IsInked(Render(test.Document, out _), width, side);
        _ = editor.SetRemoved(Page, box, true);
        var saved = await SaveAsync(editor);
        var reopened = ReadFile(saved);

        await Assert.That(removed && !removedTwice && restored).IsTrue();
        await Assert.That(afterRemove.Select(static a => a.Index).ToArray()).IsEquivalentTo([note]);
        await Assert.That(hidden).IsTrue();
        await Assert.That(afterRestore.Count).IsEqualTo(Two);
        await Assert.That(shown).IsTrue();
        await Assert.That(reopened.Single().Contents).IsEqualTo("Keep me");
    }

    /// <summary>Line widths and text sizes change and are read back; recolouring a drawing keeps it drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RestylesAnnotations()
    {
        using var test = new TestDocument(PageCount);
        var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!;
        var ink = editor.AddInk(Page, Stroke, [Stroke.Length], AnnotationColors.Ink, Width, AnnotationKind.Ink);
        var box = editor.AddShape(Page, AnnotationKind.Rectangle, BoxStart, BoxEnd, AnnotationColors.Clay, Width);
        var text = editor.AddText(Page, TextAt, "Bigger", FontSize, AnnotationColors.Ink, AnnotationKind.TextBox);
        var callout = editor.AddCallout(Page, CalloutTarget, CalloutText, "Look", FontSize, AnnotationColors.Ink);
        var before = Read(editor);

        var thickInk = editor.SetLineWidth(Page, ink, ThickWidth);
        var thickBox = editor.SetLineWidth(Page, box, ThickWidth);
        var largeText = editor.SetFontSize(Page, text, LargeFontSize);
        var largeCallout = editor.SetFontSize(Page, callout, LargeFontSize);
        var recoloured = editor.SetColor(Page, ink, AnnotationColors.Deep(AnnotationColors.Sage));
        var noWidthForText = editor.SetLineWidth(Page, text, ThickWidth);
        var noSizeForBox = editor.SetFontSize(Page, box, LargeFontSize);
        var after = Read(editor);
        var pixels = Render(test.Document, out var width);
        var reopened = await SaveAndReadAsync(editor);

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
        await Assert.That(IsInked(pixels, width, Stroke[1])).IsTrue();
        await Assert.That(reopened[0].LineWidth).IsEqualTo(ThickWidth);
        await Assert.That(reopened[2].FontSize).IsEqualTo(LargeFontSize);
    }

    /// <summary>Polygons, clouds, runs of lines and callouts are drawn, and saved as the standard PDF types other readers know.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesPolygonsCloudsAndCalloutsAsStandardTypes()
    {
        using var test = new TestDocument(PageCount);
        var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!;
        int[] indexes =
        [
            editor.AddPolygon(Page, AnnotationKind.Polygon, Triangle, AnnotationColors.Slate, Width),
            editor.AddPolygon(Page, AnnotationKind.Cloud, Square, AnnotationColors.Clay, Width),
            editor.AddPolygon(Page, AnnotationKind.PolyLine, Stroke, AnnotationColors.Ink, Width),
            editor.AddCallout(Page, SavedCalloutTarget, SavedCalloutText, "Check this", FontSize, AnnotationColors.Ink),
        ];
        var tooFew = editor.AddPolygon(Page, AnnotationKind.Polygon, Stroke.AsSpan(0, Two), AnnotationColors.Ink, Width);
        var kinds = Read(editor).Select(static a => a.Kind).ToArray();
        var pixels = Render(test.Document, out var width);
        var saved = await SaveAsync(editor);
        var text = Encoding.Latin1.GetString(saved);
        var reopened = ReadFile(saved);

        AnnotationKind[] expected = [AnnotationKind.Polygon, AnnotationKind.Cloud, AnnotationKind.PolyLine, AnnotationKind.Callout];
        await Assert.That(Array.TrueForAll(indexes, static i => i >= 0)).IsTrue();
        await Assert.That(tooFew).IsEqualTo(-1);
        await Assert.That(kinds).IsEquivalentTo(expected);
        await Assert.That(reopened.Select(static a => a.Kind).ToArray()).IsEquivalentTo(expected);
        await Assert.That(reopened[3].Contents).IsEqualTo("Check this");
        await Assert.That(IsInked(pixels, width, new((Triangle[0].X + Triangle[1].X) * Half, Triangle[0].Y))).IsTrue();
        await Assert.That(IsInked(pixels, width, new(Square[0].X + ScallopProbe, Square[0].Y - ScallopRise))).IsTrue();
        await Assert.That(text).Contains("/Subtype /Polygon");
        await Assert.That(text).Contains("/Subtype /PolyLine");
        await Assert.That(text).Contains("/Vertices [");
        await Assert.That(text).Contains("/IT /PolygonCloud");
        await Assert.That(text).Contains("/BE << /S /C /I 1 >>");
        await Assert.That(text).Contains("/IT /FreeTextCallout");
        await Assert.That(text).Contains("/CL [");
    }

    /// <summary>A moved polygon from a saved file keeps its new vertices when saved again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesSavedPolygons()
    {
        using var test = new TestDocument(PageCount);
        var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!;
        _ = editor.AddPolygon(Page, AnnotationKind.Cloud, Triangle, AnnotationColors.Clay, Width);
        var path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-polygon-{Guid.NewGuid():N}.pdf");
        try
        {
            await File.WriteAllBytesAsync(path, await SaveAsync(editor));
            using var reopened = new PdfiumEngine().Open(path, null);
            var reopenedEditor = (IAnnotationEditor)DocumentFeatures.CastFeature(reopened, typeof(IAnnotationEditor))!;
            var before = Read(reopenedEditor)[0];
            var moved = reopenedEditor.SetBounds(Page, before.Index, before.Bounds with { Top = before.Bounds.Top + Shift });
            var recoloured = reopenedEditor.SetColor(Page, before.Index, AnnotationColors.Slate);
            var pixels = Render(reopened, out var width);
            var again = ReadFile(await SaveAsync(reopenedEditor));

            await Assert.That(moved && recoloured).IsTrue();
            await Assert.That(IsInked(pixels, width, new((Triangle[0].X + Triangle[1].X) * Half, Triangle[0].Y + Shift))).IsTrue();
            await Assert.That(again[0].Kind).IsEqualTo(AnnotationKind.Cloud);
            await Assert.That(Math.Abs(again[0].Bounds.Top - (before.Bounds.Top + Shift))).IsLessThan(Tolerance);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A picture stamp is placed, listed as a stamp, drawn and moved.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlacesPictureStamps()
    {
        using var test = new TestDocument(PageCount);
        var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!;
        var pixels = new byte[PictureSide * PictureSide * BytesPerPixel];
        for (var i = 0; i < pixels.Length; i += BytesPerPixel)
        {
            pixels[i] = Grey;
            pixels[i + GreenOffset] = Grey;
            pixels[i + RedOffset] = Grey;
            pixels[i + AlphaOffset] = White;
        }

        var stamp = editor.AddImageStamp(Page, PictureAt, pixels, PictureSide, PictureSide);
        var tooShort = editor.AddImageStamp(Page, PictureAt, pixels.AsSpan(0, BytesPerPixel), PictureSide, PictureSide);
        var moved = editor.SetBounds(Page, stamp, PictureMoved);
        var rendered = Render(test.Document, out var width);
        var annotation = Read(editor).Single();

        await Assert.That(stamp).IsGreaterThanOrEqualTo(0);
        await Assert.That(tooShort).IsEqualTo(-1);
        await Assert.That(moved).IsTrue();
        await Assert.That(annotation.Kind).IsEqualTo(AnnotationKind.Stamp);
        await Assert.That(IsInked(rendered, width, new(PictureMoved.Left + PictureProbe, PictureMoved.Top + PictureProbe))).IsTrue();
        await Assert.That(IsInked(rendered, width, new(PictureAt.Left + PictureProbe, PictureAt.Top + PictureProbe))).IsFalse();
    }

    /// <summary>New annotations and replies record the chosen author; a blank author falls back to the user name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecordsTheChosenAuthor()
    {
        using var test = new TestDocument(PageCount);
        var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!;
        var defaultAuthor = editor.Author;
        editor.Author = "  Sam Reviewer ";
        var note = editor.AddNote(Page, NoteAt, "Hello", AnnotationColors.Sand);
        _ = editor.AddReply(Page, note, "Thanks", ReviewState.None);
        var replies = new List<AnnotationReply>();
        editor.GetReplies(Page, note, replies);
        var listed = Read(editor).Single();
        editor.Author = " ";

        await Assert.That(defaultAuthor).IsEqualTo(Environment.UserName);
        await Assert.That(listed.Author).IsEqualTo("Sam Reviewer");
        await Assert.That(listed.Modified).IsNotNull();
        await Assert.That(replies.Single().Author).IsEqualTo("Sam Reviewer");
        await Assert.That(editor.Author).IsEqualTo(Environment.UserName);
    }

    /// <summary>Reads the annotations on the edited page.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The annotations.</returns>
    private static List<PageAnnotation> Read(IAnnotationEditor editor)
    {
        var annotations = new List<PageAnnotation>();
        editor.GetAnnotations(Page, annotations);
        return annotations;
    }

    /// <summary>Saves a document into memory.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The saved bytes.</returns>
    private static async Task<byte[]> SaveAsync(IAnnotationEditor editor)
    {
        await using var stream = new MemoryStream();
        await Assert.That(await editor.SaveAsync(stream, CancellationToken.None)).IsTrue();
        return stream.ToArray();
    }

    /// <summary>Saves a document and reads the edited page's annotations from the saved file.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The annotations in the saved file.</returns>
    private static async Task<List<PageAnnotation>> SaveAndReadAsync(IAnnotationEditor editor) => ReadFile(await SaveAsync(editor));

    /// <summary>Opens saved bytes and reads the edited page's annotations.</summary>
    /// <param name="bytes">The saved file.</param>
    /// <returns>The annotations.</returns>
    private static List<PageAnnotation> ReadFile(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-editing-{Guid.NewGuid():N}.pdf");
        try
        {
            File.WriteAllBytes(path, bytes);
            using var document = new PdfiumEngine().Open(path, null);
            return Read((IAnnotationEditor)DocumentFeatures.CastFeature(document, typeof(IAnnotationEditor))!);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Renders the edited page at one pixel per point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="width">The page width in pixels.</param>
    /// <returns>The pixels.</returns>
    private static byte[] Render(IDocument document, out int width)
    {
        TestPdf.GetPageSize(Page, out width, out var height);
        var pixels = new byte[width * height * BytesPerPixel];
        _ = document.Render(new(Page, 1, PageRotation.None, 0, 0, RenderFlags.Annotations), new(pixels, width, height, width * BytesPerPixel));
        return pixels;
    }

    /// <summary>Determines whether any pixel within a few points of a page point is not white.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="width">The page width in pixels.</param>
    /// <param name="point">The page point.</param>
    /// <returns><see langword="true"/> when something is drawn there.</returns>
    private static bool IsInked(byte[] pixels, int width, PagePoint point)
    {
        for (var dy = -Reach; dy <= Reach; dy++)
        {
            for (var dx = -Reach; dx <= Reach; dx++)
            {
                var offset = ((((int)point.Y + dy) * width) + (int)point.X + dx) * BytesPerPixel;
                if (pixels.AsSpan(offset, BytesPerPixel - 1).ContainsAnyExcept(White))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
