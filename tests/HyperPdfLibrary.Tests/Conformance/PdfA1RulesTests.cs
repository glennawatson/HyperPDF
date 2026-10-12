// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>
/// The PDF/A-1 font, transparency, annotation and action rules of the corrigenda give clause-specific results on
/// hand-built files, and files using what PDF/A-1 forbids stay readable when they make no claim.
/// </summary>
public sealed class PdfA1RulesTests
{
    /// <summary>Gets the names of every case.</summary>
    /// <returns>The names.</returns>
    public static IEnumerable<string> CaseNames() => Array.ConvertAll(PdfA1Cases.All(), static item => item.Name);

    /// <summary>Each case passes or fails its rule as expected, and the rule is reported among the checked rules of PDF/A-1b.</summary>
    /// <param name="name">The case name.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(CaseNames))]
    public async Task CaseGivesItsClauseResult(string name)
    {
        var item = Array.Find(PdfA1Cases.All(), candidate => candidate.Name == name)!;
        using var document = PdfDocumentReader.Open(item.File, null);

        var report = PdfDocumentConformance.GetConformance(document).PdfAValidation!;

        await Assert.That(report.Level).IsEqualTo(PdfALevel.PdfA1B);
        await Assert.That(PdfARuleResults.Checked(report, item.Rule)).IsTrue();
        await Assert.That(PdfARuleResults.Failed(report, item.Rule)).IsEqualTo(item.Fails);
    }

    /// <summary>Without a claim, files that use what PDF/A-1 forbids open, report no rule check, and render.</summary>
    /// <param name="name">The case name.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("hide-action")]
    [Arguments("extgstate-smask")]
    [Arguments("image-smask")]
    [Arguments("form-group")]
    [Arguments("button-stream")]
    [Arguments("width-mismatch")]
    public async Task UnclaimedFilesStayReadable(string name)
    {
        var item = Array.Find(PdfA1Cases.All(), candidate => candidate.Name == name)!;
        var unclaimed = Unclaim(item.File);
        using var page = new RenderTestPage(unclaimed);

        var report = PdfDocumentConformance.GetConformance(page.Document);
        var image = page.RenderPage();

        await Assert.That(report.PdfA).IsNull();
        await Assert.That(report.PdfAValidation).IsNull();
        await Assert.That(image.Width).IsGreaterThan(0);
    }

    /// <summary>Removes the PDF/A claim by renaming the part property, keeping every byte offset.</summary>
    /// <param name="file">The claimed file.</param>
    /// <returns>The unclaimed file.</returns>
    private static byte[] Unclaim(byte[] file)
    {
        var copy = (byte[])file.Clone();
        var at = copy.AsSpan().IndexOf("pdfaid:part>"u8);
        while (at >= 0)
        {
            // Same length, so the cross-reference offsets stay right.
            "pdfaid:prt_>"u8.CopyTo(copy.AsSpan(at));
            var next = copy.AsSpan(at + 1).IndexOf("pdfaid:part>"u8);
            at = next < 0 ? -1 : at + 1 + next;
        }

        return copy;
    }
}
