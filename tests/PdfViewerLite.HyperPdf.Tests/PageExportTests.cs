// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Printing;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Tests for exporting chosen pages natively through <see cref="IPageExporter"/>, against the PDFium behaviour.</summary>
public sealed class PageExportTests
{
    /// <summary>The printed sides of a booklet of three pages: one sheet, both sides.</summary>
    private const int BookletSheetSides = 2;

    /// <summary>The sheets across a poster page.</summary>
    private const int PosterTilesAcross = 2;

    /// <summary>The pages in the source document.</summary>
    private const int Pages = 4;

    /// <summary>The characters in the first page's heading, "Page 1".</summary>
    private const int HeadingLength = 6;

    /// <summary>Half the true size, as a percentage.</summary>
    private const int HalfScale = 50;

    /// <summary>The share by which printed sizes may differ from the expected scale.</summary>
    private const double ScaleTolerance = 0.05;

    /// <summary>The index of the test document's wide page.</summary>
    private const int WidePage = 1;

    /// <summary>The third page, exported first.</summary>
    private const int ThirdPage = 2;

    /// <summary>The number of pages exported.</summary>
    private const int ExportedPages = 2;

    /// <summary>The filled value that must survive printing.</summary>
    private const string PrintedField = "Printed field value";

    /// <summary>The visible signature used to check print copies.</summary>
    private const string PrintedSignature = "Sample Signer";

    /// <summary>The signature text size in PDF points.</summary>
    private const float SignatureSize = 24;

    /// <summary>The pages arranged side by side in a print grid.</summary>
    private const int GridPages = 2;

    /// <summary>The width of drawn ink in points.</summary>
    private const float InkWidth = 2;

    /// <summary>The bytes in one rendered pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The value of an unpainted colour channel.</summary>
    private const byte White = 0xFF;

    /// <summary>Where the note is added.</summary>
    private static readonly PagePoint NoteAt = new(100, 100);

    /// <summary>A drawn signature on empty paper below the page's text.</summary>
    private static readonly PagePoint[] DrawnSignature = [new(100, 500), new(160, 440), new(220, 500), new(280, 440)];

    /// <summary>Print layouts keep filled fields in the saved page content.</summary>
    /// <param name="imposition">The print layout.</param>
    /// <param name="pagesPerSheet">The pages on each sheet.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PrintImposition.Pages, 2)]
    [Arguments(PrintImposition.Booklet, 1)]
    [Arguments(PrintImposition.Poster, 1)]
    public async Task PrintLayoutsKeepFilledFields(PrintImposition imposition, int pagesPerSheet)
    {
        var source = FilledForm();
        try
        {
            var layout = new SheetLayout(pagesPerSheet, PaperSize.A4, false) { Imposition = imposition, PosterTiles = 1 };
            using var run = ExportRun.Create(source, [0], layout);

            await Assert.That(run.Written).IsTrue();
            await Assert.That(ExportRun.GetAllText(run.Output!)).Contains(PrintedField);
        }
        finally
        {
            File.Delete(source);
        }
    }

    /// <summary>Print layouts keep visible signatures only when annotations are included.</summary>
    /// <param name="imposition">The print layout.</param>
    /// <param name="includeAnnotations">Whether the signature should print.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PrintImposition.Pages, true)]
    [Arguments(PrintImposition.Pages, false)]
    [Arguments(PrintImposition.Booklet, true)]
    [Arguments(PrintImposition.Booklet, false)]
    [Arguments(PrintImposition.Poster, true)]
    [Arguments(PrintImposition.Poster, false)]
    public async Task PrintLayoutsRespectSignatureChoice(PrintImposition imposition, bool includeAnnotations)
    {
        var source = ExportRun.CreateEditedSource(TestPdf.Create(1), static document =>
            _ = DocumentFeature<IAnnotationEditor>.Cast(document)!.AddText(
                0,
                NoteAt,
                PrintedSignature,
                SignatureSize,
                AnnotationColors.Ink,
                AnnotationKind.Signature));
        try
        {
            var layout = new SheetLayout(GridPages, PaperSize.A4, includeAnnotations) { Imposition = imposition, PosterTiles = 1 };
            using var run = ExportRun.Create(source, [0], layout);

            await Assert.That(run.Written).IsTrue();
            await Assert.That(ExportRun.GetAllText(run.Output!).Contains(PrintedSignature, StringComparison.Ordinal)).IsEqualTo(includeAnnotations);
        }
        finally
        {
            File.Delete(source);
        }
    }

    /// <summary>
    /// A drawn signature, even after its colour is changed, is still on the sheet when the print copy is flattened onto
    /// a grid, because the ink carries its own appearance.
    /// </summary>
    /// <param name="recolour">Whether the ink's colour is changed before printing.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PrintLayoutsKeepDrawnSignatures(bool recolour)
    {
        var signed = ExportRun.CreateEditedSource(TestPdf.Create(1), document =>
        {
            var editor = DocumentFeature<IAnnotationEditor>.Cast(document)!;
            var index = editor.AddInk(0, DrawnSignature, [DrawnSignature.Length], AnnotationColors.Ink, InkWidth, AnnotationKind.Signature);
            _ = !recolour || editor.SetColor(0, index, AnnotationColors.Clay);
        });
        var blank = TestPdf.WriteTempFile(1);
        try
        {
            var withInk = await PrintedInkAsync(signed);
            var without = await PrintedInkAsync(blank);

            await Assert.That(withInk).IsGreaterThan(without);
        }
        finally
        {
            File.Delete(signed);
            File.Delete(blank);
        }
    }

    /// <summary>Fitting a filled form to paper keeps the field value in the printed page content.</summary>
    /// <param name="paper">The chosen paper.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PaperSize.A4)]
    [Arguments(PaperSize.Letter)]
    public async Task FitsFilledFormsToPaper(PaperSize paper)
    {
        var source = FilledForm();
        try
        {
            using var run = ExportRun.Create(source, [0], new(1, paper, true) { FitToPaper = true });
            SheetGrid.GetSheetSize(paper, false, out var width, out var height);

            await Assert.That(run.Written).IsTrue();
            await Assert.That(run.Output!.GetPageSizes()[0].Width).IsEqualTo(width);
            await Assert.That(run.Output.GetPageSizes()[0].Height).IsEqualTo(height);
            await Assert.That(run.Output.GetText(0, 0, run.Output.GetCharacterCount(0))).Contains(PrintedField);
        }
        finally
        {
            File.Delete(source);
        }
    }

    /// <summary>Fitting to paper puts upright pages on upright paper and wide pages on sideways paper.</summary>
    /// <param name="paper">The chosen paper.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PaperSize.A4)]
    [Arguments(PaperSize.Letter)]
    public async Task FitsEachPageToPaperOfItsOrientation(PaperSize paper)
    {
        var source = TestPdf.WriteTempFile(Pages);
        try
        {
            using var run = ExportRun.Create(source, [0, WidePage], new(1, paper, true) { FitToPaper = true });
            SheetGrid.GetSheetSize(paper, false, out var width, out var height);
            var sizes = run.Output!.GetPageSizes();
            PageSize upright = new(width, height);

            await Assert.That(sizes[0]).IsEqualTo(upright);
            await Assert.That(sizes[1]).IsEqualTo(upright.Rotate(PageRotation.Rotate90));
            await Assert.That(run.Output.GetText(1, 0, run.Output.GetCharacterCount(1))).Contains("Page 2");
        }
        finally
        {
            File.Delete(source);
        }
    }

    /// <summary>Verifies the chosen pages come out in order, with the annotation added before exporting.</summary>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <c>run.Output</c> is <see langword="null"/>.</exception>
    [Test]
    public async Task ExportsChosenPagesWithAnnotations()
    {
        var source = ExportRun.CreateEditedSource(TestPdf.Create(Pages), static document =>
            _ = DocumentFeature<IAnnotationEditor>.Cast(document)!.AddNote(0, NoteAt, "On the first page", AnnotationColors.Sand));
        try
        {
            using var run = ExportRun.Create(source, [ThirdPage, 0], SheetLayout.Default);
            var output = run.Output ?? throw new InvalidOperationException();
            List<PageAnnotation> annotations = [];
            DocumentFeature<IAnnotationEditor>.Cast(output!)!.GetAnnotations(1, annotations);

            await Assert.That(run.Written).IsTrue();
            await Assert.That(output.PageCount).IsEqualTo(ExportedPages);
            await Assert.That(output.GetText(0, 0, output.GetCharacterCount(0))).Contains("Page 3");
            await Assert.That(output.GetText(1, 0, output.GetCharacterCount(1))).Contains("Page 1");
            await Assert.That(annotations.Exists(static a => a.Kind == AnnotationKind.Note)).IsTrue();
        }
        finally
        {
            File.Delete(source);
        }
    }

    /// <summary>Leaving annotations out keeps form widgets but drops notes, even when pages are copied as they are.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlainExportLeavesOutMarkupButKeepsWidgets()
    {
        var source = ExportRun.CreateEditedSource(TestPdf.CreateForm(), static document =>
            _ = DocumentFeature<IAnnotationEditor>.Cast(document)!.AddNote(0, NoteAt, "Left out", AnnotationColors.Sand));
        try
        {
            using var run = ExportRun.Create(source, [0], new(1, PaperSize.A4, false));

            await Assert.That(CountSubtypes(run.FilePath, KnownName.Widget)).IsGreaterThan(0);
            await Assert.That(CountSubtypes(run.FilePath, KnownName.Text)).IsEqualTo(0);
        }
        finally
        {
            File.Delete(source);
        }
    }

    /// <summary>Verifies several pages share a sheet, sideways for two per sheet, and annotations can be left out.</summary>
    /// <param name="perSheet">Pages per sheet.</param>
    /// <param name="sheets">The sheets expected for four pages.</param>
    /// <param name="landscape">Whether the sheet should be sideways.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(2, 2, true)]
    [Arguments(4, 1, false)]
    public async Task LaysPagesOntoSheets(int perSheet, int sheets, bool landscape)
    {
        var source = ExportRun.CreateEditedSource(TestPdf.Create(Pages), static document =>
            _ = DocumentFeature<IAnnotationEditor>.Cast(document)!.AddNote(0, NoteAt, "Left out", AnnotationColors.Sand));
        try
        {
            using var run = ExportRun.Create(source, [0, 1, ThirdPage, Pages - 1], new(perSheet, PaperSize.A4, false));
            var size = run.Output!.GetPageSizes()[0];
            List<PageAnnotation> annotations = [];
            DocumentFeature<IAnnotationEditor>.Cast(run.Output)!.GetAnnotations(0, annotations);

            await Assert.That(run.Output.PageCount).IsEqualTo(sheets);
            await Assert.That(size.Width > size.Height).IsEqualTo(landscape);
            await Assert.That(annotations.Count).IsEqualTo(0);
        }
        finally
        {
            File.Delete(source);
        }
    }

    /// <summary>A booklet puts two pages side by side on sideways sheets, both sides, padded to a multiple of four.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PrintsABooklet()
    {
        using var run = ExportGenerated([0, 1, ThirdPage], SheetLayout.Default with { Imposition = PrintImposition.Booklet });
        var size = run.Output!.GetPageSizes()[0];

        await Assert.That(run.Output.PageCount).IsEqualTo(BookletSheetSides);
        await Assert.That(size.Width > size.Height).IsTrue();
    }

    /// <summary>A poster spreads each page over tiles-squared sheets, each filled with part of the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PrintsAPoster()
    {
        using var run = ExportGenerated([0], SheetLayout.Default with { Imposition = PrintImposition.Poster, PosterTiles = PosterTilesAcross });
        var size = run.Output!.GetPageSizes()[0];

        await Assert.That(run.Output.PageCount).IsEqualTo(PosterTilesAcross * PosterTilesAcross);
        await Assert.That(size.Height > size.Width).IsTrue();
        await Assert.That(HasInk(run.Output, 0)).IsTrue();
    }

    /// <summary>Verifies pages outside the document are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesMissingPages()
    {
        var source = TestPdf.WriteTempFile(1);
        try
        {
            using var document = new HyperPdfEngine().Open(source, null);
            await using var stream = new MemoryStream();

            await Assert.That(DocumentFeature<IPageExporter>.Cast(document)!.ExportPages([Pages], SheetLayout.Default, stream)).IsFalse();
            await Assert.That(DocumentFeature<IPageExporter>.Cast(document)!.ExportPages([], SheetLayout.Default, stream)).IsFalse();
            await Assert.That(stream.Length).IsEqualTo(0);
        }
        finally
        {
            File.Delete(source);
        }
    }

    /// <summary>Each scaling choice prints the page at the size it promises, on the chosen paper.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScalesPagesAsChosen()
    {
        var actual = await PrintedTextWidthAsync(PaperSize.A3, PrintScaling.ActualSize, PrintScale.TrueSize);
        var half = await PrintedTextWidthAsync(PaperSize.A3, PrintScaling.Custom, HalfScale);
        var fitLarge = await PrintedTextWidthAsync(PaperSize.A3, PrintScaling.FitToPaper, PrintScale.TrueSize);
        var shrinkLarge = await PrintedTextWidthAsync(PaperSize.A3, PrintScaling.ShrinkOversized, PrintScale.TrueSize);
        var shrinkSmall = await PrintedTextWidthAsync(PaperSize.A5, PrintScaling.ShrinkOversized, PrintScale.TrueSize);

        await Assert.That(half / actual).IsEqualTo(HalfScale / (double)PrintScale.TrueSize).Within(ScaleTolerance);
        await Assert.That(fitLarge).IsGreaterThan(actual * (1 + ScaleTolerance));
        await Assert.That(shrinkLarge).IsEqualTo(actual).Within(actual * ScaleTolerance);
        await Assert.That(shrinkSmall).IsLessThan(actual * (1 - ScaleTolerance));
    }

    /// <summary>Counts the annotations of the first page of a file that have a subtype.</summary>
    /// <param name="path">The file.</param>
    /// <param name="subtype">The subtype.</param>
    /// <returns>The number of annotations.</returns>
    private static int CountSubtypes(string path, KnownName subtype)
    {
        using var document = PdfDocumentReader.Open(path, null);
        var annotations = PdfDocumentPages.GetPage(document, 0).Dictionary.GetArray(KnownName.Annots);
        var count = 0;
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            count += annotations.GetDictionary(i)?.IsName(KnownName.Subtype, subtype) == true ? 1 : 0;
        }

        return count;
    }

    /// <summary>Creates a form whose text field holds <see cref="PrintedField"/>, saved by PDFium.</summary>
    /// <returns>The file path.</returns>
    private static string FilledForm() => ExportRun.CreateEditedSource(TestPdf.CreateForm(), static document =>
    {
        var filler = DocumentFeature<IFormFiller>.Cast(document)!;
        List<FormField> fields = [];
        filler.GetFields(0, fields);
        _ = filler.SetText(0, fields[0].Index, PrintedField);
    });

    /// <summary>Determines whether a page renders anything but white.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns><see langword="true"/> when something is drawn.</returns>
    private static bool HasInk(IDocument document, int page) => CountInk(document, page) > 0;

    /// <summary>Counts the pixels of a page that are not white paper.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>The inked pixels.</returns>
    private static int CountInk(IDocument document, int page)
    {
        var size = document.GetPageSizes()[page];
        var width = (int)size.Width;
        var height = (int)size.Height;
        var pixels = new byte[width * height * BytesPerPixel];
        _ = document.Render(new(page, 1, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, width, height, width * BytesPerPixel));
        var inked = 0;
        for (var offset = 0; offset < pixels.Length; offset += BytesPerPixel)
        {
            if (pixels.AsSpan(offset, BytesPerPixel - 1).ContainsAnyExcept(White))
            {
                inked++;
            }
        }

        return inked;
    }

    /// <summary>Exports the first page of a file two to a sheet and counts the sheet's inked pixels.</summary>
    /// <param name="source">The source file.</param>
    /// <returns>The pixels that are not white paper.</returns>
    private static Task<int> PrintedInkAsync(string source)
    {
        using var run = ExportRun.Create(source, [0], new(GridPages, PaperSize.A4, true));
        return Task.FromResult(CountInk(run.Output!, 0));
    }

    /// <summary>Exports the first page on a paper with a scaling choice and measures its heading's printed width.</summary>
    /// <param name="paper">The paper.</param>
    /// <param name="scaling">The scaling choice.</param>
    /// <param name="percent">The custom percentage.</param>
    /// <returns>The heading's width in points.</returns>
    private static async Task<double> PrintedTextWidthAsync(PaperSize paper, PrintScaling scaling, int percent)
    {
        using var run = ExportGenerated([0], new(1, paper, true) { FitToPaper = true, Scaling = scaling, ScalePercent = percent });
        SheetGrid.GetSheetSize(paper, false, out var width, out var height);
        PageSize sheet = new(width, height);
        await Assert.That(run.Output!.GetPageSizes()[0]).IsEqualTo(sheet);
        List<PageRect> bounds = [];
        run.Output.GetTextBounds(0, 0, HeadingLength, bounds);
        return bounds.Sum(static rect => (double)rect.Width);
    }

    /// <summary>Exports pages of a generated document through HyperPDF.</summary>
    /// <param name="pages">The pages.</param>
    /// <param name="layout">The layout.</param>
    /// <returns>The export, read back with PDFium.</returns>
    private static ExportRun ExportGenerated(int[] pages, in SheetLayout layout)
    {
        var source = TestPdf.WriteTempFile(Pages);
        try
        {
            return ExportRun.Create(source, pages, layout);
        }
        finally
        {
            File.Delete(source);
        }
    }
}
