// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.PdfUa;
using HyperPdfLibrary.Tests.Accessibility;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.PdfUa;

/// <summary>Checks that mandatory accessibility findings reach PDF/UA-1 save preflight.</summary>
public sealed class PdfUaReportChecksTests
{
    /// <summary>An invalid catalog or element language is a PDF/UA-1 language defect.</summary>
    /// <param name="catalogLanguage">Whether to set the invalid language on the catalog.</param>
    /// <param name="cancellationToken">Cancels the report.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task InvalidLanguageReachesPreflight(bool catalogLanguage, CancellationToken cancellationToken)
    {
        var spec = catalogLanguage
            ? new AccessibilitySpec { Lang = "/Lang (not a language)" }
            : new AccessibilitySpec { Layout = static (pdf, document) => AccessibilityPdfs.Kids(pdf.Element("P", document, "/K 0 /Lang (not a language)")) };
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(spec));
        var findings = new List<PdfUaFinding>();

        PdfUaReportChecks.Check(document, findings, cancellationToken);

        await Assert.That(findings.Select(static finding => finding.Requirement)).Contains(PdfUaRequirement.Language);
    }

    /// <summary>A remapped standard structure type is a PDF/UA-1 structure defect.</summary>
    /// <param name="cancellationToken">Cancels the report.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemappedStandardTypeReachesPreflight(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new() { RootExtra = "/RoleMap << /P /Span >>" }));
        var findings = new List<PdfUaFinding>();

        PdfUaReportChecks.Check(document, findings, cancellationToken);

        await Assert.That(findings.Select(static finding => finding.Requirement)).Contains(PdfUaRequirement.StructureTypes);
    }
}
