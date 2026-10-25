// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>
/// Tests for the PDF/A file structure and syntax rules: each test breaks one piece of file syntax in a small file written
/// byte for byte and checks the clause veraPDF numbers for it fails, while the untouched file passes it.
/// </summary>
public sealed class PdfAFileStructureRulesTests
{
    /// <summary>One byte more than the longest name.</summary>
    private const int TooLongName = 128;

    /// <summary>One byte more than the longest string in PDF/A-2.</summary>
    private const int TooLongString = 32_768;

    /// <summary>HyperPDF's limit on validating nested embedded PDFs.</summary>
    private const int NestingLimit = 32;

    /// <summary>The PDF/A-2 rule that an embedded file is a valid PDF/A-1 or PDF/A-2 file.</summary>
    private const string EmbeddedPdfARule = "6.8:5";

    /// <summary>The PDF/A-2 file structure clauses an untouched file passes.</summary>
    private static readonly string[] StructureClauses =
    [
        "6.1.2:1", "6.1.2:2", "6.1.3:1", "6.1.3:3", "6.1.4:2", "6.1.6:1", "6.1.6:2", "6.1.7.1:1", "6.1.7.1:2", "6.1.7.1:3",
        "6.1.7.2:1", "6.1.9:1", "6.1.10:1", "6.1.13:1", "6.1.13:3", "6.1.13:4",
    ];

    /// <summary>An untouched file breaks none of the structure clauses, and each is checked rather than skipped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntouchedFilePassesStructureRules()
    {
        var report = Validate(new(), PdfALevel.PdfA2B);
        var failed = Clauses(report.Failed);
        var checkedClauses = Clauses(report.Checked);

        foreach (var clause in StructureClauses)
        {
            await Assert.That(failed).DoesNotContain(clause);
            await Assert.That(checkedClauses).Contains(clause);
        }
    }

    /// <summary>A hexadecimal string with an odd number of digits fails, in an object and in a shown string.</summary>
    /// <param name="inContent">Whether the string is shown by the page's content rather than held by an object.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OddHexStringFails(bool inContent)
    {
        var pdf = new StructureTestPdf();
        if (inContent)
        {
            pdf.Content = "BT /F1 1 Tf <414> Tj ET";
        }
        else
        {
            pdf.CatalogEntries = " /Lang <656>";
        }

        await Assert.That(Failed(pdf, PdfALevel.PdfA2B)).Contains("6.1.6:1");
    }

    /// <summary>A hexadecimal string with a byte that is not a digit fails.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HexStringWithLetterFails()
    {
        var pdf = new StructureTestPdf { CatalogEntries = " /Lang <65GG>" };

        await Assert.That(Failed(pdf, PdfALevel.PdfA2B)).Contains("6.1.6:2");
    }

    /// <summary>A hexadecimal string the page's content holds but no operator shows is not checked, as in veraPDF.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnshownContentStringPasses()
    {
        var pdf = new StructureTestPdf { Content = "<414> 1 0 0 1 0 0 cm" };

        await Assert.That(Failed(pdf, PdfALevel.PdfA2B)).DoesNotContain("6.1.6:1");
    }

    /// <summary>A header other than <c>%PDF-1.n</c> fails PDF/A-2.</summary>
    /// <param name="header">The header line.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("%PDF-1.8")]
    [Arguments("%PDF-1.7 ")]
    [Arguments("%PDF-2.0")]
    public async Task WrongHeaderFails(string header)
    {
        var pdf = new StructureTestPdf { Header = header };

        await Assert.That(Failed(pdf, PdfALevel.PdfA2B)).Contains("6.1.2:1");
    }

    /// <summary>A PDF 2.0 header passes PDF/A-4 and fails PDF/A-2.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Pdf20HeaderPassesPdfA4()
    {
        var pdf = new StructureTestPdf { Header = "%PDF-2.0" };

        await Assert.That(Failed(pdf, PdfALevel.PdfA4)).DoesNotContain("6.1.2:1");
    }

    /// <summary>A comment line without four bytes above 127 fails.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextCommentFails()
    {
        var pdf = new StructureTestPdf { Comment = "%abcd" };

        await Assert.That(Failed(pdf, PdfALevel.PdfA2B)).Contains("6.1.2:2");
    }

    /// <summary>Bytes after the last end-of-file marker and its line end fail.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DataAfterEndFails()
    {
        var pdf = new StructureTestPdf { AfterEnd = "\nextra" };

        await Assert.That(Failed(pdf, PdfALevel.PdfA2B)).Contains("6.1.3:3");
    }

    /// <summary>A trailer without /ID fails.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingIdentifierFails()
    {
        var pdf = new StructureTestPdf { TrailerEntries = string.Empty };

        await Assert.That(Failed(pdf, PdfALevel.PdfA2B)).Contains("6.1.3:1");
        await Assert.That(Failed(pdf, PdfALevel.PdfA1B)).Contains("6.1.3:1");
    }

    /// <summary>Two spaces between the object number and the generation fail.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ObjectHeaderSpacingFails()
    {
        var pdf = new StructureTestPdf { ObjectHeader = static number => $"{number}  0 obj\n" };

        await Assert.That(Failed(pdf, PdfALevel.PdfA2B)).Contains("6.1.9:1");
    }

    /// <summary>A stream whose /Length is short fails, and so does one without a line end after <c>stream</c>.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StreamSyntaxFails()
    {
        var wrongLength = new StructureTestPdf();
        wrongLength.Extra.Add("<< /Length 2 >>\nstream\nabcdef\nendstream");
        var noLineEnd = new StructureTestPdf();
        noLineEnd.Extra.Add("<< /Length 6 >>\nstream abcdef\nendstream");

        await Assert.That(Failed(wrongLength, PdfALevel.PdfA2B)).Contains("6.1.7.1:1");
        await Assert.That(Failed(noLineEnd, PdfALevel.PdfA2B)).Contains("6.1.7.1:2");
    }

    /// <summary>A stream with an external file or a filter PDF/A does not allow fails.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StreamDictionaryFails()
    {
        var external = new StructureTestPdf();
        external.Extra.Add(StructureTestPdf.Stream(" /F (data.bin)", "abc"));
        var lzw = new StructureTestPdf();
        lzw.Extra.Add(StructureTestPdf.Stream(" /Filter /LZWDecode", "abc"));

        await Assert.That(Failed(external, PdfALevel.PdfA2B)).Contains("6.1.7.1:3");
        await Assert.That(Failed(lzw, PdfALevel.PdfA2B)).Contains("6.1.7.2:1");
        await Assert.That(Failed(lzw, PdfALevel.PdfA1B)).Contains("6.1.10:1");
    }

    /// <summary>An integer, a name or a string past the implementation limits fails.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImplementationLimitsFail()
    {
        var integer = new StructureTestPdf { CatalogEntries = " /Big 2147483648" };
        var name = new StructureTestPdf { CatalogEntries = $" /{new string('N', TooLongName)} 1" };
        var text = new StructureTestPdf { CatalogEntries = $" /Long ({new string('s', TooLongString)})" };

        await Assert.That(Failed(integer, PdfALevel.PdfA2B)).Contains("6.1.13:1");
        await Assert.That(Failed(name, PdfALevel.PdfA2B)).Contains("6.1.13:4");
        await Assert.That(Failed(text, PdfALevel.PdfA2B)).Contains("6.1.13:3");
    }

    /// <summary>A real number past 32,767 fails PDF/A-1 and passes PDF/A-2.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LargeRealFailsPdfA1Only()
    {
        var pdf = new StructureTestPdf { CatalogEntries = " /Big 40000.5" };

        await Assert.That(Failed(pdf, PdfALevel.PdfA1B)).Contains("6.1.12:2");
        await Assert.That(Failed(pdf, PdfALevel.PdfA2B)).DoesNotContain("6.1.13:2");
    }

    /// <summary>An inline image with an LZW filter fails PDF/A-2's inline filter rule.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InlineLzwImageFails()
    {
        var pdf = new StructureTestPdf { Content = "q BI /W 1 /H 1 /CS /G /BPC 8 /F /LZW ID \u0080 EI Q" };

        await Assert.That(Failed(pdf, PdfALevel.PdfA2B)).Contains("6.1.10:1");
        await Assert.That(Failed(pdf, PdfALevel.PdfA1B)).Contains("6.1.10:2");
    }

    /// <summary>A malformed language tag fails PDF/A-2a, and an empty one passes.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="fails">Whether it fails.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("en-", true)]
    [Arguments("-GB", true)]
    [Arguments("en-GB", false)]
    [Arguments("", false)]
    public async Task LanguageTagChecked(string tag, bool fails)
    {
        var pdf = new StructureTestPdf { CatalogEntries = $" /Lang ({tag})" };

        await Assert.That(Failed(pdf, PdfALevel.PdfA2A).Contains("6.7.4:1")).IsEqualTo(fails);
    }

    /// <summary>A permissions dictionary with a key other than /DocMDP and /UR3 fails.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownPermissionFails()
    {
        var pdf = new StructureTestPdf { CatalogEntries = " /Perms << /Other 1 >>" };

        await Assert.That(Failed(pdf, PdfALevel.PdfA2B)).Contains("6.1.12:1");
    }

    /// <summary>An embedded file without /AFRelationship or a MIME type fails PDF/A-4.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmbeddedFileWithoutRelationshipFails()
    {
        var pdf = new StructureTestPdf { Header = "%PDF-2.0", CatalogEntries = " /Names << /EmbeddedFiles << /Names [(a.txt) 5 0 R] >> >>" };
        pdf.Extra.Add("<< /Type /Filespec /F (a.txt) /UF (a.txt) /EF << /F 6 0 R >> >>");
        pdf.Extra.Add(StructureTestPdf.Stream(" /Type /EmbeddedFile", "hello"));

        var failed = Failed(pdf, PdfALevel.PdfA4);

        await Assert.That(failed).Contains("6.9:4");
        await Assert.That(failed).Contains("6.9:1");
    }

    /// <summary>
    /// A valid embedded PDF/A-2b file passes the PDF/A-2 embedded file rule, until the file sits beyond HyperPDF's nesting
    /// limit of 32, where it is no longer checked and so not proven valid.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmbeddedPdfAIsCheckedUpToTheNestingLimit()
    {
        var inner = Encoding.Latin1.GetString(PdfAFixtures.Create(PdfAFixtureLevel.A2B));
        var pdf = new StructureTestPdf { CatalogEntries = " /Names << /EmbeddedFiles << /Names [(a.pdf) 5 0 R] >> >>" };
        pdf.Extra.Add("<< /Type /Filespec /F (a.pdf) /UF (a.pdf) /EF << /F 6 0 R >> /AFRelationship /Source >>");
        pdf.Extra.Add(StructureTestPdf.Stream(" /Type /EmbeddedFile /Subtype /application#2Fpdf", inner));
        using var document = PdfDocumentReader.Open(pdf.Build(), null);

        var top = Clauses(PdfAValidator.Validate(document, PdfALevel.PdfA2B, 0, CancellationToken.None).Failed);
        var tooDeep = Clauses(PdfAValidator.Validate(document, PdfALevel.PdfA2B, NestingLimit, CancellationToken.None).Failed);

        await Assert.That(top).DoesNotContain(EmbeddedPdfARule);
        await Assert.That(tooDeep).Contains(EmbeddedPdfARule);
    }

    /// <summary>Validation stops when cancelled.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledValidationThrows()
    {
        using var document = PdfDocumentReader.Open(new StructureTestPdf().Build(), null);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.That(() => PdfAValidator.Validate(document, PdfALevel.PdfA2B, cancelled.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Validates a written file.</summary>
    /// <param name="pdf">The file.</param>
    /// <param name="level">The level.</param>
    /// <returns>The report.</returns>
    private static PdfAValidationReport Validate(StructureTestPdf pdf, PdfALevel level)
    {
        using var document = PdfDocumentReader.Open(pdf.Build(), null);
        return PdfAValidator.Validate(document, level);
    }

    /// <summary>Gets the clauses and tests a file fails.</summary>
    /// <param name="pdf">The file.</param>
    /// <param name="level">The level.</param>
    /// <returns>The failures as <c>clause:test</c>.</returns>
    private static HashSet<string> Failed(StructureTestPdf pdf, PdfALevel level) => Clauses(Validate(pdf, level).Failed);

    /// <summary>Gets results as <c>clause:test</c>.</summary>
    /// <param name="results">The results.</param>
    /// <returns>The clauses.</returns>
    private static HashSet<string> Clauses(IEnumerable<PdfARuleResult> results) => [.. results.Select(static result => $"{result.Clause}:{result.Test}")];
}
