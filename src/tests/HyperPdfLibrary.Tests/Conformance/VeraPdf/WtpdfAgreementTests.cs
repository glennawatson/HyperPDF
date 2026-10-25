// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Accessibility;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Compares the Well-Tagged PDF check with veraPDF's own WTPDF reuse profile on small files that each break one rule: both must
/// fail the same clauses. veraPDF cites a Note tag under 8.2.4 and a containment error as Table 5 of ISO/TS 32005, where
/// HyperPdf cites 8.2.5.14.1 and 8.2.4, so those are matched by name.
/// </summary>
[NotInParallel(nameof(VeraPdfValidator))]
public sealed class WtpdfAgreementTests
{
    /// <summary>A symbolic TrueType font whose dictionary has an Encoding, which clause 8.4.5.7 forbids.</summary>
    private const string SymbolicTrueTypeWithEncoding =
        $"<< /Type /Font /Subtype /TrueType /BaseFont /Sym /FirstChar 0 /LastChar 0 /Widths [500] /Encoding /WinAnsiEncoding {SymbolicDescriptor} >>";

    /// <summary>The same font without an Encoding.</summary>
    private const string SymbolicTrueTypeWithoutEncoding =
        $"<< /Type /Font /Subtype /TrueType /BaseFont /Sym /FirstChar 0 /LastChar 0 /Widths [500] {SymbolicDescriptor} >>";

    /// <summary>The font descriptor of a symbolic font.</summary>
    private const string SymbolicDescriptor =
        "/FontDescriptor << /Type /FontDescriptor /FontName /Sym /Flags 4 /FontBBox [0 0 1 1] /ItalicAngle 0 /Ascent 1 /Descent 0 /CapHeight 1 /StemV 1 >>";

    /// <summary>A composite font that uses the Japanese CMap UniJIS-UCS2-H with a Korean CIDSystemInfo ordering, which clause 8.4.5.3.1 forbids.</summary>
    private const string JapaneseCMapWithKoreanOrdering =
        "<< /Type /Font /Subtype /Type0 /BaseFont /Cid /Encoding /UniJIS-UCS2-H /DescendantFonts [<< /Type /Font /Subtype /CIDFontType2 /BaseFont /Cid "
        + "/CIDSystemInfo << /Registry (Adobe) /Ordering (Korea1) /Supplement 0 >> /CIDToGIDMap /Identity >>] >>";

    /// <summary>The clause HyperPdf gives containment findings.</summary>
    private const string ContainmentClause = "8.2.4 (ISO/TS 32005 Table 5)";

    /// <summary>The clause veraPDF gives a file that lacks the accessibility declaration.</summary>
    private const string AccessibilityDeclarationClause = "6.1.3";

    /// <summary>The clause veraPDF gives a file that lacks the reuse declaration.</summary>
    private const string DeclarationClause = "6.1.2";

    /// <summary>The clauses only HyperPdf reports at the accessibility level: the PDF/UA-2 requirement and the Formula description.</summary>
    private static readonly string[] HyperPdfOnly = ["6", "8.2.5.29.2"];

    /// <summary>The tag types the small files hold, one file each.</summary>
    private static readonly string[] Types = ["P", "H", "Figure", "Ruby", "Note", "Warichu", "Caption", "Formula"];

    /// <summary>For each tag type, a file that is PDF 2.0 and has metadata fails exactly the clauses in both checkers.</summary>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task StructureRulesAgreeWithVeraPdf(CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("wtpdf-agreement");
        var disagreements = new List<string>();
        foreach (var type in Types)
        {
            var path = Path.Combine(folder.Path, $"{type}.pdf");
            await File.WriteAllBytesAsync(path, Build(type, true), cancellationToken);
            await CompareAsync(home, path, type, true, disagreements, cancellationToken);
        }

        await Assert.That(disagreements).IsEmpty().Because(string.Join(" | ", disagreements));
    }

    /// <summary>A file the library declares as WTPDF reuse is accepted by veraPDF's reuse profile, so the declaration address and metadata are ones veraPDF reads.</summary>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeclaredReuseFilePassesVeraPdf(CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("wtpdf-declared");
        var path = Path.Combine(folder.Path, "declared.pdf");
        using (var document = PdfDocumentReader.Open(Build("P", true), null))
        {
            PdfDocumentWtpdf.Declare(document, PdfWtpdfProfile.Reuse);
            await File.WriteAllBytesAsync(path, HyperPdfLibrary.Writing.PdfCompactWriter.Save(document.Objects, HyperPdfLibrary.Writing.PdfCompactOptions.Default), cancellationToken);
        }

        var job = (await VeraPdfValidator.ValidateAsync(home, "wt1r", [path], cancellationToken))[Path.GetFullPath(path)];

        await Assert.That(job.Failures.Select(static f => $"{f.Clause}/{f.TestNumber}").ToArray()).IsEmpty().Because(job.FailureSummary);
    }

    /// <summary>
    /// For each tag type, HyperPdf fails the clauses veraPDF's WTPDF accessibility profile fails, apart from two known
    /// differences: veraPDF's 6.1.3 is the missing declaration, which HyperPdf reports as <c>DeclaresAccessibility</c>, and HyperPdf
    /// adds clause 6 (the file must also meet PDF/UA-2) and 8.2.5.29.2 (a Formula without MathML needs Alt or ActualText), which
    /// veraPDF's profile does not check.
    /// </summary>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task AccessibilityRulesAgreeWithVeraPdf(CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("wtpdf-accessibility");
        var disagreements = new List<string>();
        foreach (var type in Types)
        {
            var path = Path.Combine(folder.Path, $"{type}.pdf");
            await File.WriteAllBytesAsync(path, Build(type, true), cancellationToken);
            using var document = await PdfDocumentReader.OpenAsync(path, null, cancellationToken);
            var mine = PdfDocumentWtpdf.Check(document, cancellationToken).Findings.Select(static f => f.Clause).Where(static c => !HyperPdfOnly.Contains(c)).Distinct().Order().ToArray();
            var job = (await VeraPdfValidator.ValidateAsync(home, "wt1a", [path], cancellationToken))[Path.GetFullPath(path)];
            var theirs = job.Failures.Select(static f => Normalize(f.Clause)).Where(static c => c != AccessibilityDeclarationClause).Distinct().Order().ToArray();
            if (!mine.SequenceEqual(theirs))
            {
                disagreements.Add($"{type}: HyperPdf [{string.Join(", ", mine)}] veraPDF [{string.Join(", ", theirs)}]");
            }
        }

        await Assert.That(disagreements).IsEmpty().Because(string.Join(" | ", disagreements));
    }

    /// <summary>Optional content (8.7), destination (8.8) and embedded file description (8.14.1) rules fail the same files in both checkers.</summary>
    /// <param name="catalogExtra">The entries added to the catalog.</param>
    /// <param name="pageExtra">The entries added to the page.</param>
    /// <param name="clause">The clause that is tested.</param>
    /// <param name="fails">Whether the file breaks the clause.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/OCProperties << /OCGs [] /D << /Name (Default) >> /Configs [<< /Name (Alt) >>] >>", "", "8.7", false)]
    [Arguments("/OCProperties << /OCGs [] /D << /Name (Default) >> /Configs [<< >>] >>", "", "8.7", true)]
    [Arguments("/OCProperties << /OCGs [] /D << /AS [] >> >>", "", "8.7", true)]
    [Arguments("", "/Tabs /S /Annots [<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /Contents (Go) /Dest [3 0 R /Fit] >>]", "8.8", true)]
    [Arguments("", "/Tabs /S /Annots [<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /Contents (Go) /Dest [6 0 R /Fit] >>]", "8.8", false)]
    [Arguments("", "/Tabs /S /Annots [<< /Type /Annot /Subtype /Square /Rect [0 0 10 10] /RC (one) /Contents (two) >>]", "8.9.2.3", true)]
    [Arguments("", "/Tabs /S /Annots [<< /Type /Annot /Subtype /Stamp /Rect [0 0 10 10] >>]", "8.9.2.4.7", true)]
    [Arguments("", "/Tabs /S /Annots [<< /Type /Annot /Subtype /Stamp /Rect [0 0 10 10] /Name /Approved >>]", "8.9.2.4.7", false)]
    public async Task NavigationRulesAgreeWithVeraPdf(string catalogExtra, string pageExtra, string clause, bool fails, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("wtpdf-navigation");
        var path = Path.Combine(folder.Path, "navigation.pdf");
        await File.WriteAllBytesAsync(path, WtpdfNavigationTests.Build(catalogExtra, pageExtra, []), cancellationToken);

        await AssertAgreementAsync(home, path, clause, fails, cancellationToken);
    }

    /// <summary>Rules for annotations in the structure tree fail the same files in both checkers.</summary>
    /// <param name="holder">The tag type that holds the annotation.</param>
    /// <param name="holderExtra">Extra entries on the holding tag.</param>
    /// <param name="annotation">The annotation's subtype and extra entries.</param>
    /// <param name="clause">The clause that is tested.</param>
    /// <param name="fails">Whether the file breaks the clause.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("P", "", "/Subtype /Highlight", "8.9.2.3", true)]
    [Arguments("Annot", "", "/Subtype /Highlight", "8.9.2.3", false)]
    [Arguments("P", "", "/Subtype /Popup", "8.9.2.4.9", true)]
    [Arguments("Annot", "/Alt (Note)", "/Subtype /Square /Contents (Other)", "8.9.4.2", true)]
    [Arguments("Annot", "/Alt (Note)", "/Subtype /Square /Contents (Note)", "8.9.4.2", false)]
    [Arguments("Form", "", "/Subtype /Widget /FT /Btn /T (b)", "8.10.2.3", true)]
    [Arguments("Form", "", "/Subtype /Widget /FT /Btn /T (b) /Contents (Send)", "8.10.2.3", false)]
    [Arguments("Form", "", "/Subtype /Widget /FT /Btn /T (b) /AA << >>", "8.10.2.3", true)]
    public async Task AnnotationTreeRulesAgreeWithVeraPdf(string holder, string holderExtra, string annotation, string clause, bool fails, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("wtpdf-annotation-tree");
        var path = Path.Combine(folder.Path, "annotation.pdf");
        await File.WriteAllBytesAsync(path, WtpdfAnnotationTreeTests.Build(holder, holderExtra, annotation), cancellationToken);

        await AssertAgreementAsync(home, path, clause, fails, cancellationToken);
    }

    /// <summary>Font rules of 8.4.5 that repeat the PDF/A-4 rules fail the same files in both checkers.</summary>
    /// <param name="font">The font dictionary text.</param>
    /// <param name="clause">The clause that is tested.</param>
    /// <param name="fails">Whether the file breaks the clause.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(SymbolicTrueTypeWithEncoding, "8.4.5.7", true)]
    [Arguments(SymbolicTrueTypeWithoutEncoding, "8.4.5.7", false)]
    [Arguments(JapaneseCMapWithKoreanOrdering, "8.4.5.3.1", true)]
    public async Task FontRulesAgreeWithVeraPdf(string font, string clause, bool fails, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("wtpdf-fonts");
        var path = Path.Combine(folder.Path, "font.pdf");
        await File.WriteAllBytesAsync(path, AccessibilityPdfs.Build(new() { Font = _ => font }), cancellationToken);

        await AssertAgreementAsync(home, path, clause, fails, cancellationToken);
    }

    /// <summary>A text field whose RV has no equal V fails clause 8.10.3.3 in both checkers.</summary>
    /// <param name="values">The field's V and RV entries.</param>
    /// <param name="fails">Whether the file breaks the clause.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/RV (x)", true)]
    [Arguments("/RV (x) /V (y)", true)]
    [Arguments("/V (x)", false)]
    public async Task TextFieldValuesAgreeWithVeraPdf(string values, bool fails, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("wtpdf-field");
        var path = Path.Combine(folder.Path, "field.pdf");
        string[] more = [$"<< /Type /Annot /Subtype /Widget /Rect [0 0 10 10] /FT /Tx /T (a) /Contents (a) {values} >>"];
        await File.WriteAllBytesAsync(path, WtpdfNavigationTests.Build("/AcroForm << /Fields [9 0 R] >>", "/Tabs /S /Annots [9 0 R]", more), cancellationToken);

        await AssertAgreementAsync(home, path, "8.10.3.3", fails, cancellationToken);
    }

    /// <summary>An embedded file without a Desc fails clause 8.14.1 in both checkers, and one with a Desc does not.</summary>
    /// <param name="description">The file specification's Desc entry, or empty.</param>
    /// <param name="fails">Whether the file breaks the clause.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("", true)]
    [Arguments("/Desc (A table of figures)", false)]
    public async Task EmbeddedFileDescriptionAgreesWithVeraPdf(string description, bool fails, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("wtpdf-embedded");
        var path = Path.Combine(folder.Path, "embedded.pdf");
        await File.WriteAllBytesAsync(path, WtpdfNavigationTests.BuildWithAttachment(description), cancellationToken);

        await AssertAgreementAsync(home, path, "8.14.1", fails, cancellationToken);
    }

    /// <summary>A file that is not PDF 2.0 and has no metadata fails the version and metadata clauses in both checkers.</summary>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task VersionAndMetadataRulesAgreeWithVeraPdf(CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("wtpdf-agreement-bare");
        var path = Path.Combine(folder.Path, "bare.pdf");
        await File.WriteAllBytesAsync(path, Build("P", false), cancellationToken);
        var disagreements = new List<string>();

        await CompareAsync(home, path, "bare", false, disagreements, cancellationToken);

        await Assert.That(disagreements).IsEmpty().Because(string.Join(" | ", disagreements));
    }

    /// <summary>Requires both checkers to agree on whether one clause fails on a file.</summary>
    /// <param name="home">veraPDF's folder.</param>
    /// <param name="path">The file.</param>
    /// <param name="clause">The clause.</param>
    /// <param name="fails">Whether the file is expected to break it.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task.</returns>
    private static async Task AssertAgreementAsync(string home, string path, string clause, bool fails, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(path, null, cancellationToken);
        var mine = PdfDocumentWtpdf.Check(document, cancellationToken).Findings.Any(f => f.Clause == clause);
        var job = (await VeraPdfValidator.ValidateAsync(home, "wt1a", [path], cancellationToken))[Path.GetFullPath(path)];
        var theirs = job.Failures.Exists(f => Normalize(f.Clause) == clause);

        using (Assert.Multiple())
        {
            await Assert.That(mine).IsEqualTo(fails).Because($"HyperPdf, clause {clause}");
            await Assert.That(theirs).IsEqualTo(fails).Because($"veraPDF, clause {clause}: {job.FailureSummary}");
        }
    }

    /// <summary>Compares the clauses both checkers fail on one file.</summary>
    /// <param name="home">veraPDF's folder.</param>
    /// <param name="path">The file.</param>
    /// <param name="name">The case's name, for the message.</param>
    /// <param name="hasMetadata">Whether the file has an XMP metadata stream.</param>
    /// <param name="disagreements">Receives a line when the clauses differ.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task.</returns>
    private static async Task CompareAsync(string home, string path, string name, bool hasMetadata, List<string> disagreements, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(path, null, cancellationToken);
        var report = PdfDocumentWtpdf.Check(document, cancellationToken);
        var mine = report.Findings.Where(static f => f.Profile == PdfWtpdfProfile.Reuse).Select(static f => f.Clause).Distinct().Order().ToArray();
        var job = (await VeraPdfValidator.ValidateAsync(home, "wt1r", [path], cancellationToken))[Path.GetFullPath(path)];
        var theirs = job.Failures.Select(static f => Normalize(f.Clause)).Distinct().Order().ToList();

        // The declaration itself is reported as DeclaresReuse, because a file cannot be required to carry the claim before the claim is written.
        // veraPDF does not look for it in a file with no XMP at all, which it reports under 8.11.1 instead.
        var missingDeclaration = theirs.Remove(DeclarationClause);
        if (hasMetadata && missingDeclaration == report.DeclaresReuse)
        {
            disagreements.Add($"{name}: declaration present {report.DeclaresReuse} but veraPDF missing {missingDeclaration}");
        }

        if (!mine.SequenceEqual(theirs))
        {
            disagreements.Add($"{name}: HyperPdf [{string.Join(", ", mine)}] veraPDF [{string.Join(", ", theirs)}]");
        }
    }

    /// <summary>Gives a veraPDF clause the name HyperPdf uses for the same rule.</summary>
    /// <param name="clause">veraPDF's clause.</param>
    /// <returns>HyperPdf's clause.</returns>
    private static string Normalize(string clause)
    {
        if (clause.StartsWith("Table 5.", StringComparison.Ordinal))
        {
            return ContainmentClause;
        }

        return clause == "8.2.4" ? "8.2.5.14.1" : clause;
    }

    /// <summary>Builds a file with a PDF 2.0 Document holding one tag.</summary>
    /// <param name="type">The tag's type.</param>
    /// <param name="complete">Whether the file says it is PDF 2.0 and has an XMP metadata stream.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(string type, bool complete) => MiniPdf.Build(
    [
        $"<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> /Lang (en-US){(complete ? " /Version /2.0 /Metadata 8 0 R" : string.Empty)} >>",
        "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
        "<< /Type /StructElem /S /Document /NS 7 0 R /P 5 0 R /K [6 0 R] >>",
        "<< /Type /StructTreeRoot /K 4 0 R /Namespaces [7 0 R] >>",
        $"<< /Type /StructElem /S /{type} /NS 7 0 R /P 4 0 R /Pg 3 0 R /K 0 >>",
        "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>",
        MiniPdf.Stream("/Type /Metadata /Subtype /XML", AccessibilityPdfs.Packet(PdfUaClaim.Part2, true, "2024")),
    ]);
}
