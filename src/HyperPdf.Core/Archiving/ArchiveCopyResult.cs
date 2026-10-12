// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Archiving;

/// <summary>The outcome of writing a PDF/A copy.</summary>
/// <param name="Level">The level asked for.</param>
/// <param name="Changes">What was changed so the copy meets the level.</param>
/// <param name="Problems">Why the copy could not be made; empty when it was written.</param>
[DebuggerDisplay("ArchiveCopyResult: {Level}, converted {Converted}")]
public sealed record ArchiveCopyResult(ArchiveLevel Level, IReadOnlyList<ArchiveNote> Changes, IReadOnlyList<ArchiveNote> Problems)
{
    /// <summary>Gets a value indicating whether the copy was written.</summary>
    public bool Converted => Problems.Count == 0;
}
