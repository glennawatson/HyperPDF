// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Tests.PdfUa;

/// <summary>Checks the PDF/UA-1 rules on encryption (7.16), dynamic XFA forms (7.15) and TrapNet annotations (7.18.2).</summary>
public sealed class PdfUaSecurityXfaTrapNetTests
{
    /// <summary>The user password of the encrypted samples.</summary>
    private const string UserPassword = "user";

    /// <summary>The owner password of the encrypted samples.</summary>
    private const string OwnerPassword = "owner";

    /// <summary>An XFA configuration that asks for dynamic rendering.</summary>
    private const string DynamicConfig = "<config xmlns=\"http://www.xfa.org/schema/xci/3.0/\"><acrobat><acrobat7><dynamicRender>required</dynamicRender></acrobat7></acrobat></config>";

    /// <summary>An XFA configuration that asks for a static form.</summary>
    private const string StaticConfig = "<config xmlns=\"http://www.xfa.org/schema/xci/3.0/\"><acrobat><acrobat7><dynamicRender>forbidden</dynamicRender></acrobat7></acrobat></config>";

    /// <summary>
    /// An encrypted file is reported for PDF/UA-1 when it blocks accessibility extraction, and only then. The writer refuses
    /// to make such a file for a claimed document, so the file is made without a claim and the claimed part is given to the scan.
    /// </summary>
    /// <param name="permissions">What the user password allows.</param>
    /// <param name="part">The claimed part.</param>
    /// <param name="expected">The expected number of findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfAccessPermissions.Print | PdfAccessPermissions.CopyContent, PdfUaClaim.Part1, 1)]
    [Arguments(PdfAccessPermissions.Print | PdfAccessPermissions.ExtractForAccessibility, PdfUaClaim.Part1, 0)]
    [Arguments(PdfAccessPermissions.Print | PdfAccessPermissions.CopyContent, PdfUaClaim.Part2, 0)]
    public async Task EncryptedFileMustAllowAccessibilityExtraction(PdfAccessPermissions permissions, int part, int expected, CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(Build(0, string.Empty, string.Empty), null);
        await using var output = new MemoryStream();
        _ = PdfDocumentProtection.Protect(source, Options(permissions), output);
        using var document = await PdfDocumentReader.OpenAsync(output.ToArray(), UserPassword, cancellationToken);
        var pools = document.Objects.ScratchPools;
        var findings = AccessibilityFindingCollection.Rent(pools);
        try
        {
            AccessibilitySecurityScan.Run(document, part, findings);

            await Assert.That(findings.Counts[(int)PdfAccessibilityCode.SecurityBlocksAssistiveTechnology]).IsEqualTo(expected);
        }
        finally
        {
            AccessibilityFindingCollection.Release(pools, findings);
        }
    }

    /// <summary>The writer refuses to encrypt a claimed file in a way that blocks screen readers, and says which clause.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WriterRefusesToBlockScreenReaders()
    {
        using var source = PdfDocumentReader.Open(Build(PdfUaClaim.Part1, string.Empty, string.Empty), null);
        await using var output = new MemoryStream();

        var error = await Assert.That(() => PdfDocumentProtection.Protect(source, Options(PdfAccessPermissions.Print), output)).Throws<PdfException>();

        await Assert.That(error!.Message).Contains("clause 7.16");
        await Assert.That(output.Length).IsEqualTo(0);
    }

    /// <summary>A dynamic XFA form is reported, a static one is not, and a damaged packet does not stop the report.</summary>
    /// <param name="config">The XFA configuration packet.</param>
    /// <param name="expected">The expected number of findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(DynamicConfig, 1)]
    [Arguments(StaticConfig, 0)]
    [Arguments("<config><acrobat7><dynamicRender>required", 0)]
    public async Task DynamicXfaIsReported(string config, int expected, CancellationToken cancellationToken)
    {
        var bytes = Build(PdfUaClaim.Part1, "/AcroForm << /Fields [] /XFA [(config) 5 0 R] >>", string.Empty, MiniPdf.Stream(string.Empty, config));
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);

        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(report.GetCount(PdfAccessibilityCode.DynamicXfaForm)).IsEqualTo(expected);
    }

    /// <summary>A TrapNet annotation is reported for PDF/UA-1 even when it is hidden, and not for other claims.</summary>
    /// <param name="part">The claimed part; 0 for none.</param>
    /// <param name="flags">The annotation flags.</param>
    /// <param name="expected">The expected number of findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, 0, 1)]
    [Arguments(1, 2, 1)]
    [Arguments(2, 0, 0)]
    [Arguments(0, 0, 0)]
    public async Task TrapNetIsReportedForPartOne(int part, int flags, int expected, CancellationToken cancellationToken)
    {
        var annotation = $"<< /Type /Annot /Subtype /TrapNet /Rect [0 0 10 10] /F {flags} >>";
        using var document = await PdfDocumentReader.OpenAsync(Build(part, string.Empty, $"/Annots [{annotation}]"), null, cancellationToken);

        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(report.GetCount(PdfAccessibilityCode.TrapNetAnnotation)).IsEqualTo(expected);
    }

    /// <summary>A link whose URI action is a server-side image map is reported for PDF/UA-1 only.</summary>
    /// <param name="part">The claimed part; 0 for none.</param>
    /// <param name="isMap">The IsMap value.</param>
    /// <param name="expected">The expected number of findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, "true", 1)]
    [Arguments(1, "false", 0)]
    [Arguments(2, "true", 0)]
    [Arguments(0, "true", 0)]
    public async Task ImageMapLinkIsReportedForPartOne(int part, string isMap, int expected, CancellationToken cancellationToken)
    {
        var link = $"<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /Contents (Go) /A << /S /URI /URI (https://example.test/) /IsMap {isMap} >> >>";
        using var document = await PdfDocumentReader.OpenAsync(Build(part, string.Empty, $"/Annots [{link}]"), null, cancellationToken);

        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(report.GetCount(PdfAccessibilityCode.LinkIsMap)).IsEqualTo(expected);
    }

    /// <summary>A form XObject that stores its content in another file is reported for PDF/UA-1 only.</summary>
    /// <param name="part">The claimed part; 0 for none.</param>
    /// <param name="entries">The extra entries of the form XObject.</param>
    /// <param name="expected">The expected number of findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, "/Ref << /F (other.pdf) /Page 0 >>", 1)]
    [Arguments(1, "", 0)]
    [Arguments(2, "/Ref << /F (other.pdf) /Page 0 >>", 0)]
    [Arguments(0, "/Ref << /F (other.pdf) /Page 0 >>", 0)]
    public async Task ReferenceXObjectIsReportedForPartOne(int part, string entries, int expected, CancellationToken cancellationToken)
    {
        var form = MiniPdf.Stream($"/Type /XObject /Subtype /Form /BBox [0 0 10 10] {entries}", "0 0 m");
        using var document = await PdfDocumentReader.OpenAsync(Build(part, string.Empty, "/Resources << /XObject << /Fm0 5 0 R >> >>", form), null, cancellationToken);

        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(report.GetCount(PdfAccessibilityCode.ReferenceXObject)).IsEqualTo(expected);
    }

    /// <summary>Media clip data without CT and Alt is reported for PDF/UA-1 only.</summary>
    /// <param name="part">The claimed part; 0 for none.</param>
    /// <param name="clipEntries">The clip's extra entries.</param>
    /// <param name="expected">The expected number of findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, "", 1)]
    [Arguments(1, "/CT (video/mp4)", 1)]
    [Arguments(1, "/CT (video/mp4) /Alt [(en) (A talk)]", 0)]
    [Arguments(2, "", 0)]
    [Arguments(0, "", 0)]
    public async Task MediaClipNeedsTypeAndDescription(int part, string clipEntries, int expected, CancellationToken cancellationToken)
    {
        var screen = $"<< /Type /Annot /Subtype /Screen /Rect [0 0 10 10] /Contents (Clip) /A << /S /Rendition /R << /S /MR /C << /S /MCD /D (clip.mp4) {clipEntries} >> >> >> >>";
        using var document = await PdfDocumentReader.OpenAsync(Build(part, string.Empty, $"/Annots [{screen}]"), null, cancellationToken);

        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(report.GetCount(PdfAccessibilityCode.MediaClipNoAlternative)).IsEqualTo(expected);
    }

    /// <summary>Gets the options of an AES-128 file, which follows ISO 32000-1 and so can clear the accessibility bit.</summary>
    /// <param name="permissions">What the user password allows.</param>
    /// <returns>The options.</returns>
    private static PdfEncryptionOptions Options(PdfAccessPermissions permissions) =>
        new() { Algorithm = PdfEncryptionAlgorithm.Aes128, UserPassword = UserPassword, OwnerPassword = OwnerPassword, Permissions = permissions };

    /// <summary>Builds a one page file with a claim, optional catalog entries and optional page entries.</summary>
    /// <param name="part">The claimed part; 0 for none.</param>
    /// <param name="catalogEntries">Extra catalog entries.</param>
    /// <param name="pageEntries">Extra page entries.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(int part, string catalogEntries, string pageEntries) => Build(part, catalogEntries, pageEntries, "null");

    /// <summary>Builds a one page file with a claim, optional catalog entries, optional page entries and a fifth object.</summary>
    /// <param name="part">The claimed part; 0 for none.</param>
    /// <param name="catalogEntries">Extra catalog entries.</param>
    /// <param name="pageEntries">Extra page entries.</param>
    /// <param name="fifthObject">The text of object 5.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(int part, string catalogEntries, string pageEntries, string fifthObject) => MiniPdf.Build(
    [
        $"<< /Type /Catalog /Pages 2 0 R /Metadata 3 0 R {catalogEntries} >>",
        "<< /Type /Pages /Count 1 /Kids [4 0 R] >>",
        MiniPdf.Stream("/Type /Metadata /Subtype /XML", Accessibility.AccessibilityPdfs.Packet(part, true)),
        $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] {pageEntries} >>",
        fifthObject,
    ]);
}
