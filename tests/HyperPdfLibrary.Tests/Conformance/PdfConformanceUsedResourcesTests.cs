// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>
/// The conformance reading report counts only resources that content uses for rendering (ISO 19005-1 Cor.2 §6.2.10):
/// a named resource nothing draws gives no finding.
/// </summary>
public sealed class PdfConformanceUsedResourcesTests
{
    /// <summary>The PDF/A part the pages claim.</summary>
    private const int PartOne = 1;

    /// <summary>A Helvetica resource with no embedded program.</summary>
    private const string Helvetica = "/F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>";

    /// <summary>Content that shows text in <c>/F1</c>.</summary>
    private const string ShowF1 = "BT /F1 12 Tf (a) Tj ET";

    /// <summary>Content that fills a square without naming any resource.</summary>
    private const string FillOnly = "0 0 10 10 re f";

    /// <summary>An unused unembedded font gives no finding; selecting it with <c>Tf</c> gives the finding.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontCountsOnlyWhenSelected()
    {
        var unused = Claimed();
        unused.Resources = $"/Font << {Helvetica} >>";
        unused.Content = FillOnly;
        var used = Claimed();
        used.Resources = unused.Resources;
        used.Content = ShowF1;

        await Assert.That(Report(unused).TryGet(PdfConformanceFinding.NonEmbeddedFont, out _)).IsFalse();
        await Assert.That(Report(used).TryGet(PdfConformanceFinding.NonEmbeddedFont, out var seen)).IsTrue();
        await Assert.That(seen.Detail).IsEqualTo("Helvetica");
    }

    /// <summary>A form's resources count only when page content draws the form with <c>Do</c>.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormResourcesCountOnlyWhenDrawn()
    {
        static RenderTestPdf Page(string content)
        {
            var pdf = Claimed();
            var form = pdf.AddStream($"/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Resources << /Font << {Helvetica} >> >>", ShowF1);
            pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/XObject << /Fm {form} 0 R >>");
            pdf.Content = content;
            return pdf;
        }

        await Assert.That(Report(Page(FillOnly)).TryGet(PdfConformanceFinding.NonEmbeddedFont, out _)).IsFalse();
        await Assert.That(Report(Page("/Fm Do")).TryGet(PdfConformanceFinding.NonEmbeddedFont, out _)).IsTrue();
    }

    /// <summary>A tiling pattern's resources count only when content paints with the pattern.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PatternResourcesCountOnlyWhenPainted()
    {
        static RenderTestPdf Page(string content)
        {
            var pdf = Claimed();
            var pattern = pdf.AddStream(
                $"/Type /Pattern /PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 10 10] /XStep 10 /YStep 10 /Resources << /Font << {Helvetica} >> >>",
                ShowF1);
            pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/Pattern << /P1 {pattern} 0 R >>");
            pdf.Content = content;
            return pdf;
        }

        await Assert.That(Report(Page(FillOnly)).TryGet(PdfConformanceFinding.NonEmbeddedFont, out _)).IsFalse();
        await Assert.That(Report(Page("/Pattern cs /P1 scn 0 0 10 10 re f")).TryGet(PdfConformanceFinding.NonEmbeddedFont, out _)).IsTrue();
    }

    /// <summary>A graphics state with alpha below 1 counts as transparency only when content sets it with <c>gs</c>.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GraphicsStateCountsOnlyWhenSet()
    {
        static RenderTestPdf Page(string content)
        {
            var pdf = Claimed();
            var state = pdf.AddObject("<< /Type /ExtGState /ca 0.5 >>");
            pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/ExtGState << /GS {state} 0 R >>");
            pdf.Content = content;
            return pdf;
        }

        await Assert.That(Report(Page(FillOnly)).TryGet(PdfConformanceFinding.Transparency, out _)).IsFalse();
        await Assert.That(Report(Page($"/GS gs {FillOnly}")).TryGet(PdfConformanceFinding.Transparency, out _)).IsTrue();
    }

    /// <summary>A form that draws itself, and a shared form drawn twice, are walked once and still found.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CyclesAndSharedFormsStayBounded()
    {
        var pdf = Claimed();
        var number = pdf.NextNumber;
        _ = pdf.AddStream(
            string.Create(CultureInfo.InvariantCulture, $"/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Resources << /Font << {Helvetica} >> /XObject << /Fm {number} 0 R >> >>"),
            $"{ShowF1} /Fm Do");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/XObject << /Fm {number} 0 R /Again {number} 0 R >>");
        pdf.Content = "/Fm Do /Again Do";

        var report = Report(pdf);

        await Assert.That(report.TryGet(PdfConformanceFinding.NonEmbeddedFont, out var seen)).IsTrue();
        await Assert.That(seen.Count).IsEqualTo(1);
    }

    /// <summary>Damaged content, an undecodable filter and a form name nothing defines leave a report without throwing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MalformedContentStaysBounded()
    {
        var pdf = Claimed();
        var broken = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Filter /FlateDecode", "not deflate data");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/Font << {Helvetica} >> /XObject << /Bad {broken} 0 R >>");
        pdf.Content = "BT /F1 12 Tf (unterminated /Missing Do /Bad Do ] ] >> } q q q";

        var report = Report(pdf);

        await Assert.That(report.PdfA).IsNotNull();
        await Assert.That(report.PdfAValidation).IsNotNull();
    }

    /// <summary>Builds a page that claims PDF/A-1b.</summary>
    /// <returns>The builder.</returns>
    private static RenderTestPdf Claimed()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, PartOne, "B", null);
        return pdf;
    }

    /// <summary>Opens a built file and reads its report.</summary>
    /// <param name="pdf">The builder.</param>
    /// <returns>The report.</returns>
    private static PdfConformanceReport Report(RenderTestPdf pdf)
    {
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        return PdfDocumentConformance.GetConformance(document);
    }
}
