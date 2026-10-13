// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Flattening;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Checks the engine's flattened copy writer: the copy has no widgets or form left, reopens in HyperPDF and in PDFium, passes
/// qpdf, looks like the original, and leaves the open file alone. Signed files are refused.
/// </summary>
public sealed class FlattenCopyTests
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>
    /// The largest mean difference per channel between a page and its flattened copy in HyperPDF; the same renderer draws
    /// both. The three reference forms and the generated form measured 0 to 0.0035.
    /// </summary>
    private const double SameEngineThreshold = 0.01;

    /// <summary>
    /// The largest mean difference per channel between HyperPDF's and PDFium's render of a page, from the anti-aliasing of text
    /// and curves; the two engines measured 1.02 on the generated form, as in the form appearance tests, and less on the reference forms.
    /// </summary>
    private const double CrossEngineThreshold = 1.5;

    /// <summary>The widgets in the generated form with drawn appearances: a rich text field, a push button and two radio buttons.</summary>
    private const int FormWidgets = 4;

    /// <summary>The seconds a copy may take.</summary>
    private const int CopySeconds = 60;

    /// <summary>The text typed into the field before the copy.</summary>
    private const string TypedName = "Glenn Watson";

    /// <summary>A form with drawn appearances is flattened to a copy that reopens in both engines, passes qpdf and looks the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormCopyReopensInBothEnginesAndLooksTheSame()
    {
        var original = await DrawnFormAsync();
        using var source = new HyperPdfTestDocument(original);
        var before = await File.ReadAllBytesAsync(source.FilePath);
        var (result, copy) = await FlattenAsync(source.Document, FlattenCopyOptions.Default);
        using var reopened = PdfDocumentReader.Open(copy, null);
        using var originalPair = new EnginePair(original);
        using var copyPair = new EnginePair(copy);
        var qpdf = await QpdfCheck.RunAsync(copy, CancellationToken.None) ?? string.Empty;

        await Assert.That(result.Flattened).IsTrue();
        await Assert.That(result.FormFieldsFlattened).IsEqualTo(FormWidgets);
        await Assert.That(result.FormRemoved).IsTrue();
        await Assert.That(await File.ReadAllBytesAsync(source.FilePath)).IsEquivalentTo(before);
        await Assert.That(reopened.Catalog.ContainsKey(KnownName.AcroForm)).IsFalse();
        await Assert.That(PdfDocumentPages.GetPage(reopened, 0).Dictionary.ContainsKey(KnownName.Annots)).IsFalse();
        await Assert.That(PdfiumReferenceAnnotations.GetAnnotations(copyPair.Pdfium, 0).Count).IsEqualTo(0);
        await Assert.That(qpdf).IsEmpty().Because(qpdf);
        await Assert.That(Difference(Hyper(originalPair), Hyper(copyPair))).IsLessThan(SameEngineThreshold);
        await Assert.That(Difference(Pdfium(originalPair), Hyper(copyPair))).IsLessThan(CrossEngineThreshold);
        await Assert.That(Difference(Pdfium(copyPair), Hyper(copyPair))).IsLessThan(CrossEngineThreshold);

        // The fields are drawn into the copy: without them the page looks different.
        await Assert.That(Difference(Hyper(originalPair, RenderFlags.None), Hyper(copyPair))).IsGreaterThan(SameEngineThreshold);
    }

    /// <summary>The reference forms flatten to copies that reopen in both engines, pass qpdf and look like the originals.</summary>
    /// <param name="id">The reference file's manifest id.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("pdfjs-textfields")]
    [Arguments("pypdf-latex-forms")]
    [Arguments("pypdf-libreoffice-form")]
    public async Task ReferenceFormsFlatten(string id)
    {
        var original = ReferencePdfFile.Read(id);
        using var source = new HyperPdfTestDocument(original);
        var (result, copy) = await FlattenAsync(source.Document, FlattenCopyOptions.Default);
        using var reopened = PdfDocumentReader.Open(copy, null);
        using var originalPair = new EnginePair(original);
        using var copyPair = new EnginePair(copy);
        var qpdf = await QpdfCheck.RunAsync(copy, CancellationToken.None) ?? string.Empty;
        var widgets = 0;
        for (var page = 0; page < reopened.PageCount; page++)
        {
            widgets += PdfiumReferenceAnnotations.GetAnnotations(copyPair.Pdfium, page).Count(static a => a.SubtypeName == "Widget");
        }

        await Assert.That(result.Flattened).IsTrue();
        await Assert.That(result.FormFieldsFlattened).IsGreaterThan(0);
        await Assert.That(result.FormRemoved).IsTrue();
        await Assert.That(reopened.Catalog.ContainsKey(KnownName.AcroForm)).IsFalse();
        await Assert.That(widgets).IsEqualTo(0);
        await Assert.That(qpdf).IsEmpty().Because(qpdf);
        await Assert.That(Difference(Hyper(originalPair), Hyper(copyPair))).IsLessThan(SameEngineThreshold);
        await Assert.That(Difference(Pdfium(copyPair), Hyper(copyPair))).IsLessThan(CrossEngineThreshold);
    }

    /// <summary>A value typed in the viewer and not yet saved is part of the flattened copy.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsavedFieldEditsAreFlattened()
    {
        using var source = new HyperPdfTestDocument(TestPdf.CreateForm());
        var filler = DocumentFeature<IFormFiller>.Cast(source.Document)!;
        var fields = new List<FormField>();
        filler.GetFields(0, fields);
        _ = filler.SetText(0, fields[0].Index, TypedName);

        var (result, copy) = await FlattenAsync(source.Document, FlattenCopyOptions.Default);
        using var reopened = PdfDocumentReader.Open(copy, null);

        await Assert.That(result.Flattened).IsTrue();
        await Assert.That(PdfDocumentPages.GetPage(reopened, 0).Dictionary.ContainsKey(KnownName.Annots)).IsFalse();
        await Assert.That(DrawnContent(reopened)).Contains(TypedName);
    }

    /// <summary>A signed document is refused with the reason, and nothing is written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignedDocumentIsRefusedAndNothingIsWritten()
    {
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        using var source = new HyperPdfTestDocument(TestSignedPdf.Create(1, certificate));
        var writer = DocumentFeature<IFlattenCopyWriter>.Cast(source.Document)!;
        await using var output = new MemoryStream();

        var result = await writer.WriteFlattenedCopyAsync(output, FlattenCopyOptions.Default, CancellationToken.None);

        await Assert.That(result.Flattened).IsFalse();
        await Assert.That(result.Problems[0].Description).Contains("signed");
        await Assert.That(output.Length).IsEqualTo(0);
    }

    /// <summary>A cancelled token stops the copy before anything is written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellationStopsTheCopy()
    {
        using var source = new HyperPdfTestDocument(TestPdf.CreateForm());
        var writer = DocumentFeature<IFlattenCopyWriter>.Cast(source.Document)!;
        await using var output = new MemoryStream();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.That(async () => await writer.WriteFlattenedCopyAsync(output, FlattenCopyOptions.Default, cancelled.Token)).Throws<OperationCanceledException>();
        await Assert.That(output.Length).IsEqualTo(0);
    }

    /// <summary>Each option reaches the library: the print look drops a field not marked to print, and a form-only copy keeps the page's markup.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptionsReachTheLibrary()
    {
        using var source = new HyperPdfTestDocument(TestPdf.CreateForm());
        var (printed, printCopy) = await FlattenAsync(source.Document, FlattenCopyOptions.Default with { Look = FlattenCopyLook.Print });
        var (markup, markupCopy) = await FlattenAsync(source.Document, FlattenCopyOptions.Default with { Scope = FlattenCopyScope.CommentsAndMarkup });
        using var printReopened = PdfDocumentReader.Open(printCopy, null);
        using var markupReopened = PdfDocumentReader.Open(markupCopy, null);

        await Assert.That(printed.Flattened).IsTrue();
        await Assert.That(printed.FormFieldsFlattened).IsGreaterThan(0);
        await Assert.That(printReopened.Catalog.ContainsKey(KnownName.AcroForm)).IsFalse();
        await Assert.That(markup.FormFieldsFlattened).IsEqualTo(0);
        await Assert.That(markupReopened.Catalog.ContainsKey(KnownName.AcroForm)).IsTrue();
    }

    /// <summary>Builds the form with drawn appearances, as the form appearance tests do, and saves it.</summary>
    /// <returns>The PDF bytes.</returns>
    private static async Task<byte[]> DrawnFormAsync()
    {
        using var document = PdfDocumentReader.Open(FormAppearanceSamples.CreateWidgetsForm(), null);
        var form = PdfDocumentForms.GetForm(document);
        _ = PdfFormEditing.RegenerateAppearance(form, 0, FormAppearanceSamples.RichTextIndex);
        _ = PdfFormEditing.RegenerateAppearance(form, 0, FormAppearanceSamples.PushButtonIndex);
        _ = PdfFormEditing.SetChecked(form, 0, FormAppearanceSamples.BeveledRadioIndex, true);
        _ = PdfFormEditing.SetChecked(form, 0, FormAppearanceSamples.InsetRadioIndex, true);
        await using var output = new MemoryStream();
        await PdfDocumentSaving.SaveAsync(document, output, CancellationToken.None);
        return output.ToArray();
    }

    /// <summary>Writes a flattened copy of a document.</summary>
    /// <param name="document">The open document.</param>
    /// <param name="options">What to flatten.</param>
    /// <returns>The result and the copy's bytes.</returns>
    private static async Task<Outcome> FlattenAsync(IDocument document, FlattenCopyOptions options)
    {
        var writer = DocumentFeature<IFlattenCopyWriter>.Cast(document)!;
        await using var output = new MemoryStream();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(CopySeconds));
        var result = await writer.WriteFlattenedCopyAsync(output, options, timeout.Token);
        return new(result, output.ToArray());
    }

    /// <summary>Gets the content of the forms a page draws, which holds the flattened fields.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The decoded content of every form XObject of the first page, one character per byte.</returns>
    private static string DrawnContent(PdfDocument document)
    {
        var forms = PdfDocumentPages.GetPage(document, 0).Resources?.GetDictionary(KnownName.XObject);
        var text = new StringBuilder();
        for (var i = 0; forms is not null && i < forms.Count; i++)
        {
            if (forms.Get(forms.GetKeyAt(i)).AsStream() is { } form)
            {
                _ = text.Append(Encoding.Latin1.GetString(form.DecodeToArray()));
            }
        }

        return text.ToString();
    }

    /// <summary>Renders the first page with HyperPDF, with its annotations.</summary>
    /// <param name="pair">The document.</param>
    /// <returns>The BGRA pixels with their size.</returns>
    /// <exception cref="InvalidOperationException">The page could not be rendered.</exception>
    private static Raster Hyper(EnginePair pair) => Hyper(pair, RenderFlags.Annotations);

    /// <summary>Renders the first page with HyperPDF.</summary>
    /// <param name="pair">The document.</param>
    /// <param name="flags">The render options.</param>
    /// <returns>The BGRA pixels with their size.</returns>
    /// <exception cref="InvalidOperationException">The page could not be rendered.</exception>
    private static Raster Hyper(EnginePair pair, RenderFlags flags)
    {
        var (width, height) = Size(pair.HyperPdf.GetPageSizes()[0]);
        DocumentFeature<IFormHighlight>.Cast(pair.HyperPdf)!.Highlight = new(0, 0);
        var pixels = new byte[width * height * BytesPerPixel];
        return pair.HyperPdf.Render(new(0, 1, PageRotation.None, 0, 0, flags), new(pixels, width, height, width * BytesPerPixel))
            ? new(pixels, width, height)
            : throw new InvalidOperationException("The page could not be rendered.");
    }

    /// <summary>Renders the first page with PDFium and its form fill environment.</summary>
    /// <param name="pair">The document.</param>
    /// <returns>The BGRA pixels with their size.</returns>
    private static Raster Pdfium(EnginePair pair)
    {
        var (width, height) = Size(pair.HyperPdf.GetPageSizes()[0]);
        return new(PdfiumReferenceFormRendering.Render(pair.Pdfium, 0, width, height).Pixels.ToArray(), width, height);
    }

    /// <summary>Rounds a page size to whole pixels.</summary>
    /// <param name="size">The page size in points.</param>
    /// <returns>The width and height in pixels.</returns>
    private static PixelSize Size(PageSize size) => new((int)Math.Round(size.Width), (int)Math.Round(size.Height));

    /// <summary>Measures how far apart two renders are.</summary>
    /// <param name="first">The first render.</param>
    /// <param name="second">The second render.</param>
    /// <returns>The mean absolute difference per channel.</returns>
    private static double Difference(Raster first, Raster second)
    {
        long total = 0;
        for (var i = 0; i < first.Pixels.Length; i++)
        {
            total += Math.Abs(first.Pixels[i] - second.Pixels[i]);
        }

        return (double)total / first.Pixels.Length;
    }

    /// <summary>A size in whole pixels.</summary>
    /// <param name="Width">The width.</param>
    /// <param name="Height">The height.</param>
    private readonly record struct PixelSize(int Width, int Height);

    /// <summary>A rendered page.</summary>
    /// <param name="Pixels">The BGRA pixels.</param>
    /// <param name="Width">The width in pixels.</param>
    /// <param name="Height">The height in pixels.</param>
    private sealed record Raster(byte[] Pixels, int Width, int Height);

    /// <summary>A flattened copy and what was done to make it.</summary>
    /// <param name="Result">The result.</param>
    /// <param name="Copy">The copy's bytes.</param>
    private sealed record Outcome(FlattenCopyResult Result, byte[] Copy);
}
