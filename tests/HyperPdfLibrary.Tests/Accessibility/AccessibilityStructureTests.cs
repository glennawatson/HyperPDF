// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Accessibility;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Tests for the structure findings: types, headings, figures, tables and lists.</summary>
[NotInParallel]
public sealed class AccessibilityStructureTests
{
    /// <summary>The number of marked content lines the table documents draw.</summary>
    private const int TableLines = 4;

    /// <summary>Unmapped types, mapped types and role map loops.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RoleMapProblemsAreFlagged()
    {
        var unmapped = await AccessibilityPdfs.ReportAsync(Typed("Mystery", string.Empty));
        var mapped = await AccessibilityPdfs.ReportAsync(Typed("Mystery", "/RoleMap << /Mystery /P >>"));
        var cycle = await AccessibilityPdfs.ReportAsync(Typed("X", "/RoleMap << /X /Y /Y /X >>"));

        await Assert.That(unmapped.GetCount(PdfAccessibilityCode.UnmappedStructureType)).IsEqualTo(1);
        await Assert.That(unmapped.Findings[0].ElementId).IsNotNull();
        await Assert.That(mapped.Findings.Count).IsEqualTo(0);
        await Assert.That(cycle.GetCount(PdfAccessibilityCode.RoleMapCycle)).IsEqualTo(1);
        await Assert.That(cycle.GetCount(PdfAccessibilityCode.UnmappedStructureType)).IsEqualTo(0);
    }

    /// <summary>Skipped heading levels and a heading that does not start at level 1 are flagged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkippedHeadingLevelsAreFlagged()
    {
        var skipped = await AccessibilityPdfs.ReportAsync(Headings("H1", "H3"));
        var orderly = await AccessibilityPdfs.ReportAsync(Headings("H1", "H2", "H3", "H2", "H1"));
        var startsDeep = await AccessibilityPdfs.ReportAsync(Headings("H2"));

        await Assert.That(skipped.GetCount(PdfAccessibilityCode.HeadingLevelSkipped)).IsEqualTo(1);
        await Assert.That(orderly.GetCount(PdfAccessibilityCode.HeadingLevelSkipped)).IsEqualTo(0);
        await Assert.That(startsDeep.GetCount(PdfAccessibilityCode.HeadingLevelSkipped)).IsEqualTo(1);
    }

    /// <summary>Repeated level 1 headings are allowed by both PDF/UA parts.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatedLevelOneHeadingsAreAllowed()
    {
        var part1 = await AccessibilityPdfs.ReportAsync(Headings("H1", "H1"));
        var part2 = await AccessibilityPdfs.ReportAsync(Headings("H1", "H1") with { Xmp = AccessibilityPdfs.Packet(AccessibilityPdfs.PartTwo, true) });

        await Assert.That(part1.GetCount(PdfAccessibilityCode.MultipleLevelOneHeadings)).IsEqualTo(0);
        await Assert.That(part2.GetCount(PdfAccessibilityCode.MultipleLevelOneHeadings)).IsEqualTo(0);
    }

    /// <summary>Strongly structured PDF/UA-1 sections may each contain a generic heading.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeparateSectionsMayHaveGenericHeadings()
    {
        var report = await AccessibilityPdfs.ReportAsync(new()
        {
            Layout = static (pdf, document) =>
            {
                var first = pdf.Reserve();
                var second = pdf.Reserve();
                var firstHeading = pdf.Element("H", first, "/K 0");
                var secondHeading = pdf.Element("H", second, "/K 1");
                pdf.SetElement(first, "Sect", document, $"/K [{firstHeading} 0 R]");
                pdf.SetElement(second, "Sect", document, $"/K [{secondHeading} 0 R]");
                return AccessibilityPdfs.Kids(first, second);
            },
        });

        await Assert.That(report.GetCount(PdfAccessibilityCode.MultipleLevelOneHeadings)).IsEqualTo(0);
        await Assert.That(report.GetCount(PdfAccessibilityCode.HeadingLevelSkipped)).IsEqualTo(0);
        await Assert.That(report.GetCount(PdfAccessibilityCode.HeadingTypeNotAllowed)).IsEqualTo(0);
    }

    /// <summary>A figure needs <c>/Alt</c> or <c>/ActualText</c>.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FiguresNeedADescription()
    {
        var bare = await AccessibilityPdfs.ReportAsync(Figure(string.Empty));
        var alt = await AccessibilityPdfs.ReportAsync(Figure("/Alt (A box)"));
        var actual = await AccessibilityPdfs.ReportAsync(Figure("/ActualText (A box)"));

        await Assert.That(bare.GetCount(PdfAccessibilityCode.FigureNoDescription)).IsEqualTo(1);
        await Assert.That(bare.Findings[0].PageIndex).IsEqualTo(0);
        await Assert.That(alt.Findings.Count).IsEqualTo(0);
        await Assert.That(actual.Findings.Count).IsEqualTo(0);
    }

    /// <summary>A list item must sit directly in a list.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListItemsNeedAList()
    {
        var inList = await AccessibilityPdfs.ReportAsync(ListItem("L"));
        var loose = await AccessibilityPdfs.ReportAsync(ListItem("Div"));

        await Assert.That(inList.Findings.Count).IsEqualTo(0);
        await Assert.That(loose.GetCount(PdfAccessibilityCode.ListItemOutsideList)).IsEqualTo(1);
    }

    /// <summary>A table with no header cell is flagged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TableWithoutHeadersIsFlagged()
    {
        var report = await AccessibilityPdfs.ReportAsync(Table(string.Empty, "TD", "TD"));

        await Assert.That(report.GetCount(PdfAccessibilityCode.TableNoHeaderCells)).IsEqualTo(1);
        await Assert.That(report.GetCount(PdfAccessibilityCode.TableHeaderNoScope)).IsEqualTo(0);
    }

    /// <summary>Header cells that state no scope, and no cell lists headers, are flagged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeadersWithoutScopeAreFlagged()
    {
        var report = await AccessibilityPdfs.ReportAsync(Table(string.Empty, "TH", "TD"));

        await Assert.That(report.GetCount(PdfAccessibilityCode.TableHeaderNoScope)).IsEqualTo(1);
        await Assert.That(report.GetCount(PdfAccessibilityCode.TableNoHeaderCells)).IsEqualTo(0);
        await Assert.That(report.GetCount(PdfAccessibilityCode.TableCellNoHeader)).IsEqualTo(0);
    }

    /// <summary>A header with a scope above its cells makes a clean table.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScopedHeadersMakeACleanTable()
    {
        var report = await AccessibilityPdfs.ReportAsync(Table("/A << /O /Table /Scope /Column >>", "TH", "TD"));

        await Assert.That(report.Findings.Count).IsEqualTo(0);
    }

    /// <summary>A data cell above every header cell is labelled by none and is flagged once for the table.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnlabelledCellsAreFlagged()
    {
        var report = await AccessibilityPdfs.ReportAsync(Table("/A << /O /Table /Scope /Column >>", "TD", "TH"));

        await Assert.That(report.GetCount(PdfAccessibilityCode.TableCellNoHeader)).IsEqualTo(1);
        await Assert.That(report.GetCount(PdfAccessibilityCode.TableHeaderNoScope)).IsEqualTo(0);
    }

    /// <summary>Makes a spec with one element of a type and extra root entries.</summary>
    /// <param name="type">The element's type.</param>
    /// <param name="rootExtra">Extra structure tree root entries.</param>
    /// <returns>The spec.</returns>
    private static AccessibilitySpec Typed(string type, string rootExtra) =>
        new() { RootExtra = rootExtra, Layout = (pdf, document) => AccessibilityPdfs.Kids(pdf.Element(type, document, "/K 0")) };

    /// <summary>Makes a spec whose elements are headings of the given types, in order.</summary>
    /// <param name="types">The heading types.</param>
    /// <returns>The spec.</returns>
    private static AccessibilitySpec Headings(params string[] types) => new()
    {
        Layout = (pdf, document) =>
        {
            var kids = new StringBuilder();
            foreach (var type in types)
            {
                _ = kids.Append(AccessibilityPdfs.Kids(pdf.Element(type, document, "/K 0")));
            }

            return kids.ToString();
        },
    };

    /// <summary>Makes a spec with a figure that has the given entries.</summary>
    /// <param name="entries">The figure's entries.</param>
    /// <returns>The spec.</returns>
    private static AccessibilitySpec Figure(string entries) =>
        new() { Layout = (pdf, document) => AccessibilityPdfs.Kids(pdf.Element("Figure", document, $"/K 2 {entries}")) };

    /// <summary>Makes a spec with a list item inside a parent of the given type.</summary>
    /// <param name="parentType">The parent's type.</param>
    /// <returns>The spec.</returns>
    private static AccessibilitySpec ListItem(string parentType) => new()
    {
        Layout = (pdf, document) =>
        {
            var parent = pdf.Reserve();
            var item = pdf.Element("LI", parent, "/K 0");
            pdf.SetElement(parent, parentType, document, $"/K [{item} 0 R]");
            return AccessibilityPdfs.Kids(parent);
        },
    };

    /// <summary>Makes a spec with a two-row table: one cell of each given type per row, each with its own text.</summary>
    /// <param name="headerEntries">Entries for every <c>TH</c> cell.</param>
    /// <param name="firstRow">The first row's cell type.</param>
    /// <param name="secondRow">The second row's cell type.</param>
    /// <returns>The spec.</returns>
    private static AccessibilitySpec Table(string headerEntries, string firstRow, string secondRow) => new()
    {
        Content = AccessibilityPdfs.Lines(TableLines),
        Layout = (pdf, document) =>
        {
            var table = pdf.Reserve();
            var rows = new StringBuilder();
            string[] types = [firstRow, secondRow];
            for (var i = 0; i < types.Length; i++)
            {
                var row = pdf.Reserve();
                var entries = types[i] == "TH" ? headerEntries : string.Empty;
                var cell = pdf.Element(types[i], row, string.Create(CultureInfo.InvariantCulture, $"/K {i} {entries}"));
                pdf.SetElement(row, "TR", table, $"/K [{cell} 0 R]");
                _ = rows.Append(AccessibilityPdfs.Kids(row));
            }

            pdf.SetElement(table, "Table", document, $"/K [{rows}]");
            return AccessibilityPdfs.Kids(table);
        },
    };
}
