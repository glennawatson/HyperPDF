// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Features;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Receives what a page copy, extraction or deletion changed that the user should know: separation pages that were not
/// all carried, and trap networks that were left out or fixed. Pass one to an operation's overload, then read it
/// afterwards; one report is for one operation.
/// </summary>
[DebuggerDisplay("PdfPageEditReport: {SeparationChanges.Count} separation changes, {TrapNetworks.Count} trap networks")]
public sealed class PdfPageEditReport
{
    /// <summary>The separation changes recorded.</summary>
    private readonly List<PdfSeparationChange> _separationChanges = [];

    /// <summary>The trap network changes recorded.</summary>
    private readonly List<PdfTrapNetworkChange> _trapNetworks = [];

    /// <summary>The form changes recorded.</summary>
    private readonly List<string> _formChanges = [];

    /// <summary>Gets the printed pages of a preseparated document whose separation pages were not all carried, in group order.</summary>
    public IReadOnlyList<PdfSeparationChange> SeparationChanges => _separationChanges;

    /// <summary>
    /// Gets what the copy did with the copied pages' trap networks: the ones it left out because they no longer matched
    /// their pages, and the ones it fixed. The page indexes are those of the source pages. Show the user the
    /// <see cref="PdfTrapNetworkChange.Message"/> of every change that was not <see cref="PdfTrapNetworkAction.Repaired"/>.
    /// </summary>
    public IReadOnlyList<PdfTrapNetworkChange> TrapNetworks => _trapNetworks;

    /// <summary>
    /// Gets what the copy changed in the target's form that the user should know, such as XFA form data that was removed
    /// because the copied pages added fields it does not describe. Each entry is a plain sentence to show.
    /// </summary>
    public IReadOnlyList<string> FormChanges => _formChanges;

    /// <summary>Records a change to the target's form.</summary>
    /// <param name="message">The sentence to show the user.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddFormChange(string message) => _formChanges.Add(message);

    /// <summary>Records a trap network change.</summary>
    /// <param name="change">The change.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddTrapNetwork(PdfTrapNetworkChange change) => _trapNetworks.Add(change);

    /// <summary>Records a separation change.</summary>
    /// <param name="change">The change.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Add(PdfSeparationChange change) => _separationChanges.Add(change);
}
