// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>The preseparated pages of a document, grouped by printed page, and the problems found reading them.</summary>
/// <param name="Groups">The groups, in order of each group's first page; empty for a document of ordinary composite pages.</param>
/// <param name="Findings">The problems found, in page order.</param>
[DebuggerDisplay("PdfSeparationInfo: {Groups.Length} groups, {Findings.Length} findings")]
public sealed record PdfSeparationInfo(PdfSeparationGroup[] Groups, PdfSeparationFinding[] Findings)
{
    /// <summary>Gets the result for a document with no separation pages.</summary>
    public static PdfSeparationInfo Empty { get; } = new([], []);
}
