// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks the namespace rules of ISO/TS 32005 clauses 5.3 and 5.4 and the conformance declaration of clause 4.</summary>
public sealed class StructureNamespaceUseTests
{
    /// <summary>The text in <see cref="DeclarationPacket"/> that stands for the declared address.</summary>
    private const string AddressToken = "ADDRESS";

    /// <summary>A declaration packet around one address, written the way the standard's example is.</summary>
    private const string DeclarationPacket =
        "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">"
        + "<rdf:Description rdf:about=\"\" xmlns:pdfd=\"http://pdfa.org/declarations/\"><pdfd:declarations><rdf:Bag><rdf:li rdf:parseType=\"Resource\">"
        + "<pdfd:conformsTo>" + AddressToken + "</pdfd:conformsTo></rdf:li></rdf:Bag></pdfd:declarations></rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";

    /// <summary>In a file that uses the PDF 2.0 namespace, a type both namespaces define belongs in it, and a type only it defines has to say so.</summary>
    /// <param name="type">The tag's type.</param>
    /// <param name="inPdf20Namespace">Whether the tag declares the PDF 2.0 namespace.</param>
    /// <param name="shared">The expected number of findings for a type both namespaces define.</param>
    /// <param name="undeclared">The expected number of findings for a type only the PDF 2.0 namespace defines.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("P", true, 0, 0)]
    [Arguments("P", false, 1, 0)]
    [Arguments("Aside", true, 0, 0)]
    [Arguments("Aside", false, 0, 1)]
    [Arguments("Art", false, 0, 0)]
    [Arguments("TOC", false, 0, 0)]
    public async Task TypesBelongInTheirNamespace(string type, bool inPdf20Namespace, int shared, int undeclared, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(type, inPdf20Namespace, true, string.Empty, null), null, cancellationToken);

        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(report.GetCount(PdfAccessibilityCode.SharedTypeNotInPdf20Namespace)).IsEqualTo(shared);
            await Assert.That(report.GetCount(PdfAccessibilityCode.Pdf20TypeWithoutNamespace)).IsEqualTo(undeclared);
        }
    }

    /// <summary>A file that uses only PDF 1.7 elements is exempt (clause 5.2), and so is a custom type a role map sends to a standard one.</summary>
    /// <param name="type">The tag's type.</param>
    /// <param name="pdf20Document">Whether the Document tag is in the PDF 2.0 namespace.</param>
    /// <param name="roleMap">The root's role map entry.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("P", false, "")]
    [Arguments("Aside", false, "")]
    [Arguments("Custom", true, "/RoleMap << /Custom /P >>")]
    public async Task ExemptionsAreHonoured(string type, bool pdf20Document, string roleMap, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(type, false, pdf20Document, roleMap, null), null, cancellationToken);

        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(report.GetCount(PdfAccessibilityCode.SharedTypeNotInPdf20Namespace) + report.GetCount(PdfAccessibilityCode.Pdf20TypeWithoutNamespace)).IsEqualTo(0);
    }

    /// <summary>A declaration that names ISO/TS 32005 is accepted at the address the standard gives and reported at any other.</summary>
    /// <param name="address">The declared address.</param>
    /// <param name="expected">The expected number of findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("https://pdfa.org/declarations#iso32005", 0)]
    [Arguments("http://pdfa.org/declarations#iso32005", 1)]
    [Arguments("https://pdfa.org/declarations/#iso32005", 1)]
    [Arguments("https://pdfa.org/declarations#ISO32005", 1)]
    [Arguments("https://pdfa.org/declarations#iso32004", 0)]
    public async Task DeclarationUsesTheAddressOfTheStandard(string address, int expected, CancellationToken cancellationToken)
    {
        var packet = DeclarationPacket.Replace(AddressToken, address, StringComparison.Ordinal);
        using var document = await PdfDocumentReader.OpenAsync(Build("P", true, true, string.Empty, packet), null, cancellationToken);

        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(report.GetCount(PdfAccessibilityCode.Iso32005ClaimMalformed)).IsEqualTo(expected);
    }

    /// <summary>Builds a file whose Document holds one tag.</summary>
    /// <param name="type">The tag's type.</param>
    /// <param name="tagInPdf20Namespace">Whether the tag declares the PDF 2.0 namespace.</param>
    /// <param name="documentInPdf20Namespace">Whether the Document tag declares the PDF 2.0 namespace.</param>
    /// <param name="roleMap">An entry for the structure tree root, such as a role map.</param>
    /// <param name="packet">The XMP packet, or <see langword="null"/> for none.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(string type, bool tagInPdf20Namespace, bool documentInPdf20Namespace, string roleMap, string? packet)
    {
        var documentNamespace = documentInPdf20Namespace ? "/NS 7 0 R" : string.Empty;
        var tagNamespace = tagInPdf20Namespace ? "/NS 7 0 R" : string.Empty;
        var namespaces = documentInPdf20Namespace || tagInPdf20Namespace ? "/Namespaces [7 0 R]" : string.Empty;
        var metadata = packet is null ? string.Empty : "/Metadata 8 0 R";
        List<string> objects =
        [
            $"<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> /Lang (en-US) {metadata} >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
            $"<< /Type /StructElem /S /Document {documentNamespace} /P 5 0 R /K [6 0 R] >>",
            $"<< /Type /StructTreeRoot /K 4 0 R {namespaces} {roleMap} >>",
            $"<< /Type /StructElem /S /{type} {tagNamespace} /P 4 0 R /Pg 3 0 R /K 0 >>",
            "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>",
        ];
        if (packet is not null)
        {
            objects.Add(MiniPdf.Stream("/Type /Metadata /Subtype /XML", packet));
        }

        return MiniPdf.Build([.. objects]);
    }
}
