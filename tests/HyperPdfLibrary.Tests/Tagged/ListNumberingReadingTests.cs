// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Checks explicit and inherited numbering on tagged reading nodes.</summary>
public sealed class ListNumberingReadingTests
{
    /// <summary>The PDF 2.0 unordered-list value.</summary>
    private const int UnorderedValue = 9;

    /// <summary>The PDF 2.0 ordered-list value.</summary>
    private const int OrderedValue = 10;

    /// <summary>The PDF 2.0 description-list value.</summary>
    private const int DescriptionValue = 11;

    /// <summary>The ancestor's ordered-list attribute dictionary.</summary>
    private const string DecimalAttributes = "/A << /O /List /ListNumbering /Decimal >>";

    /// <summary>Every standard numbering name reaches the page's tagged list node.</summary>
    /// <param name="name">The standard attribute value.</param>
    /// <param name="expected">The public numbering value.</param>
    /// <param name="cancellationToken">Cancels document opening.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("None", (int)PdfListNumbering.None)]
    [Arguments("Disc", (int)PdfListNumbering.Disc)]
    [Arguments("Circle", (int)PdfListNumbering.Circle)]
    [Arguments("Square", (int)PdfListNumbering.Square)]
    [Arguments("Decimal", (int)PdfListNumbering.Decimal)]
    [Arguments("UpperRoman", (int)PdfListNumbering.UpperRoman)]
    [Arguments("LowerRoman", (int)PdfListNumbering.LowerRoman)]
    [Arguments("UpperAlpha", (int)PdfListNumbering.UpperAlpha)]
    [Arguments("LowerAlpha", (int)PdfListNumbering.LowerAlpha)]
    [Arguments("Unordered", UnorderedValue)]
    [Arguments("Ordered", OrderedValue)]
    [Arguments("Description", DescriptionValue)]
    public async Task StandardNamesReachReadingNodes(string name, int expected, CancellationToken cancellationToken)
    {
        var bytes = Build($"/A << /O /List /ListNumbering /{name} >>", string.Empty, string.Empty);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var node = ReadList(document);
        await Assert.That(node.Element!.Attributes.ListNumbering).IsEqualTo(name);
        await Assert.That((int)node.ListNumbering).IsEqualTo(expected);
    }

    /// <summary>A list without its own numbering uses the nearest ancestor's inheritable attribute.</summary>
    /// <param name="cancellationToken">Cancels opening.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingNumberingInherits(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(
            Build(string.Empty, DecimalAttributes, string.Empty),
            null,
            cancellationToken);
        var node = ReadList(document);
        await Assert.That(node.Element!.Attributes.ListNumbering).IsEmpty();
        await Assert.That(node.ListNumbering).IsEqualTo(PdfListNumbering.Decimal);
    }

    /// <summary>Explicit None and private names stop inheritance instead of becoming a guessed style.</summary>
    /// <param name="name">The explicit numbering name.</param>
    /// <param name="cancellationToken">Cancels opening.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("None")]
    [Arguments("PrivateNumbering")]
    public async Task ExplicitUnnumberedValueStopsInheritance(string name, CancellationToken cancellationToken)
    {
        var bytes = Build($"/A << /O /List /ListNumbering /{name} >>", DecimalAttributes, string.Empty);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var node = ReadList(document);
        await Assert.That(node.Element!.Attributes.ListNumbering).IsEqualTo(name);
        await Assert.That(node.ListNumbering).IsEqualTo(PdfListNumbering.None);
    }

    /// <summary>Invalid non-name values do not stop reading and leave the ancestor's numbering available.</summary>
    /// <param name="value">The malformed attribute value.</param>
    /// <param name="cancellationToken">Cancels opening.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("42")]
    [Arguments("(Decimal)")]
    [Arguments("null")]
    public async Task MalformedOwnValuesRemainReadable(string value, CancellationToken cancellationToken)
    {
        var bytes = Build($"/A << /O /List /ListNumbering {value} >>", DecimalAttributes, string.Empty);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        await Assert.That(ReadList(document).ListNumbering).IsEqualTo(PdfListNumbering.Decimal);
    }

    /// <summary>A class-owned value overrides an inherited parent value.</summary>
    /// <param name="cancellationToken">Cancels opening.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClassesOverrideInheritedValues(CancellationToken cancellationToken)
    {
        var bytes = Build("/C /Numbered", DecimalAttributes, "/ClassMap << /Numbered << /O /List /ListNumbering /UpperAlpha >> >>");
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        await Assert.That(ReadList(document).ListNumbering).IsEqualTo(PdfListNumbering.UpperAlpha);
    }

    /// <summary>Layout and table owners cannot supply a list-numbering attribute.</summary>
    /// <param name="owner">The unrelated standard attribute owner.</param>
    /// <param name="cancellationToken">Cancels opening.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Layout")]
    [Arguments("Table")]
    [Arguments("PrintField")]
    [Arguments("Artifact")]
    public async Task OtherOwnersDoNotSupplyNumbering(string owner, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build($"/A << /O /{owner} /ListNumbering /Decimal >>", string.Empty, string.Empty), null, cancellationToken);
        await Assert.That(ReadList(document).ListNumbering).IsEqualTo(PdfListNumbering.None);
    }

    /// <summary>A private attribute owner can supply standard list numbering without changing the raw name.</summary>
    /// <param name="owner">An owner permitted by the List attribute definition.</param>
    /// <param name="cancellationToken">Cancels opening.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("PrivateOwner")]
    [Arguments("FENote")]
    public async Task PrivateOwnerRemainsReadable(string owner, CancellationToken cancellationToken)
    {
        var bytes = Build($"/A << /O /{owner} /ListNumbering /Ordered >>", string.Empty, string.Empty);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var node = ReadList(document);
        await Assert.That(node.Element!.Attributes.ListNumbering).IsEqualTo("Ordered");
        await Assert.That((int)node.ListNumbering).IsEqualTo(OrderedValue);
    }

    /// <summary>Layout inference keeps the style it detected independently of tagged attributes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InferredNumberingRemainsAvailable()
    {
        var node = new PdfSemanticNode(PdfSemanticRole.List, PdfNodeOrigin.Inferred, 0) { ListNumbering = PdfListNumbering.Decimal };
        await Assert.That(node.ListNumbering).IsEqualTo(PdfListNumbering.Decimal);
    }

    /// <summary>A cancelled tagged-node build stops before publishing any reading nodes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledBuildDoesNotPublishNodes()
    {
        using var document = PdfDocumentReader.Open(Build(DecimalAttributes, string.Empty, string.Empty), null);
        var nodes = new List<PdfSemanticNode>();
        var builder = new TaggedNodeBuilder(document, 0);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.That(() => builder.Build(PdfDocumentTagged.GetStructureTree(document)!, nodes, cancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(nodes).IsEmpty();
    }

    /// <summary>Builds an input whose list belongs to a blank page through replacement text.</summary>
    /// <param name="own">The list's attribute entries.</param>
    /// <param name="parent">The enclosing Div's attribute entries.</param>
    /// <param name="classes">The root's class map.</param>
    /// <returns>The input PDF bytes.</returns>
    private static byte[] Build(string own, string parent, string classes) => MiniPdf.Build(
    [
        "<< /Type /Catalog /Version /2.0 /Pages 2 0 R /StructTreeRoot 3 0 R >>",
        "<< /Type /Pages /Count 1 /Kids [7 0 R] >>",
        $"<< /Type /StructTreeRoot /K 4 0 R {classes} >>",
        "<< /Type /StructElem /S /Document /P 3 0 R /K 5 0 R >>",
        $"<< /Type /StructElem /S /Div /P 4 0 R /K 6 0 R {parent} >>",
        $"<< /Type /StructElem /S /L /P 5 0 R /Pg 7 0 R /ActualText (item) {own} >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] >>",
    ]);

    /// <summary>Reads the list through the same tagged-node builder used for page structure.</summary>
    /// <param name="document">The open document.</param>
    /// <returns>The list's reading node.</returns>
    private static PdfSemanticNode ReadList(PdfDocument document)
    {
        var nodes = new List<PdfSemanticNode>();
        new TaggedNodeBuilder(document, 0).Build(PdfDocumentTagged.GetStructureTree(document)!, nodes);
        return nodes[0].Children[0].Children[0];
    }
}
