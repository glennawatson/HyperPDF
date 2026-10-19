// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Tests for the tables, lists and figures the optimiser writes as inferred tags, and for what the accessibility report finds in them.</summary>
[NotInParallel]
public sealed class InferredTagWritingTests
{
    /// <summary>The description the host gives the figure.</summary>
    private const string FigureDescription = "A grey square";

    /// <summary>The language the options give.</summary>
    private const string Language = "en-GB";

    /// <summary>The roles the sample page is expected to produce.</summary>
    private static readonly PdfSemanticRole[] ExpectedRoles =
    [
        PdfSemanticRole.Table,
        PdfSemanticRole.TableRow,
        PdfSemanticRole.TableHeaderCell,
        PdfSemanticRole.TableCell,
        PdfSemanticRole.List,
        PdfSemanticRole.ListItem,
        PdfSemanticRole.ListLabel,
        PdfSemanticRole.ListBody,
        PdfSemanticRole.Figure,
        PdfSemanticRole.Caption,
    ];

    /// <summary>The codes that point at table, list, role or marking problems in the structure the optimiser wrote.</summary>
    private static readonly PdfAccessibilityCode[] StructureCodes =
    [
        PdfAccessibilityCode.NotMarked,
        PdfAccessibilityCode.NoStructureTree,
        PdfAccessibilityCode.UntaggedContent,
        PdfAccessibilityCode.UnmappedStructureType,
        PdfAccessibilityCode.RoleMapCycle,
        PdfAccessibilityCode.HeadingLevelSkipped,
        PdfAccessibilityCode.TableNoHeaderCells,
        PdfAccessibilityCode.TableHeaderNoScope,
        PdfAccessibilityCode.TableCellNoHeader,
        PdfAccessibilityCode.ListItemOutsideList,
    ];

    /// <summary>The tags written for a table, a list and a figure read back as the same roles.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesTablesListsAndFigures()
    {
        using var document = Optimize(null);
        var roles = new List<PdfSemanticRole>();
        foreach (var node in PdfReadingStructure.ReadTagged(document, 0).Nodes)
        {
            Collect(node, roles);
        }

        foreach (var role in ExpectedRoles)
        {
            await Assert.That(roles).Contains(role);
        }
    }

    /// <summary>The structure checks of the accessibility report find nothing wrong in the tags the optimiser wrote.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PassesStructureChecks()
    {
        using var document = Optimize(FigureDescription);
        var report = PdfDocumentAccessibility.GetAccessibilityReport(document);

        foreach (var code in StructureCodes)
        {
            await Assert.That(report.GetCount(code)).IsEqualTo(0);
        }

        await Assert.That(report.GetCount(PdfAccessibilityCode.FigureNoDescription)).IsEqualTo(0);
    }

    /// <summary>
    /// The tags the optimiser wrote break none of the Well-Tagged PDF structure rules (clauses 8.2.4 and 8.2.5), apart from
    /// 8.2.5.2: the optimiser keeps the file's version, so the Document tag has no PDF 2.0 namespace until the file is made PDF 2.0.
    /// </summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task PassesWtpdfStructureChecks(CancellationToken cancellationToken)
    {
        using var document = Optimize(FigureDescription);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        var structure = report.Findings.Where(static f => f.Clause.StartsWith("8.2", StringComparison.Ordinal) && f.Clause != "8.2.5.2").Select(static f => $"{f.Clause}: {f.Message}").ToArray();
        await Assert.That(structure).IsEmpty().Because(string.Join(" | ", structure));
    }

    /// <summary>A figure with no description from the host is reported, as before.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsFigureWithoutDescription()
    {
        using var document = Optimize(null);

        await Assert.That(PdfDocumentAccessibility.GetAccessibilityReport(document).GetCount(PdfAccessibilityCode.FigureNoDescription)).IsEqualTo(1);
    }

    /// <summary>Optimises the sample page with inferred tags.</summary>
    /// <param name="description">The description the host gives figures, or <see langword="null"/> for none.</param>
    /// <returns>The optimised document.</returns>
    private static PdfDocument Optimize(string? description)
    {
        PdfFigureAltTextProvider? provider = description is null ? null : (_, _, _) => description;
        var options = PdfOptimizeOptions.KeepQuality with { AddInferredTags = true, Language = Language, FigureAltText = provider };
        var result = OptimizerTestKit.Optimize(InferredSamples.TableListAndFigure(), options);
        return SkiaTestRegistration.Open(result.Bytes, null);
    }

    /// <summary>Lists a node's role and those of its descendants.</summary>
    /// <param name="node">The node.</param>
    /// <param name="roles">Receives the roles.</param>
    private static void Collect(PdfSemanticNode node, List<PdfSemanticRole> roles)
    {
        roles.Add(node.Role);
        foreach (var child in node.Children)
        {
            Collect(child, roles);
        }
    }
}
