// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Reads and edits the name trees of the catalog's <c>/Names</c> dictionary (ISO 32000-2 7.9.6). An edit keeps the tree
/// balanced: each node holds at most 32 entries, every node but the root has <c>/Limits</c>, and a single add or remove
/// changes one path from the root. Adding a list of entries rebuilds the tree once, so cost grows with the number of entries.
/// </summary>
public static class PdfDocumentNameTrees
{
    /// <summary>What an edit of a name tree is called when the document's certification refuses it.</summary>
    private const string EditDescription = "change the named items";

    /// <summary>The spellings of the tree keys, by <see cref="PdfNameTreeKind"/> value.</summary>
    private static readonly string[] Keys =
    [
        "Dests",
        "EmbeddedFiles",
        "JavaScript",
        "AP",
        "Pages",
        "Templates",
        "IDS",
        "URLS",
        "AlternatePresentations",
        "Renditions",
    ];

    /// <summary>Gets the entries of a name tree, in name order.</summary>
    /// <param name="document">The document.</param>
    /// <param name="kind">The tree.</param>
    /// <returns>The entries; empty when the document has no such tree.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<PdfNameTreeEntry> GetEntries(PdfDocument document, PdfNameTreeKind kind)
    {
        ArgumentNullException.ThrowIfNull(document);
        var entries = new List<NameTreeEntry>();
        NameTree.EnumerateRaw(document.Catalog.GetDictionary(KnownName.Names)?.GetDictionary(GetKey(document.Objects, kind)), entries);
        var result = new PdfNameTreeEntry[entries.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new(PdfText.Decode(entries[i].Key.AsStringBytes()), entries[i].Value);
        }

        return result;
    }

    /// <summary>Adds an entry to a name tree, or replaces the value of a name the tree already has.</summary>
    /// <param name="document">The document.</param>
    /// <param name="kind">The tree.</param>
    /// <param name="name">The name.</param>
    /// <param name="value">The value, as it is stored: a direct value or a reference.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a tree.</exception>
    /// <exception cref="InvalidOperationException">The document is certified or claims a PDF/A level that does not allow the entry.</exception>
    public static void SetEntry(PdfDocument document, PdfNameTreeKind kind, string name, PdfValue value)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(name);
        RequireAllowed(document, kind);
        PdfDocumentEditing.RunObjectEdit(
            document,
            "Add name",
            PdfChangeKinds.Other,
            PdfCacheScopes.None,
            EditDescription,
            new SetState(kind, name, value),
            static (edits, state) => PdfNameTreeEditing.Set(
                edits,
                GetKey(edits.Store, state.Kind),
                PdfValue.FromString(PdfText.Encode(state.Name, edits.Store.Version)),
                state.Value,
                CancellationToken.None));
    }

    /// <summary>Adds an entry to a name tree, or replaces the value of a name the tree already has.</summary>
    /// <param name="document">The document.</param>
    /// <param name="kind">The tree.</param>
    /// <param name="name">The name.</param>
    /// <param name="value">The value, as it is stored: a direct value or a reference.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the entry is added.</returns>
    public static async ValueTask SetEntryAsync(PdfDocument document, PdfNameTreeKind kind, string name, PdfValue value, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, kind, cancellationToken).ConfigureAwait(false);
        SetEntry(document, kind, name, value);
    }

    /// <summary>Adds many entries to a name tree in one rebuild, replacing the values of names the tree already has.</summary>
    /// <param name="document">The document.</param>
    /// <param name="kind">The tree.</param>
    /// <param name="entries">The entries.</param>
    /// <param name="cancellationToken">Cancels the rebuild; the document is unchanged when it is cancelled.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a tree.</exception>
    /// <exception cref="InvalidOperationException">The document is certified or claims a PDF/A level that does not allow the entries.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetEntries(PdfDocument document, PdfNameTreeKind kind, IReadOnlyList<PdfNameTreeEntry> entries, CancellationToken cancellationToken) =>
        SetEntriesCore(document, kind, entries, cancellationToken);

    /// <summary>Adds many entries to a name tree in one rebuild, replacing the values of names the tree already has.</summary>
    /// <param name="document">The document.</param>
    /// <param name="kind">The tree.</param>
    /// <param name="entries">The entries.</param>
    /// <param name="cancellationToken">Cancels the loading and the rebuild; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the entries are added.</returns>
    public static async ValueTask SetEntriesAsync(PdfDocument document, PdfNameTreeKind kind, IReadOnlyList<PdfNameTreeEntry> entries, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, kind, cancellationToken).ConfigureAwait(false);
        SetEntriesCore(document, kind, entries, cancellationToken);
    }

    /// <summary>Removes an entry from a name tree; a tree left with no entries is removed from the catalog.</summary>
    /// <param name="document">The document.</param>
    /// <param name="kind">The tree.</param>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when the name was in the tree.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a tree.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static bool RemoveEntry(PdfDocument document, PdfNameTreeKind kind, string name)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(name);
        RequireKind(kind);
        var state = new RemoveState(kind, name);
        PdfDocumentEditing.RunObjectEdit(
            document,
            "Remove name",
            PdfChangeKinds.Other,
            PdfCacheScopes.None,
            EditDescription,
            state,
            static (edits, state) => state.Removed = PdfNameTreeEditing.Remove(
                edits,
                GetKey(edits.Store, state.Kind),
                PdfValue.FromString(PdfText.Encode(state.Name, edits.Store.Version)),
                CancellationToken.None));
        return state.Removed;
    }

    /// <summary>Removes an entry from a name tree; a tree left with no entries is removed from the catalog.</summary>
    /// <param name="document">The document.</param>
    /// <param name="kind">The tree.</param>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns><see langword="true"/> when the name was in the tree.</returns>
    public static async ValueTask<bool> RemoveEntryAsync(PdfDocument document, PdfNameTreeKind kind, string name, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, kind, cancellationToken).ConfigureAwait(false);
        return RemoveEntry(document, kind, name);
    }

    /// <summary>
    /// Rebuilds a name tree so every node is evenly filled, holds at most 32 entries and has correct limits. A tree read
    /// from another program may be unbalanced, have no limits, or keep its nodes inside other objects.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="kind">The tree.</param>
    /// <param name="cancellationToken">Cancels the rebuild; the document is unchanged when it is cancelled.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a tree.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Rebalance(PdfDocument document, PdfNameTreeKind kind, CancellationToken cancellationToken) =>
        RebalanceCore(document, kind, cancellationToken);

    /// <summary>Rebuilds a name tree so every node is evenly filled and has correct limits.</summary>
    /// <param name="document">The document.</param>
    /// <param name="kind">The tree.</param>
    /// <param name="cancellationToken">Cancels the loading and the rebuild; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the tree is rebuilt.</returns>
    public static async ValueTask RebalanceAsync(PdfDocument document, PdfNameTreeKind kind, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, kind, cancellationToken).ConfigureAwait(false);
        RebalanceCore(document, kind, cancellationToken);
    }

    /// <summary>Gets the key a tree has in <c>/Names</c>.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="kind">The tree.</param>
    /// <returns>The key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfName GetKey(PdfObjectStore store, PdfNameTreeKind kind) => store.Names.Intern(Keys[(int)kind]);

    /// <summary>Adds many entries to a name tree in one rebuild.</summary>
    /// <param name="document">The document.</param>
    /// <param name="kind">The tree.</param>
    /// <param name="entries">The entries.</param>
    /// <param name="cancellationToken">Cancels the rebuild.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a tree.</exception>
    /// <exception cref="InvalidOperationException">The document is certified or claims a PDF/A level that does not allow the entries.</exception>
    private static void SetEntriesCore(PdfDocument document, PdfNameTreeKind kind, IReadOnlyList<PdfNameTreeEntry> entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(entries);
        RequireAllowed(document, kind);
        PdfDocumentEditing.RunObjectEdit(
            document,
            "Add names",
            PdfChangeKinds.Other,
            PdfCacheScopes.None,
            EditDescription,
            new SetManyState(kind, entries, cancellationToken),
            static (edits, state) =>
            {
                var added = new List<NameTreeEntry>(state.Entries.Count);
                foreach (var entry in state.Entries)
                {
                    added.Add(new(PdfValue.FromString(PdfText.Encode(entry.Name, edits.Store.Version)), entry.Value));
                }

                PdfNameTreeEditing.SetMany(edits, GetKey(edits.Store, state.Kind), added, state.CancellationToken);
            });
    }

    /// <summary>Rebuilds a name tree.</summary>
    /// <param name="document">The document.</param>
    /// <param name="kind">The tree.</param>
    /// <param name="cancellationToken">Cancels the rebuild.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a tree.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    private static void RebalanceCore(PdfDocument document, PdfNameTreeKind kind, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        RequireKind(kind);
        PdfDocumentEditing.RunObjectEdit(
            document,
            "Rebalance names",
            PdfChangeKinds.Other,
            PdfCacheScopes.None,
            EditDescription,
            new RebalanceState(kind, cancellationToken),
            static (edits, state) => PdfNameTreeEditing.Rebalance(edits, GetKey(edits.Store, state.Kind), state.CancellationToken));
    }

    /// <summary>Checks that a tree kind is one the enumeration defines.</summary>
    /// <param name="kind">The tree.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a tree.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RequireKind(PdfNameTreeKind kind) => ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)kind, (uint)Keys.Length, nameof(kind));

    /// <summary>Checks that the document's conformance claim allows entries in a tree.</summary>
    /// <param name="document">The document.</param>
    /// <param name="kind">The tree.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a tree.</exception>
    /// <exception cref="InvalidOperationException">The claim does not allow the entries.</exception>
    private static void RequireAllowed(PdfDocument document, PdfNameTreeKind kind)
    {
        RequireKind(kind);
        switch (kind)
        {
            case PdfNameTreeKind.EmbeddedFiles:
                {
                    PdfEditConformance.RequireEmbeddedFilesAllowed(document);
                    break;
                }

            case PdfNameTreeKind.JavaScript:
                {
                    PdfEditConformance.RequireScriptsAllowed(document, "document JavaScript");
                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>Loads the nodes of a tree a core will read.</summary>
    /// <param name="document">The document.</param>
    /// <param name="kind">The tree.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the nodes are cached.</returns>
    private static ValueTask PrefetchAsync(PdfDocument document, PdfNameTreeKind kind, CancellationToken cancellationToken)
    {
        RequireKind(kind);
        return document.Catalog.GetDictionary(KnownName.Names)?.GetDictionary(GetKey(document.Objects, kind)) is { } root
            ? PdfPrefetcher.PrefetchAsync(document.Objects, root, PdfPrefetchKind.PageTree, cancellationToken)
            : ValueTask.CompletedTask;
    }

    /// <summary>The arguments of an add.</summary>
    /// <param name="Kind">The tree.</param>
    /// <param name="Name">The name.</param>
    /// <param name="Value">The value.</param>
    [DebuggerDisplay("SetState: {Name}")]
    private readonly record struct SetState(PdfNameTreeKind Kind, string Name, PdfValue Value);

    /// <summary>The arguments of an add of many entries.</summary>
    /// <param name="Kind">The tree.</param>
    /// <param name="Entries">The entries.</param>
    /// <param name="CancellationToken">Cancels the rebuild.</param>
    [DebuggerDisplay("SetManyState: {Entries.Count} entries")]
    private readonly record struct SetManyState(PdfNameTreeKind Kind, IReadOnlyList<PdfNameTreeEntry> Entries, CancellationToken CancellationToken);

    /// <summary>The arguments of a rebalance.</summary>
    /// <param name="Kind">The tree.</param>
    /// <param name="CancellationToken">Cancels the rebuild.</param>
    [DebuggerDisplay("RebalanceState: {Kind}")]
    private readonly record struct RebalanceState(PdfNameTreeKind Kind, CancellationToken CancellationToken);

    /// <summary>The arguments of a removal, and whether it removed the name.</summary>
    /// <param name="kind">The tree.</param>
    /// <param name="name">The name.</param>
    [DebuggerDisplay("RemoveState: {Name}")]
    private sealed class RemoveState(PdfNameTreeKind kind, string name)
    {
        /// <summary>Gets the tree.</summary>
        internal PdfNameTreeKind Kind { get; } = kind;

        /// <summary>Gets the name.</summary>
        internal string Name { get; } = name;

        /// <summary>Gets or sets a value indicating whether the name was in the tree.</summary>
        internal bool Removed { get; set; }
    }
}
