// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Engine.Tests.RenderingComparison;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Renders annotations that have no appearance stream with PDFium and HyperPDF. The subtypes PDFium generates are compared
/// pixel for pixel. Three subtypes differ on purpose, because PDFium draws nothing for them: a Link with a border
/// (ISO 32000-2 §12.5.6.5), a Caret (§12.5.6.11) and a Redact annotation, whose preview outlines the marked region
/// without applying the redaction (§12.5.6.23).
/// </summary>
public sealed class AnnotationFallbackParityTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest increase in mean difference that drawing the generated annotations may cause.</summary>
    private const double AnnotationDelta = 0.5;

    /// <summary>The mean difference below which two renders count as identical.</summary>
    private const double Identical = 0.001;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The device coordinate of a sample on the left edge of the 20 to 80 rectangle.</summary>
    private const int EdgeSample = 21;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>A channel value that counts as drawn rather than white.</summary>
    private const int NotWhite = 250;

    /// <summary>The offset of the green channel in a BGRA pixel.</summary>
    private const int GreenChannel = 1;

    /// <summary>The offset of the red channel in a BGRA pixel.</summary>
    private const int RedChannel = 2;

    /// <summary>The device column of a sample on the left edge of a one point outline drawn inside x 20.</summary>
    private const int OutlineSample = 20;

    /// <summary>The fewest entries a /Border array needs to give a width.</summary>
    private const int BorderWithWidth = 3;

    /// <summary>The position of the width in a /Border array.</summary>
    private const int BorderWidthSlot = 2;

    /// <summary>A link with a 4 point red border.</summary>
    private const string LinkEntries = "/Subtype /Link /Rect [20 20 80 80] /C [1 0 0] /Border [0 0 4]";

    /// <summary>A caret filled with blue.</summary>
    private const string CaretEntries = "/Subtype /Caret /Rect [20 20 80 80] /C [0 0 1]";

    /// <summary>A redaction annotation that names its post-redaction look.</summary>
    private const string RedactEntries = "/Subtype /Redact /Rect [20 20 80 80] /C [0 0 1] /IC [0 0 0] /OverlayText (X)";

    /// <summary>The annotations PDFium draws nothing for, and a pixel inside what HyperPDF draws for each.</summary>
    private static readonly SampleCase[] FallbackCases =
    [
        new(LinkEntries, EdgeSample, Middle), new(CaretEntries, Middle, Middle), new(RedactEntries, OutlineSample, Middle),
    ];

    /// <summary>The reference PDFs whose annotations are compared.</summary>
    private static readonly string[] SampleIds =
    [
        "pypdf-annotations", "pdfjs-annotation-attachment", "pdfjs-textfields", "pypdf-latex-forms", "pypdf-libreoffice-form", "pdfjs-js-buttons",
    ];

    /// <summary>The subtypes PDFium generates an appearance for.</summary>
    private static readonly KnownName[] PdfiumGenerated =
    [
        KnownName.Square, KnownName.Circle, KnownName.Highlight, KnownName.Underline, KnownName.StrikeOut, KnownName.Squiggly,
        KnownName.Ink, KnownName.Text, KnownName.FreeText, KnownName.Line, KnownName.Polygon, KnownName.PolyLine,
    ];

    /// <summary>What a page's annotations without appearances are.</summary>
    private enum PageKind
    {
        /// <summary>The page has none that matter.</summary>
        None = 0,

        /// <summary>The page has some, and PDFium generates all of them.</summary>
        Comparable = 1,

        /// <summary>The page has some that only HyperPDF draws.</summary>
        Deliberate = 2,
    }

    /// <summary>PDFium draws nothing for a Link border, a Caret or a Redact annotation without an appearance; HyperPDF draws each.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FallbackSubtypesPdfiumSkipsAreDrawnByHyperPdf()
    {
        foreach (var (entries, column, row) in FallbackCases)
        {
            using var pair = new EnginePair(AnnotationPage(entries));
            var info = new PageRenderInfo(0, 1, PageRotation.None, 0, 0, RenderFlags.Annotations);
            var pdfium = PdfiumReferenceRendering.Render(pair.Bytes, info);
            var hyperPdf = RenderPage(pair.HyperPdf, info, pdfium.Width, pdfium.Height);

            await Assert.That(IsWhite(pdfium.Pixels.ToArray(), pdfium.Width, column, row)).IsTrue().Because($"PDFium draws nothing for {entries}");
            await Assert.That(IsWhite(hyperPdf, pdfium.Width, column, row)).IsFalse().Because($"HyperPDF draws {entries}");
        }
    }

    /// <summary>The Hidden flag leaves the fallback subtypes out in both engines, so only the drawn difference above remains.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HiddenFallbackSubtypesMatchPdfium()
    {
        using var pair = new EnginePair(AnnotationPage(
            $"{LinkEntries} /F 2",
            $"{CaretEntries} /F 2",
            $"{RedactEntries} /F 2"));
        var info = new PageRenderInfo(0, 1, PageRotation.None, 0, 0, RenderFlags.Annotations);
        var pdfium = PdfiumReferenceRendering.Render(pair.Bytes, info);
        var hyperPdf = RenderPage(pair.HyperPdf, info, pdfium.Width, pdfium.Height);

        await Assert.That(MeanDifference(pdfium.Pixels.ToArray(), hyperPdf)).IsLessThan(Identical);
    }

    /// <summary>
    /// On every page of the reference PDFs that has annotations without appearances of the subtypes PDFium generates, drawing
    /// the annotations does not move HyperPDF further from PDFium than the same page without annotations. Pages whose
    /// annotations include a Link border, a Caret or a Redact annotation without an appearance differ on purpose, and are
    /// only listed. The test fails when no page qualifies, so it never passes with zero samples.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReferencePdfGeneratedAnnotationsMatchPdfium()
    {
        var compared = 0;
        var deliberate = 0;
        var problems = new List<string>();
        foreach (var path in ReferencePdfFile.RequirePaths(SampleIds))
        {
            using var pair = new EnginePair(await File.ReadAllBytesAsync(path));
            for (var page = 0; page < pair.Pdfium.PageCount; page++)
            {
                var kind = Classify(pair.Library, page);
                if (kind == PageKind.None)
                {
                    continue;
                }

                var delta = AnnotationDeltaFor(pair, page);
                var label = $"{Path.GetFileName(path)} page {page}";
                if (kind == PageKind.Comparable)
                {
                    compared++;
                    TestContext.Current?.Output.WriteLine($"{label}: PDFium generates these annotations; extra difference {delta:F4}.");
                    if (delta > AnnotationDelta)
                    {
                        problems.Add($"{label}: extra difference {delta:F4}");
                    }
                }
                else
                {
                    deliberate++;
                    TestContext.Current?.Output.WriteLine($"{label}: recorded difference, HyperPDF draws a Link border, Caret or Redact preview that PDFium does not; extra difference {delta:F4}.");
                }
            }
        }

        TestContext.Current?.Output.WriteLine($"Compared {compared} pages; {deliberate} pages differ on purpose.");
        await Assert.That(problems).IsEmpty();
        await Assert.That(compared).IsGreaterThan(0).Because("no reference PDF page has annotations without appearances that PDFium generates");
    }

    /// <summary>Renders a page of both engines with and without annotations and measures what the annotations add to their difference.</summary>
    /// <param name="pair">The documents.</param>
    /// <param name="page">The zero-based page.</param>
    /// <returns>The mean difference with annotations less the mean difference without.</returns>
    private static double AnnotationDeltaFor(EnginePair pair, int page)
    {
        var with = new PageRenderInfo(page, 1, PageRotation.None, 0, 0, RenderFlags.Annotations);
        var without = with with { Flags = RenderFlags.None };
        return Difference(pair, with) - Difference(pair, without);
    }

    /// <summary>Measures how far HyperPDF's render of a page is from PDFium's.</summary>
    /// <param name="pair">The documents.</param>
    /// <param name="info">The render request.</param>
    /// <returns>The mean absolute difference per channel.</returns>
    private static double Difference(EnginePair pair, PageRenderInfo info)
    {
        var pdfium = PdfiumReferenceRendering.Render(pair.Bytes, info);
        return MeanDifference(pdfium.Pixels.ToArray(), RenderPage(pair.HyperPdf, info, pdfium.Width, pdfium.Height));
    }

    /// <summary>Decides whether a page has annotations without appearances, and whether PDFium would draw all of them.</summary>
    /// <param name="document">The managed document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <returns>The page's kind.</returns>
    private static PageKind Classify(PdfDocument document, int pageIndex)
    {
        var annotations = PdfDocumentPages.GetPage(document, pageIndex).Dictionary.GetArray(KnownName.Annots);
        var kind = PageKind.None;
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is not { } annotation || annotation.GetDictionary(KnownName.AP) is not null)
            {
                continue;
            }

            var subtype = annotation.GetName(KnownName.Subtype).ToKnownName();
            if (IsDeliberate(annotation, subtype))
            {
                return PageKind.Deliberate;
            }

            if (subtype == KnownName.Link || Array.IndexOf(PdfiumGenerated, subtype) >= 0)
            {
                kind = PageKind.Comparable;
            }
        }

        return kind;
    }

    /// <summary>Determines whether HyperPDF draws an annotation that PDFium leaves out.</summary>
    /// <param name="annotation">The annotation without an appearance.</param>
    /// <param name="subtype">Its subtype.</param>
    /// <returns><see langword="true"/> for a Caret, a Redact annotation, or a Link with a visible border.</returns>
    private static bool IsDeliberate(PdfDictionary annotation, KnownName subtype) =>
        subtype is KnownName.Caret or KnownName.Redact || (subtype == KnownName.Link && HasVisibleBorder(annotation));

    /// <summary>Determines whether a link names a border that HyperPDF draws: a positive width, and a colour that is not empty.</summary>
    /// <param name="annotation">The link.</param>
    /// <returns><see langword="true"/> when HyperPDF draws a border.</returns>
    private static bool HasVisibleBorder(PdfDictionary annotation)
    {
        if (annotation.GetArray(KnownName.C) is { Count: 0 })
        {
            return false;
        }

        return annotation.GetDictionary(KnownName.BS) is { } style
            ? !style.ContainsKey(KnownName.W) || style.GetSingle(KnownName.W) > 0
            : annotation.GetArray(KnownName.Border) is { Count: >= BorderWithWidth } border && border.GetSingle(BorderWidthSlot) > 0;
    }

    /// <summary>Renders a page with HyperPDF.</summary>
    /// <param name="document">The document.</param>
    /// <param name="info">The render request.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The BGRA pixels.</returns>
    /// <exception cref="InvalidOperationException">The page could not be rendered.</exception>
    private static byte[] RenderPage(IDocument document, in PageRenderInfo info, int width, int height)
    {
        var pixels = new byte[width * height * BytesPerPixel];
        return document.Render(info, new(pixels, width, height, width * BytesPerPixel)) ? pixels : throw new InvalidOperationException("The page could not be rendered.");
    }

    /// <summary>Computes the mean absolute difference per channel between two BGRA images.</summary>
    /// <param name="expected">The reference pixels.</param>
    /// <param name="actual">The pixels under test.</param>
    /// <returns>The mean difference, from 0 to 255.</returns>
    private static double MeanDifference(byte[] expected, byte[] actual)
    {
        long total = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            total += Math.Abs(expected[i] - actual[i]);
        }

        return (double)total / expected.Length;
    }

    /// <summary>Determines whether a pixel is white.</summary>
    /// <param name="pixels">The BGRA pixels.</param>
    /// <param name="width">The width.</param>
    /// <param name="column">The column.</param>
    /// <param name="row">The row.</param>
    /// <returns><see langword="true"/> when every colour channel is near full.</returns>
    private static bool IsWhite(byte[] pixels, int width, int column, int row)
    {
        var start = ((row * width) + column) * BytesPerPixel;
        return pixels[start] >= NotWhite && pixels[start + GreenChannel] >= NotWhite && pixels[start + RedChannel] >= NotWhite;
    }

    /// <summary>Builds a one-annotation page.</summary>
    /// <param name="annotations">Each annotation's entries other than /Type.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] AnnotationPage(params ReadOnlySpan<string> annotations)
    {
        var pdf = new RenderTestPdf(Size, Size);
        var references = string.Concat(annotations.ToArray().Select(entries => $"{pdf.AddObject($"<< /Type /Annot {entries} >>")} 0 R "));
        pdf.PageEntries = $"/Annots [{references}]";
        return pdf.ToBytes();
    }

    /// <summary>A hand-built annotation and where it draws.</summary>
    /// <param name="Entries">The annotation's entries.</param>
    /// <param name="Column">A device column inside what it draws.</param>
    /// <param name="Row">A device row inside what it draws.</param>
    private readonly record struct SampleCase(string Entries, int Column, int Row);
}
