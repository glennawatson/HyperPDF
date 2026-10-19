// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.PdfUa;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks the parent-child rules of ISO/TS 32005:2023 clause 7.2 against small trees.</summary>
public sealed class StructureContainmentTests
{
    /// <summary>The object number of the first child in the occurrence test's file.</summary>
    private const int FirstKidObject = 8;

    /// <summary>The table rows a few well-known rules come from.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TableSaysWhatCommonParentsHold()
    {
        using (Assert.Multiple())
        {
            await Assert.That(StructureContainment.Allows("P", "Span")).IsTrue();
            await Assert.That(StructureContainment.Allows("P", "P")).IsFalse();
            await Assert.That(StructureContainment.Allows("L", "LI")).IsTrue();
            await Assert.That(StructureContainment.Allows("L", "P")).IsFalse();
            await Assert.That(StructureContainment.Allows("Table", "TR")).IsTrue();
            await Assert.That(StructureContainment.Allows("TR", "TD")).IsTrue();
            await Assert.That(StructureContainment.Allows("TR", "P")).IsFalse();
            await Assert.That(StructureContainment.Allows(StructureContainment.NumberedHeading, "Span")).IsTrue();
            await Assert.That(StructureContainment.AllowsAtRoot("Document")).IsTrue();
            await Assert.That(StructureContainment.AllowsAtRoot("P")).IsFalse();
            await Assert.That(StructureContainment.Allows("NotAType", "P")).IsTrue();
        }
    }

    /// <summary>A section inside a paragraph is reported in a PDF 2.0 tree, a section inside a section is not, and a PDF 1.7 only tree is exempt.</summary>
    /// <param name="parent">The parent type.</param>
    /// <param name="child">The child type.</param>
    /// <param name="useNamespace">Whether the tags are in the PDF 2.0 namespace.</param>
    /// <param name="expected">The expected number of findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("P", "Sect", true, 1)]
    [Arguments("Sect", "P", true, 0)]
    [Arguments("P", "Sect", false, 0)]
    [Arguments("L", "P", true, 1)]
    [Arguments("TR", "P", true, 2)]
    public async Task ParentRulesApplyToPdf20Trees(string parent, string child, bool useNamespace, int expected, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(parent, child, useNamespace), null, cancellationToken);

        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(report.GetCount(PdfAccessibilityCode.StructureContainmentViolation)).IsEqualTo(expected);
    }

    /// <summary>A second caption in a list, or a second header group in a table, is reported once; a table with one of each is not.</summary>
    /// <param name="parent">The parent type.</param>
    /// <param name="children">The children's types, in order.</param>
    /// <param name="expected">The expected number of findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("L", "Caption,Caption", 1)]
    [Arguments("L", "Caption,LI", 0)]
    [Arguments("Table", "THead,TBody,TFoot", 0)]
    [Arguments("Table", "THead,THead,TFoot,TFoot", 2)]
    [Arguments("Figure", "Caption,Caption", 1)]
    public async Task ChildTypesAllowedOnceAreCounted(string parent, string children, int expected, CancellationToken cancellationToken)
    {
        var kids = children.Split(',');
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
            "<< /Type /StructElem /S /Document /NS 7 0 R /P 5 0 R /K [6 0 R] >>",
            "<< /Type /StructTreeRoot /K 4 0 R /Namespaces [7 0 R] >>",
            $"<< /Type /StructElem /S /{parent} /NS 7 0 R /P 4 0 R /K [{string.Join(' ', Enumerable.Range(0, kids.Length).Select(static i => $"{FirstKidObject + i} 0 R"))}] >>",
            "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>",
        };
        foreach (var kid in kids)
        {
            objects.Add($"<< /Type /StructElem /S /{kid} /NS 7 0 R /P 6 0 R >>");
        }

        using var document = await PdfDocumentReader.OpenAsync(MiniPdf.Build([.. objects]), null, cancellationToken);

        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(report.GetCount(PdfAccessibilityCode.StructureOccurrenceViolation)).IsEqualTo(expected);
    }

    /// <summary>For a PDF/UA-2 claim the finding is a requirement with its clause; for part 1 it is not a rule.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task Part2MakesContainmentARequirement(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build("P", "Sect", true), null, cancellationToken);

        var part2 = PdfUaPreflight.Check(document.Objects, false, PdfUaClaim.Part2, cancellationToken);
        var part1 = PdfUaPreflight.Check(document.Objects, false, PdfUaClaim.Part1, cancellationToken);

        await Assert.That(part2.Exists(static f => f.Requirement == PdfUaRequirement.Part2Containment)).IsTrue();
        await Assert.That(part1.Exists(static f => f.Requirement == PdfUaRequirement.Part2Containment)).IsFalse();
        await Assert.That(PdfUaRequirements.Clause(PdfUaRequirement.Part2Containment, PdfUaClaim.Part2)).IsEqualTo("8.2.4");
    }

    /// <summary>Builds a document of a Document with two nested tags.</summary>
    /// <param name="parent">The first nested tag's type.</param>
    /// <param name="child">The second nested tag's type, inside the first.</param>
    /// <param name="useNamespace">Whether the tags are in the PDF 2.0 namespace.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(string parent, string child, bool useNamespace)
    {
        var ns = useNamespace ? " /NS 8 0 R" : string.Empty;
        var namespaces = useNamespace ? " /Namespaces [8 0 R]" : string.Empty;
        return MiniPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
            $"<< /Type /StructElem /S /Document{ns} /P 5 0 R /K [6 0 R] >>",
            $"<< /Type /StructTreeRoot /K 4 0 R{namespaces} >>",
            $"<< /Type /StructElem /S /{parent}{ns} /P 4 0 R /K [7 0 R] >>",
            $"<< /Type /StructElem /S /{child}{ns} /P 6 0 R /Pg 3 0 R /K 0 >>",
            "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>",
        ]);
    }
}
