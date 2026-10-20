// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Checks the PDF/X claim report.</summary>
public sealed class PdfXReaderTests
{
    /// <summary>The object number of the Info dictionary in the sample file.</summary>
    private const int InfoObject = 5;

    /// <summary>The offset of the header's minor version digit in <c>%PDF-1.7</c>.</summary>
    private const int HeaderMinorOffset = 7;

    /// <summary>The X-4 claim as a PDF string.</summary>
    private const string X4Claim = "(PDF/X-4)";

    /// <summary>A title as a PDF string.</summary>
    private const string JobTitle = "(Job)";

    /// <summary>A page with a trim box.</summary>
    private const string TrimPage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /TrimBox [5 5 95 95] >>";

    /// <summary>An output intent with an identifier and a registry but no profile.</summary>
    private const string RegistryIntent = "/OutputIntents [<< /Type /OutputIntent /S /GTS_PDFX /OutputConditionIdentifier (FOGRA39) /RegistryName (http://www.color.org) >>]";

    /// <summary>A file that meets every check passes them all and lists what it did not check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConformingFilePassesEveryCheck()
    {
        using var document = Open(X4Claim, JobTitle, RegistryIntent, TrimPage);

        var report = PdfXReader.Read(document);

        await Assert.That(report.Level).IsEqualTo(PdfXLevel.X4);
        await Assert.That(report.AllChecksPassed).IsTrue();
        await Assert.That(report.NotChecked.Length).IsGreaterThan(0);
    }

    /// <summary>A missing title, output intent and trim box each fail their own check and the raw claim stays visible.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BrokenFileFailsEachCheck()
    {
        using var document = Open(X4Claim, null, string.Empty, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>");

        var report = PdfXReader.Read(document);
        var failed = Array.FindAll(report.Findings, static finding => !finding.Passed);

        await Assert.That(report.Claim).IsEqualTo("PDF/X-4");
        await Assert.That(report.AllChecksPassed).IsFalse();
        await Assert.That(Array.Exists(failed, static f => f.Rule == "Title")).IsTrue();
        await Assert.That(Array.Exists(failed, static f => f.Rule == "OutputIntent")).IsTrue();
        await Assert.That(Array.Exists(failed, static f => f.Rule == "PageBoxes")).IsTrue();
    }

    /// <summary>A page with both a trim box and an art box fails.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BothBoxesFail()
    {
        using var document = Open(X4Claim, JobTitle, RegistryIntent, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /TrimBox [5 5 95 95] /ArtBox [5 5 95 95] >>");

        var report = PdfXReader.Read(document);

        await Assert.That(Array.Exists(report.Findings, static f => f.Rule == "PageBoxes" && !f.Passed)).IsTrue();
    }

    /// <summary>A level without a rule set reports the claim and says nothing was checked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsupportedLevelIsNotChecked()
    {
        using var document = Open("(PDF/X-3:2003)", JobTitle, RegistryIntent, TrimPage);

        var report = PdfXReader.Read(document);

        await Assert.That(report.Level).IsEqualTo(PdfXLevel.X3);
        await Assert.That(report.Findings.Length).IsEqualTo(0);
        await Assert.That(report.AllChecksPassed).IsFalse();
        await Assert.That(report.NotChecked.Length).IsEqualTo(1);
    }

    /// <summary>A file with no claim is not claimed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnclaimedFileHasNoReport()
    {
        using var document = Open(null, JobTitle, string.Empty, TrimPage);

        var report = PdfXReader.Read(document);

        await Assert.That(report.IsClaimed).IsFalse();
        await Assert.That(report.Level).IsEqualTo(PdfXLevel.None);
    }

    /// <summary>Opens a one-page file with an Info dictionary.</summary>
    /// <param name="version">The <c>GTS_PDFXVersion</c> PDF string, or null.</param>
    /// <param name="title">The title PDF string, or null.</param>
    /// <param name="intents">The catalog's output intent entry, or empty.</param>
    /// <param name="page">The page object.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string? version, string? title, string intents, string page)
    {
        var versionEntry = version is null ? string.Empty : $"/GTS_PDFXVersion {version} ";
        var titleEntry = title is null ? string.Empty : $"/Title {title} ";
        var info = $"<< {versionEntry}{titleEntry}>>";
        var bytes = MiniPdf.BuildWithTrailer(
            $"/Info {InfoObject} 0 R",
            $"<< /Type /Catalog /Pages 2 0 R {intents} >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            page,
            "<< >>",
            info);

        // The builder writes a PDF 1.7 header; PDF/X-4 is based on PDF 1.6, so change the header's minor digit.
        bytes[HeaderMinorOffset] = (byte)'6';
        return PdfDocumentReader.Open(bytes, null);
    }
}
