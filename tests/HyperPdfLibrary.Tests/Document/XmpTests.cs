// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests for XMP metadata reading.</summary>
public sealed class XmpTests
{
    /// <summary>The year in the sample creation date.</summary>
    private const int CreateYear = 2024;

    /// <summary>The PDF/A part in the sample packet.</summary>
    private const int PdfAPart = 2;

    /// <summary>The title in the sample packet.</summary>
    private const string SampleTitle = "Title";

    /// <summary>The processing instruction that starts the sample packet.</summary>
    private const string PacketStart = "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>";

    /// <summary>The marker that ends the title property.</summary>
    private const string TitleEnd = "</dc:title>";

    /// <summary>The metadata catalog entry.</summary>
    private const string MetadataEntry = "/Metadata 4 0 R";

    /// <summary>The metadata stream entries.</summary>
    private const string MetadataStream = "/Type /Metadata /Subtype /XML";

    /// <summary>The whole sample packet.</summary>
    private const string Packet = PacketStart + Body;

    /// <summary>The sample packet body, without the packet processing instruction.</summary>
    private const string Body = """
        <x:xmpmeta xmlns:x="adobe:ns:meta/">
        <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
        <rdf:Description rdf:about=""
          xmlns:dc="http://purl.org/dc/elements/1.1/"
          xmlns:pdf="http://ns.adobe.com/pdf/1.3/"
          xmlns:xmp="http://ns.adobe.com/xap/1.0/"
          xmlns:pdfaid="http://www.aiim.org/pdfa/ns/id/"
          xmlns:pdfuaid="http://www.aiim.org/pdfua/ns/id/"
          pdf:Producer="Test Producer" pdfaid:part="2" pdfaid:conformance="B">
        <dc:title><rdf:Alt><rdf:li xml:lang="fr">Titre</rdf:li><rdf:li xml:lang="x-default">Title</rdf:li></rdf:Alt></dc:title>
        <dc:creator><rdf:Seq><rdf:li>Ann</rdf:li><rdf:li>Bob</rdf:li></rdf:Seq></dc:creator>
        <dc:subject><rdf:Bag><rdf:li>a</rdf:li><rdf:li>b</rdf:li></rdf:Bag></dc:subject>
        <xmp:CreateDate>2024-01-02T03:04:05Z</xmp:CreateDate>
        <xmp:CreatorTool>Tool</xmp:CreatorTool>
        <pdfuaid:part>1</pdfuaid:part>
        </rdf:Description>
        </rdf:RDF>
        </x:xmpmeta>
        <?xpacket end="w"?>
        """;

    /// <summary>The common properties are read, with the default language title first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CommonPropertiesAreRead()
    {
        using var document = OpenWith(Packet);
        var xmp = PdfDocumentMetadata.GetXmp(document)!;

        await Assert.That(xmp.IsWellFormed).IsTrue();
        await Assert.That(xmp.Title).IsEqualTo(SampleTitle);
        await Assert.That(xmp.Creators).IsEquivalentTo(["Ann", "Bob"]);
        await Assert.That(xmp.Subjects).IsEquivalentTo(["a", "b"]);
        await Assert.That(xmp.Producer).IsEqualTo("Test Producer");
        await Assert.That(xmp.CreatorTool).IsEqualTo("Tool");
        await Assert.That(xmp.CreateDate!.Value.Year).IsEqualTo(CreateYear);
        await Assert.That(xmp.PdfAPart).IsEqualTo(PdfAPart);
        await Assert.That(xmp.PdfAConformance).IsEqualTo("B");
        await Assert.That(xmp.PdfUaPart).IsEqualTo(1);
        await Assert.That(Encoding.Latin1.GetString(xmp.RawPacket)).IsEqualTo(Packet);
    }

    /// <summary>A UTF-16 packet with a byte order mark reads the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Utf16PacketIsRead()
    {
        var preamble = Encoding.Unicode.GetPreamble();
        var body = Encoding.Unicode.GetBytes(Body);
        using var document = OpenWith(Encoding.Latin1.GetString([.. preamble, .. body]));

        await Assert.That(PdfDocumentMetadata.GetXmp(document)!.Title).IsEqualTo(SampleTitle);
    }

    /// <summary>A damaged packet keeps the properties read before the damage and reports itself as not well formed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedPacketKeepsEarlierProperties()
    {
        var cut = Packet.IndexOf(TitleEnd, StringComparison.Ordinal) + TitleEnd.Length;
        using var document = OpenWith(Packet[..cut]);
        var xmp = PdfDocumentMetadata.GetXmp(document)!;

        await Assert.That(xmp.IsWellFormed).IsFalse();
        await Assert.That(xmp.Title).IsEqualTo(SampleTitle);
        await Assert.That(xmp.Creators.Length).IsEqualTo(0);
    }

    /// <summary>A DTD is refused rather than expanded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DoctypeIsRefused()
    {
        using var document = OpenWith("<!DOCTYPE x [<!ENTITY e \"boom\">]><x/>");

        await Assert.That(PdfDocumentMetadata.GetXmp(document)!.IsWellFormed).IsFalse();
    }

    /// <summary>A page's metadata stream is read, and a document without metadata gives null.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageMetadataIsRead()
    {
        using var document = StructureDocuments.OpenPage(string.Empty, MetadataEntry, MiniPdf.Stream(MetadataStream, Packet));

        await Assert.That(PdfDocumentMetadata.GetXmp(document)).IsNull();
        await Assert.That(PdfDocumentMetadata.GetXmp(document, PdfDocumentPages.GetPage(document, 0))!.Title).IsEqualTo(SampleTitle);
    }

    /// <summary>Opens a document whose catalog metadata stream holds a packet.</summary>
    /// <param name="packet">The packet bytes, one per character.</param>
    /// <returns>The document.</returns>
    private static PdfDocument OpenWith(string packet) => StructureDocuments.Open(MetadataEntry, MiniPdf.Stream(MetadataStream, packet));
}
