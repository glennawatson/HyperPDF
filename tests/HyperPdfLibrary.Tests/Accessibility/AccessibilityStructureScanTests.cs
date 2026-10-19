// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks lazy structure-walk leases and cleanup after interrupted scans.</summary>
public sealed class AccessibilityStructureScanTests
{
    /// <summary>The number of names with actual root role-map entries.</summary>
    private const int MapCount = 2;

    /// <summary>Unmapped role-map entries do not require per-report working dictionaries.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Run_UnusedRoleMap_LeavesWorkingCollectionsUnleased(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new() { RootExtra = "/RoleMap << /Unused /P >>" }));
        var findings = AccessibilityFindingCollection.Rent(document.Objects.ScratchPools);
        var state = Create(document, findings, cancellationToken);
        try
        {
            AccessibilityStructureScan.Run(ref state);
            await Assert.That(state.NamespaceMaps).IsNull();
            await Assert.That(state.RoleMapFacts).IsNull();
            await Assert.That(state.Owners).IsNull();
            await Assert.That(state.TablePages).IsNull();
        }
        finally
        {
            AccessibilityStructureScan.Release(state);
            AccessibilityFindingCollection.Release(document.Objects.ScratchPools, findings);
        }
    }

    /// <summary>Interrupted scans return cleared mapped-name storage for the next independent scan.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Release_AfterCancellation_ReturnsClearedExclusiveStorage(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()
        {
            RootExtra = "/RoleMap << /Mapped /P /Loop /Loop >>",
            Layout = static (pdf, parent) => AccessibilityPdfs.Kids(
                pdf.Element("Mapped", parent, "/K 0"),
                pdf.Element("P", parent, "/K 1"),
                pdf.Element("Loop", parent, "/K 2")),
        }));
        var pools = document.Objects.ScratchPools;
        var findings = AccessibilityFindingCollection.Rent(pools);
        var state = Create(document, findings, cancellationToken);
        Dictionary<PdfName, AccessibilityStructureScan.RoleMapFacts>? first;
        try
        {
            AccessibilityStructureScan.Run(ref state);
            first = state.RoleMapFacts;
            await Assert.That(first).IsNotNull();
            await Assert.That(first!.Count).IsEqualTo(MapCount);
            await Assert.That(AccessibilityFindingCollection.GetCounts(findings)[0].Code).IsEqualTo(PdfAccessibilityCode.RoleMapCycle);
            using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await cancelled.CancelAsync();
            state = state with { Cancellation = cancelled.Token };
            await Assert.That(() => AccessibilityStructureScan.Run(ref state)).Throws<OperationCanceledException>();
        }
        finally
        {
            AccessibilityStructureScan.Release(state);
            AccessibilityFindingCollection.Release(pools, findings);
        }

        var reused = pools.Slots<Dictionary<PdfName, AccessibilityStructureScan.RoleMapFacts>>().Take();
        try
        {
            await Assert.That(reused).IsSameReferenceAs(first);
            await Assert.That(reused!.Count).IsEqualTo(0);
            await Assert.That(pools.Slots<Dictionary<PdfName, AccessibilityStructureScan.RoleMapFacts>>().Take()).IsNull();
        }
        finally
        {
            if (reused is not null)
            {
                _ = pools.Slots<Dictionary<PdfName, AccessibilityStructureScan.RoleMapFacts>>().Offer(reused);
            }
        }
    }

    /// <summary>Creates independently owned walk state.</summary>
    /// <param name="document">The document to check.</param>
    /// <param name="findings">Receives findings.</param>
    /// <param name="cancellation">Cancels the walk.</param>
    /// <returns>The walk state.</returns>
    private static AccessibilityStructureState Create(PdfDocument document, AccessibilityFindings findings, CancellationToken cancellation)
    {
        var tree = PdfDocumentTagged.GetStructureTree(document)!;
        return new(
            tree,
            tree.Root.GetDictionary(KnownName.RoleMap),
            PdfUaClaim.Part1,
            findings,
            document.Objects.ScratchPools,
            null,
            null,
            null,
            null,
            null,
            string.Empty,
            false,
            cancellation);
    }
}
