// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Tests.Writing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.PageObjects;

/// <summary>Deletes, moves and recolours page objects and checks what the page then draws.</summary>
public sealed class PageObjectEditTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = PageObjectSamples.Size;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 3;

    /// <summary>How solid an object made half see-through is.</summary>
    private const float HalfSolid = 0.5F;

    /// <summary>The share of a box a crop test cuts off its left, which is the 20 columns of the box and the 20 beside it.</summary>
    private const float CropShare = 0.25F;

    /// <summary>A column just inside the left edge of the gradient test's box.</summary>
    private const int GradientLeftColumn = 22;

    /// <summary>A column just inside the right edge of the gradient test's box.</summary>
    private const int GradientRightColumn = 178;

    /// <summary>The row through the middle of the gradient test's box.</summary>
    private const int MiddleRow = 100;

    /// <summary>How far a channel at the ends of a gradient may be from the pure colour.</summary>
    private const int GradientTolerance = 14;

    /// <summary>A number that is not the value of any blend mode.</summary>
    private const int UndefinedBlendMode = 99;

    /// <summary>The value of a channel at full strength.</summary>
    private const int FullChannel = 255;

    /// <summary>The length of a dash and of a gap, in points.</summary>
    private const float DashLength = 6F;

    /// <summary>The value of the green and blue channels where half solid red covers white.</summary>
    private const int HalfChannel = 128;

    /// <summary>How far a pixel under half solid red may be from the exact blend, which depends on how colours are rounded.</summary>
    private const int PaleTolerance = 4;

    /// <summary>The tolerance for positions in points.</summary>
    private const float PositionTolerance = 0.05F;

    /// <summary>Two, for a quarter turn and for the middle of a box.</summary>
    private const float Halves = 2F;

    /// <summary>How far a turned object's width and height may be from the other's, in points; a turned stroke or glyph box is measured a little differently.</summary>
    private const float TurnTolerance = 0.5F;

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

    /// <summary>The left edge of the list item in the hanging indent test.</summary>
    private const float ListLeft = 20F;

    /// <summary>How far in the lines under the first line of that list item start.</summary>
    private const float Hanging = 14F;

    /// <summary>The lines of the paragraph the wrapping test starts from.</summary>
    private const int ThreeLines = 3;

    /// <summary>The font size of the lined up paragraphs.</summary>
    private const float ParagraphSize = 12F;

    /// <summary>The distance from the baseline of one line of those paragraphs to the next.</summary>
    private const float ParagraphLeading = 14F;

    /// <summary>The height of the baseline of the first line of those paragraphs.</summary>
    private const float ParagraphTop = 160F;

    /// <summary>The right edge of the right aligned paragraph.</summary>
    private const float RightEdge = 180F;

    /// <summary>The middle of the centred paragraph.</summary>
    private const float MiddleOfLines = 100F;

    /// <summary>The left edge of the justified and the turned paragraph.</summary>
    private const float StartOfLines = 20F;

    /// <summary>One half.</summary>
    private const float Half = 0.5F;

    /// <summary>How far an edge of a line may be from where it should be, in points.</summary>
    private const float EdgeTolerance = 1F;

    /// <summary>The first line of a turned paragraph is this far across the page.</summary>
    private const float TurnedAcross = 60F;

    /// <summary>The text the lined up paragraphs are replaced with; it takes more lines than they had.</summary>
    private const string LongParagraph = "The quick brown fox jumps over the lazy dog and runs away from the farm and keeps running through the fields";

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

    /// <summary>A copy of an object of a tagged page is written without structure links: its marked content is an artifact and holds no /MCID.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ACopyHasNoMarkedContentNumbers()
    {
        var content = PageObjectSamples.Read(TestPdf.CreateTagged());
        var text = content.Objects.First(static o => o is PdfTextObject);

        var form = PdfPageContentObjectCopy.CreateForm(content, text);
        await using var inflater = new System.IO.Compression.ZLibStream(new MemoryStream(form.CopyRawData()), System.IO.Compression.CompressionMode.Decompress);
        using var reader = new StreamReader(inflater, System.Text.Encoding.Latin1);
        var written = await reader.ReadToEndAsync();

        using (Assert.Multiple())
        {
            await Assert.That(written).Contains("/Artifact BMC");
            await Assert.That(written).DoesNotContain("/MCID");
            await Assert.That(written).Contains("BT");
        }
    }

    /// <summary>A line the built-in fonts cannot show uses the bundled font when it has the letters, the fallback when it lacks them, and no embedding when a built-in font will do.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NewTextPicksTheBuiltInBundledOrFallbackFont()
    {
        var content = PageObjectSamples.Read(TestPdf.CreateTagged());
        var emoji = PdfBundledFonts.OpenOrThrow(PdfBundledFontFamily.Emoji, false, false);

        var plain = PdfNewTextRun.Create(content, "Plain", AppearanceFont.Helvetica, emoji);
        var greek = PdfNewTextRun.Create(content, "Καλημέρα", AppearanceFont.Helvetica, emoji);
        var smile = PdfNewTextRun.Create(content, "\U0001F600", AppearanceFont.Helvetica, emoji);

        using (Assert.Multiple())
        {
            await Assert.That(plain.IsEmbedded).IsFalse();
            await Assert.That(greek.IsEmbedded).IsTrue();
            await Assert.That(PdfNewTextRun.NeedsFallback("Καλημέρα", AppearanceFont.Helvetica)).IsFalse();
            await Assert.That(PdfNewTextRun.NeedsFallback("\U0001F600", AppearanceFont.Helvetica)).IsTrue();
            await Assert.That(smile.IsEmbedded).IsTrue();
        }
    }

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

    /// <summary>
    /// Every kind of object on the mixed page (path, text, image, shading, form and inline image) can be moved and deleted: after the edit
    /// is written and read back, the moved object is that far along and the others have not moved, and a deleted one is gone and the others stay.
    /// </summary>
    /// <param name="index">The object's place in painting order.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    public async Task EveryKindOfObjectCanBeMovedAndDeleted(int index)
    {
        var pdf = PageObjectSamples.Mixed();
        var original = ReadBounds(pdf);
        var moved = ReadBounds(PageObjectSamples.Edit(pdf, content => content.Objects[index].Translate(Shift, 0)));
        var deleted = ReadBounds(PageObjectSamples.Edit(pdf, content => content.Objects[index].Delete()));

        await Assert.That(moved.Length).IsEqualTo(original.Length);
        await Assert.That(deleted.Length).IsEqualTo(original.Length - 1);
        for (var i = 0; i < original.Length; i++)
        {
            var expected = i == index ? original[i] + Shift : original[i];
            await Assert.That(moved[i]).IsEqualTo(expected).Within(PositionTolerance);
        }

        var remaining = original.Where((_, i) => i != index).ToArray();
        for (var i = 0; i < remaining.Length; i++)
        {
            await Assert.That(deleted[i]).IsEqualTo(remaining[i]).Within(PositionTolerance);
        }
    }

    /// <summary>Every kind of object on the mixed page can be turned a quarter about its middle: after the edit is written and read back, its width and height have swapped.</summary>
    /// <param name="index">The object's place in painting order.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    public async Task EveryKindOfObjectCanBeTurnedAQuarter(int index)
    {
        var pdf = PageObjectSamples.Mixed();
        var original = ReadBox(pdf, index);
        var turned = ReadBox(
            PageObjectSamples.Edit(
                pdf,
                content =>
                {
                    var item = content.Objects[index];
                    var box = item.Bounds;
                    item.Transform(Matrix3x2.CreateRotation(MathF.PI / Halves, new((box.Left + box.Right) / Halves, (box.Bottom + box.Top) / Halves)));
                }),
            index);

        await Assert.That(turned.Width).IsEqualTo(original.Height).Within(TurnTolerance);
        await Assert.That(turned.Height).IsEqualTo(original.Width).Within(TurnTolerance);
    }

    /// <summary>Every kind of object on the mixed page can be made half see-through, and reads back as half see-through, with the others untouched.</summary>
    /// <param name="index">The object's place in painting order.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    public async Task EveryKindOfObjectCanBeMadeSeeThrough(int index)
    {
        var edited = PageObjectSamples.Edit(PageObjectSamples.Mixed(), content => content.Objects[index].SetOpacity(HalfSolid));
        using var document = PdfDocumentReader.Open(edited, null);
        var objects = PdfDocumentPageContent.GetPageContent(document, 0).Objects;

        for (var i = 0; i < objects.Count; i++)
        {
            await Assert.That(objects[i].OriginalFillAlpha).IsEqualTo(i == index ? HalfSolid : 1F).Within(PositionTolerance);
        }
    }

    /// <summary>An object cropped by a clip of its own shows in the new place when it is moved, and not in the old, because its clip moves with it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovingAnObjectMovesItsOwnClipToo()
    {
        var pdf = PageObjectSamples.Page("q 20 20 60 40 re W n 1 0 0 rg 0 0 200 200 re f Q");
        var edited = PageObjectSamples.Edit(pdf, static content => content.Objects[0].Translate(Shift, 0));
        var image = PageObjectSamples.Render(edited);

        await Assert.That(image.IsNear(RedColumn, RedRow, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(MovedColumn, RedRow, Rgb.Red255, Tolerance)).IsTrue();
    }

    /// <summary>A clip that several objects share stays where it is when one of them moves, so the others are still clipped by it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovingOneObjectLeavesAClipTheOthersShare()
    {
        var pdf = PageObjectSamples.Page("q 0 0 200 100 re W n 1 0 0 rg 20 20 60 40 re f 0 0 1 rg 120 20 40 40 re f Q");
        var edited = PageObjectSamples.Edit(pdf, static content => content.Objects[0].Translate(0, Shift));
        var image = PageObjectSamples.Render(edited);

        // The first rectangle moved up, out of the clip that stops at 100, so it is gone; the second is still drawn.
        await Assert.That(image.IsNear(RedColumn, RedRow, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(BlueColumn, RedRow, Rgb.Blue255, Tolerance)).IsTrue();
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

    /// <summary>Making an object half solid lightens it over the white page, and leaves the other object alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpacityMakesOnlyThatObjectSeeThrough()
    {
        var pdf = PageObjectSamples.Page(TwoRectangles);
        var edited = PageObjectSamples.Edit(pdf, static content => content.Objects[0].SetOpacity(HalfSolid));
        var image = PageObjectSamples.Render(edited);

        await Assert.That(image.IsNear(RedColumn, RedRow, new(FullChannel, HalfChannel, HalfChannel), PaleTolerance)).IsTrue();
        await Assert.That(image.IsNear(BlueColumn, BlueRow, Rgb.Blue255, Tolerance)).IsTrue();
    }

    /// <summary>A dash pattern written for an object is kept in the file, and a bad one is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LineStyleIsWrittenAndValidated()
    {
        var pdf = PageObjectSamples.Page("4 w 20 100 m 180 100 l S");
        var style = new PdfLineStyle(PdfLineCap.Round, PdfLineJoin.Bevel, [DashLength, DashLength], 0F);
        var edited = PageObjectSamples.Edit(pdf, content => content.Objects[0].SetLineStyle(style));
        using var document = PdfDocumentReader.Open(edited, null);
        var text = System.Text.Encoding.Latin1.GetString(WritingTestDocuments.PageContents(document.Objects)[0]);

        await Assert.That(text).Contains("[6 6] 0 d");
        await Assert.That(text).Contains("1 J");
        await Assert.That(text).Contains("2 j");
        await Assert.That(() => PageObjectSamples.Edit(pdf, static content => content.Objects[0].SetLineStyle(new(PdfLineCap.Butt, PdfLineJoin.Miter, [0F, 0F], 0F))))
            .Throws<ArgumentOutOfRangeException>();
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

    /// <summary>Making text half solid lightens it, and the text after it stays solid because the old opacity is put back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeeThroughTextLeavesTheNextTextSolid()
    {
        var pdf = PageObjectSamples.Page("0 g BT /F1 60 Tf 10 100 Td (II) Tj 0 -70 Td (II) Tj ET");
        var edited = PageObjectSamples.Edit(pdf, static content => content.Objects[0].SetOpacity(HalfSolid));
        using var document = PdfDocumentReader.Open(edited, null);
        var content = PdfDocumentPageContent.GetPageContent(document, 0);
        var text = System.Text.Encoding.Latin1.GetString(WritingTestDocuments.PageContents(document.Objects)[0]);

        await Assert.That(content.Objects.Count).IsEqualTo(TwoObjects);
        await Assert.That(content.Objects[0].OriginalFillAlpha).IsEqualTo(HalfSolid);
        await Assert.That(content.Objects[1].OriginalFillAlpha).IsEqualTo(1F);
        await Assert.That(text).Contains(" gs");
    }

    /// <summary>A linear gradient fills the object from one colour to the other, and the shape outside it is untouched.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GradientFillShadesAcrossTheObject()
    {
        var pdf = PageObjectSamples.Page("1 0 0 rg 20 20 160 160 re f");
        var gradient = PdfGradient.Linear(PdfPaint.FromRgb(0, 0, 1), PdfPaint.FromRgb(1, 1, 1), 0F);
        var edited = PageObjectSamples.Edit(pdf, content => content.Objects[0].SetGradientFill(gradient));
        var image = PageObjectSamples.Render(edited);

        // The left of the box is blue and the right is white, with the page's white above and below the box.
        await Assert.That(image.IsNear(GradientLeftColumn, MiddleRow, new(0, 0, FullChannel), GradientTolerance)).IsTrue();
        await Assert.That(image.IsNear(GradientRightColumn, MiddleRow, new(FullChannel, FullChannel, FullChannel), GradientTolerance)).IsTrue();
        await Assert.That(() => PageObjectSamples.Edit(pdf, static content => content.Objects[0].SetGradientFill(PdfGradient.Radial(new(KnownName.Separation, [1F], default), PdfPaint.Black))))
            .Throws<ArgumentException>();
    }

    /// <summary>Cropping a drawing hides the cut part only, as a clip written in the drawing's own coordinates, and text is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CropClipsADrawingToPartOfItsBox()
    {
        var pdf = PageObjectSamples.Page("1 0 0 rg 20 20 160 160 re f");
        var edited = PageObjectSamples.Edit(pdf, static content => content.Objects[0].SetCrop(CropShare, 0F, 0F, 0F));
        var image = PageObjectSamples.Render(edited);

        // The left quarter of the box is cut away and the rest stays red.
        await Assert.That(image.IsNear(GradientLeftColumn, MiddleRow, new(FullChannel, FullChannel, FullChannel), Tolerance)).IsTrue();
        await Assert.That(image.IsNear(GradientRightColumn, MiddleRow, Rgb.Red255, Tolerance)).IsTrue();
        await Assert.That(static () => PageObjectSamples.Edit(PageObjectSamples.Page("BT /F1 20 Tf 10 100 Td (AAA) Tj ET"), static content => content.Objects[0].SetCrop(CropShare, 0F, 0F, 0F)))
            .Throws<InvalidOperationException>();
    }

    /// <summary>Replacing a paragraph wraps the new text to the old width: more words take more lines, fewer words take fewer, and no line is wider.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParagraphReplacementWrapsToTheOldWidth()
    {
        var pdf = PageObjectSamples.Page("BT /F1 12 Tf 20 160 Td (The quick brown fox jumps) Tj 0 -14 Td (over the lazy dog and) Tj 0 -14 Td (runs away) Tj ET");
        const string Long = "The quick brown fox jumps over the lazy dog and runs away from the farm and keeps running through the fields";
        var widest = 0F;
        foreach (var line in ReadLines(pdf))
        {
            widest = Math.Max(widest, line.Right);
        }

        var longer = ReadLines(ReplaceParagraph(pdf, Long));
        var shorter = ReadLines(ReplaceParagraph(pdf, "A short text"));
        var longerWidest = 0F;
        var words = new List<string>();
        foreach (var line in longer)
        {
            longerWidest = Math.Max(longerWidest, line.Right);
            words.Add(line.Text);
        }

        await Assert.That(longer.Count).IsGreaterThan(ThreeLines);
        await Assert.That(string.Join(' ', words)).IsEqualTo(Long);
        await Assert.That(shorter.Count).IsEqualTo(1);
        await Assert.That(longerWidest).IsLessThanOrEqualTo(widest + PositionTolerance);
    }

    /// <summary>The lines of a list item under its first line keep their place further in, under the words and not the marker.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListItemKeepsItsHangingIndent()
    {
        var pdf = PageObjectSamples.Page("BT /F1 12 Tf 20 160 Td (- first line of the item) Tj 14 -14 Td (continues here and) Tj 0 -14 Td (ends) Tj ET");
        var lines = ReadLines(ReplaceParagraph(pdf, "- first line of the item continues here and keeps going on and on through several more lines of the same item"));

        await Assert.That(lines.Count).IsGreaterThan(ThreeLines);
        await Assert.That(lines[0].Left).IsEqualTo(ListLeft).Within(PositionTolerance);
        for (var i = 1; i < lines.Count; i++)
        {
            await Assert.That(lines[i].Left).IsEqualTo(ListLeft + Hanging).Within(PositionTolerance);
        }
    }

    /// <summary>A right aligned paragraph keeps its right edge when the text is replaced with a longer one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RightAlignedParagraphKeepsItsRightEdge()
    {
        var pdf = PageObjectSamples.Page(LinesAt(["The quick brown fox", "jumps over the", "lazy dog and runs away"], static width => RightEdge - width, 0F));
        var lines = ReadLines(ReplaceParagraph(pdf, LongParagraph));

        await Assert.That(lines.Count).IsGreaterThan(ThreeLines);
        foreach (var line in lines)
        {
            await Assert.That(line.Right).IsEqualTo(RightEdge).Within(EdgeTolerance);
        }
    }

    /// <summary>A centred paragraph stays centred on the same middle when the text is replaced with a longer one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CentredParagraphKeepsItsMiddle()
    {
        var pdf = PageObjectSamples.Page(LinesAt(["The quick brown fox jumps", "over the lazy", "dog and runs away"], static width => MiddleOfLines - (width * Half), 0F));
        var lines = ReadLines(ReplaceParagraph(pdf, LongParagraph));

        await Assert.That(lines.Count).IsGreaterThan(ThreeLines);
        foreach (var line in lines)
        {
            await Assert.That((line.Left + line.Right) * Half).IsEqualTo(MiddleOfLines).Within(EdgeTolerance);
        }
    }

    /// <summary>A justified paragraph keeps both of its edges on every line but the last when the text is replaced with a longer one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JustifiedParagraphKeepsBothEdges()
    {
        string[] old = ["The quick brown fox jumps", "over the lazy dog and runs", "away from the farm and keeps", "fields"];
        var widest = 0F;
        for (var i = 0; i < old.Length - 1; i++)
        {
            widest = Math.Max(widest, WidthOf(old[i]));
        }

        var pdf = PageObjectSamples.Page(LinesAt(old, static _ => StartOfLines, widest));
        var lines = ReadLines(ReplaceParagraph(pdf, $"{LongParagraph} {LongParagraph}"));

        await Assert.That(lines.Count).IsGreaterThan(old.Length);
        for (var i = 0; i < lines.Count - 1; i++)
        {
            await Assert.That(lines[i].Left).IsEqualTo(StartOfLines).Within(EdgeTolerance);
            await Assert.That(lines[i].Right).IsEqualTo(StartOfLines + widest).Within(EdgeTolerance);
        }

        await Assert.That(lines[^1].Right).IsLessThanOrEqualTo(StartOfLines + widest + EdgeTolerance);
    }

    /// <summary>A paragraph turned a quarter turn keeps its direction, its place and its width when the text is replaced with a longer one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TurnedParagraphKeepsItsDirection()
    {
        string[] old = ["The quick brown fox", "jumps over the lazy", "dog and runs away"];
        var content = new System.Text.StringBuilder("BT /F1 12 Tf ");
        for (var i = 0; i < old.Length; i++)
        {
            _ = content.Append(System.Globalization.CultureInfo.InvariantCulture, $"0 1 -1 0 {TurnedAcross + (i * ParagraphLeading)} {StartOfLines} Tm ({old[i]}) Tj ");
        }

        var pdf = PageObjectSamples.Page(content.Append("ET").ToString());
        var before = ReadRuns(pdf);
        var after = ReadRuns(ReplaceParagraph(pdf, LongParagraph));
        var topBefore = 0F;
        foreach (var run in before)
        {
            topBefore = Math.Max(topBefore, run.Top);
        }

        await Assert.That(after.Count).IsGreaterThan(ThreeLines);
        var words = new List<string>();
        foreach (var run in after)
        {
            await Assert.That(run.DirectionX).IsEqualTo(0F).Within(PositionTolerance);
            await Assert.That(run.DirectionY).IsEqualTo(1F).Within(PositionTolerance);
            await Assert.That(run.Top).IsLessThanOrEqualTo(topBefore + EdgeTolerance);
            words.Add(run.Text);
        }

        await Assert.That(string.Join(' ', words)).IsEqualTo(LongParagraph);
        await Assert.That(after[^1].Left).IsGreaterThan(after[0].Left);
    }

    /// <summary>Replacing one turned line writes the new text in the same direction.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReplacingTurnedTextKeepsItsDirection()
    {
        var pdf = PageObjectSamples.Page($"BT /F1 12 Tf 0 1 -1 0 {TurnedAcross} {StartOfLines} Tm (Turned words) Tj ET");
        var edited = PageObjectSamples.Edit(pdf, static content => PdfPageContentTextReplacement.Replace(content, (PdfTextObject)content.Objects[0], "New words"));
        var runs = ReadRuns(edited);

        await Assert.That(runs.Count).IsEqualTo(1);
        await Assert.That(runs[0].Text).IsEqualTo("New words");
        await Assert.That(runs[0].DirectionY).IsEqualTo(1F).Within(PositionTolerance);
    }

    /// <summary>A blend mode is written as a graphics state, read back, and a mode that is not defined is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BlendModeIsWrittenAndReadBack()
    {
        var pdf = PageObjectSamples.Page(TwoRectangles);
        var edited = PageObjectSamples.Edit(pdf, static content => content.Objects[0].SetBlendMode(PdfBlendMode.Multiply));
        using var document = PdfDocumentReader.Open(edited, null);
        var content = PdfDocumentPageContent.GetPageContent(document, 0);
        var spelling = document.Objects.Names.GetSpelling(content.Objects[0].OriginalBlendMode);

        await Assert.That(System.Text.Encoding.ASCII.GetString(spelling)).IsEqualTo("Multiply");
        await Assert.That(content.Objects[1].OriginalBlendMode.IsNone).IsTrue();
        await Assert.That(() => PageObjectSamples.Edit(pdf, static content => content.Objects[0].SetBlendMode((PdfBlendMode)UndefinedBlendMode)))
            .Throws<ArgumentOutOfRangeException>();
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

    /// <summary>Moving a tagged object to the front keeps its text tied to its tag and writes its marked-content number once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovingATaggedObjectWritesItsMarkedContentNumberOnce()
    {
        var edited = PageObjectSamples.Edit(TestPdf.CreateTagged(), static content => PdfPageContentObjectCopy.MoveInline(content, [content.Objects[HeadingObject]], false));
        using var after = PdfDocumentReader.Open(edited, null);
        var marked = PdfDocumentTagged.GetMarkedContent(after, 0);
        var source = System.Text.Encoding.ASCII.GetString(PdfDocumentPageContent.GetPageContent(after, 0).Source);

        using (Assert.Multiple())
        {
            await Assert.That(source.Split("/H1 << /MCID 0 >> BDC").Length - 1).IsEqualTo(1);
            await Assert.That(source).Contains("/Artifact BMC");
            await Assert.That(marked.GetGlyphs(HeadingId).Length).IsGreaterThan(0);
            await Assert.That(marked.GetGlyphs(FirstId).Length).IsGreaterThan(0);
        }
    }

    /// <summary>Moving one object of a marked-content sequence to the front moves the whole sequence, so its structure link stays whole and is written once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovingPartOfASequenceMovesTheWholeSequence()
    {
        var pdf = PageObjectSamples.Page(
            "/P << /MCID 0 >> BDC BT /F1 12 Tf 10 100 Td (Aaa) Tj ET BT /F1 12 Tf 10 80 Td (Bbb) Tj ET EMC BT /F1 12 Tf 10 60 Td (Ccc) Tj ET");
        var edited = PageObjectSamples.Edit(pdf, static content => PdfPageContentObjectCopy.MoveInline(content, [content.Objects[0]], false));
        var after = PageObjectSamples.Read(edited);
        var order = string.Join(",", after.Objects.OfType<PdfTextObject>().Select(static o => o.Text));
        var source = System.Text.Encoding.ASCII.GetString(after.Source);

        using (Assert.Multiple())
        {
            await Assert.That(order).IsEqualTo("Ccc,Aaa,Bbb");
            await Assert.That(source.Split("/P << /MCID 0 >> BDC").Length - 1).IsEqualTo(1);
        }
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

    /// <summary>Reads the bounds of one object of the first page.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="index">The object's place in painting order.</param>
    /// <returns>The bounds.</returns>
    private static PdfRectangle ReadBox(byte[] pdf, int index)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        return PdfDocumentPageContent.GetPageContent(document, 0).Objects[index].Bounds;
    }

    /// <summary>Reads the left edge of every object of the first page, in painting order.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <returns>The left edges.</returns>
    private static float[] ReadBounds(byte[] pdf)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        return [.. PdfDocumentPageContent.GetPageContent(document, 0).Objects.Select(static item => item.Bounds.Left)];
    }

    /// <summary>Replaces the paragraph made of every text object of the first page.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="text">The new text.</param>
    /// <returns>The edited PDF bytes.</returns>
    private static byte[] ReplaceParagraph(byte[] pdf, string text) => PageObjectSamples.Edit(pdf, content =>
    {
        var lines = new List<PdfTextObject>();
        foreach (var item in content.Objects)
        {
            if (item is PdfTextObject line)
            {
                lines.Add(line);
            }
        }

        PdfPageContentTextReplacement.ReplaceParagraph(content, lines, text, AppearanceFont.Helvetica, 0F, null);
    });

    /// <summary>Measures a line of text in the font and size of the lined up paragraphs.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The width in points.</returns>
    private static float WidthOf(string text) => AppearanceFontMetrics.MeasureAdvance(AppearanceFont.Helvetica, System.Text.Encoding.Latin1.GetBytes(text), ParagraphSize);

    /// <summary>Builds the content of a paragraph with one line of text per row, each starting where a function says.</summary>
    /// <param name="lines">The words of each line.</param>
    /// <param name="startOf">Gives where a line starts from how wide it is.</param>
    /// <param name="stretchTo">The width every line but the last is stretched to with word spacing, or zero to leave them as they are.</param>
    /// <returns>The page content.</returns>
    private static string LinesAt(string[] lines, Func<float, float> startOf, float stretchTo)
    {
        var content = new System.Text.StringBuilder("BT /F1 12 Tf ");
        for (var i = 0; i < lines.Length; i++)
        {
            var width = WidthOf(lines[i]);
            var spaces = lines[i].Count(static letter => letter == ' ');
            var spacing = stretchTo > 0F && i < lines.Length - 1 && spaces > 0 ? (stretchTo - width) / spaces : 0F;
            _ = content.Append(System.Globalization.CultureInfo.InvariantCulture, $"{spacing} Tw 1 0 0 1 {startOf(width)} {ParagraphTop - (i * ParagraphLeading)} Tm ({lines[i]}) Tj ");
        }

        return content.Append("ET").ToString();
    }

    /// <summary>Reads the text objects of the first page that have words, with the way each runs.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <returns>The words, direction and box of each, in the order they are written.</returns>
    private static List<TurnedRun> ReadRuns(byte[] pdf)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        var runs = new List<TurnedRun>();
        foreach (var item in PdfDocumentPageContent.GetPageContent(document, 0).Objects)
        {
            if (item is not PdfTextObject { Text.Length: > 0 } line)
            {
                continue;
            }

            var matrix = line.TextMatrix * line.Matrix;
            runs.Add(new(line.Text, matrix.M11, matrix.M12, line.Bounds.Left, line.Bounds.Top));
        }

        return runs;
    }

    /// <summary>Reads the lines of text of the first page, from the top down.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <returns>The words and right edge of each line that has words.</returns>
    private static List<ParagraphLine> ReadLines(byte[] pdf)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        var lines = new List<ParagraphLine>();
        foreach (var item in PdfDocumentPageContent.GetPageContent(document, 0).Objects)
        {
            if (item is PdfTextObject { Text.Length: > 0 } line)
            {
                lines.Add(new(line.Text, line.Bounds.Top, line.Bounds.Left, line.Bounds.Right));
            }
        }

        lines.Sort(static (a, b) => b.Top.CompareTo(a.Top));
        return lines;
    }

    /// <summary>One line of text of a page.</summary>
    /// <param name="Text">The line's words.</param>
    /// <param name="Top">The top of the line's box.</param>
    /// <param name="Left">The left edge of the line's box.</param>
    /// <param name="Right">The right edge of the line's box.</param>
    private sealed record ParagraphLine(string Text, float Top, float Left, float Right);

    /// <summary>One run of text of a page, with the way it runs.</summary>
    /// <param name="Text">The run's words.</param>
    /// <param name="DirectionX">The sideways part of the direction the run goes in.</param>
    /// <param name="DirectionY">The upward part of the direction the run goes in.</param>
    /// <param name="Left">The left edge of the run's box.</param>
    /// <param name="Top">The top edge of the run's box.</param>
    private sealed record TurnedRun(string Text, float DirectionX, float DirectionY, float Left, float Top);
}
