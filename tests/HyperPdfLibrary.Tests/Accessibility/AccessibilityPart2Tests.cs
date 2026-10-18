// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Tests for the PDF/UA-2 claim, the PDF 2.0 structure namespace and namespace role maps (ISO 14289-2 clauses 5 and 8.2).</summary>
public sealed class AccessibilityPart2Tests
{
    /// <summary>The <c>pdfuaid:rev</c> of ISO 14289-2:2024.</summary>
    private const string Revision = "2024";

    /// <summary>Gets a PDF 2.0 file that claims PDF/UA-2 revision 2024 and puts its document element in the PDF 2.0 namespace.</summary>
    private static AccessibilitySpec Valid => new() { Xmp = AccessibilityPdfs.Packet(AccessibilityPdfs.PartTwo, true, Revision), CatalogExtra = "/Version /2.0", Pdf20Root = true };

    /// <summary>A part 2 file with the right revision, version and root has none of the part 2 findings.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidPart2FileHasNoClaimOrRootFindings()
    {
        var report = await AccessibilityPdfs.ReportAsync(Valid);

        await Assert.That(report.Claim.Part).IsEqualTo(AccessibilityPdfs.PartTwo);
        await Assert.That(report.Claim.Revision).IsEqualTo(Revision);
        await Assert.That(report.Claim.UsesPdf20Namespaces).IsTrue();
        await Assert.That(report.GetCount(PdfAccessibilityCode.ClaimRevisionInvalid)).IsEqualTo(0);
        await Assert.That(report.GetCount(PdfAccessibilityCode.DocumentRootInvalid)).IsEqualTo(0);
    }

    /// <summary>A missing or different revision is flagged for part 2 only.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RevisionMustBe2024ForPart2()
    {
        var missing = await AccessibilityPdfs.ReportAsync(Valid with { Xmp = AccessibilityPdfs.Packet(AccessibilityPdfs.PartTwo, true) });
        var other = await AccessibilityPdfs.ReportAsync(Valid with { Xmp = AccessibilityPdfs.Packet(AccessibilityPdfs.PartTwo, true, "2023") });
        var partOne = await AccessibilityPdfs.ReportAsync(new() { Xmp = AccessibilityPdfs.Packet(1, true) });

        await Assert.That(missing.GetCount(PdfAccessibilityCode.ClaimRevisionInvalid)).IsEqualTo(1);
        await Assert.That(other.GetCount(PdfAccessibilityCode.ClaimRevisionInvalid)).IsEqualTo(1);
        await Assert.That(partOne.GetCount(PdfAccessibilityCode.ClaimRevisionInvalid)).IsEqualTo(0);
    }

    /// <summary>PDF/UA-2 rejects non-integer and malformed revision values.</summary>
    /// <param name="revision">The claimed revision.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("year")]
    [Arguments("2024.0")]
    [Arguments("202")]
    [Arguments("20240")]
    [Arguments("")]
    public async Task MalformedRevisionIsFlagged(string revision)
    {
        var report = await AccessibilityPdfs.ReportAsync(Valid with { Xmp = AccessibilityPdfs.Packet(AccessibilityPdfs.PartTwo, true, revision) });

        await Assert.That(report.GetCount(PdfAccessibilityCode.ClaimRevisionInvalid)).IsEqualTo(1);
    }

    /// <summary>The structure tree must contain exactly one root element for PDF/UA-2.</summary>
    /// <param name="rootCount">The number of root children.</param>
    /// <param name="cancellationToken">Cancels the report.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(2)]
    public async Task IncorrectRootCountIsFlagged(int rootCount, CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(Valid));
        var root = document.Catalog.GetDictionary(KnownName.StructTreeRoot)!;
        var children = new PdfArray(document.Objects, rootCount);
        var child = root.GetRaw(KnownName.K);
        for (var index = 0; index < rootCount; index++)
        {
            var copy = StoreReading.Resolve(document.Objects, child).AsDictionary()!.Clone();
            children.Add(PdfValue.FromDictionary(copy));
        }

        root.Set(KnownName.K, PdfValue.FromArray(children));
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(report.GetCount(PdfAccessibilityCode.DocumentRootInvalid)).IsEqualTo(1);
    }

    /// <summary>Recovery must not hide an extra malformed or repeated root child from the report.</summary>
    /// <param name="repeatDocument">Whether the extra child repeats the Document reference.</param>
    /// <param name="cancellationToken">Cancels the report.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ExtraRawRootChildIsFlagged(bool repeatDocument, CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(Valid));
        var root = document.Catalog.GetDictionary(KnownName.StructTreeRoot)!;
        var child = root.GetRaw(KnownName.K);
        var extra = repeatDocument ? child : PdfValue.FromInteger(0);
        root.Set(KnownName.K, PdfValue.FromArray(new(document.Objects, [child, extra])));

        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(report.GetCount(PdfAccessibilityCode.DocumentRootInvalid)).IsEqualTo(1);
    }

    /// <summary>A sole root with a type other than Document is rejected for PDF/UA-2.</summary>
    /// <param name="cancellationToken">Cancels the report.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RootTypeMustBeDocument(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(Valid));
        var root = document.Catalog.GetDictionary(KnownName.StructTreeRoot)!;
        var element = root.GetDictionary(KnownName.K)!;
        element.Set(KnownName.S, PdfValue.FromName(document.Objects.Names.Intern("Part"u8)));

        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(report.GetCount(PdfAccessibilityCode.DocumentRootInvalid)).IsEqualTo(1);
    }

    /// <summary>The document element must be in the PDF 2.0 namespace for part 2.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentRootMustBeInThePdf20Namespace()
    {
        var report = await AccessibilityPdfs.ReportAsync(Valid with { Pdf20Root = false });

        await Assert.That(report.GetCount(PdfAccessibilityCode.DocumentRootInvalid)).IsEqualTo(1);
    }

    /// <summary>The <c>H</c> type and an unmapped <c>Note</c> type are flagged for part 2 only.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovedTypesAreFlaggedForPart2()
    {
        var heading = await AccessibilityPdfs.ReportAsync(Valid with { Layout = static (pdf, document) => AccessibilityPdfs.Kids(pdf.Element("H", document, "/K 0")) });
        var note = await AccessibilityPdfs.ReportAsync(Valid with { Layout = static (pdf, document) => AccessibilityPdfs.Kids(pdf.Element("Note", document, "/K 0")) });
        var partOne = await AccessibilityPdfs.ReportAsync(new() { Layout = static (pdf, document) => AccessibilityPdfs.Kids(pdf.Element("H", document, "/K 0")) });

        await Assert.That(heading.GetCount(PdfAccessibilityCode.HeadingTypeNotAllowed)).IsEqualTo(1);
        await Assert.That(note.GetCount(PdfAccessibilityCode.NoteTypeNotAllowed)).IsEqualTo(1);
        await Assert.That(partOne.GetCount(PdfAccessibilityCode.HeadingTypeNotAllowed)).IsEqualTo(0);
    }

    /// <summary>A role map that sends a standard type to another type is flagged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemappedStandardTypesAreFlagged()
    {
        var remapped = await AccessibilityPdfs.ReportAsync(new() { RootExtra = "/RoleMap << /P /Span >>" });
        var custom = await AccessibilityPdfs.ReportAsync(new() { RootExtra = "/RoleMap << /Custom /P >>" });

        await Assert.That(remapped.GetCount(PdfAccessibilityCode.StandardTypeRemapped)).IsEqualTo(1);
        await Assert.That(custom.GetCount(PdfAccessibilityCode.StandardTypeRemapped)).IsEqualTo(0);
    }

    /// <summary>A namespace role map that returns to its own namespace is flagged as a different type or a loop.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamespaceRoleMapsMustLeaveTheNamespace()
    {
        var sameNamespace = await AccessibilityPdfs.ReportAsync(Valid with { Layout = static (pdf, document) => NamespaceElement(pdf, document, "Y") });
        var loop = await AccessibilityPdfs.ReportAsync(Valid with { Layout = static (pdf, document) => NamespaceElement(pdf, document, "X") });
        var leaves = await AccessibilityPdfs.ReportAsync(Valid with { Layout = static (pdf, document) => NamespaceElement(pdf, document, "P", "http://iso.org/pdf2/ssn") });

        await Assert.That(sameNamespace.GetCount(PdfAccessibilityCode.RoleMapSameNamespace)).IsEqualTo(1);
        await Assert.That(sameNamespace.GetCount(PdfAccessibilityCode.RoleMapCycle)).IsEqualTo(0);
        await Assert.That(loop.GetCount(PdfAccessibilityCode.RoleMapCycle)).IsEqualTo(1);
        await Assert.That(leaves.GetCount(PdfAccessibilityCode.RoleMapSameNamespace)).IsEqualTo(0);
        await Assert.That(leaves.GetCount(PdfAccessibilityCode.RoleMapCycle)).IsEqualTo(0);
    }

    /// <summary>Writes an element of type <c>X</c> in a custom namespace whose role map sends <c>X</c> to another type.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="document">The document element's number.</param>
    /// <param name="target">The type <c>X</c> is mapped to.</param>
    /// <returns>The document element's kids.</returns>
    private static string NamespaceElement(TaggedPdfBuilder pdf, int document, string target) => NamespaceElement(pdf, document, target, "urn:example:custom");

    /// <summary>Writes an element of type <c>X</c> in a custom namespace whose role map sends <c>X</c> to a type in another namespace.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="document">The document element's number.</param>
    /// <param name="target">The type <c>X</c> is mapped to.</param>
    /// <param name="targetUri">The URI of the namespace the target type is in.</param>
    /// <returns>The document element's kids.</returns>
    private static string NamespaceElement(TaggedPdfBuilder pdf, int document, string target, string targetUri)
    {
        var targetSpace = pdf.Add(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Namespace /NS ({targetUri}) >>"));
        var space = pdf.Add(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Namespace /NS (urn:example:custom) /RoleMapNS << /X [/{target} {targetSpace} 0 R] >> >>"));
        return AccessibilityPdfs.Kids(pdf.Element("X", document, string.Create(CultureInfo.InvariantCulture, $"/NS {space} 0 R /K 0")));
    }
}
