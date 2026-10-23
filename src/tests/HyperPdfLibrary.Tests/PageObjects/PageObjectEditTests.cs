// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.PageObjects;

/// <summary>Deletes, moves and recolours page objects and checks what the page then draws.</summary>
public sealed class PageObjectEditTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = PageObjectSamples.Size;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 3;

    /// <summary>The tolerance for positions in points.</summary>
    private const float PositionTolerance = 0.05F;

    /// <summary>The centre column of the red rectangle.</summary>
    private const int RedColumn = 50;

    /// <summary>The centre row of the red rectangle, counted from the top.</summary>
    private const int RedRow = 160;

    /// <summary>The centre column of the blue rectangle.</summary>
    private const int BlueColumn = 140;

    /// <summary>The centre row of the blue rectangle, counted from the top.</summary>
    private const int BlueRow = 60;

    /// <summary>The distance the red rectangle moves.</summary>
    private const float Shift = 100;

    /// <summary>The centre column of the red rectangle after it moves.</summary>
    private const int MovedColumn = 150;

    /// <summary>The font size of the text samples.</summary>
    private const float TextSize = 20;

    /// <summary>The distance text moves up.</summary>
    private const float Lift = 30;

    /// <summary>The number of text objects in the two-line text sample.</summary>
    private const int TwoObjects = 2;

    /// <summary>The index of the second glyph removed from "Hello".</summary>
    private const int SecondGlyph = 2;

    /// <summary>The marked content id of the heading, first paragraph, second paragraph and figure of the tagged sample.</summary>
    private const int HeadingId = 0;

    /// <summary>The marked content id of the first paragraph of the tagged sample.</summary>
    private const int FirstId = 1;

    /// <summary>The marked content id of the second paragraph of the tagged sample.</summary>
    private const int SecondId = 2;

    /// <summary>The marked content id of the figure of the tagged sample.</summary>
    private const int FigureId = 3;

    /// <summary>The index of the heading among the objects of the tagged sample page: after the header and the second paragraph.</summary>
    private const int HeadingObject = 2;

    /// <summary>The highest channel value.</summary>
    private const byte Full = 255;

    /// <summary>The row, from the top, in the middle of the image on the mixed page.</summary>
    private const int ImageRow = 155;

    /// <summary>The column in the middle of the form on the mixed page.</summary>
    private const int FormColumn = 50;

    /// <summary>The row, from the top, in the middle of the form on the mixed page.</summary>
    private const int FormRow = 80;

    /// <summary>The page with a red and a blue rectangle.</summary>
    private const string TwoRectangles = "1 0 0 rg 20 20 60 40 re f 0 0 1 rg 120 120 40 40 re f";

    /// <summary>Deleting an object leaves the others where they were.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeleteRemovesOnlyThatObject()
    {
        var pdf = PageObjectSamples.Page(TwoRectangles);
        var edited = PageObjectSamples.Edit(pdf, static content => content.Objects[0].Delete());
        var image = PageObjectSamples.Render(edited);

        await Assert.That(image.IsNear(RedColumn, RedRow, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(BlueColumn, BlueRow, Rgb.Blue255, Tolerance)).IsTrue();
    }

    /// <summary>Moving an object draws it in the new place and nowhere else.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MoveShiftsTheObject()
    {
        var pdf = PageObjectSamples.Page(TwoRectangles);
        var edited = PageObjectSamples.Edit(pdf, static content => content.Objects[0].Translate(Shift, 0));
        var image = PageObjectSamples.Render(edited);

        await Assert.That(image.IsNear(RedColumn, RedRow, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(MovedColumn, RedRow, Rgb.Red255, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(BlueColumn, BlueRow, Rgb.Blue255, Tolerance)).IsTrue();
    }

    /// <summary>A moved object reports its moved bounds, and the content it leaves behind keeps the page's state.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovedObjectsReportMovedBoundsAndLeaveLaterObjectsAlone()
    {
        var pdf = PageObjectSamples.Page("q 1 0 0 rg 20 20 60 40 re f Q 0 0 1 rg 120 120 40 40 re f");
        using var document = PdfDocumentReader.Open(pdf, null);
        var content = PdfDocumentPageContent.GetPageContent(document, 0);
        var original = content.Objects[0].Bounds;
        content.Objects[0].Translate(Shift, 0);

        await Assert.That(content.Objects[0].Bounds.Left).IsEqualTo(original.Left + Shift).Within(PositionTolerance);
        await Assert.That(content.Objects[0].IsModified).IsTrue();
        await Assert.That(content.Objects[1].IsModified).IsFalse();
    }

    /// <summary>Recolouring changes the colour the object is drawn in and no other.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecolourChangesOnlyThatObject()
    {
        var pdf = PageObjectSamples.Page(TwoRectangles);
        var edited = PageObjectSamples.Edit(pdf, static content => content.Objects[0].SetFillPaint(PdfPaint.FromRgb(0, 1, 0)));
        var image = PageObjectSamples.Render(edited);

        await Assert.That(image.IsNear(RedColumn, RedRow, Rgb.Green255, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(BlueColumn, BlueRow, Rgb.Blue255, Tolerance)).IsTrue();
    }

    /// <summary>Recolouring text does not change the colour of text drawn after it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecolouredTextLeavesTheStateAlone()
    {
        var pdf = PageObjectSamples.Page("BT /F1 20 Tf 10 100 Td (AAA) Tj 0 -40 Td (BBB) Tj ET");
        var edited = PageObjectSamples.Edit(pdf, static content => content.Objects[0].SetFillPaint(PdfPaint.FromRgb(1, 0, 0)));
        using var document = PdfDocumentReader.Open(edited, null);
        var content = PdfDocumentPageContent.GetPageContent(document, 0);

        await Assert.That(content.Objects.Count).IsEqualTo(TwoObjects);
        await Assert.That(content.Objects[0].FillPaint.Components[0]).IsEqualTo(1);
        await Assert.That(content.Objects[1].FillPaint.Components[0]).IsEqualTo(0);
        await Assert.That(content.Objects[1].FillPaint.ColorSpace.Is(HyperPdfLibrary.Objects.KnownName.DeviceGray)).IsTrue();
    }

    /// <summary>Deleting text leaves the text after it where it was.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeletingTextKeepsTheNextTextInPlace()
    {
        var pdf = PageObjectSamples.Page("BT /F1 20 Tf 10 100 Td (AAA) Tj (BBB) Tj ET");
        float before;
        using (var document = PdfDocumentReader.Open(pdf, null))
        {
            before = PdfDocumentPageContent.GetPageContent(document, 0).Objects[1].Bounds.Left;
        }

        var edited = PageObjectSamples.Edit(pdf, static content => content.Objects[0].Delete());
        using var after = PdfDocumentReader.Open(edited, null);
        var content = PdfDocumentPageContent.GetPageContent(after, 0);

        await Assert.That(content.Objects.Count).IsEqualTo(1);
        await Assert.That(content.Objects[0].Bounds.Left).IsEqualTo(before).Within(PositionTolerance);
        await Assert.That(((PdfTextObject)content.Objects[0]).Text).IsEqualTo("BBB");
    }

    /// <summary>Removing glyphs leaves the others at the same positions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovingGlyphsKeepsTheRestInPlace()
    {
        var pdf = PageObjectSamples.Page("BT /F1 20 Tf 10 100 Td [(Hello) -300 (World)] TJ ET");
        float[] before;
        using (var document = PdfDocumentReader.Open(pdf, null))
        {
            var text = (PdfTextObject)PdfDocumentPageContent.GetPageContent(document, 0).Objects[0];
            before = [.. text.Glyphs.ToArray().Select(static glyph => glyph.Origin.X)];
        }

        var edited = PageObjectSamples.Edit(pdf, static content =>
        {
            var text = (PdfTextObject)content.Objects[0];
            _ = text.RemoveGlyph(1);
            _ = text.RemoveGlyph(SecondGlyph);
        });
        using var after = PdfDocumentReader.Open(edited, null);
        var remaining = (PdfTextObject)PdfDocumentPageContent.GetPageContent(after, 0).Objects[0];

        await Assert.That(remaining.Text).IsEqualTo("HloWorld");
        await Assert.That(remaining.Glyphs[0].Origin.X).IsEqualTo(before[0]).Within(PositionTolerance);
        await Assert.That(remaining.Glyphs[1].Origin.X).IsEqualTo(before[3]).Within(PositionTolerance);
        await Assert.That(remaining.Glyphs[3].Origin.X).IsEqualTo(before[5]).Within(PositionTolerance);
    }

    /// <summary>Moving text moves it by the distance and leaves the text after it in place.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovingTextLeavesLaterTextInPlace()
    {
        var pdf = PageObjectSamples.Page("BT /F1 20 Tf 10 100 Td (AAA) Tj (BBB) Tj ET");
        PdfRectangle[] before;
        using (var document = PdfDocumentReader.Open(pdf, null))
        {
            var objects = PdfDocumentPageContent.GetPageContent(document, 0).Objects;
            before = [objects[0].Bounds, objects[1].Bounds];
        }

        var edited = PageObjectSamples.Edit(pdf, static content => content.Objects[0].Translate(0, Lift));
        using var after = PdfDocumentReader.Open(edited, null);
        var moved = PdfDocumentPageContent.GetPageContent(after, 0).Objects;

        await Assert.That(moved[0].Bounds.Bottom).IsEqualTo(before[0].Bottom + Lift).Within(PositionTolerance);
        await Assert.That(moved[0].Bounds.Left).IsEqualTo(before[0].Left).Within(PositionTolerance);
        await Assert.That(moved[1].Bounds.Left).IsEqualTo(before[1].Left).Within(PositionTolerance);
        await Assert.That(moved[1].Bounds.Bottom).IsEqualTo(before[1].Bottom).Within(PositionTolerance);
    }

    /// <summary>Editing a tagged page keeps its marked content and the structure tree.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TagsStillResolveAfterEdits()
    {
        var pdf = TestPdf.CreateTagged();
        int elements;
        using (var document = PdfDocumentReader.Open(pdf, null))
        {
            elements = PdfDocumentTagged.GetStructureTree(document)!.ElementCount;
        }

        var edited = PageObjectSamples.Edit(pdf, static content => content.Objects[HeadingObject].Delete());
        using var after = PdfDocumentReader.Open(edited, null);
        var marked = PdfDocumentTagged.GetMarkedContent(after, 0);
        var source = System.Text.Encoding.ASCII.GetString(PdfDocumentPageContent.GetPageContent(after, 0).Source);

        await Assert.That(PdfDocumentTagged.GetStructureTree(after)!.ElementCount).IsEqualTo(elements);
        await Assert.That(source).Contains("/H1 << /MCID 0 >> BDC");
        await Assert.That(marked.GetGlyphs(HeadingId).Length).IsEqualTo(0);
        await Assert.That(marked.GetGlyphs(FirstId).Length).IsGreaterThan(0);
        await Assert.That(marked.GetGlyphs(SecondId).Length).IsGreaterThan(0);
        await Assert.That(marked.HasContent(FigureId)).IsTrue();
    }

    /// <summary>An edit joins the document's undo history.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditsCanBeUndone()
    {
        var pdf = PageObjectSamples.Page(TwoRectangles);
        using var page = new RenderTestPage(pdf);
        var content = PdfDocumentPageContent.GetPageContent(page.Document, 0);
        content.Objects[0].Delete();
        PdfPageContentApplication.Apply(content);
        var deleted = page.RenderPage();
        _ = PdfDocumentEditing.Undo(page.Document);
        var restored = page.RenderPage();

        await Assert.That(deleted.IsNear(RedColumn, RedRow, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(restored.IsNear(RedColumn, RedRow, Rgb.Red255, Tolerance)).IsTrue();
    }

    /// <summary>The edited page saved incrementally and compactly reopens with the same drawing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditedPageSavesBothWays()
    {
        var pdf = PageObjectSamples.Page(TwoRectangles);
        using var document = PdfDocumentReader.Open(pdf, null);
        var content = PdfDocumentPageContent.GetPageContent(document, 0);
        content.Objects[0].Translate(Shift, 0);
        PdfPageContentApplication.Apply(content);
        var compact = PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);
        var incremental = PdfIncrementalWriter.Save(document.Objects);

        await Assert.That(PageObjectSamples.Render(compact).IsNear(MovedColumn, RedRow, Rgb.Red255, Tolerance)).IsTrue();
        await Assert.That(PageObjectSamples.Render(incremental).IsNear(MovedColumn, RedRow, Rgb.Red255, Tolerance)).IsTrue();
        await Assert.That(PageObjectSamples.Render(incremental).IsNear(RedColumn, RedRow, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>Replacing an image's pixels draws the new pixels in the same place.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReplacingImageDataDrawsTheNewPixels()
    {
        var pdf = PageObjectSamples.Mixed();
        var edited = PageObjectSamples.Edit(pdf, static content =>
        {
            var image = (PdfImageObject)content.Objects[2];
            image.SetPixels(1, 1, PdfImagePixelFormat.Rgb24, [0, Full, 0]);
        });
        var image = PageObjectSamples.Render(edited);

        await Assert.That(image.IsNear(MovedColumn, ImageRow, Rgb.Green255, Tolerance)).IsTrue();
    }

    /// <summary>Changing a form's content writes a new form and leaves the shared original alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangedFormIsWrittenAsANewForm()
    {
        var pdf = PageObjectSamples.Mixed();
        var before = PageObjectSamples.Render(pdf);
        var edited = PageObjectSamples.Edit(pdf, static content =>
        {
            var form = (PdfFormObject)content.Objects[4];
            form.GetContent().Objects[0].SetFillPaint(PdfPaint.FromRgb(0, 1, 0));
        });
        var after = PageObjectSamples.Render(edited);
        using var document = PdfDocumentReader.Open(pdf, null);
        var formStream = ((PdfFormObject)PdfDocumentPageContent.GetPageContent(document, 0).Objects[4]).Stream;

        await Assert.That(before.IsNear(FormColumn, FormRow, Rgb.Blue255, Tolerance)).IsTrue();
        await Assert.That(after.IsNear(FormColumn, FormRow, Rgb.Green255, Tolerance)).IsTrue();
        await Assert.That(System.Text.Encoding.ASCII.GetString(formStream.DecodeToArray())).Contains("0 0 1 rg");
    }

    /// <summary>The font size is kept for the text a TextSize page draws.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextSizeIsKeptThroughAnEdit()
    {
        var pdf = PageObjectSamples.Page("BT /F1 20 Tf 10 100 Td (AAA) Tj ET");
        var edited = PageObjectSamples.Edit(pdf, static content => content.Objects[0].Translate(1, 1));
        using var document = PdfDocumentReader.Open(edited, null);

        await Assert.That(((PdfTextObject)PdfDocumentPageContent.GetPageContent(document, 0).Objects[0]).FontSize).IsEqualTo(TextSize);
    }
}
