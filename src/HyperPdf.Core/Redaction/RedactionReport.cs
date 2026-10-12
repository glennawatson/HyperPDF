// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Archiving;

namespace HyperPdf.Core.Redaction;

/// <summary>What applying redactions removed.</summary>
/// <param name="Areas">The marked areas applied.</param>
/// <param name="Pages">The pages changed.</param>
/// <param name="Characters">The text characters removed.</param>
/// <param name="Pictures">The pictures removed or blanked.</param>
/// <param name="Drawings">The drawings removed.</param>
/// <param name="Annotations">The links, comments and form fields removed.</param>
[DebuggerDisplay("RedactionReport: {Areas} areas on {Pages} pages")]
public sealed record RedactionReport(int Areas, int Pages, int Characters, int Pictures, int Drawings, int Annotations)
{
    /// <summary>Gets a value indicating whether nothing was marked.</summary>
    public bool NothingMarked => Areas == 0;

    /// <summary>
    /// Gets the PDF/A claim the redacted copy lost because it no longer met the level, or <see langword="null"/> when the
    /// copy kept its claim or had none.
    /// </summary>
    public ArchiveClaimRemoval? ClaimRemoved { get; init; }

    /// <summary>Gets the fixes made so the redacted copy still meets its PDF/A level, or <see langword="null"/>.</summary>
    public ArchiveClaimRepair? ClaimRepaired { get; init; }
}
