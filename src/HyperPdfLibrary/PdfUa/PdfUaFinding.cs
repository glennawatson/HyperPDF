// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.PdfUa;

/// <summary>A problem the preflight found, before it is described for the user.</summary>
/// <param name="Requirement">What it is about.</param>
/// <param name="PageIndex">The zero based page, or -1 for the whole document.</param>
[DebuggerDisplay("PdfUaFinding: {Requirement} page {PageIndex}")]
internal readonly record struct PdfUaFinding(PdfUaRequirement Requirement, int PageIndex)
{
    /// <summary>The page index of a finding about the whole document.</summary>
    internal const int DocumentLevel = -1;
}
