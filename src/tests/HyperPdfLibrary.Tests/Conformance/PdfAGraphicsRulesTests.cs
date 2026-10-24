// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Tests for the PDF/A graphics, transparency, content stream and structure type rules.</summary>
public sealed class PdfAGraphicsRulesTests
{
    /// <summary>The PDF/A-2 rule against undefined operators.</summary>
    private const string UndefinedOperator = "6.2.2:1";

    /// <summary>The PDF/A-2 rule against resources a content stream does not own.</summary>
    private const string InheritedResources = "6.2.2:2";

    /// <summary>The PDF/A-4 rule against names no resource dictionary defines.</summary>
    private const string UndefinedResources = "6.2.2:3";

    /// <summary>The PDF/A-2 rule on graphics state nesting.</summary>
    private const string Nesting = "6.1.13:8";

    /// <summary>The PDF/A-2 rule on page boundary sizes.</summary>
    private const string BoxSize = "6.1.13:11";

    /// <summary>The PDF/A-2 rule against transfer functions.</summary>
    private const string Transfer = "6.2.5:1";

    /// <summary>The PDF/A-2 rule on blend mode names.</summary>
    private const string BlendMode = "6.2.10:1";

    /// <summary>The PDF/A-1 rule on the stroke alpha.</summary>
    private const string StrokeAlpha = "6.4:5";

    /// <summary>The PDF/A-1 rule against transparency groups.</summary>
    private const string TransparencyGroup = "6.4:3";

    /// <summary>The PDF/A-2 rule against PostScript in forms.</summary>
    private const string FormPostScript = "6.2.9:1";

    /// <summary>The PDF/A-2 rule against reference XObjects.</summary>
    private const string ReferenceXObject = "6.2.9:2";

    /// <summary>The PDF/A-2 level A rule that types map to standard types.</summary>
    private const string StandardType = "6.7.3.4:1";

    /// <summary>The PDF/A-2 level A rule against circular role mappings.</summary>
    private const string CircularMapping = "6.7.3.4:2";

    /// <summary>The PDF/A-2 level A rule against remapping standard types.</summary>
    private const string RemappedStandardType = "6.7.3.4:3";

    /// <summary>Content that sets the graphics state named GS0.</summary>
    private const string UseGraphicsState = "/GS0 gs";

    /// <summary>The deepest nesting PDF/A-2 allows.</summary>
    private const int MaxNesting = 28;

    /// <summary>A page side below the smallest PDF/A page boundary.</summary>
    private const int TinyPage = 2;

    /// <summary>The page side of the normal test page.</summary>
    private const int PageSide = 100;

    /// <summary>An operator ISO 32000 does not define is reported, even inside BX and EX.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndefinedOperatorFails()
    {
        var pdf = new RenderTestPdf(PageSide, PageSide) { Content = "q BX 1 2 foo EX Q" };

        await Assert.That(Failed(pdf, PdfALevel.PdfA2B)).Contains(UndefinedOperator);
        await Assert.That(Failed(new(PageSide, PageSide) { Content = "q 0 0 10 10 re f Q" }, PdfALevel.PdfA2B)).DoesNotContain(UndefinedOperator);
    }

    /// <summary>Graphics state nesting deeper than 28 is reported; 28 is allowed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NestingDeeperThan28Fails()
    {
        var allowed = new RenderTestPdf(PageSide, PageSide) { Content = Nested(MaxNesting) };
        var deeper = new RenderTestPdf(PageSide, PageSide) { Content = Nested(MaxNesting + 1) };

        await Assert.That(Failed(allowed, PdfALevel.PdfA2B)).DoesNotContain(Nesting);
        await Assert.That(Failed(deeper, PdfALevel.PdfA2B)).Contains(Nesting);
    }

    /// <summary>A graphics state with a transfer function fails when content uses it, and is not checked when unused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TransferFunctionFailsWhenUsed()
    {
        var used = new RenderTestPdf(PageSide, PageSide) { Resources = "/ExtGState << /GS0 << /TR /Identity >> >>", Content = UseGraphicsState };
        var unused = new RenderTestPdf(PageSide, PageSide) { Resources = "/ExtGState << /GS0 << /TR /Identity >> >>" };

        await Assert.That(Failed(used, PdfALevel.PdfA2B)).Contains(Transfer);
        await Assert.That(Failed(unused, PdfALevel.PdfA2B)).DoesNotContain(Transfer);
    }

    /// <summary>PDF/A-1 allows only an alpha of 1.</summary>
    /// <param name="alpha">The stroke alpha.</param>
    /// <param name="fails">Whether the rule fails.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("0.5", true)]
    [Arguments("1", false)]
    [Arguments("1.0000001", false)]
    public async Task StrokeAlphaMustBeOne(string alpha, bool fails)
    {
        var pdf = new RenderTestPdf(PageSide, PageSide) { Resources = $"/ExtGState << /GS0 << /CA {alpha} >> >>", Content = UseGraphicsState };

        await Assert.That(Failed(pdf, PdfALevel.PdfA1B).Contains(StrokeAlpha)).IsEqualTo(fails);
    }

    /// <summary>PDF/A-2 accepts the blend modes ISO 32000-1 defines and rejects others.</summary>
    /// <param name="mode">The blend mode.</param>
    /// <param name="fails">Whether the rule fails.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/Multiply", false)]
    [Arguments("/Bogus", true)]
    [Arguments("[/Bogus /Normal]", true)]
    public async Task BlendModesAreDefinedModes(string mode, bool fails)
    {
        var pdf = new RenderTestPdf(PageSide, PageSide) { Resources = $"/ExtGState << /GS0 << /BM {mode} >> >>", Content = UseGraphicsState };

        await Assert.That(Failed(pdf, PdfALevel.PdfA2B).Contains(BlendMode)).IsEqualTo(fails);
    }

    /// <summary>PDF/A-1 does not allow a page transparency group.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TransparencyGroupFailsInPdfA1()
    {
        var pdf = new RenderTestPdf(PageSide, PageSide) { PageEntries = "/Group << /S /Transparency /CS /DeviceRGB >>" };

        await Assert.That(Failed(pdf, PdfALevel.PdfA1B)).Contains(TransparencyGroup);
    }

    /// <summary>A page boundary smaller than 3 units is reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TinyPageFails()
    {
        await Assert.That(Failed(new(TinyPage, TinyPage), PdfALevel.PdfA2B)).Contains(BoxSize);
        await Assert.That(Failed(new(PageSide, PageSide), PdfALevel.PdfA2B)).DoesNotContain(BoxSize);
    }

    /// <summary>Forms that carry PostScript or refer to another file are reported when content paints them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PostScriptAndReferenceFormsFail()
    {
        var pdf = new RenderTestPdf(PageSide, PageSide);
        var postScript = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Resources << >> /PS 5 0 R", string.Empty);
        var reference = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Resources << >> /Ref << /F (other.pdf) /Page 0 >>", string.Empty);
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/XObject << /Fm0 {postScript} 0 R /Fm1 {reference} 0 R >>");
        pdf.Content = "/Fm0 Do /Fm1 Do";

        var failed = Failed(pdf, PdfALevel.PdfA2B);

        await Assert.That(failed).Contains(FormPostScript);
        await Assert.That(failed).Contains(ReferenceXObject);
    }

    /// <summary>A form without its own resources that uses the page's font is reported; one with its own font is not.</summary>
    /// <param name="formResources">The form's resource entry.</param>
    /// <param name="fails">Whether the rule fails.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("", true)]
    [Arguments("/Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >>", false)]
    public async Task FormsOwnTheirResources(string formResources, bool fails)
    {
        var pdf = new RenderTestPdf(PageSide, PageSide);
        var form = pdf.AddStream($"/Type /XObject /Subtype /Form /BBox [0 0 10 10] {formResources}", "BT /F1 12 Tf ET");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> /XObject << /Fm0 {form} 0 R >>");
        pdf.Content = "/Fm0 Do";

        await Assert.That(Failed(pdf, PdfALevel.PdfA2B).Contains(InheritedResources)).IsEqualTo(fails);
    }

    /// <summary>PDF/A-4 reports a name no resource dictionary defines.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndefinedNameFailsInPdfA4()
    {
        var pdf = new RenderTestPdf(PageSide, PageSide) { Content = "/Missing gs" };

        await Assert.That(Failed(pdf, PdfALevel.PdfA4)).Contains(UndefinedResources);
    }

    /// <summary>Structure types must reach a standard type through the role map, without loops.</summary>
    /// <param name="roleMap">The role map entries.</param>
    /// <param name="type">The element's structure type.</param>
    /// <param name="expected">The rule that fails, or empty when all hold.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("", "/Custom", StandardType)]
    [Arguments("/Custom /P", "/Custom", "")]
    [Arguments("/Custom /Other /Other /Custom", "/Custom", CircularMapping)]
    [Arguments("/P /Custom", "/P", RemappedStandardType)]
    public async Task StructureTypesMapToStandardTypes(string roleMap, string type, string expected)
    {
        var pdf = new RenderTestPdf(PageSide, PageSide);
        var root = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructTreeRoot /K {pdf.NextNumber + 1} 0 R /RoleMap << {roleMap} >> >>"));
        _ = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructElem /S {type} /P {root} 0 R >>"));
        pdf.CatalogEntries = string.Create(CultureInfo.InvariantCulture, $"/StructTreeRoot {root} 0 R /MarkInfo << /Marked true >>");

        var failed = Failed(pdf, PdfALevel.PdfA2A);
        string[] structureRules = [StandardType, CircularMapping, RemappedStandardType];

        foreach (var rule in structureRules)
        {
            await Assert.That(failed.Contains(rule)).IsEqualTo(rule == expected);
        }
    }

    /// <summary>A cancelled validation stops.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledValidationThrows()
    {
        using var document = PdfDocumentReader.Open(new RenderTestPdf(PageSide, PageSide) { Content = "q Q" }.ToBytes(), null);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(async () => _ = await PdfAValidator.ValidateAsync(document, PdfALevel.PdfA2B, cancellation.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Builds content that nests the graphics state.</summary>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The content.</returns>
    private static string Nested(int depth)
    {
        var content = new StringBuilder();
        for (var i = 0; i < depth; i++)
        {
            _ = content.Append("q ");
        }

        for (var i = 0; i < depth; i++)
        {
            _ = content.Append("Q ");
        }

        return content.ToString();
    }

    /// <summary>Validates a built file and lists the clause and test numbers of the failed rules.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="level">The level to check.</param>
    /// <returns>The failed rules, such as <c>6.2.2:1</c>.</returns>
    private static HashSet<string> Failed(RenderTestPdf pdf, PdfALevel level)
    {
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        var report = PdfAValidator.Validate(document, level);
        return [.. report.Failed.Select(static rule => $"{rule.Clause}:{rule.Test}")];
    }
}
