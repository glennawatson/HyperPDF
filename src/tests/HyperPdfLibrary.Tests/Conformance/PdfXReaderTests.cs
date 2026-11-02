// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Writing;
using HyperPdfLibrary.Writing;
using static HyperPdfLibrary.Tests.Conformance.PdfXFixtures;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Checks the PDF/X claim report.</summary>
public sealed class PdfXReaderTests
{
    /// <summary>The rule name of the bleed box check.</summary>
    private const string BleedRule = "BleedBox";

    /// <summary>The rule name of the annotation check.</summary>
    private const string AnnotationsRule = "Annotations";

    /// <summary>The rule name of the trapping check.</summary>
    private const string TrappedRule = "Trapped";

    /// <summary>The rule name of the output intent check.</summary>
    private const string OutputIntentRule = "OutputIntent";

    /// <summary>The rule name of the page box check.</summary>
    private const string PageBoxesRule = "PageBoxes";

    /// <summary>The start of an Info dictionary for a PDF/X-4 claim.</summary>
    private const string ClaimStart = "<< /GTS_PDFXVersion";

    /// <summary>The low corner of a box inside the trim area.</summary>
    private const float InnerLow = 40;

    /// <summary>The high corner of a box inside the trim area.</summary>
    private const float InnerHigh = 60;

    /// <summary>A file that meets every check passes them all, names its source for each and lists what it did not check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConformingFilePassesEveryCheck()
    {
        using var document = Open(GoodInfo, RegistryIntent, TrimPage);

        var report = PdfXReader.Read(document);

        await Assert.That(report.Level).IsEqualTo(PdfXLevel.X4);
        await Assert.That(report.AllChecksPassed).IsTrue();
        await Assert.That(report.NotChecked.Length).IsGreaterThan(0);
        await Assert.That(Array.TrueForAll(report.Findings, static f => f.Reference.Length > 0)).IsTrue();
    }

    /// <summary>The cancellable overload gives the same report.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellableOverloadMatches()
    {
        using var document = Open(GoodInfo, RegistryIntent, TrimPage);

        var report = PdfXReader.Read(document, CancellationToken.None);

        await Assert.That(report.Findings.Length).IsEqualTo(PdfXReader.Read(document).Findings.Length);
    }

    /// <summary>A cancelled token stops the check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledTokenStops()
    {
        using var document = Open(GoodInfo, RegistryIntent, TrimPage);
        using CancellationTokenSource cancel = new();
        await cancel.CancelAsync();

        await Assert.That(() => PdfXReader.Read(document, cancel.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>A missing document is rejected.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NullDocumentIsRejected() =>
        await Assert.That(static () => PdfXReader.Read(null!)).Throws<ArgumentNullException>();

    /// <summary>A missing title, trapping key, output intent and trim box each fail their own check and the raw claim stays visible.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BrokenFileFailsEachCheck()
    {
        using var document = Open($"{ClaimStart} (PDF/X-4) >>", string.Empty, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>");

        var report = PdfXReader.Read(document);
        var failed = Array.FindAll(report.Findings, static finding => !finding.Passed);

        await Assert.That(report.Claim).IsEqualTo("PDF/X-4");
        await Assert.That(report.AllChecksPassed).IsFalse();
        await Assert.That(Array.Exists(failed, static f => f.Rule == "Title")).IsTrue();
        await Assert.That(Array.Exists(failed, static f => f.Rule == TrappedRule)).IsTrue();
        await Assert.That(Array.Exists(failed, static f => f.Rule == OutputIntentRule)).IsTrue();
        await Assert.That(Array.Exists(failed, static f => f.Rule == PageBoxesRule)).IsTrue();
    }

    /// <summary>A page with both a trim box and an art box fails.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BothBoxesFail()
    {
        using var document = Open(GoodInfo, RegistryIntent, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /TrimBox [5 5 95 95] /ArtBox [5 5 95 95] >>");

        await AssertOnlyFails(document, PageBoxesRule);
    }

    /// <summary>A bleed box inside the trim box fails; one around it passes.</summary>
    /// <param name="bleed">The bleed box array text.</param>
    /// <param name="fails">Whether the check should fail.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("[20 20 80 80]", true)]
    [Arguments("[0 0 100 100]", false)]
    [Arguments("[1 2]", true)]
    public async Task BleedBoxMustEncloseTheTrim(string bleed, bool fails)
    {
        using var document = Open(GoodInfo, RegistryIntent, BleedPage(bleed));

        var report = PdfXReader.Read(document);

        await Assert.That(Array.Exists(report.Findings, static f => f.Rule == BleedRule && !f.Passed)).IsEqualTo(fails);
    }

    /// <summary>A bleed box is compared with the art box when the page has no trim box.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BleedBoxIsComparedWithTheArtBox()
    {
        using var document = Open(GoodInfo, RegistryIntent, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /ArtBox [5 5 95 95] /BleedBox [20 20 80 80] >>");

        await AssertOnlyFails(document, BleedRule);
    }

    /// <summary>An annotation PDF/X forbids fails; a link passes.</summary>
    /// <param name="subtype">The annotation subtype name.</param>
    /// <param name="fails">Whether the check should fail.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Widget", true)]
    [Arguments("Sound", true)]
    [Arguments("3D", true)]
    [Arguments("FileAttachment", true)]
    [Arguments("Link", false)]
    public async Task ProhibitedAnnotationsFail(string subtype, bool fails)
    {
        var annotation = $"<< /Type /Annot /Subtype /{subtype} /Rect [0 0 1 1] >>";
        using var document = Open(GoodInfo, RegistryIntent, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /TrimBox [5 5 95 95] /Annots [{annotation}] >>");

        var report = PdfXReader.Read(document);

        await Assert.That(Array.Exists(report.Findings, static f => f.Rule == AnnotationsRule && !f.Passed)).IsEqualTo(fails);
    }

    /// <summary>An Info dictionary without a True or False trapping key fails.</summary>
    /// <param name="trapped">The Info entry text.</param>
    /// <param name="fails">Whether the check should fail.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/Trapped /True", false)]
    [Arguments("/Trapped /False", false)]
    [Arguments("/Trapped /Unknown", true)]
    [Arguments("", true)]
    public async Task TrappingMustBeStated(string trapped, bool fails)
    {
        using var document = Open($"{ClaimStart} (PDF/X-4) /Title (Job) {trapped} >>", RegistryIntent, TrimPage);

        var report = PdfXReader.Read(document);

        await Assert.That(Array.Exists(report.Findings, static f => f.Rule == TrappedRule && !f.Passed)).IsEqualTo(fails);
    }

    /// <summary>Content the file holds that PDF/X forbids fails its own rule and no other.</summary>
    /// <param name="content">The forbidden content and the rule it breaks.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(PdfXFixtures), nameof(ForbiddenContent))]
    public async Task ForbiddenContentFailsItsRule(ForbiddenCase content)
    {
        using var document = Open(GoodInfo, RegistryIntent + content.Catalog, content.Page, content.Extras);

        await AssertOnlyFails(document, content.Rule);
    }

    /// <summary>A file with an allowed transfer name and no forbidden content passes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultTransferFunctionIsAllowed()
    {
        using var document = Open(GoodInfo, RegistryIntent, StatePage(), "<< /Type /ExtGState /TR2 /Default >>", UseState());

        var report = PdfXReader.Read(document);

        await Assert.That(report.AllChecksPassed).IsTrue();
    }

    /// <summary>A PDF/X-4p file may name an external profile instead of embedding one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PartialProfileFileNeedsNoEmbeddedProfile()
    {
        using var document = Open(
            $"{ClaimStart} (PDF/X-4p) /Title (Job) /Trapped /False >>",
            "/OutputIntents [<< /Type /OutputIntent /S /GTS_PDFX /OutputConditionIdentifier (FOGRA39) >>]",
            TrimPage);

        var report = PdfXReader.Read(document);

        await Assert.That(report.Level).IsEqualTo(PdfXLevel.X4p);
        await Assert.That(report.AllChecksPassed).IsTrue();
    }

    /// <summary>An X-4 file whose output intent names nothing fails the output intent check.</summary>
    /// <param name="intent">The catalog entry.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/OutputIntents [<< /Type /OutputIntent /S /GTS_PDFX >>]")]
    [Arguments("/OutputIntents [<< /Type /OutputIntent /S /GTS_PDFX /OutputConditionIdentifier (FOGRA39) >>]")]
    [Arguments("/OutputIntents [<< /Type /OutputIntent /S /GTS_PDFA1 /OutputConditionIdentifier (x) >>]")]
    public async Task OutputIntentRequiresConditionAndProfile(string intent)
    {
        using var document = Open(GoodInfo, intent, TrimPage);

        await AssertOnlyFails(document, OutputIntentRule);
    }

    /// <summary>A header newer than PDF 1.6 fails the header check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NewHeaderFails()
    {
        using var document = PdfDocumentReader.Open(Build(GoodInfo, RegistryIntent, TrimPage, Pdf17), null);

        await AssertOnlyFails(document, "HeaderVersion");
    }

    /// <summary>An encrypted file fails the encryption check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EncryptedFileFails()
    {
        using var document = PdfDocumentReader.Open(WritingTestDocuments.Encrypt(Build(GoodInfo, RegistryIntent, TrimPage, Pdf16)), null);

        var report = PdfXReader.Read(document);

        await Assert.That(Array.Exists(report.Findings, static f => f.Rule == "Encryption" && !f.Passed)).IsTrue();
    }

    /// <summary>A level without a rule set reports the claim and says nothing was checked.</summary>
    /// <param name="claim">The claim text.</param>
    /// <param name="level">The level it names.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("PDF/X-1a:2001", PdfXLevel.X1a)]
    [Arguments("PDF/X-1:2001", PdfXLevel.X1a)]
    [Arguments("PDF/X-2:2003", PdfXLevel.X2)]
    [Arguments("PDF/X-3:2003", PdfXLevel.X3)]
    [Arguments("PDF/X-5g", PdfXLevel.X5)]
    [Arguments("PDF/X-6n", PdfXLevel.X6)]
    public async Task UnsupportedLevelIsNotChecked(string claim, PdfXLevel level)
    {
        using var document = Open($"{ClaimStart} ({claim}) /Title (Job) >>", RegistryIntent, TrimPage);

        var report = PdfXReader.Read(document);

        await Assert.That(report.Claim).IsEqualTo(claim);
        await Assert.That(report.Level).IsEqualTo(level);
        await Assert.That(report.Findings.Length).IsEqualTo(0);
        await Assert.That(report.AllChecksPassed).IsFalse();
        await Assert.That(report.NotChecked.Length).IsEqualTo(1);
    }

    /// <summary>A claim that is not a PDF/X version name is malformed or unknown, never conforming.</summary>
    /// <param name="claim">The claim text.</param>
    /// <param name="level">The level it classifies as.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("banana", PdfXLevel.Malformed)]
    [Arguments("PDF/X-", PdfXLevel.Malformed)]
    [Arguments("pdf/x-4", PdfXLevel.Malformed)]
    [Arguments("PDF/X 4", PdfXLevel.Malformed)]
    [Arguments("PDF/X-4 ", PdfXLevel.X4)]
    [Arguments("PDF/X-9", PdfXLevel.Unknown)]
    public async Task MalformedClaimIsShownAsSuch(string claim, PdfXLevel level)
    {
        using var document = Open($"{ClaimStart} ({claim}) /Title (Job) >>", RegistryIntent, TrimPage);

        var report = PdfXReader.Read(document);

        await Assert.That(report.IsClaimed).IsTrue();
        await Assert.That(report.Claim).IsEqualTo(claim.Trim());
        await Assert.That(report.Level).IsEqualTo(level);
        await Assert.That(report.Findings.Length > 0).IsEqualTo(level == PdfXLevel.X4);
    }

    /// <summary>A file with no claim is not claimed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnclaimedFileHasNoReport()
    {
        using var document = Open("<< /Title (Job) >>", string.Empty, TrimPage);

        var report = PdfXReader.Read(document);

        await Assert.That(report.IsClaimed).IsFalse();
        await Assert.That(report.Level).IsEqualTo(PdfXLevel.None);
        await Assert.That(report.NotChecked.Length).IsEqualTo(1);
    }

    /// <summary>The finding without a reference has an empty one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindingWithoutReferenceHasEmptyReference() =>
        await Assert.That(new PdfXFinding("Rule", true, "Message").Reference).IsEmpty();

    /// <summary>A saved edit that breaks a checked property shows as a failed finding beside the unchanged claim.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavedEditThatBreaksARuleDoesNotLeaveTheClaimValid()
    {
        using var document = Open(GoodInfo, RegistryIntent, BleedPage("[0 0 100 100]"));
        await Assert.That(PdfXReader.Read(document).AllChecksPassed).IsTrue();

        _ = PdfDocumentPageBoxes.SetBox(document, 0, PdfPageBox.Bleed, new(InnerLow, InnerLow, InnerHigh, InnerHigh));
        using var saved = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);
        var report = PdfXReader.Read(saved);

        await Assert.That(report.Claim).IsEqualTo("PDF/X-4");
        await Assert.That(report.AllChecksPassed).IsFalse();
        await Assert.That(Array.Exists(report.Findings, static f => f.Rule == BleedRule && !f.Passed)).IsTrue();
    }

    /// <summary>Asserts the report fails exactly the named rule.</summary>
    /// <param name="document">The document.</param>
    /// <param name="rule">The rule that should be the only failure.</param>
    /// <returns>A task.</returns>
    private static async Task AssertOnlyFails(PdfDocument document, string rule)
    {
        var report = PdfXReader.Read(document);
        var failed = Array.FindAll(report.Findings, static finding => !finding.Passed);

        await Assert.That(failed.Length).IsGreaterThan(0);
        await Assert.That(Array.TrueForAll(failed, finding => finding.Rule == rule)).IsTrue();
    }

    /// <summary>Opens a one-page file with an Info dictionary and a PDF 1.6 header.</summary>
    /// <param name="info">The Info dictionary.</param>
    /// <param name="catalog">Extra catalog entries.</param>
    /// <param name="page">The page object.</param>
    /// <param name="extras">The objects from number 5.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string info, string catalog, string page, params string[] extras) =>
        PdfDocumentReader.Open(Build(info, catalog, page, Pdf16, extras), null);
}
