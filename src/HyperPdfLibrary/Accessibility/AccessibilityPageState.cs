// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Holds a report's page checks without allocating a page-walk object.</summary>
/// <param name="Document">The document being checked.</param>
/// <param name="Tree">Its structure tree, when present.</param>
/// <param name="Owners">The structure walk's annotation owners.</param>
/// <param name="TablePages">The structure walk's table pages.</param>
/// <param name="Part">The claimed PDF/UA part.</param>
/// <param name="Findings">Receives the page findings.</param>
/// <param name="CheckedTables">Tables already checked, leased only when a table is encountered.</param>
/// <param name="Cancellation">Cancels the checks.</param>
internal readonly record struct AccessibilityPageState(
    PdfDocument Document,
    PdfStructureTree? Tree,
    Dictionary<int, PdfStructureElement>? Owners,
    HashSet<int>? TablePages,
    int? Part,
    AccessibilityFindings Findings,
    HashSet<PdfStructureElement>? CheckedTables,
    CancellationToken Cancellation);
