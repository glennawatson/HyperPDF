// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Security;
using HyperPdfLibrary.Tests.Syntax;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests for XMP packet shapes, the XMP and /Info combination, and metadata on pages and XObjects (ISO 32000-2 §14.3).</summary>
public sealed class XmpReadingTests
{
    /// <summary>The year in the sample dates.</summary>
    private const int SampleYear = 2024;

    /// <summary>The UTC offset in hours of the sample modification date.</summary>
    private const int OffsetHours = 2;

    /// <summary>The PDF/A part in the identification sample.</summary>
    private const int PdfAPartNumber = 3;

    /// <summary>The PDF/A revision year in the identification sample.</summary>
    private const int PdfARevisionYear = 2012;

    /// <summary>The PDF/UA part in the identification sample.</summary>
    private const int PdfUaPartNumber = 2;

    /// <summary>The width of a padding line in the xpacket sample.</summary>
    private const int PaddingWidth = 99;

    /// <summary>The number of padding lines in the xpacket sample.</summary>
    private const int PaddingLines = 40;

    /// <summary>The catalog's object number in the encrypted sample.</summary>
    private const int EncryptedPagesNumber = 2;

    /// <summary>The encryption dictionary's object number in the encrypted sample.</summary>
    private const int EncryptNumber = 3;

    /// <summary>The metadata stream's object number in the encrypted sample.</summary>
    private const int StreamNumber = 4;

    /// <summary>The /Size of the encrypted sample.</summary>
    private const int FileSize = 5;

    /// <summary>The form XObject's object number in the XObject sample.</summary>
    private const int FormNumber = 5;

    /// <summary>The image XObject's object number in the XObject sample.</summary>
    private const int ImageNumber = 6;

    /// <summary>The title in the padded packet sample.</summary>
    private const string PaddedTitle = "Padded";

    /// <summary>The title property used in the nesting sample.</summary>
    private const string KeptTitleProperty = "<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">Kept</rdf:li></rdf:Alt></dc:title>";

    /// <summary>The namespace of the resource reference structure.</summary>
    private const string ResourceRefNamespace = "http://ns.adobe.com/xap/1.0/sType/ResourceRef#";

    /// <summary>The sample page tree.</summary>
    private const string Pages = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";

    /// <summary>The sample page.</summary>
    private const string Page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] >>";

    /// <summary>A page tree without pages.</summary>
    private const string EmptyPages = "<< /Type /Pages /Kids [] /Count 0 >>";

    /// <summary>The nesting depth used to try the structure limit.</summary>
    private const int DeepNesting = 5_000;

    /// <summary>The number of entity levels in the expansion bomb.</summary>
    private const int BombLevels = 9;

    /// <summary>How many times each entity level repeats the one below it.</summary>
    private const int BombFanOut = 10;

    /// <summary>The longest the entity bomb may take to refuse, in milliseconds.</summary>
    private const int BombBudgetMilliseconds = 5_000;

    /// <summary>The XMP title property that differs from /Info.</summary>
    private const string XmpTitle = "XMP title";

    /// <summary>The /Info title.</summary>
    private const string InfoTitle = "Info title";

    /// <summary>The metadata stream entries.</summary>
    private const string MetadataStream = "/Type /Metadata /Subtype /XML";

    /// <summary>The packet header.</summary>
    private const string Header = "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">";

    /// <summary>The packet footer.</summary>
    private const string Footer = "</rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";

    /// <summary>The namespace declarations the sample descriptions use.</summary>
    private const string Namespaces =
        "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\" xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\" "
        + "xmlns:xmpMM=\"http://ns.adobe.com/xap/1.0/mm/\" xmlns:stRef=\"http://ns.adobe.com/xap/1.0/sType/ResourceRef#\" "
        + "xmlns:xmpRights=\"http://ns.adobe.com/xap/1.0/rights/\" xmlns:pdfx=\"http://ns.adobe.com/pdfx/1.3/\" "
        + "xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\" xmlns:pdfuaid=\"http://www.aiim.org/pdfua/ns/id/\" "
        + "xmlns:pdfaExtension=\"http://www.aiim.org/pdfa/ns/extension/\" xmlns:pdfaSchema=\"http://www.aiim.org/pdfa/ns/schema#\" "
        + "xmlns:pdfaProperty=\"http://www.aiim.org/pdfa/ns/property#\"";

    /// <summary>A PDF 2.0 file takes the XMP value over /Info; <c>GetInfo</c> still reports /Info alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Pdf20TakesXmpOverDifferingInfo()
    {
        var packet = Wrap($"""
            <rdf:Description rdf:about="" {Namespaces} pdf:Producer="XMP producer" xmp:CreatorTool="XMP tool" pdf:Keywords="k1, k2" pdf:Trapped="False">
            <dc:title><rdf:Alt><rdf:li xml:lang="x-default">{XmpTitle}</rdf:li></rdf:Alt></dc:title>
            <dc:creator><rdf:Seq><rdf:li>Ann</rdf:li><rdf:li>Bob</rdf:li></rdf:Seq></dc:creator>
            <dc:description><rdf:Alt><rdf:li xml:lang="x-default">XMP subject</rdf:li></rdf:Alt></dc:description>
            <xmp:CreateDate>2024-01-02T03:04:05Z</xmp:CreateDate>
            <xmp:ModifyDate>2024-05-06T07:08:09+02:00</xmp:ModifyDate>
            </rdf:Description>
            """);
        using var document = OpenWithInfo("/Version /2.0", packet);

        var raw = PdfDocumentMetadata.GetInfo(document);
        var resolved = PdfDocumentMetadata.GetResolvedInfo(document);

        await Assert.That(raw.Title).IsEqualTo(InfoTitle);
        await Assert.That(raw.Trapped).IsEqualTo("True");
        await Assert.That(resolved.Title).IsEqualTo(XmpTitle);
        await Assert.That(resolved.Author).IsEqualTo("Ann; Bob");
        await Assert.That(resolved.Subject).IsEqualTo("XMP subject");
        await Assert.That(resolved.Keywords).IsEqualTo("k1, k2");
        await Assert.That(resolved.Creator).IsEqualTo("XMP tool");
        await Assert.That(resolved.Producer).IsEqualTo("XMP producer");
        await Assert.That(resolved.Trapped).IsEqualTo("False");
        await Assert.That(resolved.Created!.Value.Year).IsEqualTo(SampleYear);
        await Assert.That(resolved.Modified!.Value.Offset).IsEqualTo(TimeSpan.FromHours(OffsetHours));
        await Assert.That(resolved.Version).IsEqualTo("2.0");
    }

    /// <summary>An earlier file keeps /Info and uses XMP only for entries /Info lacks.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EarlierVersionKeepsInfoAndFillsGapsFromXmp()
    {
        var packet = Wrap($"""
            <rdf:Description rdf:about="" {Namespaces} pdf:Producer="XMP producer">
            <dc:title><rdf:Alt><rdf:li xml:lang="x-default">{XmpTitle}</rdf:li></rdf:Alt></dc:title>
            <dc:subject><rdf:Bag><rdf:li>one</rdf:li><rdf:li>two</rdf:li></rdf:Bag></dc:subject>
            </rdf:Description>
            """);
        using var document = OpenWithInfo(string.Empty, packet);

        var resolved = PdfDocumentMetadata.GetResolvedInfo(document);

        await Assert.That(resolved.Title).IsEqualTo(InfoTitle);
        await Assert.That(resolved.Producer).IsEqualTo("Info producer");
        await Assert.That(resolved.Keywords).IsEqualTo("one; two");
        await Assert.That(resolved.Author).IsNull();
    }

    /// <summary>A document without XMP resolves to its /Info values.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentWithoutXmpResolvesToInfo()
    {
        using var document = PdfDocumentReader.Open(MiniPdf.BuildWithTrailer("/Info 4 0 R", Catalog(string.Empty), Pages, Page, $"<< /Title ({InfoTitle}) >>"), null);

        await Assert.That(PdfDocumentMetadata.GetResolvedInfo(document)).IsEqualTo(PdfDocumentMetadata.GetInfo(document));
    }

    /// <summary>Partial XMP dates read as the start of the period.</summary>
    /// <param name="text">The date text.</param>
    /// <param name="month">The expected month.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("2024", 1)]
    [Arguments("2024-03", 3)]
    [Arguments("2024-03-04", 3)]
    [Arguments("2024-03-04T05:06Z", 3)]
    public async Task PartialDatesAreRead(string text, int month)
    {
        var xmp = Parse(Wrap($"<rdf:Description rdf:about=\"\" {Namespaces}><xmp:ModifyDate>{text}</xmp:ModifyDate></rdf:Description>"));

        await Assert.That(xmp.ModifyDate!.Value.Year).IsEqualTo(SampleYear);
        await Assert.That(xmp.ModifyDate!.Value.Month).IsEqualTo(month);
    }

    /// <summary>The packet reads the same in UTF-8 (with a mark), UTF-16 and UTF-32, in both byte orders, with and without a mark.</summary>
    /// <param name="encodingName">The encoding.</param>
    /// <param name="bigEndian">Whether the encoding is big-endian.</param>
    /// <param name="mark">Whether a byte order mark is written.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("utf8", false, true)]
    [Arguments("utf16", false, true)]
    [Arguments("utf16", true, true)]
    [Arguments("utf16", false, false)]
    [Arguments("utf16", true, false)]
    [Arguments("utf32", false, true)]
    [Arguments("utf32", true, true)]
    [Arguments("utf32", false, false)]
    [Arguments("utf32", true, false)]
    public async Task PacketEncodingsAreRead(string encodingName, bool bigEndian, bool mark)
    {
        Encoding encoding = encodingName switch
        {
            "utf8" => new UTF8Encoding(mark),
            "utf16" => new UnicodeEncoding(bigEndian, mark),
            _ => new UTF32Encoding(bigEndian, mark),
        };
        var text = Wrap($"<rdf:Description rdf:about=\"\" {Namespaces}><dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">Título \U0001F4C4</rdf:li></rdf:Alt></dc:title></rdf:Description>");
        byte[] packet = [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];
        using var document = OpenWithBytes(packet);

        var xmp = PdfDocumentMetadata.GetXmp(document)!;

        await Assert.That(xmp.IsWellFormed).IsTrue();
        await Assert.That(xmp.Title).IsEqualTo("Título \U0001F4C4");
        await Assert.That(xmp.RawPacket).IsEquivalentTo(packet);
    }

    /// <summary>The xpacket padding between the end of the data and the end marker is ignored, and a packet without markers reads.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PacketPaddingAndMissingMarkersAreHandled()
    {
        var description = TitleDescription(PaddedTitle);
        var padding = string.Concat(Enumerable.Repeat($"{new string(' ', PaddingWidth)}\n", PaddingLines));
        var padded = $"{Header}{description}</rdf:RDF></x:xmpmeta>{padding}<?xpacket end=\"w\"?>";
        var bare = $"<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">{description}</rdf:RDF></x:xmpmeta>";

        await Assert.That(Parse(padded).Title).IsEqualTo(PaddedTitle);
        await Assert.That(Parse(padded).IsWellFormed).IsTrue();
        await Assert.That(Parse(bare).Title).IsEqualTo(PaddedTitle);
    }

    /// <summary>Property attributes, element properties and nested structures all read, including <c>rdf:parseType="Resource"</c>.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StructuresAreReadAsFields()
    {
        var xmp = Parse(Wrap($"""
            <rdf:Description rdf:about="" {Namespaces}>
            <xmpMM:DerivedFrom rdf:parseType="Resource"><stRef:documentID>uuid:derived</stRef:documentID><stRef:instanceID>xmp.iid:1</stRef:instanceID></xmpMM:DerivedFrom>
            <xmpMM:History><rdf:Seq>
            <rdf:li rdf:parseType="Resource"><stRef:instanceID>xmp.iid:a</stRef:instanceID></rdf:li>
            <rdf:li rdf:parseType="Resource"><stRef:instanceID>xmp.iid:b</stRef:instanceID></rdf:li>
            </rdf:Seq></xmpMM:History>
            <xmpMM:Ingredients><rdf:Bag><rdf:li><rdf:Description stRef:filePath="a.pdf"/></rdf:li></rdf:Bag></xmpMM:Ingredients>
            <xmpMM:Pantry stRef:alternatePaths="shorthand"/>
            <xmpRights:WebStatement rdf:resource="https://example.test/rights"/>
            </rdf:Description>
            """));

        await Assert.That(xmp.IsWellFormed).IsTrue();
        await Assert.That(xmp.GetValue(ResourceRefNamespace, "documentID")).IsEqualTo("uuid:derived");
        await Assert.That(xmp.GetValue(ResourceRefNamespace, "filePath")).IsEqualTo("a.pdf");
        await Assert.That(xmp.GetValue(ResourceRefNamespace, "alternatePaths")).IsEqualTo("shorthand");
        await Assert.That(xmp.WebStatement).IsEqualTo("https://example.test/rights");
        await Assert.That(xmp.GetValues(ResourceRefNamespace, "instanceID")).Contains("xmp.iid:1");
    }

    /// <summary>Language alternatives list the default first; a list without a default keeps its first entry first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LanguageAlternativesPutDefaultFirst()
    {
        var xmp = Parse(Wrap($"""
            <rdf:Description rdf:about="" {Namespaces}>
            <dc:title><rdf:Alt><rdf:li xml:lang="de">Titel</rdf:li><rdf:li xml:lang="en-AU">Title AU</rdf:li><rdf:li xml:lang="x-default">Title</rdf:li></rdf:Alt></dc:title>
            <dc:description><rdf:Alt><rdf:li xml:lang="fr">Description</rdf:li><rdf:li xml:lang="de">Beschreibung</rdf:li></rdf:Alt></dc:description>
            </rdf:Description>
            """));

        await Assert.That(xmp.GetValues(XmpMetadata.DublinCoreNamespace, "title")).IsEquivalentTo(["Title", "Titel", "Title AU"]);
        await Assert.That(xmp.Description).IsEqualTo("Description");
    }

    /// <summary>The rights, media management, PDF/X, PDF/A and PDF/UA namespaces and the PDF/A extension schema read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IdentificationAndRightsNamespacesAreRead()
    {
        var xmp = Parse(Wrap($"""
            <rdf:Description rdf:about="" {Namespaces} pdfaid:part="3" pdfaid:conformance="A" pdfaid:rev="2012" pdfuaid:part="2"
              pdfx:GTS_PDFXVersion="PDF/X-4" xmpMM:DocumentID="uuid:doc" xmpMM:InstanceID="uuid:inst" xmpMM:OriginalDocumentID="uuid:orig"
              xmpRights:Marked="True" pdf:PDFVersion="1.7">
            <dc:rights><rdf:Alt><rdf:li xml:lang="x-default">All rights reserved</rdf:li></rdf:Alt></dc:rights>
            <xmpRights:Owner><rdf:Bag><rdf:li>Owner A</rdf:li></rdf:Bag></xmpRights:Owner>
            <xmpRights:UsageTerms><rdf:Alt><rdf:li xml:lang="x-default">Terms</rdf:li></rdf:Alt></xmpRights:UsageTerms>
            <pdfaExtension:schemas><rdf:Bag><rdf:li rdf:parseType="Resource">
            <pdfaSchema:schema>Example</pdfaSchema:schema>
            <pdfaSchema:namespaceURI>http://example.test/ns/</pdfaSchema:namespaceURI>
            <pdfaSchema:prefix>ex</pdfaSchema:prefix>
            <pdfaSchema:property><rdf:Seq><rdf:li rdf:parseType="Resource">
            <pdfaProperty:name>Flag</pdfaProperty:name><pdfaProperty:valueType>Boolean</pdfaProperty:valueType>
            </rdf:li></rdf:Seq></pdfaSchema:property>
            </rdf:li></rdf:Bag></pdfaExtension:schemas>
            </rdf:Description>
            """));

        await Assert.That(xmp.PdfAPart).IsEqualTo(PdfAPartNumber);
        await Assert.That(xmp.PdfAConformance).IsEqualTo("A");
        await Assert.That(xmp.PdfARevision).IsEqualTo(PdfARevisionYear);
        await Assert.That(xmp.PdfUaPart).IsEqualTo(PdfUaPartNumber);
        await Assert.That(xmp.PdfXVersion).IsEqualTo("PDF/X-4");
        await Assert.That(xmp.DocumentId).IsEqualTo("uuid:doc");
        await Assert.That(xmp.InstanceId).IsEqualTo("uuid:inst");
        await Assert.That(xmp.OriginalDocumentId).IsEqualTo("uuid:orig");
        await Assert.That(xmp.RightsMarked).IsTrue();
        await Assert.That(xmp.RightsOwners).IsEquivalentTo(["Owner A"]);
        await Assert.That(xmp.UsageTerms).IsEqualTo("Terms");
        await Assert.That(xmp.Rights).IsEqualTo("All rights reserved");
        await Assert.That(xmp.PdfVersion).IsEqualTo("1.7");
        await Assert.That(xmp.GetValue(XmpMetadata.PdfASchemaNamespace, "prefix")).IsEqualTo("ex");
        await Assert.That(xmp.GetValue(XmpMetadata.PdfAPropertyNamespace, "valueType")).IsEqualTo("Boolean");
        await Assert.That(xmp.GetValue(XmpMetadata.PdfAPropertyNamespace, "name")).IsEqualTo("Flag");
    }

    /// <summary>An entity expansion bomb and an external entity are refused quickly and nothing is loaded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EntityBombIsRefusedInsideLimits()
    {
        var builder = new StringBuilder("<?xml version=\"1.0\"?><!DOCTYPE bomb [<!ENTITY e0 \"lol\">");
        for (var i = 1; i < BombLevels; i++)
        {
            var previous = $"&e{i - 1};";
            _ = builder.Append($"<!ENTITY e{i} \"{string.Concat(Enumerable.Repeat(previous, BombFanOut))}\">");
        }

        _ = builder.Append($"<!ENTITY ext SYSTEM \"file:///etc/passwd\">]><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">&e{BombLevels - 1};&ext;</x:xmpmeta>");
        var start = Stopwatch.GetTimestamp();

        var xmp = XmpParser.Parse(Encoding.UTF8.GetBytes(builder.ToString()), CancellationToken.None);

        await Assert.That(xmp.IsWellFormed).IsFalse();
        await Assert.That(xmp.Properties.Count).IsEqualTo(0);
        await Assert.That(Stopwatch.GetElapsedTime(start).TotalMilliseconds).IsLessThan(BombBudgetMilliseconds);
    }

    /// <summary>Malformed XML of several kinds is reported as not well formed without throwing.</summary>
    /// <param name="packet">The packet text.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("<x:xmpmeta")]
    [Arguments("not xml at all")]
    [Arguments("<a><b></a></b>")]
    [Arguments("<a>&undefined;</a>")]
    [Arguments("")]
    public async Task MalformedPacketsDoNotThrow(string packet)
    {
        var xmp = XmpParser.Parse(Encoding.UTF8.GetBytes(packet), CancellationToken.None);

        await Assert.That(xmp.IsWellFormed).IsFalse();
    }

    /// <summary>Structures nested far deeper than any real packet stop inside the nesting limit without overflowing the stack.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeepStructuresStopAtTheNestingLimit()
    {
        var open = string.Concat(Enumerable.Repeat("<dc:x rdf:parseType=\"Resource\">", DeepNesting));
        var close = string.Concat(Enumerable.Repeat("</dc:x>", DeepNesting));
        var xmp = Parse(Wrap($"<rdf:Description rdf:about=\"\" {Namespaces}>{KeptTitleProperty}{open}<dc:leaf>deep</dc:leaf>{close}</rdf:Description>"));

        await Assert.That(xmp.IsWellFormed).IsTrue();
        await Assert.That(xmp.Title).IsEqualTo("Kept");
        await Assert.That(xmp.GetValue(XmpMetadata.DublinCoreNamespace, "leaf")).IsNull();
    }

    /// <summary>A page, a form XObject and an image XObject each read their own metadata stream.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageAndXObjectMetadataAreRead()
    {
        using var document = PdfDocumentReader.Open(
            MiniPdf.Build(
                Catalog(string.Empty),
                Pages,
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] /Metadata 4 0 R /Resources << /XObject << /Fm 5 0 R /Im 6 0 R >> >> >>",
                TitleStream("Page"),
                MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 1 1] /Metadata 7 0 R", string.Empty),
                MiniPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Metadata 8 0 R", "x"),
                TitleStream("Form"),
                TitleStream("Image")),
            null);

        var page = PdfDocumentPages.GetPage(document, 0);
        var form = StructureDocuments.Dictionary(document, FormNumber);
        var image = StructureDocuments.Dictionary(document, ImageNumber);

        await Assert.That(PdfDocumentMetadata.GetXmp(document)).IsNull();
        await Assert.That(PdfDocumentMetadata.GetXmp(document, page)!.Title).IsEqualTo("Page");
        await Assert.That(PdfDocumentMetadata.ReadXmp(document, form)!.Title).IsEqualTo("Form");
        await Assert.That(PdfDocumentMetadata.ReadXmp(document, image)!.Title).IsEqualTo("Image");
    }

    /// <summary>An encrypted file with <c>/EncryptMetadata false</c> still reads its clear-text XMP.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EncryptedFileWithClearMetadataReadsXmp()
    {
        using var setup = EncryptionSetup.Revision4(false, new());
        var packet = Encoding.UTF8.GetBytes(Wrap(TitleDescription(XmpTitle)));
        var file = new RawPdf()
            .Object(1, Catalog("/Metadata 4 0 R"))
            .Object(EncryptedPagesNumber, EmptyPages)
            .Object(EncryptNumber, setup.EncryptText)
            .Stream(StreamNumber, MetadataStream, packet)
            .Table(FileSize, $"/Root 1 0 R /Encrypt {EncryptNumber} 0 R /ID [<{setup.FileIdHex}> <{setup.FileIdHex}>]")
            .ToArray();
        using var document = PdfDocumentReader.Open(file, null);

        await Assert.That(document.IsEncrypted).IsTrue();
        await Assert.That(PdfDocumentMetadata.GetXmp(document)!.Title).IsEqualTo(XmpTitle);
    }

    /// <summary>Wraps description markup in a complete packet.</summary>
    /// <param name="description">The description markup.</param>
    /// <returns>The packet text.</returns>
    private static string Wrap(string description) => Header + description + Footer;

    /// <summary>Parses packet text as UTF-8.</summary>
    /// <param name="packet">The packet text.</param>
    /// <returns>The metadata.</returns>
    private static XmpMetadata Parse(string packet) => XmpParser.Parse(Encoding.UTF8.GetBytes(packet), CancellationToken.None);

    /// <summary>Builds the sample catalog.</summary>
    /// <param name="entries">Extra catalog entries.</param>
    /// <returns>The catalog object body.</returns>
    private static string Catalog(string entries) => $"<< /Type /Catalog /Pages 2 0 R {entries} >>";

    /// <summary>Builds a metadata stream object holding only a default-language title.</summary>
    /// <param name="title">The title.</param>
    /// <returns>The stream object body.</returns>
    private static string TitleStream(string title) => MiniPdf.Stream(MetadataStream, Wrap(TitleDescription(title)));

    /// <summary>Builds a description holding only a default-language title.</summary>
    /// <param name="title">The title.</param>
    /// <returns>The description markup.</returns>
    private static string TitleDescription(string title) =>
        $"<rdf:Description rdf:about=\"\" {Namespaces}><dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">{title}</rdf:li></rdf:Alt></dc:title></rdf:Description>";

    /// <summary>Opens a document whose catalog metadata is the packet and whose /Info disagrees.</summary>
    /// <param name="catalogEntries">Extra catalog entries.</param>
    /// <param name="packet">The packet text.</param>
    /// <returns>The document.</returns>
    private static PdfDocument OpenWithInfo(string catalogEntries, string packet) =>
        PdfDocumentReader.Open(
            MiniPdf.BuildWithTrailer(
                "/Info 5 0 R",
                Catalog($"{catalogEntries} /Metadata 4 0 R"),
                Pages,
                Page,
                MiniPdf.Stream(MetadataStream, packet),
                $"<< /Title ({InfoTitle}) /Producer (Info producer) /Trapped /True >>"),
            null);

    /// <summary>Opens a document whose catalog metadata is the given bytes.</summary>
    /// <param name="packet">The packet bytes.</param>
    /// <returns>The document.</returns>
    private static PdfDocument OpenWithBytes(byte[] packet)
    {
        var file = new RawPdf()
            .Object(1, Catalog("/Metadata 4 0 R"))
            .Object(EncryptedPagesNumber, EmptyPages)
            .Object(EncryptNumber, "null")
            .Stream(StreamNumber, MetadataStream, packet)
            .Table(FileSize, "/Root 1 0 R");
        return PdfDocumentReader.Open(file.ToArray(), null);
    }
}
