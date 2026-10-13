// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks the document properties the viewer shows when a file keeps them in XMP as well as in /Info (ISO 32000-2 §14.3.3).</summary>
public sealed class DocumentMetadataTests
{
    /// <summary>The year of the XMP creation date.</summary>
    private const int CreateYear = 2024;

    /// <summary>The title in the XMP packet.</summary>
    private const string XmpTitle = "XMP title";

    /// <summary>The title in /Info.</summary>
    private const string InfoTitle = "Info title";

    /// <summary>The packet: a title, two creators and a creation date.</summary>
    private const string Packet = """
        <x:xmpmeta xmlns:x="adobe:ns:meta/"><rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
        <rdf:Description rdf:about="" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:xmp="http://ns.adobe.com/xap/1.0/">
        <dc:title><rdf:Alt><rdf:li xml:lang="x-default">XMP title</rdf:li></rdf:Alt></dc:title>
        <dc:creator><rdf:Seq><rdf:li>Ann</rdf:li><rdf:li>Bob</rdf:li></rdf:Seq></dc:creator>
        <xmp:CreateDate>2024-01-02T03:04:05Z</xmp:CreateDate>
        </rdf:Description></rdf:RDF></x:xmpmeta>
        """;

    /// <summary>A PDF 2.0 file shows its XMP title, authors and creation date; the viewer and the library agree.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Pdf20ShowsXmpProperties()
    {
        using var pair = new EnginePair(Build("/Version /2.0"));

        var metadata = pair.HyperPdf.GetMetadata();

        await Assert.That(metadata.Title).IsEqualTo(XmpTitle);
        await Assert.That(metadata.Author).IsEqualTo("Ann; Bob");
        await Assert.That(metadata.Created!.Value.Year).IsEqualTo(CreateYear);
        await Assert.That(metadata.FormatVersion).IsEqualTo("2.0");
    }

    /// <summary>A PDF 1.7 file keeps its /Info title and takes only the missing author from XMP.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Pdf17KeepsInfoAndFillsGaps()
    {
        using var pair = new EnginePair(Build(string.Empty));

        var metadata = pair.HyperPdf.GetMetadata();

        await Assert.That(metadata.Title).IsEqualTo(InfoTitle);
        await Assert.That(metadata.Author).IsEqualTo("Ann; Bob");
    }

    /// <summary>Builds a one-page file with /Info and an XMP packet that differ.</summary>
    /// <param name="catalogEntries">Extra catalog entries.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(string catalogEntries) => MiniPdf.BuildWithTrailer(
        "/Info 5 0 R",
        $"<< /Type /Catalog /Pages 2 0 R /Metadata 4 0 R {catalogEntries} >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
        MiniPdf.Stream("/Type /Metadata /Subtype /XML", Packet),
        $"<< /Title ({InfoTitle}) >>");
}
