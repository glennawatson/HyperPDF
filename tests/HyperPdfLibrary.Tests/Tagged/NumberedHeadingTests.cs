// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Checks numbered headings beyond the six built-in PDF 1.7 levels.</summary>
public sealed class NumberedHeadingTests
{
    /// <summary>The first extended heading level.</summary>
    private const int SeventhLevel = 7;

    /// <summary>The mapped level in a custom namespace.</summary>
    private const int SecondLevel = 2;

    /// <summary>A built-in heading level.</summary>
    private const int ThirdLevel = 3;

    /// <summary>The deepest built-in heading level.</summary>
    private const int SixthLevel = 6;

    /// <summary>The example level in ISO 14289-1 clause 7.4.3.</summary>
    private const int DeepLevel = 1247;

    /// <summary>The first extended heading's spelling.</summary>
    private const string SeventhHeading = "H7";

    /// <summary>The standard's example extended heading spelling.</summary>
    private const string DeepHeading = "H1247";

    /// <summary>The largest level representable by the existing integer property.</summary>
    private const string LargestIntegerHeading = "H2147483647";

    /// <summary>A numbered heading beyond the integer API's range.</summary>
    private const string BeyondIntegerHeading = "H2147483648";

    /// <summary>A numbered heading beyond the standard-type lookup's spelling buffer.</summary>
    private const string LongHeading = "H1234567890123456789012345678901234567890";

    /// <summary>The next level after the long heading.</summary>
    private const string NextLongHeading = "H1234567890123456789012345678901234567891";

    /// <summary>A level that skips the next long heading.</summary>
    private const string SkippedLongHeading = "H1234567890123456789012345678901234567892";

    /// <summary>The empty page tree used by the reading-only fixtures.</summary>
    private const string EmptyPages = "<< /Type /Pages /Count 0 /Kids [] >>";

    /// <summary>The PDF 2.0 catalog of the reading fixtures.</summary>
    private const string Pdf20Catalog = "<< /Type /Catalog /Version /2.0 /Pages 2 0 R /StructTreeRoot 3 0 R >>";

    /// <summary>The PDF 1.7 catalog of the reading fixtures.</summary>
    private const string Pdf17Catalog = "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 3 0 R >>";

    /// <summary>The Document element enclosing the PDF 2.0 heading.</summary>
    private const string Pdf20Document = "<< /Type /StructElem /S /Document /NS 6 0 R /P 3 0 R /K 5 0 R >>";

    /// <summary>The spelling of a custom type mapped into the PDF 2.0 namespace.</summary>
    private const string CustomType = "Deep";

    /// <summary>The PDF 2.0 namespace does not flatten deeper headings to H6.</summary>
    /// <param name="spelling">The numbered heading's name.</param>
    /// <param name="level">Its actual level.</param>
    /// <param name="cancellationToken">Cancels opening and reporting.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments("H3", ThirdLevel)]
    [Arguments("H6", SixthLevel)]
    [Arguments(SeventhHeading, SeventhLevel)]
    [Arguments(DeepHeading, DeepLevel)]
    [Arguments(LargestIntegerHeading, int.MaxValue)]
    public async Task Read_Pdf20_PreservesNumberedLevel(string spelling, int level, CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
        [
            Pdf20Catalog,
            EmptyPages,
            "<< /Type /StructTreeRoot /K 4 0 R /Namespaces [6 0 R] >>",
            Pdf20Document,
            $"<< /Type /StructElem /S /{spelling} /NS 6 0 R /P 4 0 R >>",
            $"<< /Type /Namespace /NS ({PdfStructureTypes.Pdf20Namespace}) >>",
        ]);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var heading = GetHeading(document);

        await Assert.That(heading.HeadingLevel).IsEqualTo(level);
        await Assert.That(heading.Role).IsEqualTo(PdfSemanticRole.Heading);
        await Assert.That(heading.RawType).IsEqualTo(spelling);
        await Assert.That(report.GetCount(PdfAccessibilityCode.HeadingLevelSkipped)).IsEqualTo(1);
    }

    /// <summary>Namespace role mapping preserves the target heading's number.</summary>
    /// <param name="spelling">The mapped numbered heading's name.</param>
    /// <param name="level">Its actual level.</param>
    /// <param name="cancellationToken">Cancels opening and reporting.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(SeventhHeading, SeventhLevel)]
    [Arguments(DeepHeading, DeepLevel)]
    [Arguments(LargestIntegerHeading, int.MaxValue)]
    public async Task Read_CustomNamespace_PreservesMappedLevel(string spelling, int level, CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
        [
            Pdf20Catalog,
            EmptyPages,
            "<< /Type /StructTreeRoot /K 4 0 R /Namespaces [6 0 R 7 0 R] >>",
            Pdf20Document,
            $"<< /Type /StructElem /S /{CustomType} /NS 7 0 R /P 4 0 R >>",
            $"<< /Type /Namespace /NS ({PdfStructureTypes.Pdf20Namespace}) >>",
            $"<< /Type /Namespace /NS (https://example.test/structure) /RoleMapNS << /{CustomType} [/{spelling} 6 0 R] >> >>",
        ]);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        _ = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var heading = GetHeading(document);

        await Assert.That(heading.HeadingLevel).IsEqualTo(level);
        await Assert.That(heading.Role).IsEqualTo(PdfSemanticRole.Heading);
        await Assert.That(heading.RawType).IsEqualTo(CustomType);
        await Assert.That(heading.MappedType).IsEqualTo(spelling);
        await Assert.That(heading.HeadingLevelDigits.ToString()).IsEqualTo(spelling[1..]);
    }

    /// <summary>A numbered-looking custom name uses its mapped semantics, not its original digit.</summary>
    /// <param name="cancellationToken">Cancels opening and reporting.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Read_CustomNumberedName_UsesMappedLevel(CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
        [
            Pdf20Catalog,
            EmptyPages,
            "<< /Type /StructTreeRoot /K 4 0 R /Namespaces [6 0 R 7 0 R] >>",
            Pdf20Document,
            $"<< /Type /StructElem /S /{SeventhHeading} /NS 7 0 R /P 4 0 R >>",
            $"<< /Type /Namespace /NS ({PdfStructureTypes.Pdf20Namespace}) >>",
            $"<< /Type /Namespace /NS (https://example.test/structure) /RoleMapNS << /{SeventhHeading} [/H2 6 0 R] >> >>",
        ]);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        _ = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var heading = GetHeading(document);

        await Assert.That(heading.RawType).IsEqualTo(SeventhHeading);
        await Assert.That(heading.MappedType).IsEqualTo("H2");
        await Assert.That(heading.HeadingLevel).IsEqualTo(SecondLevel);
        await Assert.That(heading.HeadingLevelDigits.ToString()).IsEqualTo("2");
    }

    /// <summary>A numbered-looking extension mapped to a paragraph has no heading level.</summary>
    /// <param name="cancellationToken">Cancels opening and reporting.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Read_Pdf17_NumberedParagraphHasNoHeadingLevel(CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
        [
            Pdf17Catalog,
            EmptyPages,
            $"<< /Type /StructTreeRoot /K 4 0 R /RoleMap << /{DeepHeading} /P >> >>",
            "<< /Type /StructElem /S /Document /P 3 0 R /K 5 0 R >>",
            $"<< /Type /StructElem /S /{DeepHeading} /P 4 0 R >>",
        ]);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        _ = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var element = GetHeading(document);

        await Assert.That(element.HeadingLevel).IsEqualTo(0);
        await Assert.That(element.HeadingLevelDigits.ToString()).IsEqualTo(string.Empty);
        await Assert.That(element.Role).IsEqualTo(PdfSemanticRole.Paragraph);
    }

    /// <summary>Non-canonical names must not be emitted as standard numbered headings.</summary>
    /// <param name="spelling">The malformed structure name.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments("H07")]
    [Arguments("H0")]
    [Arguments("h7")]
    [Arguments("H-7")]
    [Arguments("H7x")]
    public async Task AddElement_Pdf20_RejectsMalformedHeading(string spelling)
    {
        var tags = new PdfTagTree(new PdfDocumentBuilder(), true);
        await Assert.That(() => tags.AddElement(tags.Root, spelling)).Throws<ArgumentException>();
    }

    /// <summary>Saved headings keep their exact number and the required PDF 1.7 role mapping.</summary>
    /// <param name="pdf20">Whether the tree uses the PDF 2.0 namespace.</param>
    /// <param name="spelling">The numbered heading.</param>
    /// <param name="cancellationToken">Cancels saving and opening.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(false, DeepHeading)]
    [Arguments(false, LongHeading)]
    [Arguments(true, DeepHeading)]
    [Arguments(true, LongHeading)]
    public async Task Save_NumberedHeading_PreservesNumberAndNamespace(bool pdf20, string spelling, CancellationToken cancellationToken)
    {
        var builder = new PdfDocumentBuilder();
        var tags = new PdfTagTree(builder, pdf20);
        _ = tags.AddElement(tags.Root, spelling);
        builder.CatalogEntries.Set(KnownName.StructTreeRoot, PdfValue.FromReference(tags.Finish()));
        if (pdf20)
        {
            builder.CatalogEntries.Set(KnownName.Version, PdfValue.FromName(builder.Names.Intern("2.0")));
        }

        await using var output = new MemoryStream();
        await builder.SaveAsync(output, cancellationToken);
        using var document = await PdfDocumentReader.OpenAsync(output.ToArray(), null, cancellationToken);
        _ = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var heading = GetHeading(document);

        await Assert.That(heading.RawType).IsEqualTo(spelling);
        await Assert.That(heading.MappedType).IsEqualTo(pdf20 ? spelling : "H6");
        await Assert.That(heading.HeadingLevelDigits.ToString()).IsEqualTo(spelling[1..]);
        await Assert.That(heading.Role).IsEqualTo(PdfSemanticRole.Heading);
        await Assert.That(heading.Namespace).IsEqualTo(pdf20 ? PdfStructureTypes.Pdf20Namespace : null);
    }

    /// <summary>PDF/UA-1 extended headings keep their number while mapping to a PDF 1.7 heading type.</summary>
    /// <param name="spelling">The user-defined numbered heading's name.</param>
    /// <param name="level">Its actual level.</param>
    /// <param name="cancellationToken">Cancels opening and reporting.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(SeventhHeading, SeventhLevel)]
    [Arguments(DeepHeading, DeepLevel)]
    public async Task Read_Pdf17_PreservesExtendedLevel(string spelling, int level, CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
        [
            Pdf17Catalog,
            EmptyPages,
            $"<< /Type /StructTreeRoot /K 4 0 R /RoleMap << /{spelling} /H6 >> >>",
            "<< /Type /StructElem /S /Document /P 3 0 R /K 5 0 R >>",
            $"<< /Type /StructElem /S /{spelling} /P 4 0 R >>",
        ]);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var heading = GetHeading(document);

        await Assert.That(heading.HeadingLevel).IsEqualTo(level);
        await Assert.That(heading.MappedType).IsEqualTo("H6");
        await Assert.That(report.GetCount(PdfAccessibilityCode.StandardTypeRemapped)).IsEqualTo(0);
    }

    /// <summary>The sequential-level check compares unlimited numbers rather than saturated integers.</summary>
    /// <param name="first">The first heading, deeper than level one.</param>
    /// <param name="second">The following heading.</param>
    /// <param name="expected">The number of skipped-level findings, including the first heading.</param>
    /// <param name="cancellationToken">Cancels opening and reporting.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(LargestIntegerHeading, BeyondIntegerHeading, 1)]
    [Arguments("H9", "H10", 1)]
    [Arguments("H99", "H100", 1)]
    [Arguments(LongHeading, NextLongHeading, 1)]
    [Arguments(LongHeading, SkippedLongHeading, 2)]
    [Arguments(LongHeading, LongHeading, 1)]
    [Arguments(SkippedLongHeading, LongHeading, 1)]
    public async Task Check_NumberedSequence_UsesExactLevels(string first, string second, int expected, CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
        [
            Pdf17Catalog,
            EmptyPages,
            $"<< /Type /StructTreeRoot /K 4 0 R /RoleMap << /{first} /H6 /{second} /H6 >> >>",
            "<< /Type /StructElem /S /Document /P 3 0 R /K [5 0 R 6 0 R] >>",
            $"<< /Type /StructElem /S /{first} /P 4 0 R >>",
            $"<< /Type /StructElem /S /{second} /P 4 0 R >>",
        ]);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(report.GetCount(PdfAccessibilityCode.HeadingLevelSkipped)).IsEqualTo(expected);
    }

    /// <summary>A PDF 2.0 tag tree accepts valid Hn spellings deeper than H6.</summary>
    /// <param name="spelling">The numbered heading's name.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(SeventhHeading)]
    [Arguments(DeepHeading)]
    [Arguments(LargestIntegerHeading)]
    [Arguments(BeyondIntegerHeading)]
    [Arguments(LongHeading)]
    public async Task AddElement_Pdf20_AcceptsNumberedHeading(string spelling)
    {
        var tags = new PdfTagTree(new PdfDocumentBuilder(), true);
        var heading = tags.AddElement(tags.Root, spelling);

        await Assert.That(heading.Type).IsEqualTo(spelling);
    }

    /// <summary>Large numbered names retain their heading semantics without an integer-size limit.</summary>
    /// <param name="spelling">The numbered heading's name.</param>
    /// <param name="cancellationToken">Cancels opening and reporting.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(BeyondIntegerHeading)]
    [Arguments(LongHeading)]
    public async Task Read_Pdf20_LargeNumberRetainsHeadingSemantics(string spelling, CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
        [
            Pdf20Catalog,
            EmptyPages,
            "<< /Type /StructTreeRoot /K 4 0 R /Namespaces [6 0 R] >>",
            Pdf20Document,
            $"<< /Type /StructElem /S /{spelling} /NS 6 0 R /P 4 0 R >>",
            $"<< /Type /Namespace /NS ({PdfStructureTypes.Pdf20Namespace}) >>",
        ]);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        _ = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var heading = GetHeading(document);

        await Assert.That(heading.Role).IsEqualTo(PdfSemanticRole.Heading);
        await Assert.That(heading.RawType).IsEqualTo(spelling);
        await Assert.That(heading.MappedType).IsEqualTo(spelling);
        await Assert.That(heading.HeadingLevelDigits.ToString()).IsEqualTo(spelling[1..]);
        await Assert.That(heading.HeadingLevel).IsEqualTo(int.MaxValue);
    }

    /// <summary>Gets the synthetic document's sole heading after its structure has loaded.</summary>
    /// <param name="document">The opened document.</param>
    /// <returns>The heading under its Document root.</returns>
    private static PdfStructureElement GetHeading(PdfDocument document) => PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
}
