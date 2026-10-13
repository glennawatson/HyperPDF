// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Archiving;

namespace HyperPdf.Core.Documents;

/// <summary>Receives what a save found that the user should know. One report is for one save.</summary>
[DebuggerDisplay("DocumentSaveReport: claim removed {ClaimRemoved}")]
public sealed class DocumentSaveReport
{
    /// <summary>
    /// Gets or sets the PDF/A claim the save removed because the saved file no longer met it, or <see langword="null"/>
    /// when the file kept its claim or had none.
    /// </summary>
    public ArchiveClaimRemoval? ClaimRemoved { get; set; }

    /// <summary>Gets or sets the fixes the save made so the file still meets its PDF/A level, or <see langword="null"/>.</summary>
    public ArchiveClaimRepair? ClaimRepaired { get; set; }

    /// <summary>
    /// Gets or sets a sentence in plain words about a security requirement that stopped the save, such as an encrypted
    /// document that needs a PDF MAC (ISO/TS 32004) the writer could not make, or <see langword="null"/> when none did.
    /// Nothing was saved when it is set.
    /// </summary>
    public string? Failure { get; set; }

    /// <summary>
    /// Gets the warnings, in plain words, about trap networks the save removed because the pages they were made for had
    /// changed. Trap networks are the colour overlaps a print shop adds; HyperPDF cannot make new ones. Empty when none were.
    /// </summary>
    public List<string> TrapNetworkWarnings { get; } = [];

    /// <summary>
    /// Gets the warnings, in plain words, about document requirements the save removed because an edit left the file
    /// without the feature each one names. Empty when none were.
    /// </summary>
    public List<string> RequirementWarnings { get; } = [];
}
