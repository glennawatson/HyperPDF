// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Layers;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Creates and changes layers (optional content groups, ISO 32000-2 8.11). A layer is named by the
/// <see cref="PdfLayer.Id"/> that <see cref="PdfDocumentLayers.GetOptionalContent"/> reports. The edits change the default
/// configuration, which is how the document opens; they do not change the viewing state of an open document. Each edit
/// joins the open transaction or makes its own, so it can be undone. A document certified with a signature refuses the
/// edit, and a document that claims PDF/A-1 refuses layers.
/// </summary>
public static class PdfDocumentLayerEditing
{
    /// <summary>What an edit of the layers is called when the document's certification refuses it.</summary>
    private const string EditDescription = "change the layers";

    /// <summary>Adds a layer and lists it last in the default configuration.</summary>
    /// <param name="document">The document.</param>
    /// <param name="spec">The layer.</param>
    /// <returns>The layer's object; its <see cref="PdfObjectId.Number"/> is the layer's id.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The name is empty, or the intent is not valid.</exception>
    /// <exception cref="InvalidOperationException">The document is certified, or claims PDF/A-1.</exception>
    public static PdfObjectId AddLayer(PdfDocument document, PdfLayerSpec spec)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(spec);
        var state = new AddState(document, spec);
        Run(document, "Add layer", state, static (edits, run) => run.Id = PdfLayerEditor.Add(edits, run.Document, run.Spec));
        return state.Id;
    }

    /// <summary>Adds a layer and lists it last in the default configuration.</summary>
    /// <param name="document">The document.</param>
    /// <param name="spec">The layer.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The layer's object.</returns>
    public static async ValueTask<PdfObjectId> AddLayerAsync(PdfDocument document, PdfLayerSpec spec, CancellationToken cancellationToken)
    {
        await PdfEditPrefetch.CatalogEntryAsync(document, KnownName.OCProperties, cancellationToken).ConfigureAwait(false);
        return AddLayer(document, spec);
    }

    /// <summary>Removes a layer from the document. A layer that content still uses is not removed.</summary>
    /// <param name="document">The document.</param>
    /// <param name="layerId">The layer's id.</param>
    /// <returns><see langword="true"/> when the document had the layer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The document is certified, or a page still uses the layer.</exception>
    public static bool RemoveLayer(PdfDocument document, int layerId)
    {
        ArgumentNullException.ThrowIfNull(document);
        var state = new RemoveState(document, layerId);
        Run(document, "Remove layer", state, static (edits, run) => run.Removed = PdfLayerEditor.Remove(edits, run.Document, run.LayerId));
        return state.Removed;
    }

    /// <summary>Removes a layer from the document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="layerId">The layer's id.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns><see langword="true"/> when the document had the layer.</returns>
    public static async ValueTask<bool> RemoveLayerAsync(PdfDocument document, int layerId, CancellationToken cancellationToken)
    {
        await PrefetchPagesAsync(document, cancellationToken).ConfigureAwait(false);
        return RemoveLayer(document, layerId);
    }

    /// <summary>Renames a layer.</summary>
    /// <param name="document">The document.</param>
    /// <param name="layerId">The layer's id.</param>
    /// <param name="name">The new name.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The name is empty, or the document has no such layer.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static void RenameLayer(PdfDocument document, int layerId, string name)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(name);
        Run(document, "Rename layer", new NameState(layerId, name), static (edits, run) => PdfLayerEditor.Rename(edits, run.LayerId, run.Name));
    }

    /// <summary>Renames a layer.</summary>
    /// <param name="document">The document.</param>
    /// <param name="layerId">The layer's id.</param>
    /// <param name="name">The new name.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the layer is renamed.</returns>
    public static async ValueTask RenameLayerAsync(PdfDocument document, int layerId, string name, CancellationToken cancellationToken)
    {
        await PdfEditPrefetch.CatalogEntryAsync(document, KnownName.OCProperties, cancellationToken).ConfigureAwait(false);
        RenameLayer(document, layerId, name);
    }

    /// <summary>Sets whether a layer starts shown. Turning a layer on turns off the other layers of its radio groups.</summary>
    /// <param name="document">The document.</param>
    /// <param name="layerId">The layer's id.</param>
    /// <param name="visible">Whether it starts shown.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The document has no such layer.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static void SetLayerVisible(PdfDocument document, int layerId, bool visible)
    {
        ArgumentNullException.ThrowIfNull(document);
        Run(document, visible ? "Show layer by default" : "Hide layer by default", new FlagState(layerId, visible), static (edits, run) => PdfLayerEditor.SetVisible(edits, run.LayerId, run.Flag));
    }

    /// <summary>Sets whether a layer starts shown.</summary>
    /// <param name="document">The document.</param>
    /// <param name="layerId">The layer's id.</param>
    /// <param name="visible">Whether it starts shown.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the state is set.</returns>
    public static async ValueTask SetLayerVisibleAsync(PdfDocument document, int layerId, bool visible, CancellationToken cancellationToken)
    {
        await PdfEditPrefetch.CatalogEntryAsync(document, KnownName.OCProperties, cancellationToken).ConfigureAwait(false);
        SetLayerVisible(document, layerId, visible);
    }

    /// <summary>Locks or unlocks a layer, so users can or cannot change whether it is shown.</summary>
    /// <param name="document">The document.</param>
    /// <param name="layerId">The layer's id.</param>
    /// <param name="locked">Whether the layer is locked.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The document has no such layer.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static void SetLayerLocked(PdfDocument document, int layerId, bool locked)
    {
        ArgumentNullException.ThrowIfNull(document);
        Run(document, locked ? "Lock layer" : "Unlock layer", new FlagState(layerId, locked), static (edits, run) => PdfLayerEditor.SetLocked(edits, run.LayerId, run.Flag));
    }

    /// <summary>Locks or unlocks a layer.</summary>
    /// <param name="document">The document.</param>
    /// <param name="layerId">The layer's id.</param>
    /// <param name="locked">Whether the layer is locked.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the layer is locked or unlocked.</returns>
    public static async ValueTask SetLayerLockedAsync(PdfDocument document, int layerId, bool locked, CancellationToken cancellationToken)
    {
        await PdfEditPrefetch.CatalogEntryAsync(document, KnownName.OCProperties, cancellationToken).ConfigureAwait(false);
        SetLayerLocked(document, layerId, locked);
    }

    /// <summary>Sets or removes how a layer is meant to be used.</summary>
    /// <param name="document">The document.</param>
    /// <param name="layerId">The layer's id.</param>
    /// <param name="usage">The usage, or <see langword="null"/> to remove it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The document has no such layer, or the creator information is incomplete.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static void SetLayerUsage(PdfDocument document, int layerId, PdfLayerUsage? usage)
    {
        ArgumentNullException.ThrowIfNull(document);
        Run(document, "Change layer usage", new UsageState(layerId, usage), static (edits, run) => PdfLayerEditor.SetUsage(edits, run.LayerId, run.Usage));
    }

    /// <summary>Sets or removes how a layer is meant to be used.</summary>
    /// <param name="document">The document.</param>
    /// <param name="layerId">The layer's id.</param>
    /// <param name="usage">The usage, or <see langword="null"/> to remove it.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the usage is set.</returns>
    public static async ValueTask SetLayerUsageAsync(PdfDocument document, int layerId, PdfLayerUsage? usage, CancellationToken cancellationToken)
    {
        await PdfEditPrefetch.CatalogEntryAsync(document, KnownName.OCProperties, cancellationToken).ConfigureAwait(false);
        SetLayerUsage(document, layerId, usage);
    }

    /// <summary>
    /// Sets the order the layers are listed in, with labels and sublayers (the default configuration's <c>/Order</c>). A layer
    /// the order leaves out is not shown in a viewer's layer panel, unless the document claims PDF/A, which needs every layer listed.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="order">The entries: a layer with its sublayers, or a label (layer id 0) with the layers below it.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An entry names a layer the document does not have, names one twice, or nests too deep.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static void SetLayerOrder(PdfDocument document, IReadOnlyList<PdfLayerNode> order)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(order);
        Run(document, "Reorder layers", new OrderState(document, order), static (edits, run) => PdfLayerEditor.SetOrder(edits, run.Document, run.Order));
    }

    /// <summary>Sets the order the layers are listed in.</summary>
    /// <param name="document">The document.</param>
    /// <param name="order">The entries.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the order is set.</returns>
    public static async ValueTask SetLayerOrderAsync(PdfDocument document, IReadOnlyList<PdfLayerNode> order, CancellationToken cancellationToken)
    {
        await PdfEditPrefetch.CatalogEntryAsync(document, KnownName.OCProperties, cancellationToken).ConfigureAwait(false);
        SetLayerOrder(document, order);
    }

    /// <summary>
    /// Sets the radio button groups (<c>/RBGroups</c>): at most one layer of a group is on. Where several start on, only the
    /// first of them stays on. An empty list removes the groups.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="groups">The layers of each group, by id.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A group has fewer than two layers or names a layer the document does not have.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static void SetRadioGroups(PdfDocument document, IReadOnlyList<IReadOnlyList<int>> groups)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(groups);
        Run(document, "Change radio groups", groups, PdfLayerEditor.SetRadioGroups);
    }

    /// <summary>Sets the radio button groups.</summary>
    /// <param name="document">The document.</param>
    /// <param name="groups">The layers of each group, by id.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the groups are set.</returns>
    public static async ValueTask SetRadioGroupsAsync(PdfDocument document, IReadOnlyList<IReadOnlyList<int>> groups, CancellationToken cancellationToken)
    {
        await PdfEditPrefetch.CatalogEntryAsync(document, KnownName.OCProperties, cancellationToken).ConfigureAwait(false);
        SetRadioGroups(document, groups);
    }

    /// <summary>Loads the pages a layer removal checks.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the page tree is cached.</returns>
    private static async ValueTask PrefetchPagesAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        await PdfEditPrefetch.CatalogEntryAsync(document, KnownName.OCProperties, cancellationToken).ConfigureAwait(false);
        await PdfEditPrefetch.CatalogEntryAsync(document, KnownName.Pages, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs a layer edit in a transaction.</summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="label">The undo label.</param>
    /// <param name="state">The arguments.</param>
    /// <param name="edit">The edit.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Run<TState>(PdfDocument document, string label, TState state, Action<PdfObjectEdits, TState> edit) =>
        PdfDocumentEditing.RunObjectEdit(document, label, PdfChangeKinds.Other, PdfCacheScopes.None, EditDescription, state, edit);

    /// <summary>The arguments of a rename.</summary>
    /// <param name="LayerId">The layer.</param>
    /// <param name="Name">The name.</param>
    [DebuggerDisplay("NameState: {LayerId} {Name}")]
    private readonly record struct NameState(int LayerId, string Name);

    /// <summary>The arguments of an on-or-off change.</summary>
    /// <param name="LayerId">The layer.</param>
    /// <param name="Flag">The new value.</param>
    [DebuggerDisplay("FlagState: {LayerId} {Flag}")]
    private readonly record struct FlagState(int LayerId, bool Flag);

    /// <summary>The arguments of a usage change.</summary>
    /// <param name="LayerId">The layer.</param>
    /// <param name="Usage">The usage.</param>
    [DebuggerDisplay("UsageState: {LayerId}")]
    private readonly record struct UsageState(int LayerId, PdfLayerUsage? Usage);

    /// <summary>The arguments of an order change.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Order">The order.</param>
    [DebuggerDisplay("OrderState: {Order.Count} entries")]
    private readonly record struct OrderState(PdfDocument Document, IReadOnlyList<PdfLayerNode> Order);

    /// <summary>The arguments of an add, and the layer it made.</summary>
    /// <param name="document">The document.</param>
    /// <param name="spec">The layer.</param>
    [DebuggerDisplay("AddState: {Spec}")]
    private sealed class AddState(PdfDocument document, PdfLayerSpec spec)
    {
        /// <summary>Gets the document.</summary>
        internal PdfDocument Document { get; } = document;

        /// <summary>Gets the layer.</summary>
        internal PdfLayerSpec Spec { get; } = spec;

        /// <summary>Gets or sets the layer's object.</summary>
        internal PdfObjectId Id { get; set; }
    }

    /// <summary>The arguments of a removal, and its result.</summary>
    /// <param name="document">The document.</param>
    /// <param name="layerId">The layer.</param>
    [DebuggerDisplay("RemoveState: {LayerId}")]
    private sealed class RemoveState(PdfDocument document, int layerId)
    {
        /// <summary>Gets the document.</summary>
        internal PdfDocument Document { get; } = document;

        /// <summary>Gets the layer.</summary>
        internal int LayerId { get; } = layerId;

        /// <summary>Gets or sets a value indicating whether the layer was removed.</summary>
        internal bool Removed { get; set; }
    }
}
