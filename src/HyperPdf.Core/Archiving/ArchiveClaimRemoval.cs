// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Archiving;

/// <summary>
/// A save that removed a file's PDF/A claim, because the saved file no longer met the level it claimed and could not be
/// fixed. Tell the user, so they know the file is no longer PDF/A.
/// </summary>
/// <param name="Level">The level the file claimed.</param>
/// <param name="BrokenRules">The rules the file broke, in plain words.</param>
[DebuggerDisplay("ArchiveClaimRemoval: {Level}, {BrokenRules.Count} rules")]
public sealed record ArchiveClaimRemoval(ArchiveLevel Level, IReadOnlyList<string> BrokenRules)
{
    /// <summary>Gets why the file could not be fixed, in plain words; empty when no reason was given.</summary>
    public IReadOnlyList<string> Reasons { get; init; } = [];
}
