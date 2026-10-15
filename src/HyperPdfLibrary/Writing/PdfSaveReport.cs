// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.PdfUa;
using HyperPdfLibrary.Requirements;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Receives what a save found that the user should know. Pass one to a writer's save overload, then read it after the
/// save; one report is for one save.
/// </summary>
[DebuggerDisplay("PdfSaveReport: claim {Claim}")]
public sealed class PdfSaveReport
{
    /// <summary>What the save did with the trap networks.</summary>
    private PdfTrapNetworkChange[] _trapNetworks = [];

    /// <summary>The requirements the save removed.</summary>
    private PdfRequirementChange[] _requirements = [];

    /// <summary>
    /// Gets the check of the document's PDF/A claim, or <see langword="null"/> when the saved file claims no PDF/A level.
    /// When <see cref="PdfAClaimCheck.Repaired"/> is set, the written file broke its level and the save wrote a repaired
    /// copy instead; an incremental save then writes that copy in full (<see cref="PdfAClaimCheck.Rewritten"/>). The
    /// document in memory is not changed by a repair. When <see cref="PdfAClaimCheck.Removed"/> is set, the file could not
    /// be repaired, so the claim was removed before writing; tell the user why.
    /// </summary>
    public PdfAClaimCheck? Claim { get; internal set; }

    /// <summary>
    /// Gets or sets what the save does when the document claims PDF/UA-1 and a problem it cannot fix would make the claim
    /// false. The default refuses the save with a <see cref="PdfException"/> whose <see cref="PdfException.Error"/> is
    /// <see cref="PdfError.ClaimUnmet"/> and writes nothing; <see cref="PdfUaUnmetClaimAction.RemoveClaim"/> removes the claim
    /// and saves, which the caller offers after telling the user why the claim cannot be kept.
    /// </summary>
    public PdfUaUnmetClaimAction UnmetUaClaim { get; set; }

    /// <summary>
    /// Gets the check of the document's PDF/UA-1 claim, or <see langword="null"/> when the saved file claims none. The
    /// problems the save could fix are repaired in the document in memory as one undoable edit and listed as repaired; tell
    /// the user what they were. When a save is refused, the report still holds the problems, so the caller can say why.
    /// </summary>
    public PdfUaClaimCheck? UaClaim { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether the saved revision is an encrypted document's, which needs a PDF MAC (ISO/TS 32004), and
    /// so was given a fresh one that covers its bytes. The PDF MAC of the earlier revisions was not changed.
    /// </summary>
    public bool PdfMacWritten { get; internal set; }

    /// <summary>
    /// Gets a sentence in plain words about a security requirement that stopped the save, such as an encrypted document that
    /// needs a PDF MAC the writer could not make, or <see langword="null"/> when no such requirement stopped it. Nothing was saved.
    /// </summary>
    public string? Failure { get; internal set; }

    /// <summary>
    /// Gets what the save did with the document's trap networks: the ones it removed because they no longer matched their
    /// pages, and the ones it fixed. Removals and fixes are made in the document in memory as one undoable edit. Show the
    /// user the <see cref="PdfTrapNetworkChange.Message"/> of every change that was not <see cref="PdfTrapNetworkAction.Repaired"/>.
    /// </summary>
    public IReadOnlyList<PdfTrapNetworkChange> TrapNetworks => _trapNetworks;

    /// <summary>
    /// Gets the document requirements (<c>/Requirements</c>) the save removed because an edit left the feature they name out
    /// of the file, so the file does not claim a need it no longer has. The removals are made in the document in memory as one
    /// undoable edit. Show the user the <see cref="PdfRequirementChange.Message"/> of every change.
    /// </summary>
    public IReadOnlyList<PdfRequirementChange> Requirements => _requirements;

    /// <summary>Clears what the report holds, so the host can reuse it for another save.</summary>
    internal void Reset()
    {
        Claim = null;
        UaClaim = null;
        PdfMacWritten = false;
        Failure = null;
        _trapNetworks = [];
        _requirements = [];
    }

    /// <summary>Records what the save did with the trap networks.</summary>
    /// <param name="changes">The changes.</param>
    internal void SetTrapNetworks(PdfTrapNetworkChange[] changes) => _trapNetworks = changes;

    /// <summary>Records which requirements the save removed.</summary>
    /// <param name="changes">The changes.</param>
    internal void SetRequirements(PdfRequirementChange[] changes) => _requirements = changes;
}
