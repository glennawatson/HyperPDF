// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Holds one structure walk and its exclusively leased working collections.</summary>
/// <param name="Tree">The document's structure tree.</param>
/// <param name="RoleMap">The root role map, when present.</param>
/// <param name="Part">The claimed PDF/UA part.</param>
/// <param name="Findings">Receives the walk's findings.</param>
/// <param name="Pools">Owns the walk's reusable storage.</param>
/// <param name="NamespaceMaps">Facts about namespace role maps, leased on first use.</param>
/// <param name="RoleMapFacts">Facts about mapped root types, leased on first use.</param>
/// <param name="Owners">Annotation owners, leased when an object reference is found.</param>
/// <param name="TablePages">Pages on which a table starts, leased when a table is found.</param>
/// <param name="LastHeading">The last numbered heading level.</param>
/// <param name="UsesPdf20Namespace">Whether the walk encountered the PDF 2.0 namespace.</param>
/// <param name="Cancellation">Cancels the walk.</param>
internal readonly record struct AccessibilityStructureState(
    PdfStructureTree Tree,
    PdfDictionary? RoleMap,
    int? Part,
    AccessibilityFindings Findings,
    ScratchPools Pools,
    Dictionary<AccessibilityStructureScan.NamespaceType, NamespaceMapProblem>? NamespaceMaps,
    Dictionary<PdfName, AccessibilityStructureScan.RoleMapFacts>? RoleMapFacts,
    Dictionary<int, PdfStructureElement>? Owners,
    HashSet<int>? TablePages,
    int LastHeading,
    bool UsesPdf20Namespace,
    CancellationToken Cancellation);
