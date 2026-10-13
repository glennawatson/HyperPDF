// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Draws form appearances with the managed library, saves them in the document and renders the saved file with HyperPDF and with
/// PDFium, which has to open the appearance streams and draw them the same way. PDFium draws widgets only through its form fill
/// environment, so its reference render is made with that.
/// </summary>
public sealed class FormAppearanceParityTests
{
    /// <summary>The largest mean difference per channel accepted for the whole page; the two engines measured 1.02, from the anti-aliasing of the text and curves.</summary>
    internal const double PageThreshold = 1.5;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The page size in points.</summary>
    private const int PageSize = 300;

    /// <summary>How far a channel may be from the icon's flat colour, in levels of 255.</summary>
    private const int ColorTolerance = 6;

    /// <summary>The x, in points, of the middle of the push button.</summary>
    private const int ButtonCenterX = 150;

    /// <summary>The y, in points from the top of the page, of a point in the icon above the caption: the button runs 130 to 230 up the page.</summary>
    private const int IconY = 105;

    /// <summary>The red channel's position in a BGRA pixel.</summary>
    private const int RedSlot = 2;

    /// <summary>The green channel's position in a BGRA pixel.</summary>
    private const int GreenSlot = 1;

    /// <summary>The environment variable that names a folder to keep the saved file in, for checking it with other tools.</summary>
    private const string OutputVariable = "HYPERPDF_FORM_OUTPUT";

    /// <summary>The saved page, with a rich text field, a push button with an icon and caption, and beveled and inset radio buttons, renders alike in both engines.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavedAppearancesRenderLikePdfium()
    {
        var bytes = await Generate();
        using var pair = new EnginePair(bytes);

        await Assert.That(Difference(pair, nameof(SavedAppearancesRenderLikePdfium))).IsLessThan(PageThreshold);
    }

    /// <summary>The push button's icon is drawn above its caption, in the icon's own colour, by both engines.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PushButtonIconIsDrawnAboveItsCaption()
    {
        var bytes = await Generate();
        using var pair = new EnginePair(bytes);
        var hyper = Pixels(pair.HyperPdf);
        var pdfium = PdfiumReferenceFormRendering.Render(pair.Pdfium, 0, PageSize, PageSize).Pixels.ToArray();

        await Assert.That(IsRed(hyper, ButtonCenterX, IconY)).IsTrue();
        await Assert.That(IsRed(pdfium, ButtonCenterX, IconY)).IsTrue();
        await Assert.That(PdfiumReferenceAnnotations.GetAnnotations(pair.Pdfium, 0)[FormAppearanceSamples.PushButtonIndex].SubtypeName).IsEqualTo("Widget");
    }

    /// <summary>A field in a Shift-JIS Type0 font is saved with its two-byte codes, and PDFium opens the saved file and reads the value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShiftJisFieldReopensInPdfium()
    {
        using var document = PdfDocumentReader.Open(FormAppearanceSamples.CreateShiftJisForm(), null);
        _ = PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, 0, "日本");
        await using var output = new MemoryStream();
        await PdfDocumentSaving.SaveAsync(document, output, CancellationToken.None);
        using var pair = new EnginePair(output.ToArray());

        var annotations = PdfiumReferenceAnnotations.GetAnnotations(pair.Pdfium, 0);

        await Assert.That(annotations.Count).IsEqualTo(1);
        await Assert.That(annotations[0].FieldValue).IsEqualTo("日本");
        await Assert.That(PdfiumReferenceFormRendering.Render(pair.Pdfium, 0, PageSize, PageSize).Pixels.Length).IsEqualTo(PageSize * PageSize * BytesPerPixel);
    }

    /// <summary>Renders the page in both engines and measures how far apart they are.</summary>
    /// <param name="pair">The document in both engines.</param>
    /// <param name="name">The test name, for the saved images.</param>
    /// <returns>The mean absolute difference per channel.</returns>
    internal static double Difference(EnginePair pair, string name)
    {
        var reference = PdfiumReferenceFormRendering.Render(pair.Pdfium, 0, PageSize, PageSize);
        var expected = reference.Pixels.ToArray();
        var actual = Pixels(pair.HyperPdf);
        long total = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            total += Math.Abs(expected[i] - actual[i]);
        }

        var difference = (double)total / expected.Length;
        if (difference >= PageThreshold)
        {
            new ParityImages(expected, actual, reference.Width, reference.Height).Save(name);
        }

        return difference;
    }

    /// <summary>Builds the form, has the library draw every appearance and saves the result.</summary>
    /// <returns>The saved PDF bytes.</returns>
    private static async Task<byte[]> Generate()
    {
        using var document = PdfDocumentReader.Open(FormAppearanceSamples.CreateWidgetsForm(), null);
        var form = PdfDocumentForms.GetForm(document);
        _ = PdfFormEditing.RegenerateAppearance(form, 0, FormAppearanceSamples.RichTextIndex);
        _ = PdfFormEditing.RegenerateAppearance(form, 0, FormAppearanceSamples.PushButtonIndex);
        _ = PdfFormEditing.SetChecked(form, 0, FormAppearanceSamples.BeveledRadioIndex, true);
        _ = PdfFormEditing.SetChecked(form, 0, FormAppearanceSamples.InsetRadioIndex, true);
        await using var output = new MemoryStream();
        await PdfDocumentSaving.SaveAsync(document, output, CancellationToken.None);
        var bytes = output.ToArray();
        if (Environment.GetEnvironmentVariable(OutputVariable) is { Length: > 0 } folder)
        {
            _ = Directory.CreateDirectory(folder);
            await File.WriteAllBytesAsync(Path.Combine(folder, "form-appearances.pdf"), bytes);
        }

        return bytes;
    }

    /// <summary>Gets the request for the page with annotations.</summary>
    /// <returns>The request.</returns>
    private static PageRenderInfo Info() => new(0, 1, PageRotation.None, 0, 0, RenderFlags.Annotations);

    /// <summary>Renders the page in HyperPDF.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The BGRA pixels.</returns>
    /// <exception cref="InvalidOperationException">The page could not be rendered.</exception>
    private static byte[] Pixels(IDocument document)
    {
        // PDFium draws no tint over fields, so the viewer's form highlight is turned off.
        DocumentFeature<IFormHighlight>.Cast(document)!.Highlight = new(0, 0);
        var pixels = new byte[PageSize * PageSize * BytesPerPixel];
        return document.Render(Info(), new(pixels, PageSize, PageSize, PageSize * BytesPerPixel))
            ? pixels
            : throw new InvalidOperationException("The page could not be rendered.");
    }

    /// <summary>Determines whether a pixel is the icon's red.</summary>
    /// <param name="pixels">The BGRA pixels.</param>
    /// <param name="x">The x in pixels.</param>
    /// <param name="y">The y in pixels, from the top.</param>
    /// <returns><see langword="true"/> when it is red.</returns>
    private static bool IsRed(byte[] pixels, int x, int y)
    {
        var offset = ((y * PageSize) + x) * BytesPerPixel;
        return pixels[offset + RedSlot] > byte.MaxValue - ColorTolerance && pixels[offset + GreenSlot] < ColorTolerance && pixels[offset] < ColorTolerance;
    }
}
