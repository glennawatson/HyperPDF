// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Edits the outline (bookmarks). Entries are named by the <see cref="PdfOutlineItem.Id"/> that
/// <see cref="PdfDocumentNavigation.GetOutline"/> reports; an invalid id stands for the top level. Each edit joins the
/// open transaction or makes its own, so it can be undone, and keeps <c>/Prev</c>, <c>/Next</c>, <c>/First</c>,
/// <c>/Last</c> and <c>/Count</c> correct. A document certified with a signature refuses the edit.
/// </summary>
public static class PdfDocumentOutlineEditing
{
    /// <summary>What an edit of the outline is called when the document's certification refuses it.</summary>
    private const string EditDescription = "change the bookmarks";

    /// <summary>The index that appends.</summary>
    private const int Append = -1;

    /// <summary>Adds a bookmark, with the bookmarks below it, as the last child of an entry.</summary>
    /// <param name="document">The document.</param>
    /// <param name="parent">The parent entry, or an invalid id for the top level.</param>
    /// <param name="spec">The bookmark.</param>
    /// <returns>The new entry's object.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The parent is not a bookmark, or the bookmark's target is not valid.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfObjectId AddBookmark(PdfDocument document, PdfObjectId parent, PdfBookmarkSpec spec) => AddBookmark(document, parent, Append, spec);

    /// <summary>Adds a bookmark, with the bookmarks below it, at a position among an entry's children.</summary>
    /// <param name="document">The document.</param>
    /// <param name="parent">The parent entry, or an invalid id for the top level.</param>
    /// <param name="index">The position; a negative number or one past the last child appends.</param>
    /// <param name="spec">The bookmark.</param>
    /// <returns>The new entry's object.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The parent is not a bookmark, or the bookmark's target is not valid.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static PdfObjectId AddBookmark(PdfDocument document, PdfObjectId parent, int index, PdfBookmarkSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return AddBookmarksCore(document, parent, index, [spec], CancellationToken.None)[0];
    }

    /// <summary>Adds a bookmark, with the bookmarks below it, at a position among an entry's children.</summary>
    /// <param name="document">The document.</param>
    /// <param name="parent">The parent entry, or an invalid id for the top level.</param>
    /// <param name="index">The position; a negative number or one past the last child appends.</param>
    /// <param name="spec">The bookmark.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The new entry's object.</returns>
    public static async ValueTask<PdfObjectId> AddBookmarkAsync(PdfDocument document, PdfObjectId parent, int index, PdfBookmarkSpec spec, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        return AddBookmarksCore(document, parent, index, [spec], cancellationToken)[0];
    }

    /// <summary>Adds many bookmarks, each with the bookmarks below it, as children of an entry in one edit. The cost grows with the number of bookmarks added.</summary>
    /// <param name="document">The document.</param>
    /// <param name="parent">The parent entry, or an invalid id for the top level.</param>
    /// <param name="index">The position of the first; a negative number or one past the last child appends.</param>
    /// <param name="specs">The bookmarks.</param>
    /// <param name="cancellationToken">Cancels the work; the document is unchanged when it is cancelled.</param>
    /// <returns>The object of each bookmark added at the top of <paramref name="specs"/>, in order.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The parent is not a bookmark, or a bookmark's target is not valid.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyList<PdfObjectId> AddBookmarks(PdfDocument document, PdfObjectId parent, int index, IReadOnlyList<PdfBookmarkSpec> specs, CancellationToken cancellationToken) =>
        AddBookmarksCore(document, parent, index, specs, cancellationToken);

    /// <summary>Adds many bookmarks, each with the bookmarks below it, as children of an entry in one edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="parent">The parent entry, or an invalid id for the top level.</param>
    /// <param name="index">The position of the first; a negative number or one past the last child appends.</param>
    /// <param name="specs">The bookmarks.</param>
    /// <param name="cancellationToken">Cancels the loading and the work; the document is unchanged when it is cancelled.</param>
    /// <returns>The object of each bookmark added at the top of <paramref name="specs"/>, in order.</returns>
    public static async ValueTask<IReadOnlyList<PdfObjectId>> AddBookmarksAsync(
        PdfDocument document,
        PdfObjectId parent,
        int index,
        IReadOnlyList<PdfBookmarkSpec> specs,
        CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        return AddBookmarksCore(document, parent, index, specs, cancellationToken);
    }

    /// <summary>Removes a bookmark and the bookmarks below it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The entry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The object is not a bookmark.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RemoveBookmark(PdfDocument document, PdfObjectId id) =>
        Run(document, "Remove bookmark", id, static (editor, entry) => editor.Remove(entry));

    /// <summary>Removes a bookmark and the bookmarks below it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The entry.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the bookmark is removed.</returns>
    public static async ValueTask RemoveBookmarkAsync(PdfDocument document, PdfObjectId id, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        RemoveBookmark(document, id);
    }

    /// <summary>Moves a bookmark, with the bookmarks below it, to a position among another entry's children.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The entry.</param>
    /// <param name="newParent">The new parent, or an invalid id for the top level.</param>
    /// <param name="index">The position, counted after the entry is taken out; a negative number or one past the last child appends.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The object is not a bookmark, or the new parent is the bookmark or lies below it.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MoveBookmark(PdfDocument document, PdfObjectId id, PdfObjectId newParent, int index) =>
        Run(document, "Move bookmark", new MoveState(id, newParent, index), static (editor, state) => editor.Move(state.Id, state.NewParent, state.Index));

    /// <summary>Moves a bookmark, with the bookmarks below it, to a position among another entry's children.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The entry.</param>
    /// <param name="newParent">The new parent, or an invalid id for the top level.</param>
    /// <param name="index">The position, counted after the entry is taken out; a negative number or one past the last child appends.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the bookmark is moved.</returns>
    public static async ValueTask MoveBookmarkAsync(PdfDocument document, PdfObjectId id, PdfObjectId newParent, int index, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        MoveBookmark(document, id, newParent, index);
    }

    /// <summary>Changes a bookmark's title.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The entry.</param>
    /// <param name="title">The new title.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The object is not a bookmark.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static void RenameBookmark(PdfDocument document, PdfObjectId id, string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        Run(document, "Rename bookmark", new RenameState(id, title), static (editor, state) => editor.Rename(state.Id, state.Title));
    }

    /// <summary>Changes a bookmark's title.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The entry.</param>
    /// <param name="title">The new title.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the bookmark is renamed.</returns>
    public static async ValueTask RenameBookmarkAsync(PdfDocument document, PdfObjectId id, string title, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        RenameBookmark(document, id, title);
    }

    /// <summary>Changes where a bookmark leads.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The entry.</param>
    /// <param name="target">The new target.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The object is not a bookmark, or the target is not valid.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static void SetBookmarkTarget(PdfDocument document, PdfObjectId id, PdfBookmarkTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Run(document, "Change bookmark target", new TargetState(id, target), static (editor, state) => editor.SetTarget(state.Id, state.Target));
    }

    /// <summary>Changes where a bookmark leads.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The entry.</param>
    /// <param name="target">The new target.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the target is changed.</returns>
    public static async ValueTask SetBookmarkTargetAsync(PdfDocument document, PdfObjectId id, PdfBookmarkTarget target, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        SetBookmarkTarget(document, id, target);
    }

    /// <summary>Changes a bookmark's colour and style flags.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The entry.</param>
    /// <param name="style">The style, or <see langword="null"/> to remove the colour and flags.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The object is not a bookmark.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetBookmarkStyle(PdfDocument document, PdfObjectId id, PdfBookmarkStyle? style) =>
        Run(document, "Change bookmark style", new StyleState(id, style), static (editor, state) => editor.SetStyle(state.Id, state.Style));

    /// <summary>Changes a bookmark's colour and style flags.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The entry.</param>
    /// <param name="style">The style, or <see langword="null"/> to remove the colour and flags.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the style is changed.</returns>
    public static async ValueTask SetBookmarkStyleAsync(PdfDocument document, PdfObjectId id, PdfBookmarkStyle? style, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        SetBookmarkStyle(document, id, style);
    }

    /// <summary>Opens or closes a bookmark, so the bookmarks below it start shown or hidden.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The entry.</param>
    /// <param name="open">Whether the bookmark is open.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The object is not a bookmark.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetBookmarkOpen(PdfDocument document, PdfObjectId id, bool open) =>
        Run(document, open ? "Open bookmark" : "Close bookmark", new OpenState(id, open), static (editor, state) => editor.SetOpen(state.Id, state.Open));

    /// <summary>Opens or closes a bookmark, so the bookmarks below it start shown or hidden.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The entry.</param>
    /// <param name="open">Whether the bookmark is open.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the bookmark is opened or closed.</returns>
    public static async ValueTask SetBookmarkOpenAsync(PdfDocument document, PdfObjectId id, bool open, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        SetBookmarkOpen(document, id, open);
    }

    /// <summary>Links a bookmark to the structure element it stands for (<c>/SE</c>), or removes the link.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The entry.</param>
    /// <param name="element">The structure element, or an invalid id to remove the link.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The object is not a bookmark.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetBookmarkStructureElement(PdfDocument document, PdfObjectId id, PdfObjectId element) =>
        Run(document, "Link bookmark to structure", new LinkState(id, element), static (editor, state) => editor.SetStructureElement(state.Id, state.Element));

    /// <summary>Works out every bookmark's count again from the bookmarks below it, as the standard defines it. Use it on an outline from another program whose counts are wrong or missing.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the work; the document is unchanged when it is cancelled.</param>
    /// <returns>The number of entries whose count was rewritten.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int RepairBookmarkCounts(PdfDocument document, CancellationToken cancellationToken) => RepairCore(document, cancellationToken);

    /// <summary>Works out every bookmark's count again from the bookmarks below it, as the standard defines it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the loading and the work; the document is unchanged when it is cancelled.</param>
    /// <returns>The number of entries whose count was rewritten.</returns>
    public static async ValueTask<int> RepairBookmarkCountsAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        return RepairCore(document, cancellationToken);
    }

    /// <summary>Adds bookmarks in a transaction.</summary>
    /// <param name="document">The document.</param>
    /// <param name="parent">The parent entry, or an invalid id for the top level.</param>
    /// <param name="index">The position of the first.</param>
    /// <param name="specs">The bookmarks.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The object of each bookmark added at the top of <paramref name="specs"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    private static List<PdfObjectId> AddBookmarksCore(PdfDocument document, PdfObjectId parent, int index, IReadOnlyList<PdfBookmarkSpec> specs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(specs);
        var state = new AddState(parent, index, specs, cancellationToken);
        Run(document, specs.Count == 1 ? "Add bookmark" : "Add bookmarks", state, static (editor, run) => editor.Add(run.Parent, run.Index, run.Specs, run.Ids, run.CancellationToken));
        return state.Ids;
    }

    /// <summary>Works out the counts again in a transaction.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The number of entries whose count was rewritten.</returns>
    private static int RepairCore(PdfDocument document, CancellationToken cancellationToken)
    {
        var state = new RepairState(cancellationToken);
        Run(document, "Repair bookmark counts", state, static (editor, run) => run.Changed = editor.RepairCounts(run.CancellationToken));
        return state.Changed;
    }

    /// <summary>Runs an outline edit in a transaction.</summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="label">The undo label.</param>
    /// <param name="state">The arguments.</param>
    /// <param name="action">The edit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    private static void Run<TState>(PdfDocument document, string label, TState state, Action<PdfOutlineEditor, TState> action)
    {
        ArgumentNullException.ThrowIfNull(document);
        PdfDocumentEditing.RunObjectEdit(
            document,
            label,
            PdfChangeKinds.Other,
            PdfCacheScopes.None,
            EditDescription,
            new Step<TState>(document, state, action),
            static (edits, step) => step.Action(new(step.Document, edits), step.State));
    }

    /// <summary>Loads the outline a core will read.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the entries are cached.</returns>
    private static ValueTask PrefetchAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Catalog.GetDictionary(KnownName.Outlines) is { } root
            ? PdfPrefetcher.PrefetchAsync(document.Objects, root, PdfPrefetchKind.Outline, cancellationToken)
            : ValueTask.CompletedTask;
    }

    /// <summary>An edit and what it reads.</summary>
    /// <typeparam name="TState">The type of the arguments.</typeparam>
    /// <param name="Document">The document.</param>
    /// <param name="State">The arguments.</param>
    /// <param name="Action">The edit.</param>
    [DebuggerDisplay("Step: {Document}")]
    private readonly record struct Step<TState>(PdfDocument Document, TState State, Action<PdfOutlineEditor, TState> Action);

    /// <summary>The arguments of a move.</summary>
    /// <param name="Id">The entry.</param>
    /// <param name="NewParent">The new parent.</param>
    /// <param name="Index">The position.</param>
    [DebuggerDisplay("MoveState: {Id}")]
    private readonly record struct MoveState(PdfObjectId Id, PdfObjectId NewParent, int Index);

    /// <summary>The arguments of a rename.</summary>
    /// <param name="Id">The entry.</param>
    /// <param name="Title">The title.</param>
    [DebuggerDisplay("RenameState: {Title}")]
    private readonly record struct RenameState(PdfObjectId Id, string Title);

    /// <summary>The arguments of a target change.</summary>
    /// <param name="Id">The entry.</param>
    /// <param name="Target">The target.</param>
    [DebuggerDisplay("TargetState: {Id}")]
    private readonly record struct TargetState(PdfObjectId Id, PdfBookmarkTarget Target);

    /// <summary>The arguments of a style change.</summary>
    /// <param name="Id">The entry.</param>
    /// <param name="Style">The style.</param>
    [DebuggerDisplay("StyleState: {Id}")]
    private readonly record struct StyleState(PdfObjectId Id, PdfBookmarkStyle? Style);

    /// <summary>The arguments of an open or close.</summary>
    /// <param name="Id">The entry.</param>
    /// <param name="Open">Whether to open it.</param>
    [DebuggerDisplay("OpenState: {Id} {Open}")]
    private readonly record struct OpenState(PdfObjectId Id, bool Open);

    /// <summary>The arguments of a structure element link.</summary>
    /// <param name="Id">The entry.</param>
    /// <param name="Element">The structure element.</param>
    [DebuggerDisplay("LinkState: {Id}")]
    private readonly record struct LinkState(PdfObjectId Id, PdfObjectId Element);

    /// <summary>The arguments of an add, and the entries it made.</summary>
    /// <param name="parent">The parent entry.</param>
    /// <param name="index">The position.</param>
    /// <param name="specs">The bookmarks.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    [DebuggerDisplay("AddState: {Specs.Count} bookmarks")]
    private sealed class AddState(PdfObjectId parent, int index, IReadOnlyList<PdfBookmarkSpec> specs, CancellationToken cancellationToken)
    {
        /// <summary>Gets the parent entry.</summary>
        internal PdfObjectId Parent { get; } = parent;

        /// <summary>Gets the position.</summary>
        internal int Index { get; } = index;

        /// <summary>Gets the bookmarks.</summary>
        internal IReadOnlyList<PdfBookmarkSpec> Specs { get; } = specs;

        /// <summary>Gets the token that cancels the work.</summary>
        internal CancellationToken CancellationToken { get; } = cancellationToken;

        /// <summary>Gets the entries the add made.</summary>
        internal List<PdfObjectId> Ids { get; } = [];
    }

    /// <summary>The arguments of a count repair, and what it changed.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    [DebuggerDisplay("RepairState: {Changed}")]
    private sealed class RepairState(CancellationToken cancellationToken)
    {
        /// <summary>Gets the token that cancels the work.</summary>
        internal CancellationToken CancellationToken { get; } = cancellationToken;

        /// <summary>Gets or sets the number of entries rewritten.</summary>
        internal int Changed { get; set; }
    }
}
