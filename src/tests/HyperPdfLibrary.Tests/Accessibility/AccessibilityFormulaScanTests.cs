// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks PDF/UA-1 formula alternative strings without rejecting readable documents.</summary>
public sealed class AccessibilityFormulaScanTests
{
    /// <summary>The standard mathematical-expression type.</summary>
    private const string Formula = "Formula";

    /// <summary>Replacement text does not replace the separate Alt requirement in PDF/UA-1.</summary>
    /// <param name="entries">The formula's description entries.</param>
    /// <param name="expected">The expected number of formula findings.</param>
    /// <param name="cancellationToken">Cancels the report.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments("", 1)]
    [Arguments("/ActualText (x plus y)", 1)]
    [Arguments("/Alt /NotATextString", 1)]
    [Arguments("/Alt 7", 1)]
    [Arguments("/Alt ()", 0)]
    [Arguments("/Alt (x plus y)", 0)]
    public async Task Check_PartOne_RequiresAltString(string entries, int expected, CancellationToken cancellationToken)
    {
        var spec = new AccessibilitySpec { Content = AccessibilityPdfs.Lines(1), Layout = (pdf, parent) => AccessibilityPdfs.Kids(pdf.Element(Formula, parent, $"/K 0 {entries}")) };
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(spec));
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(report.GetCount(PdfAccessibilityCode.FormulaNoAlternative)).IsEqualTo(expected);
        await Assert.That(PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!.RawType).IsEqualTo(Formula);
    }

    /// <summary>Role mapping to Formula carries the same part 1 requirement.</summary>
    /// <param name="cancellationToken">Cancels the report.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Check_RoleMappedFormula_RequiresAltString(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()
        {
            RootExtra = "/RoleMap << /Equation /Formula >>",
            Layout = static (pdf, parent) => AccessibilityPdfs.Kids(pdf.Element("Equation", parent, "/K 0 /ActualText (x)")),
        }));
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(report.GetCount(PdfAccessibilityCode.FormulaNoAlternative)).IsEqualTo(1);
    }

    /// <summary>PDF/UA-2 has distinct MathML and scientific-formula rules, not the part 1 Alt-only rule.</summary>
    /// <param name="cancellationToken">Cancels the report.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Check_PartTwo_DoesNotApplyPartOneRule(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()
        {
            Xmp = AccessibilityPdfs.Packet(AccessibilityPdfs.PartTwo, true, "2024"),
            CatalogExtra = "/Version /2.0",
            Pdf20Root = true,
            Layout = static (pdf, parent) => AccessibilityPdfs.Kids(pdf.Element(Formula, parent, "/K 0 /ActualText (water)")),
        }));
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(report.GetCount(PdfAccessibilityCode.FormulaNoAlternative)).IsEqualTo(0);
    }
}
