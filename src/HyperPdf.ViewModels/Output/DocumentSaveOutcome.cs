// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Archiving;
using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Output;

/// <summary>The result of writing and publishing a document.</summary>
/// <param name="Saved">Whether the destination was replaced.</param>
/// <param name="Error">A file-system error to show to the reader, or <see langword="null"/>.</param>
/// <param name="ClaimRemoved">The PDF/A claim the save removed, or <see langword="null"/>.</param>
internal readonly record struct DocumentSaveOutcome(bool Saved, string? Error, ArchiveClaimRemoval? ClaimRemoved)
{
    /// <summary>Initializes a new instance of the <see cref="DocumentSaveOutcome"/> struct for a save that removed no claim.</summary>
    /// <param name="saved">Whether the destination was replaced.</param>
    /// <param name="error">A file-system error to show to the reader, or <see langword="null"/>.</param>
    internal DocumentSaveOutcome(bool saved, string? error)
        : this(
        saved,
        error,
        null)
    {
    }

    /// <summary>Gets the fixes the save made so the file still meets its PDF/A level, or <see langword="null"/>.</summary>
    internal ArchiveClaimRepair? ClaimRepaired { get; init; }

    /// <summary>Gets the warnings about trap networks the save removed; empty when it removed none.</summary>
    internal IReadOnlyList<string> TrapNetworkWarnings { get; init; } = [];

    /// <summary>Gets the warnings about document requirements the save removed; empty when it removed none.</summary>
    internal IReadOnlyList<string> RequirementWarnings { get; init; } = [];

    /// <summary>Gets typed requirement removals whose text follows the interface culture.</summary>
    internal IReadOnlyList<RemovedRequirement> RemovedRequirements { get; init; } = [];
}
