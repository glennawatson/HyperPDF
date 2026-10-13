// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Archiving;

/// <summary>What saving a PDF 2.0 copy did: what it changed, moved or left out, or why it was refused.</summary>
/// <param name="Changes">What the copy changed to meet ISO 32000-2, in plain words.</param>
/// <param name="Problems">Why the copy was refused; empty when it was written.</param>
[DebuggerDisplay("Pdf20CopyResult: converted {Converted}")]
public sealed record Pdf20CopyResult(IReadOnlyList<ArchiveNote> Changes, IReadOnlyList<ArchiveNote> Problems)
{
    /// <summary>Gets a value indicating whether the copy was written.</summary>
    public bool Converted => Problems.Count == 0;
}
