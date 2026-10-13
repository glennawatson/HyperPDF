// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Keeps trap networks true when a document is saved (ISO 32000-2 §14.11.6.2). HyperPDF cannot make traps, so a network
/// that no longer matches its page, or whose match cannot be checked, is removed from the document before writing; one
/// that still matches but has the wrong flags, position or appearance state is fixed. The changes are one undoable edit
/// and are listed in <see cref="PdfSaveReport.TrapNetworks"/>. A save that writes the whole file checks every page; an
/// incremental update checks only the pages its edits touched, as it writes nothing else.
/// </summary>
internal static class PdfTrapNetworkGuard
{
    /// <summary>The undo label of the changes.</summary>
    private const string Label = "Update trap networks";

    /// <summary>Checks the document's trap networks and removes or fixes the ones that need it.</summary>
    /// <param name="store">The objects being written.</param>
    /// <param name="wholeFile">Whether the save writes every object, rather than an update of the edited ones.</param>
    /// <param name="report">Receives what was done, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled; nothing was changed.</exception>
    internal static void Apply(PdfObjectStore store, bool wholeFile, PdfSaveReport? report, CancellationToken cancellationToken)
    {
        report?.SetTrapNetworks([]);
        if ((!wholeFile && !StoreEditing.HasChanges(store)) || PdfTrapNetworkPages.Find(store, cancellationToken) is not { } pages)
        {
            return;
        }

        var checks = Check(store, pages, wholeFile, cancellationToken, out var needed);
        if (needed == 0)
        {
            return;
        }

        var changes = Fix(store, pages, checks, needed);
        report?.SetTrapNetworks(changes);
    }

    /// <summary>Checks each page's network.</summary>
    /// <param name="store">The objects being written.</param>
    /// <param name="pages">The pages with networks.</param>
    /// <param name="wholeFile">Whether every page is written; otherwise a page no edit touched is left as it is.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <param name="needed">The number of pages to change.</param>
    /// <returns>Each page's check; no problems for a page left as it is.</returns>
    private static PdfTrapNetworkCheck[] Check(PdfObjectStore store, List<PdfTrapNetworkPage> pages, bool wholeFile, CancellationToken cancellationToken, out int needed)
    {
        var edits = new PdfTrapNetworkEdits(store);
        var checks = new PdfTrapNetworkCheck[pages.Count];
        needed = 0;
        for (var i = 0; i < checks.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var check = PdfTrapNetworkValidator.Check(pages[i], edits);
            if (check.Problems == PdfTrapNetworkProblems.None || !(wholeFile || check.Touched))
            {
                continue;
            }

            checks[i] = check;
            needed++;
        }

        return checks;
    }

    /// <summary>Removes or fixes the networks that need it, in one transaction.</summary>
    /// <param name="store">The objects being written.</param>
    /// <param name="pages">The pages with networks.</param>
    /// <param name="checks">Each page's check; no problems for a page left as it is.</param>
    /// <param name="needed">The number of pages to change.</param>
    /// <returns>What was done.</returns>
    private static PdfTrapNetworkChange[] Fix(PdfObjectStore store, List<PdfTrapNetworkPage> pages, PdfTrapNetworkCheck[] checks, int needed)
    {
        var changes = new PdfTrapNetworkChange[needed];
        var transaction = PdfGuardTransactions.JoinOrBegin(store, Label, out var owned);
        try
        {
            transaction.AddKinds(PdfChangeKinds.Annotations);
            var count = 0;
            for (var i = 0; i < checks.Length; i++)
            {
                if (checks[i].Problems == PdfTrapNetworkProblems.None)
                {
                    continue;
                }

                changes[count] = new(pages[i].PageIndex, PdfTrapNetworkFix.Apply(transaction, pages[i], checks[i]), checks[i].Problems);
                count++;
            }
        }
        catch
        {
            if (owned)
            {
                transaction.Rollback();
            }

            throw;
        }

        if (owned)
        {
            transaction.Commit();
        }

        return changes;
    }
}
