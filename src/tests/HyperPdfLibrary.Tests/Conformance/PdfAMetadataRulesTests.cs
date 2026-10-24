// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Tests for the PDF/A metadata rules: packets, identification, properties, extension schemas and the Info crosswalk.</summary>
public sealed class PdfAMetadataRulesTests
{
    /// <summary>The identification namespace declaration.</summary>
    private const string IdNamespace = "xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\"";

    /// <summary>The Dublin Core namespace declaration.</summary>
    private const string DcNamespace = "xmlns:dc=\"http://purl.org/dc/elements/1.1/\"";

    /// <summary>The XMP basic namespace declaration.</summary>
    private const string XmpNamespace = "xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\"";

    /// <summary>The PDF/A-2b identification.</summary>
    private const string Claim2B = "<pdfaid:part>2</pdfaid:part><pdfaid:conformance>B</pdfaid:conformance>";

    /// <summary>The PDF/A-1b identification.</summary>
    private const string Claim1B = "<pdfaid:part>1</pdfaid:part><pdfaid:conformance>B</pdfaid:conformance>";

    /// <summary>A default title of "Report".</summary>
    private const string Title = "<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">Report</rdf:li></rdf:Alt></dc:title>";

    /// <summary>A property in a namespace no predefined schema covers.</summary>
    private const string Thing = "<ex:Thing>value</ex:Thing>";

    /// <summary>The dictionary entries of a metadata stream.</summary>
    private const string MetadataEntries = "/Type /Metadata /Subtype /XML";

    /// <summary>The namespaces of the extension schema description schemas and an example schema.</summary>
    private const string ExtensionNamespaces = "xmlns:ex=\"http://example.com/ns/\" xmlns:pdfaExtension=\"http://www.aiim.org/pdfa/ns/extension/\""
        + " xmlns:pdfaSchema=\"http://www.aiim.org/pdfa/ns/schema#\" xmlns:pdfaProperty=\"http://www.aiim.org/pdfa/ns/property#\"";

    /// <summary>The start of a packet up to the description's namespaces.</summary>
    private const string PacketStart = "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\"";

    /// <summary>The end of a packet after the description's properties.</summary>
    private const string PacketEnd = "</rdf:Description></rdf:RDF></x:xmpmeta>";

    /// <summary>The clause of the PDF/A-2 packet rules.</summary>
    private const string PacketClause = "6.6.2.1";

    /// <summary>The clause of the PDF/A-2 identification rules.</summary>
    private const string IdentificationClause = "6.6.4";

    /// <summary>The clause of the PDF/A-2 property rules.</summary>
    private const string PropertyClause = "6.6.2.3.1";

    /// <summary>The clause of the PDF/A-2 extension schema field rules.</summary>
    private const string ExtensionClause = "6.6.2.3.3";

    /// <summary>The clause of the PDF/A-1 Info crosswalk.</summary>
    private const string CrosswalkClause = "6.7.3";

    /// <summary>The part claimed by a file checked against another part.</summary>
    private const int OtherPart = 3;

    /// <summary>The part of PDF/A-2b.</summary>
    private const int Part2 = 2;

    /// <summary>A file whose catalog packet is well-formed and identifies its level passes every metadata rule.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WellFormedClaimPassesMetadataRules()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.AttachXmp(pdf, $"{IdNamespace} {DcNamespace}", $"{Claim2B}{Title}");

        var report = Validate(pdf.ToBytes(), PdfALevel.PdfA2B);

        await Assert.That(Array.Exists(report.Failed, static rule => rule.Clause.StartsWith("6.6.", StringComparison.Ordinal))).IsFalse();
    }

    /// <summary>A packet that is not well-formed XML breaks the serialization rule.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MalformedPacketFailsSerialization()
    {
        var pdf = ConformancePdf.Page();
        AttachCatalogStream(pdf, "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF");

        var report = Validate(pdf.ToBytes(), PdfALevel.PdfA2B);

        await Assert.That(Fails(report, PacketClause, "4")).IsTrue();
        await Assert.That(Fails(report, IdentificationClause, "1")).IsTrue();
    }

    /// <summary>An identification for another part breaks the part rule.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrongPartFailsIdentification()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, OtherPart, "B", null);

        var report = Validate(pdf.ToBytes(), PdfALevel.PdfA2B);

        await Assert.That(Fails(report, IdentificationClause, "2")).IsTrue();
        await Assert.That(Fails(report, IdentificationClause, "3")).IsFalse();
    }

    /// <summary>A <c>bytes</c> attribute in the packet header is reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BytesAttributeInHeaderFails()
    {
        const string header = "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\" bytes=\"1024\"?>";
        var pdf = ConformancePdf.Page();
        AttachCatalogStream(pdf, $"{header}{PacketStart} {IdNamespace}>{Claim2B}{PacketEnd}<?xpacket end=\"w\"?>");

        var report = Validate(pdf.ToBytes(), PdfALevel.PdfA2B);

        await Assert.That(Fails(report, PacketClause, "2")).IsTrue();
        await Assert.That(Fails(report, PacketClause, "3")).IsFalse();
    }

    /// <summary>A property no schema defines fails both property rules.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndefinedPropertyFails()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.AttachXmp(pdf, $"{IdNamespace} xmlns:ex=\"http://example.com/ns/\"", $"{Claim2B}{Thing}");

        var report = Validate(pdf.ToBytes(), PdfALevel.PdfA2B);

        await Assert.That(Fails(report, PropertyClause, "1")).IsTrue();
        await Assert.That(Fails(report, PropertyClause, "2")).IsTrue();
        await Assert.That(Array.Find(report.Failed, static rule => rule.Clause == PropertyClause)!.Context).Contains("ex:Thing");
    }

    /// <summary>A predefined property whose value does not match its type fails the value type rule only.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrongValueTypeFails()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.AttachXmp(pdf, $"{IdNamespace} {XmpNamespace}", $"{Claim2B}<xmp:CreateDate>yesterday</xmp:CreateDate>");

        var report = Validate(pdf.ToBytes(), PdfALevel.PdfA2B);

        await Assert.That(Fails(report, PropertyClause, "1")).IsFalse();
        await Assert.That(Fails(report, PropertyClause, "2")).IsTrue();
    }

    /// <summary>An extension schema defines a property, so it passes the property rules.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtensionSchemaDefinesProperty()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.AttachXmp(pdf, $"{IdNamespace} {ExtensionNamespaces}", $"{Claim2B}{Extension("<pdfaProperty:category>internal</pdfaProperty:category>")}{Thing}");

        var report = Validate(pdf.ToBytes(), PdfALevel.PdfA2B);

        await Assert.That(Fails(report, PropertyClause, "1")).IsFalse();
        await Assert.That(Fails(report, PropertyClause, "2")).IsFalse();
        await Assert.That(Array.Exists(report.Failed, static rule => rule.Clause == ExtensionClause)).IsFalse();
    }

    /// <summary>A property description without a category breaks the category rule.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtensionPropertyWithoutCategoryFails()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.AttachXmp(pdf, $"{IdNamespace} {ExtensionNamespaces}", $"{Claim2B}{Extension(string.Empty)}{Thing}");

        var report = Validate(pdf.ToBytes(), PdfALevel.PdfA2B);

        await Assert.That(Fails(report, ExtensionClause, "9")).IsTrue();
        await Assert.That(Fails(report, ExtensionClause, "7")).IsFalse();
    }

    /// <summary>PDF/A-2 checks metadata streams outside the catalog too; PDF/A-1 does not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageMetadataIsCheckedFromPart2()
    {
        const string page = $"{PacketStart} xmlns:ex=\"http://example.com/ns/\">{Thing}{PacketEnd}";
        var pdf = ConformancePdf.Page();
        ConformancePdf.AttachXmp(pdf, IdNamespace, Claim2B);
        var number = pdf.AddStream(MetadataEntries, page);
        pdf.PageEntries += string.Create(CultureInfo.InvariantCulture, $" /Metadata {number} 0 R");
        var bytes = pdf.ToBytes();

        var report = Validate(bytes, PdfALevel.PdfA2B);
        var partOne = Validate(bytes, PdfALevel.PdfA1B);

        await Assert.That(Fails(report, PropertyClause, "1")).IsTrue();
        await Assert.That(Array.Find(report.Failed, static rule => rule.Clause == PropertyClause)!.Context).Contains("object");
        await Assert.That(Fails(partOne, "6.7.9", "2")).IsFalse();
    }

    /// <summary>A PDF/A-1 Info title that differs from <c>dc:title</c> breaks the crosswalk; a matching one does not.</summary>
    /// <param name="title">The Info title.</param>
    /// <param name="fails">Whether the crosswalk rule fails.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Report", false)]
    [Arguments("Other", true)]
    public async Task InfoTitleMatchesXmp(string title, bool fails)
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.AttachXmp(pdf, $"{IdNamespace} {DcNamespace}", $"{Claim1B}{Title}");
        var info = pdf.AddObject($"<< /Title ({title}) >>");

        var report = Validate(WithInfo(pdf, info), PdfALevel.PdfA1B);

        await Assert.That(Fails(report, CrosswalkClause, "2")).IsEqualTo(fails);
    }

    /// <summary>The crosswalk compares dates as instants, so the same moment in two time zones matches.</summary>
    /// <param name="infoDate">The Info creation date.</param>
    /// <param name="fails">Whether the crosswalk rule fails.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("D:20240131120000+10'00'", false)]
    [Arguments("D:20240131020000Z", false)]
    [Arguments("D:20240131020000+01'00'", true)]
    public async Task InfoCreationDateComparesInstants(string infoDate, bool fails)
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.AttachXmp(pdf, $"{IdNamespace} {XmpNamespace}", $"{Claim1B}<xmp:CreateDate>2024-01-31T12:00:00+10:00</xmp:CreateDate>");
        var info = pdf.AddObject($"<< /CreationDate ({infoDate}) >>");

        var report = Validate(WithInfo(pdf, info), PdfALevel.PdfA1B);

        await Assert.That(Fails(report, CrosswalkClause, "1")).IsEqualTo(fails);
    }

    /// <summary>XMP dates read as the XMP toolkit reads them.</summary>
    /// <param name="text">The date text.</param>
    /// <param name="valid">Whether it reads as a date.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("2024", true)]
    [Arguments("2024-01-31T12:00:00.5+10:00", true)]
    [Arguments("2024-01-31T12Z", true)]
    [Arguments("", true)]
    [Arguments("2024-01-31 12:00", false)]
    [Arguments("yesterday", false)]
    [Arguments("2024-01-31T12:00:00Zjunk", false)]
    public async Task XmpDatesRead(string text, bool valid) => await Assert.That(PdfAXmpDates.IsDate(text)).IsEqualTo(valid);

    /// <summary>A packet the XML reader rejects is read after the toolkit's repair of control characters.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ControlCharactersAreRepaired()
    {
        var packet = "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\""u8
            + " xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:format>a\u0001b</dc:format></rdf:Description></rdf:RDF></x:xmpmeta>"u8;

        var read = PdfAXmpPacketReader.Read(packet.ToArray());

        await Assert.That(read.IsWellFormed).IsTrue();
        await Assert.That(read.Property("http://purl.org/dc/elements/1.1/", "format")!.Value).IsEqualTo("a b");
    }

    /// <summary>Validation stops when cancelled.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidationObservesCancellation()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, Part2, "B", null);
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(async () => await PdfAValidator.ValidateAsync(document, PdfALevel.PdfA2B, cancellation.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Builds an extension schema container with one property, <c>ex:Thing</c> of type Text.</summary>
    /// <param name="category">The category element, or empty to leave it out.</param>
    /// <returns>The property element.</returns>
    private static string Extension(string category) =>
        "<pdfaExtension:schemas><rdf:Bag><rdf:li rdf:parseType=\"Resource\">"
        + "<pdfaSchema:schema>Example</pdfaSchema:schema><pdfaSchema:namespaceURI>http://example.com/ns/</pdfaSchema:namespaceURI><pdfaSchema:prefix>ex</pdfaSchema:prefix>"
        + "<pdfaSchema:property><rdf:Seq><rdf:li rdf:parseType=\"Resource\"><pdfaProperty:name>Thing</pdfaProperty:name><pdfaProperty:valueType>Text</pdfaProperty:valueType>"
        + $"{category}<pdfaProperty:description>A thing</pdfaProperty:description></rdf:li></rdf:Seq></pdfaSchema:property>"
        + "</rdf:li></rdf:Bag></pdfaExtension:schemas>";

    /// <summary>Adds a metadata stream with the given text to the catalog.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="packet">The stream text.</param>
    private static void AttachCatalogStream(RenderTestPdf pdf, string packet)
    {
        var number = pdf.AddStream(MetadataEntries, packet);
        pdf.CatalogEntries += string.Create(CultureInfo.InvariantCulture, $" /Metadata {number} 0 R");
    }

    /// <summary>Writes a file with a document information dictionary in its trailer.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="info">The object number of the dictionary.</param>
    /// <returns>The file bytes.</returns>
    private static byte[] WithInfo(RenderTestPdf pdf, int info)
    {
        var text = Encoding.Latin1.GetString(pdf.ToBytes()).Replace("/Root 1 0 R", string.Create(CultureInfo.InvariantCulture, $"/Root 1 0 R /Info {info} 0 R"), StringComparison.Ordinal);
        return Encoding.Latin1.GetBytes(text);
    }

    /// <summary>Validates a file at a level.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="level">The level.</param>
    /// <returns>The report.</returns>
    private static PdfAValidationReport Validate(byte[] bytes, PdfALevel level)
    {
        using var document = PdfDocumentReader.Open(bytes, null);
        return PdfAValidator.Validate(document, level);
    }

    /// <summary>Determines whether a report lists a rule as failed.</summary>
    /// <param name="report">The report.</param>
    /// <param name="clause">The clause.</param>
    /// <param name="test">The test number.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool Fails(PdfAValidationReport report, string clause, string test) =>
        Array.Exists(report.Failed, rule => rule.Clause == clause && rule.Test == test);
}
