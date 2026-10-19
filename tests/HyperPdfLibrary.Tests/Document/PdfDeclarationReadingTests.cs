// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Checks PDF Declarations field associations and lenient packet reading.</summary>
public sealed class PdfDeclarationReadingTests
{
    /// <summary>The first declaration's profile.</summary>
    private const string FirstProfile = "urn:example:first";

    /// <summary>The second declaration's unknown profile.</summary>
    private const string SecondProfile = "urn:example:second";

    /// <summary>The profile property name.</summary>
    private const string ProfileProperty = "conformsTo";

    /// <summary>The namespace of lookalike fields.</summary>
    private const string OtherNamespace = "urn:other";

    /// <summary>The number of independent declarations.</summary>
    private const int DeclarationCount = 2;

    /// <summary>The count that grows both leased reference buffers.</summary>
    private const int GrowthCount = 20;

    /// <summary>Missing claim fields must not shift values from another declaration into their place.</summary>
    /// <param name="cancellationToken">Cancels packet parsing.</param>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Read_SeparateDeclarations_PreservesClaimAssociations(CancellationToken cancellationToken)
    {
        var snapshot = Parse(
            """
            <pdfd:declarations><rdf:Bag>
              <rdf:li rdf:parseType="Resource">
                <pdfd:conformsTo>urn:example:first</pdfd:conformsTo>
                <pdfd:claimData><rdf:Bag>
                  <rdf:li rdf:parseType="Resource"><pdfd:claimBy>First claimant</pdfd:claimBy></rdf:li>
                  <rdf:li rdf:parseType="Resource"><pdfd:claimDate>2019-01-17</pdfd:claimDate></rdf:li>
                </rdf:Bag></pdfd:claimData>
              </rdf:li>
              <rdf:li rdf:parseType="Resource">
                <pdfd:conformsTo>urn:example:second</pdfd:conformsTo>
                <pdfd:claimData><rdf:Bag><rdf:li rdf:parseType="Resource">
                  <pdfd:claimCredentials>Second credentials</pdfd:claimCredentials>
                  <pdfd:claimReport>#ef=report.pdf</pdfd:claimReport>
                </rdf:li></rdf:Bag></pdfd:claimData>
              </rdf:li>
            </rdf:Bag></pdfd:declarations><pdfd:after>Retained</pdfd:after>
            """,
            cancellationToken);

        await Assert.That(snapshot.Declarations.Length).IsEqualTo(DeclarationCount);
        var first = snapshot.Declarations[0];
        var second = snapshot.Declarations[1];
        await Assert.That(first.ConformsTo).IsEqualTo(FirstProfile);
        await Assert.That(first.ClaimData.Length).IsEqualTo(DeclarationCount);
        await Assert.That(first.ClaimData[0].ClaimBy).IsEqualTo("First claimant");
        await Assert.That(first.ClaimData[0].ClaimDate).IsNull();
        await Assert.That(first.ClaimData[1].ClaimBy).IsNull();
        await Assert.That(first.ClaimData[1].ClaimDate).IsEqualTo("2019-01-17");
        await Assert.That(second.ConformsTo).IsEqualTo(SecondProfile);
        await Assert.That(second.ClaimData[0].ClaimBy).IsNull();
        await Assert.That(second.ClaimData[0].ClaimCredentials).IsEqualTo("Second credentials");
        await Assert.That(second.ClaimData[0].ClaimReport).IsEqualTo("#ef=report.pdf");
        await Assert.That(snapshot.GetValues(XmpMetadata.PdfDeclarationsNamespace, ProfileProperty).ToArray()).IsEquivalentTo([FirstProfile, SecondProfile]);
        await Assert.That(snapshot.GetValue(XmpMetadata.PdfDeclarationsNamespace, "after")).IsEqualTo("Retained");
    }

    /// <summary>RDF description and resource forms identify fields by namespace, not prefix.</summary>
    /// <param name="cancellationToken">Cancels parsing.</param>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Read_DescriptionAndResourceForms_PreservesFields(CancellationToken cancellationToken)
    {
        var snapshot = Parse(
            """
            <d:declarations xmlns:d="http://pdfa.org/declarations/"><rdf:Bag>
              <rdf:li><rdf:Description d:conformsTo="urn:example:first">
                <d:claimData><rdf:Bag><rdf:li><rdf:Description d:claimBy="" d:claimDate="invalid-date">
                  <d:claimReport rdf:resource="#ef=report.pdf"/>
                </rdf:Description></rdf:li></rdf:Bag></d:claimData>
              </rdf:Description></rdf:li>
            </rdf:Bag></d:declarations>
            """,
            cancellationToken);

        await Assert.That(snapshot.Declarations.Length).IsEqualTo(1);
        var declaration = snapshot.Declarations[0];
        await Assert.That(declaration.ConformsTo).IsEqualTo(FirstProfile);
        await Assert.That(declaration.ClaimData[0].ClaimBy).IsEqualTo(string.Empty);
        await Assert.That(declaration.ClaimData[0].ClaimDate).IsEqualTo("invalid-date");
        await Assert.That(declaration.ClaimData[0].ClaimReport).IsEqualTo("#ef=report.pdf");
        await Assert.That(declaration.ClaimData[0].ClaimCredentials).IsNull();
    }

    /// <summary>Readable claims survive malformed or incomplete declaration values.</summary>
    /// <param name="cancellationToken">Cancels parsing.</param>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Read_MissingProfileAndEmptyFields_RemainsLenient(CancellationToken cancellationToken)
    {
        var snapshot = Parse(
            """
            <pdfd:declarations><rdf:Bag>
              <rdf:li rdf:parseType="Resource">
                <pdfd:claimData><rdf:Bag><rdf:li rdf:parseType="Resource">
                  <pdfd:claimBy/><pdfd:claimCredentials> First <![CDATA[and second]]> </pdfd:claimCredentials>
                </rdf:li></rdf:Bag></pdfd:claimData>
              </rdf:li>
            </rdf:Bag></pdfd:declarations>
            """,
            cancellationToken);

        await Assert.That(snapshot.IsWellFormed).IsTrue();
        await Assert.That(snapshot.Declarations.Length).IsEqualTo(1);
        var declaration = snapshot.Declarations[0];
        await Assert.That(declaration.ConformsTo).IsNull();
        await Assert.That(declaration.ClaimData[0].ClaimBy).IsEqualTo(string.Empty);
        await Assert.That(declaration.ClaimData[0].ClaimCredentials).IsEqualTo(" First and second ");
    }

    /// <summary>XML damage keeps completed declarations rather than losing the entire packet.</summary>
    /// <param name="cancellationToken">Cancels parsing.</param>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Read_TruncatedPacket_KeepsCompletedDeclaration(CancellationToken cancellationToken)
    {
        var packet = """
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns:pdfd="http://pdfa.org/declarations/">
              <rdf:Description><pdfd:declarations><rdf:Bag>
                <rdf:li rdf:parseType="Resource"><pdfd:conformsTo>urn:example:first</pdfd:conformsTo></rdf:li>
                <rdf:li rdf:parseType="Resource"><pdfd:conformsTo>unfinished
            """u8.ToArray();
        var snapshot = XmpParser.Parse(packet, cancellationToken);

        await Assert.That(snapshot.IsWellFormed).IsFalse();
        await Assert.That(snapshot.Declarations.Length).IsEqualTo(1);
        await Assert.That(snapshot.Declarations[0].ConformsTo).IsEqualTo(FirstProfile);
        await Assert.That(snapshot.RawPacket.AsSpan().SequenceEqual(packet)).IsTrue();
    }

    /// <summary>A lookalike property in another namespace is not a PDF Declaration.</summary>
    /// <param name="cancellationToken">Cancels parsing.</param>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Read_WrongNamespace_DoesNotPromoteClaim(CancellationToken cancellationToken)
    {
        var snapshot = Parse(
            """
            <other:declarations xmlns:other="urn:other"><rdf:Bag>
              <rdf:li rdf:parseType="Resource"><other:conformsTo>urn:example:first</other:conformsTo></rdf:li>
            </rdf:Bag></other:declarations>
            """,
            cancellationToken);

        await Assert.That(snapshot.Declarations.Length == 0).IsTrue();
        await Assert.That(snapshot.GetValue(OtherNamespace, ProfileProperty)).IsEqualTo(FirstProfile);
    }

    /// <summary>Empty arrays and structures remain readable without inventing a profile.</summary>
    /// <param name="property">The declaration property.</param>
    /// <param name="expectedCount">The number of structure entries.</param>
    /// <param name="cancellationToken">Cancels parsing.</param>
    /// <returns>The test task.</returns>
    [Test]
    [Arguments("<pdfd:declarations/>", 0)]
    [Arguments("<pdfd:declarations><rdf:Bag/></pdfd:declarations>", 0)]
    [Arguments("<pdfd:declarations><rdf:Bag><rdf:li rdf:parseType=\"Resource\"/></rdf:Bag></pdfd:declarations>", 1)]
    public async Task Read_EmptyStructures_DoesNotInventConformance(string property, int expectedCount, CancellationToken cancellationToken)
    {
        var snapshot = Parse(property, cancellationToken);
        await Assert.That(snapshot.IsWellFormed).IsTrue();
        await Assert.That(snapshot.Declarations.Length).IsEqualTo(expectedCount);
        if (expectedCount == 0)
        {
            return;
        }

        await Assert.That(snapshot.Declarations[0].ConformsTo).IsNull();
        await Assert.That(snapshot.Declarations[0].ClaimData.Length == 0).IsTrue();
    }

    /// <summary>Unknown fields remain available without becoming known declaration fields.</summary>
    /// <param name="cancellationToken">Cancels parsing.</param>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Read_UnknownFields_RetainsGenericPropertyView(CancellationToken cancellationToken)
    {
        var snapshot = Parse(
            """
            <pdfd:declarations><rdf:Bag><rdf:li rdf:parseType="Resource">
              <other:conformsTo xmlns:other="urn:other">urn:example:first</other:conformsTo>
              <pdfd:future>Future value</pdfd:future>
              <pdfd:claimData><rdf:Bag><rdf:li rdf:parseType="Resource">
                <pdfd:claimBy>Known</pdfd:claimBy>
                <other:claimDate xmlns:other="urn:other">Other date</other:claimDate>
                <pdfd:extra rdf:parseType="Resource"><pdfd:detail>Nested value</pdfd:detail></pdfd:extra>
              </rdf:li></rdf:Bag></pdfd:claimData>
            </rdf:li></rdf:Bag></pdfd:declarations>
            """,
            cancellationToken);

        await Assert.That(snapshot.Declarations[0].ConformsTo).IsNull();
        await Assert.That(snapshot.Declarations[0].ClaimData[0].ClaimDate).IsNull();
        await Assert.That(snapshot.GetValue(OtherNamespace, ProfileProperty)).IsEqualTo(FirstProfile);
        await Assert.That(snapshot.GetValue(XmpMetadata.PdfDeclarationsNamespace, "future")).IsEqualTo("Future value");
        await Assert.That(snapshot.GetValue(XmpMetadata.PdfDeclarationsNamespace, "detail")).IsEqualTo("Nested value");
    }

    /// <summary>Growing and reusing buffers cannot change a published claim array.</summary>
    /// <param name="cancellationToken">Cancels parsing.</param>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Read_GrowingArrays_DetachesPublishedClaims(CancellationToken cancellationToken)
    {
        var properties = new StringBuilder("<pdfd:declarations><rdf:Bag>");
        for (var declaration = 0; declaration < GrowthCount; declaration++)
        {
            _ = properties.Append("<rdf:li rdf:parseType=\"Resource\"><pdfd:conformsTo>urn:example:first</pdfd:conformsTo><pdfd:claimData><rdf:Bag>");
            for (var claim = 0; claim < GrowthCount; claim++)
            {
                _ = properties.Append("<rdf:li rdf:parseType=\"Resource\"><pdfd:claimBy>Kept</pdfd:claimBy></rdf:li>");
            }

            _ = properties.Append("</rdf:Bag></pdfd:claimData></rdf:li>");
        }

        _ = properties.Append("</rdf:Bag></pdfd:declarations>");
        var snapshot = Parse(properties.ToString(), cancellationToken);
        _ = Parse("<pdfd:declarations><rdf:Bag><rdf:li rdf:parseType=\"Resource\"/></rdf:Bag></pdfd:declarations>", cancellationToken);

        await Assert.That(snapshot.Declarations.Length).IsEqualTo(GrowthCount);
        await Assert.That(snapshot.Declarations[^1].ConformsTo).IsEqualTo(FirstProfile);
        await Assert.That(snapshot.Declarations[0].ClaimData.Length).IsEqualTo(GrowthCount);
        await Assert.That(snapshot.Declarations[0].ClaimData[^1].ClaimBy).IsEqualTo("Kept");
    }

    /// <summary>Reading one object's packet does not add its claims to another object's packet.</summary>
    /// <param name="cancellationToken">Cancels decoding and parsing.</param>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Read_ObjectMetadata_DoesNotAggregateOtherClaims(CancellationToken cancellationToken)
    {
        var first = new PdfDictionary(null);
        var second = new PdfDictionary(null);
        var firstPacket = Parse(
            "<pdfd:declarations><rdf:Bag><rdf:li rdf:parseType=\"Resource\"><pdfd:conformsTo>urn:example:first</pdfd:conformsTo></rdf:li></rdf:Bag></pdfd:declarations>",
            cancellationToken);
        var secondPacket = Parse(
            "<pdfd:declarations><rdf:Bag><rdf:li rdf:parseType=\"Resource\"><pdfd:conformsTo>urn:example:second</pdfd:conformsTo></rdf:li></rdf:Bag></pdfd:declarations>",
            cancellationToken);
        first.Set(KnownName.Metadata, PdfValue.FromStream(new(new(null), firstPacket.RawPacket.ToArray())));
        second.Set(KnownName.Metadata, PdfValue.FromStream(new(new(null), secondPacket.RawPacket.ToArray())));
        var firstMetadata = await Assert.That(PdfDocumentMetadata.ParseXmp(first, cancellationToken)).IsNotNull();
        var secondMetadata = await Assert.That(PdfDocumentMetadata.ParseXmp(second, cancellationToken)).IsNotNull();

        await Assert.That(firstMetadata.Declarations.Length).IsEqualTo(1);
        await Assert.That(secondMetadata.Declarations.Length).IsEqualTo(1);
        await Assert.That(firstMetadata.Declarations[0].ConformsTo).IsEqualTo(FirstProfile);
        await Assert.That(secondMetadata.Declarations[0].ConformsTo).IsEqualTo(SecondProfile);
    }

    /// <summary>Wraps declarations in a metadata packet without changing its scope.</summary>
    /// <param name="properties">The metadata properties.</param>
    /// <param name="cancellationToken">Cancels parsing.</param>
    /// <returns>The detached snapshot.</returns>
    private static XmpMetadata Parse(string properties, CancellationToken cancellationToken) =>
        XmpParser.Parse(
            Encoding.UTF8.GetBytes(
            $"<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\" xmlns:pdfd=\"http://pdfa.org/declarations/\"><rdf:Description>{properties}</rdf:Description></rdf:RDF>"),
            cancellationToken);
}
