// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Accessibility;

/// <summary>Holds exclusively leased finding and count storage for one report.</summary>
/// <param name="Findings">The retained findings in discovery order.</param>
/// <param name="Counts">Working counts indexed by accessibility code.</param>
internal readonly record struct AccessibilityFindings(List<PdfAccessibilityFinding> Findings, int[] Counts)
{
    /// <summary>The most findings kept for one code.</summary>
    internal const int MaxFindingsPerCode = 200;

    /// <summary>The page index of a finding about the whole document.</summary>
    internal const int DocumentLevel = -1;
}
