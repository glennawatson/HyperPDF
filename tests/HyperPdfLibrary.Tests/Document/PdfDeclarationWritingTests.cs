// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Tests.Editing;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Checks that PDF Declarations are written as the specification's tables say, and refused when a value is not allowed.</summary>
public sealed class PdfDeclarationWritingTests
{
    /// <summary>A profile address.</summary>
    private const string Profile = "http://pdfa.org/declarations/#hipaa";

    /// <summary>A second profile address.</summary>
    private const string OtherProfile = "https://example.org/profiles/privacy?edition=2&lang=en";

    /// <summary>The number of declarations after two different ones are added.</summary>
    private const int Two = 2;

    /// <summary>A declaration with its claim data survives saving and reading, in element form, and undo removes it.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeclarationRoundTripsAndUndoes(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(Build(string.Empty), null);
        var claim = new PdfDeclarationClaim("Validator & Co", "2019-01-17", "Certified <A>", "http://pdfa.org/report67890.html");
        var declaration = new PdfDeclaration(Profile, [claim]);

        PdfDocumentDeclarations.Add(document, declaration);

        using var reopened = PdfDocumentReader.Open(EditChecks.SaveIncremental(document), null);
        var read = PdfDocumentMetadata.GetXmp(reopened)!.Declarations;
        await Assert.That(read.Length).IsEqualTo(1);
        await Assert.That(read[0].ConformsTo).IsEqualTo(Profile);
        await Assert.That(read[0].ClaimData[0]).IsEqualTo(claim);
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentMetadata.GetXmp(document)!.Declarations.Length).IsEqualTo(0);
    }

    /// <summary>A second profile joins the bag, and the same profile twice is written once.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecondProfileJoinsAndRepeatIsIgnored(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(Build(string.Empty), null);

        PdfDocumentDeclarations.Add(document, new(Profile, []));
        PdfDocumentDeclarations.Add(document, new(OtherProfile, []));
        PdfDocumentDeclarations.Add(document, new(Profile, []));

        using var reopened = PdfDocumentReader.Open(EditChecks.SaveIncremental(document), null);
        var read = PdfDocumentMetadata.GetXmp(reopened)!.Declarations;
        await Assert.That(read.Length).IsEqualTo(Two);
        string[] profiles = [.. read.Select(static d => d.ConformsTo!)];
        await Assert.That(profiles).IsEquivalentTo([Profile, OtherProfile]);
    }

    /// <summary>Removing a declaration takes out that one, with its claim data, keeps the others, survives saving, and undo brings it back.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovingOneDeclarationKeepsTheOthers(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(Build(string.Empty), null);
        PdfDocumentDeclarations.Add(document, new(Profile, [new("Validator", "2019-01-17", null, null)]));
        PdfDocumentDeclarations.Add(document, new(OtherProfile, []));

        var removed = PdfDocumentDeclarations.Remove(document, Profile);
        var again = PdfDocumentDeclarations.Remove(document, Profile);

        using var reopened = PdfDocumentReader.Open(EditChecks.SaveIncremental(document), null);
        var read = PdfDocumentMetadata.GetXmp(reopened)!.Declarations;
        using (Assert.Multiple())
        {
            await Assert.That(removed).IsTrue();
            await Assert.That(again).IsFalse();
            await Assert.That(read.Length).IsEqualTo(1);
            await Assert.That(read[0].ConformsTo).IsEqualTo(OtherProfile);
        }

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentMetadata.GetXmp(document)!.Declarations.Length).IsEqualTo(Two);
    }

    /// <summary>A file with no XMP metadata gets a packet that holds the declaration.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task FileWithoutMetadataGetsAPacket(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(MiniPdf.Build("<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Count 0 /Kids [] >>"), null);

        PdfDocumentDeclarations.Add(document, new(Profile, []));

        using var reopened = PdfDocumentReader.Open(EditChecks.SaveIncremental(document), null);
        await Assert.That(PdfDocumentMetadata.GetXmp(reopened)!.Declarations.Single().ConformsTo).IsEqualTo(Profile);
    }

    /// <summary>Values the specification does not allow are refused before anything changes.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="date">The claim date, or null.</param>
    /// <param name="report">The claim report, or null.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("not an address", null, null)]
    [Arguments("relative/path", null, null)]
    [Arguments(Profile, "yesterday", null)]
    [Arguments(Profile, "2019-13-45", null)]
    [Arguments(Profile, null, "report.html")]
    [Arguments(Profile, null, "#other=x")]
    public async Task InvalidValuesAreRefused(string profile, string? date, string? report)
    {
        using var document = PdfDocumentReader.Open(Build(string.Empty), null);
        var declaration = new PdfDeclaration(profile, [new(null, date, null, report)]);

        await Assert.That(() => PdfDocumentDeclarations.Add(document, declaration)).Throws<ArgumentException>();
        await Assert.That(PdfDocumentMetadata.GetXmp(document)!.Declarations.Length).IsEqualTo(0);
    }

    /// <summary>A date with a time and an embedded-file report are accepted.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DatesWithTimeAndEmbeddedReportsAreAccepted(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(Build(string.Empty), null);
        var claim = new PdfDeclarationClaim(null, "2026-10-19T09:30:00+10:00", null, "#ef=report.pdf");

        PdfDocumentDeclarations.Add(document, new(Profile, [claim]));

        await Assert.That(PdfDocumentMetadata.GetXmp(document)!.Declarations.Single().ClaimData.Single()).IsEqualTo(claim);
    }

    /// <summary>A PDF/A file is given the extension schema that describes declarations along with the first declaration, and a second declaration does not add the schema again.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfAFileGetsTheExtensionSchema(CancellationToken cancellationToken)
    {
        const string PdfA = "<rdf:Description rdf:about=\"\" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\"><pdfaid:part>2</pdfaid:part><pdfaid:conformance>B</pdfaid:conformance></rdf:Description>";
        using var document = PdfDocumentReader.Open(Build(PdfA), null);

        PdfDocumentDeclarations.Add(document, new(Profile, []));
        PdfDocumentDeclarations.Add(document, new(OtherProfile, []));

        var xmp = PdfDocumentMetadata.GetXmp(document)!;
        var text = Encoding.UTF8.GetString(xmp.RawPacket.AsSpan());
        using (Assert.Multiple())
        {
            await Assert.That(xmp.Declarations.Length).IsEqualTo(Two);
            await Assert.That(text).Contains("pdfaSchema:namespaceURI>http://pdfa.org/declarations/<");
            await Assert.That(text.Split("pdfaSchema:namespaceURI>http://pdfa.org/declarations/<").Length).IsEqualTo(Two);
        }
    }

    /// <summary>Makes a file whose XMP has a title and the given extra description.</summary>
    /// <param name="extra">Extra RDF description markup.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(string extra) => MiniPdf.Build(
    [
        "<< /Type /Catalog /Pages 2 0 R /Metadata 3 0 R >>",
        "<< /Type /Pages /Count 0 /Kids [] >>",
        MiniPdf.Stream("/Type /Metadata /Subtype /XML", Packet(extra)),
    ]);

    /// <summary>Makes an XMP packet with a title.</summary>
    /// <param name="extra">Extra RDF description markup.</param>
    /// <returns>The packet.</returns>
    private static string Packet(string extra) =>
        "<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
        + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">"
        + "<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">Title</rdf:li></rdf:Alt></dc:title></rdf:Description>"
        + extra
        + "</rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";
}
