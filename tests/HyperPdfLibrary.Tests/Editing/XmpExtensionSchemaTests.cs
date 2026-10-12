// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Metadata;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for <see cref="XmpExtensionSchemas"/>, which describes custom XMP properties for PDF/A parts 1 to 3.</summary>
public sealed class XmpExtensionSchemaTests
{
    /// <summary>The namespace of the described property.</summary>
    private const string Namespace = "https://example.com/ns/test/1.0/";

    /// <summary>Gets a packet with no extension schemas.</summary>
    private static string Plain => "<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
        + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"uuid:1\" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\">"
        + "<pdfaid:part>2</pdfaid:part></rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";

    /// <summary>Gets a packet whose container already describes another schema.</summary>
    private static string WithContainer => "<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
        + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\" xmlns:pdfaExtension=\"http://www.aiim.org/pdfa/ns/extension/\""
        + " xmlns:pdfaSchema=\"http://www.aiim.org/pdfa/ns/schema#\"><pdfaExtension:schemas><rdf:Bag><rdf:li rdf:parseType=\"Resource\">"
        + "<pdfaSchema:namespaceURI>https://example.com/ns/other/</pdfaSchema:namespaceURI><pdfaSchema:prefix>other</pdfaSchema:prefix>"
        + "</rdf:li></rdf:Bag></pdfaExtension:schemas></rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";

    /// <summary>Gets the property described.</summary>
    private static XmpExtensionProperty Property => new("Test schema", Namespace, "test", "Flag", "Boolean", "A test flag.");

    /// <summary>A packet without a container gets one, in a description with the packet's own subject.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddsAContainerWithTheSameSubject()
    {
        var packet = XmpExtensionSchemas.Describe(Encoding.UTF8.GetBytes(Plain), Property);
        var text = Encoding.UTF8.GetString(packet!);
        await Assert.That(XmpEditor.IsWellFormed(packet!)).IsTrue();
        await Assert.That(text).Contains($"<pdfaSchema:namespaceURI>{Namespace}</pdfaSchema:namespaceURI>");
        await Assert.That(text).Contains("<pdfaProperty:valueType>Boolean</pdfaProperty:valueType>");
        await Assert.That(text).Contains("<pdfaProperty:category>external</pdfaProperty:category>");
        await Assert.That(text.Split("rdf:about=\"uuid:1\"").Length).IsEqualTo(text.Split("<rdf:Description").Length);
        await Assert.That(XmpParser.Parse(packet!).PdfAPart).IsEqualTo(Conformance.PdfAFixtureLevel.Part2);
    }

    /// <summary>A packet with a container gets one more schema in the same bag, so the property is not repeated.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddsToTheExistingContainer()
    {
        var text = Encoding.UTF8.GetString(XmpExtensionSchemas.Describe(Encoding.UTF8.GetBytes(WithContainer), Property)!);
        await Assert.That(text.IndexOf("<pdfaExtension:schemas>", StringComparison.Ordinal)).IsEqualTo(text.LastIndexOf("<pdfaExtension:schemas>", StringComparison.Ordinal));
        await Assert.That(text).Contains(Namespace);
        await Assert.That(text).Contains("https://example.com/ns/other/");
    }

    /// <summary>A packet that already describes the namespace is returned as it is.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LeavesADescribedNamespaceAlone()
    {
        var described = XmpExtensionSchemas.Describe(Encoding.UTF8.GetBytes(Plain), Property)!;
        await Assert.That(XmpExtensionSchemas.Describe(described, Property)).IsSameReferenceAs(described);
    }

    /// <summary>A UTF-16 packet is not described, so the caller leaves the custom property out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesAWidePacket()
    {
        byte[] wide = [.. Encoding.BigEndianUnicode.GetPreamble(), .. Encoding.BigEndianUnicode.GetBytes(Plain)];
        await Assert.That(XmpExtensionSchemas.Describe(wide, Property)).IsNull();
    }
}
