// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Requirements;

/// <summary>The requirements a document declares, read without running anything.</summary>
/// <param name="Requirements">The requirement dictionaries that could be read, in array order.</param>
/// <param name="SkippedEntries">How many array entries were not requirement dictionaries or had no requirement type, and were skipped.</param>
/// <param name="WasTruncated">Whether the array held more entries than the reader keeps, so the rest were not read.</param>
[DebuggerDisplay("PdfRequirementList: {Requirements.Count} requirements, {SkippedEntries} skipped")]
public sealed record PdfRequirementList(IReadOnlyList<PdfRequirement> Requirements, int SkippedEntries, bool WasTruncated)
{
    /// <summary>Gets the list of a document with no <c>/Requirements</c> entry.</summary>
    public static PdfRequirementList Empty { get; } = new([], 0, false);

    /// <summary>Gets a value indicating whether the document declares nothing to check.</summary>
    public bool IsEmpty => Requirements.Count == 0 && SkippedEntries == 0 && !WasTruncated;
}
