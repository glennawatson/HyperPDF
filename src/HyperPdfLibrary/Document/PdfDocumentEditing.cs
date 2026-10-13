// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
namespace HyperPdfLibrary.Document;

/// <summary>Manages document edit transactions and cache invalidation.</summary>
public static class PdfDocumentEditing
{
    /// <summary>Gets the undo and redo stacks of committed edits.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The document's edit history.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfEditHistory GetHistory(PdfDocument document) => StoreTransactions.GetHistory(document.Objects);

    /// <summary>
    /// Starts an edit transaction. Page and metadata operations called while it is open join it, so they commit, roll
    /// back and undo together.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="label">A short description of the edit, shown for undo.</param>
    /// <returns>The transaction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A transaction is already open.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfEditTransaction BeginEdit(PdfDocument document, string label) => StoreTransactions.BeginTransaction(document.Objects, label);

    /// <summary>Reverts the most recent committed edit; pages, links, labels and the outline are read again.</summary>
    /// <param name="document">The document.</param>
    /// <returns><see langword="true"/> when an edit was reverted.</returns>
    /// <exception cref="InvalidOperationException">A transaction is open.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Undo(PdfDocument document) => PdfDocumentPageContent.AfterHistory(document, StoreTransactions.GetHistory(document.Objects).Undo());

    /// <summary>Applies the most recently undone edit again.</summary>
    /// <param name="document">The document.</param>
    /// <returns><see langword="true"/> when an edit was applied.</returns>
    /// <exception cref="InvalidOperationException">A transaction is open.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Redo(PdfDocument document) => PdfDocumentPageContent.AfterHistory(document, StoreTransactions.GetHistory(document.Objects).Redo());

    /// <summary>
    /// Starts an edit transaction whose changes are known to affect only some parts of the document, so committing it, or
    /// undoing it later, keeps the caches built from the other parts.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="label">A short description of the edit, shown for undo.</param>
    /// <param name="scopes">The parts of the document the transaction's changes can affect.</param>
    /// <returns>The transaction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A transaction is already open.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfEditTransaction BeginEdit(PdfDocument document, string label, PdfCacheScopes scopes) => StoreTransactions.BeginTransaction(document.Objects, label, scopes);

    /// <summary>Drops every cache read from the objects, so the next reads see the current edits.</summary>
    /// <param name="document">The document.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void InvalidateCaches(PdfDocument document) => PdfDocumentEditing.InvalidateCaches(document, PdfCacheScopes.All);

    /// <summary>
    /// Drops the caches read from the objects, so the next reads see the current edits. The text pages, marked content and
    /// structure tree are kept when the edit leaves the parts they are built from alone.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="scopes">The parts of the document the edit can have changed.</param>
    internal static void InvalidateCaches(PdfDocument document, PdfCacheScopes scopes)
    {
        // Every edit path ends here, so generated appearances compare this count to know the objects they were drawn from changed.
        _ = Interlocked.Increment(ref document.State.ObjectsRevision);

        // An edit that changed the page tree in place left the pages it produced; any other change reads the tree again.
        Volatile.Write(ref document.State.PageSet, Interlocked.Exchange(ref document.State.NextPageSet, null));
        Volatile.Write(ref document.State.LabelRanges, null);
        Volatile.Write(ref document.State.Separations, null);
        Volatile.Write(ref document.State.Outline, null);
        Volatile.Write(ref document.State.Signatures, null);
        Volatile.Write(ref document.State.Attachments, null);
        Volatile.Write(ref document.State.SignatureDetails, null);
        Volatile.Write(ref document.State.SecurityStore, null);

        // The text of a page and its marked content come from the page's content, resources and boxes, and sit at the page's index.
        if ((scopes & (PdfCacheScopes.PageSet | PdfCacheScopes.PageContent)) != PdfCacheScopes.None)
        {
            Volatile.Write(ref document.State.TextPages, null);
            Volatile.Write(ref document.State.MarkedContent, null);
        }

        // The tree comes from the structure objects, and names the pages by index.
        if ((scopes & (PdfCacheScopes.PageSet | PdfCacheScopes.Structure)) != PdfCacheScopes.None)
        {
            Volatile.Write(ref document.State.StructureTree, null);
        }

        // The layers a reader chose to show stay when the edit left every layer object alone.
        PdfDocumentLayers.RefreshOptionalContent(document);
    }

    /// <summary>Runs an edit inside a transaction: the open one, or a new one committed on success and rolled back on failure.</summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="label">The undo label used when no transaction is open.</param>
    /// <param name="kinds">The kinds of change the edit makes.</param>
    /// <param name="state">The arguments the edit reads.</param>
    /// <param name="edit">The edit.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RunEdit<TState>(PdfDocument document, string label, PdfChangeKinds kinds, TState state, Action<PdfEditTransaction, TState> edit)
        where TState : allows ref struct => PdfDocumentEditing.RunEdit(document, label, kinds, PdfCacheScopes.All, state, edit);

    /// <summary>Runs an edit inside a transaction: the open one, or a new one committed on success and rolled back on failure.</summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="label">The undo label used when no transaction is open.</param>
    /// <param name="kinds">The kinds of change the edit makes.</param>
    /// <param name="scopes">The parts of the document the edit can change; the caches built from the other parts stay.</param>
    /// <param name="state">The arguments the edit reads.</param>
    /// <param name="edit">The edit.</param>
    internal static void RunEdit<TState>(PdfDocument document, string label, PdfChangeKinds kinds, PdfCacheScopes scopes, TState state, Action<PdfEditTransaction, TState> edit)
        where TState : allows ref struct
    {
        var transaction = PdfDocumentEditing.JoinOrBegin(document, label, scopes, out var owned);
        try
        {
            transaction.AddKinds(kinds);
            transaction.AddScopes(scopes);
            edit(transaction, state);
        }
        catch
        {
            // The pages an in-place edit left describe changes that are being undone.
            _ = Interlocked.Exchange(ref document.State.NextPageSet, null);
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
        else
        {
            PdfDocumentEditing.InvalidateCaches(document, scopes);
        }
    }

    /// <summary>
    /// Runs an edit that works on a <see cref="PdfObjectEdits"/> inside a transaction, after checking that the document's
    /// certification allows it. The changed objects are written when the edit returns.
    /// </summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="label">The undo label used when no transaction is open.</param>
    /// <param name="kinds">The kinds of change the edit makes.</param>
    /// <param name="what">The edit in plain words for a refusal, such as "change the bookmarks".</param>
    /// <param name="state">The arguments the edit reads.</param>
    /// <param name="edit">The edit.</param>
    /// <exception cref="InvalidOperationException">The document is certified and its level does not allow the edit.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RunObjectEdit<TState>(PdfDocument document, string label, PdfChangeKinds kinds, string what, TState state, Action<PdfObjectEdits, TState> edit) =>
        PdfDocumentEditing.RunObjectEdit(document, label, kinds, PdfCacheScopes.All, what, state, edit);

    /// <summary>
    /// Runs an edit that works on a <see cref="PdfObjectEdits"/> inside a transaction, after checking that the document's
    /// certification allows it. The changed objects are written when the edit returns.
    /// </summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="label">The undo label used when no transaction is open.</param>
    /// <param name="kinds">The kinds of change the edit makes.</param>
    /// <param name="scopes">The parts of the document the edit can change; the caches built from the other parts stay.</param>
    /// <param name="what">The edit in plain words for a refusal, such as "change the bookmarks".</param>
    /// <param name="state">The arguments the edit reads.</param>
    /// <param name="edit">The edit.</param>
    /// <exception cref="InvalidOperationException">The document is certified and its level does not allow the edit.</exception>
    internal static void RunObjectEdit<TState>(PdfDocument document, string label, PdfChangeKinds kinds, PdfCacheScopes scopes, string what, TState state, Action<PdfObjectEdits, TState> edit)
    {
        PdfEditPermission.Require(document, kinds, what);
        PdfDocumentEditing.RunEdit(
            document,
            label,
            kinds,
            scopes,
            new ObjectEditState<TState>(document.Objects, state, edit),
            static (transaction, run) =>
            {
                var edits = new PdfObjectEdits(transaction, run.Store);
                run.Edit(edits, run.State);
                edits.Flush();
            });
    }

    /// <summary>Joins the open transaction, or starts one the caller commits.</summary>
    /// <param name="document">The document.</param>
    /// <param name="label">The description for a new transaction.</param>
    /// <param name="scopes">The parts of the document a new transaction can change.</param>
    /// <param name="owned">Whether a new transaction was started.</param>
    /// <returns>The transaction.</returns>
    /// <exception cref="InvalidOperationException">A transaction that another thread started is open.</exception>
    private static PdfEditTransaction JoinOrBegin(PdfDocument document, string label, PdfCacheScopes scopes, out bool owned)
    {
        lock (document.Objects.Gate)
        {
            if (StoreTransactions.GetCurrentTransaction(document.Objects) is { } open)
            {
                // A transaction belongs to the thread that started it: an edit from another thread joining it would be
                // committed or rolled back by that thread's decision, and lost or failed with it.
                if (open.OwnerThreadId != Environment.CurrentManagedThreadId)
                {
                    throw new InvalidOperationException($"Another thread has the edit \"{open.Label}\" open on this document. Wait for it to finish, then edit again.");
                }

                owned = false;
                return open;
            }

            owned = true;
            return StoreTransactions.BeginTransaction(document.Objects, label, scopes);
        }
    }

    /// <summary>The arguments of an edit that works on a <see cref="PdfObjectEdits"/>.</summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="Store">The document's objects.</param>
    /// <param name="State">The arguments.</param>
    /// <param name="Edit">The edit.</param>
    private readonly record struct ObjectEditState<TState>(PdfObjectStore Store, TState State, Action<PdfObjectEdits, TState> Edit);
}
