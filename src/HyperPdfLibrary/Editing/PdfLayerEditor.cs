// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Layers;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Creates and changes layers (optional content groups, ISO 32000-2 8.11) and the default configuration's
/// <c>/Order</c>, <c>/ON</c>, <c>/OFF</c>, <c>/RBGroups</c> and <c>/Locked</c> entries (Table 99). A layer is named by
/// its object number, the <see cref="PdfLayer.Id"/> that the reader reports.
/// </summary>
internal static class PdfLayerEditor
{
    /// <summary>The name given to a default configuration this editor creates.</summary>
    private const string DefaultConfigurationName = "Default";

    /// <summary>The first version with optional content (8.11).</summary>
    private static readonly PdfVersion LayerVersion = new(1, 5);

    /// <summary>The first version with locked layers (Table 99).</summary>
    private static readonly PdfVersion LockedVersion = new(1, 6);

    /// <summary>The intents the standard defines for a group (8.11.2.3).</summary>
    private static readonly string[] Intents = ["View", "Design"];

    /// <summary>Adds a layer and lists it in the default configuration.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="document">The document.</param>
    /// <param name="spec">The layer.</param>
    /// <returns>The layer's object.</returns>
    /// <exception cref="ArgumentException">The name is empty, or the intent is not one the standard defines.</exception>
    /// <exception cref="InvalidOperationException">The document claims PDF/A-1, which forbids layers.</exception>
    internal static PdfObjectId Add(PdfObjectEdits edits, PdfDocument document, PdfLayerSpec spec)
    {
        PdfEditConformance.RequireLayersAllowed(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(spec.Name);

        if (Array.IndexOf(Intents, spec.Intent) < 0)
        {
            throw new ArgumentException("The intent must be View or Design.", nameof(spec));
        }

        var store = edits.Store;
        var group = new PdfDictionary(store);
        group.Set(KnownName.Type, PdfValue.FromName(KnownName.OCG));
        group.Set(KnownName.Name, PdfValue.FromString(PdfText.Encode(spec.Name, store.Version)));
        if (!string.Equals(spec.Intent, Intents[0], StringComparison.Ordinal))
        {
            group.Set(KnownName.Intent, PdfValue.FromName(store.Names.Intern(spec.Intent)));
        }

        if (spec.Usage is { } usage)
        {
            group.Set(KnownName.Usage, PdfValue.FromDictionary(BuildUsage(store, usage)));
        }

        var id = edits.Add(group);
        var reference = PdfValue.FromReference(id);
        PdfCatalogEditor.RaiseVersion(edits, spec.IsLocked ? LockedVersion : LayerVersion);
        var properties = PdfEditDictionaries.Child(edits, edits.Catalog(), KnownName.OCProperties);
        PdfEditDictionaries.ChildArray(edits, properties, KnownName.OCGs).Add(reference);
        var configuration = PdfEditDictionaries.Child(edits, properties, KnownName.D);
        if (configuration.Count == 0)
        {
            configuration.Set(KnownName.Name, PdfValue.FromString(PdfText.Encode(DefaultConfigurationName, store.Version)));
        }

        AppendToOrder(edits, properties, configuration, reference);
        SetDefaultState(edits, configuration, reference, spec.IsVisible);
        if (spec.IsLocked)
        {
            AddTo(edits, configuration, KnownName.Locked, reference);
        }

        return id;
    }

    /// <summary>Removes a layer from every configuration and deletes it.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="document">The document.</param>
    /// <param name="layerId">The layer's object number.</param>
    /// <returns><see langword="true"/> when the document had the layer.</returns>
    /// <exception cref="InvalidOperationException">A page, annotation or form still uses the layer.</exception>
    internal static bool Remove(PdfObjectEdits edits, PdfDocument document, int layerId)
    {
        var properties = edits.CatalogForRead.GetDictionary(KnownName.OCProperties);
        if (!TryFind(properties, layerId, out var found))
        {
            return false;
        }

        foreach (var page in PdfDocumentPages.GetPageSet(document).Pages)
        {
            if (OptionalContentPages.Collect(document.Objects, page).Contains(layerId))
            {
                throw new InvalidOperationException(
                    $"Page {page.Index + 1} still has content on this layer. Remove or move that content first; the layer was not removed.");
            }
        }

        var editable = PdfEditDictionaries.Child(edits, edits.Catalog(), KnownName.OCProperties);
        _ = PdfEditDictionaries.RemoveAll(PdfEditDictionaries.ChildArray(edits, editable, KnownName.OCGs), layerId);
        StripConfiguration(edits, PdfEditDictionaries.Child(edits, editable, KnownName.D), layerId);
        StripOtherConfigurations(edits, editable, layerId);
        edits.Delete(found.AsReference());
        return true;
    }

    /// <summary>Renames a layer.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="layerId">The layer's object number.</param>
    /// <param name="name">The new name.</param>
    /// <exception cref="ArgumentException">The name is empty, or the layer does not exist.</exception>
    internal static void Rename(PdfObjectEdits edits, int layerId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _ = Require(edits, layerId, out var id);
        edits.Edit(id)!.Set(KnownName.Name, PdfValue.FromString(PdfText.Encode(name, edits.Store.Version)));
    }

    /// <summary>Sets whether a layer starts shown. Turning a layer on turns off the other layers of its radio groups.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="layerId">The layer's object number.</param>
    /// <param name="visible">Whether it starts shown.</param>
    /// <exception cref="ArgumentException">The layer does not exist.</exception>
    internal static void SetVisible(PdfObjectEdits edits, int layerId, bool visible)
    {
        var reference = Require(edits, layerId, out _);
        var properties = PdfEditDictionaries.Child(edits, edits.Catalog(), KnownName.OCProperties);
        var configuration = PdfEditDictionaries.Child(edits, properties, KnownName.D);
        SetDefaultState(edits, configuration, reference, visible);
        if (!visible || configuration.GetArray(KnownName.RBGroups) is not { } groups)
        {
            return;
        }

        for (var i = 0; i < groups.Count; i++)
        {
            if (groups.GetArray(i) is { } group && PdfEditDictionaries.Contains(group, layerId))
            {
                TurnOffOthers(edits, configuration, group, layerId);
            }
        }
    }

    /// <summary>Locks or unlocks a layer in the default configuration.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="layerId">The layer's object number.</param>
    /// <param name="locked">Whether users cannot change the layer's state.</param>
    /// <exception cref="ArgumentException">The layer does not exist.</exception>
    internal static void SetLocked(PdfObjectEdits edits, int layerId, bool locked)
    {
        var reference = Require(edits, layerId, out _);
        var properties = PdfEditDictionaries.Child(edits, edits.Catalog(), KnownName.OCProperties);
        var configuration = PdfEditDictionaries.Child(edits, properties, KnownName.D);
        if (locked)
        {
            PdfCatalogEditor.RaiseVersion(edits, LockedVersion);
            AddTo(edits, configuration, KnownName.Locked, reference);
            return;
        }

        RemoveFrom(edits, configuration, KnownName.Locked, layerId);
    }

    /// <summary>Sets or removes how a layer is used.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="layerId">The layer's object number.</param>
    /// <param name="usage">The usage, or <see langword="null"/> to remove it.</param>
    /// <exception cref="ArgumentException">The layer does not exist, or the creator information is incomplete.</exception>
    internal static void SetUsage(PdfObjectEdits edits, int layerId, PdfLayerUsage? usage)
    {
        _ = Require(edits, layerId, out var id);
        var group = edits.Edit(id)!;
        if (usage is null)
        {
            _ = group.Remove(KnownName.Usage);
            return;
        }

        group.Set(KnownName.Usage, PdfValue.FromDictionary(BuildUsage(edits.Store, usage)));
    }

    /// <summary>Sets the order the layers are listed in, and the labels and nesting they are shown with.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="document">The document.</param>
    /// <param name="order">The entries: a layer with its sublayers, or a label (layer id 0) with the layers below it.</param>
    /// <exception cref="ArgumentException">An entry names a layer the document does not have, names it twice, or nests too deep; or the claimed PDF/A level needs every layer listed.</exception>
    internal static void SetOrder(PdfObjectEdits edits, PdfDocument document, IReadOnlyList<PdfLayerNode> order)
    {
        var properties = PdfEditDictionaries.Child(edits, edits.Catalog(), KnownName.OCProperties);
        var references = ReferencesByNumber(properties.GetArray(KnownName.OCGs));
        var seen = new HashSet<int>();
        var array = new PdfArray(edits.Store, order.Count);
        BuildOrder(array, order, references, seen, 0);
        if (seen.Count != references.Count && PdfEditConformance.ClaimsPdfA(document))
        {
            throw new ArgumentException("The claimed PDF/A level needs every layer listed in the order.", nameof(order));
        }

        PdfEditDictionaries.Child(edits, properties, KnownName.D).Set(KnownName.Order, PdfValue.FromArray(array));
    }

    /// <summary>Sets the radio button groups: at most one layer of a group is on. Where several start on, only the first stays on.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="groups">The layers of each group, by object number.</param>
    /// <exception cref="ArgumentException">A group has fewer than two layers, or names a layer the document does not have.</exception>
    internal static void SetRadioGroups(PdfObjectEdits edits, IReadOnlyList<IReadOnlyList<int>> groups)
    {
        var properties = PdfEditDictionaries.Child(edits, edits.Catalog(), KnownName.OCProperties);
        var references = ReferencesByNumber(properties.GetArray(KnownName.OCGs));
        var configuration = PdfEditDictionaries.Child(edits, properties, KnownName.D);
        var outer = new PdfArray(edits.Store, groups.Count);
        foreach (var members in groups)
        {
            if (members.Count < 2)
            {
                throw new ArgumentException("A radio button group needs at least two layers.", nameof(groups));
            }

            var inner = new PdfArray(edits.Store, members.Count);
            foreach (var number in members)
            {
                inner.Add(references.TryGetValue(number, out var reference) ? reference : throw new ArgumentException($"Object {number} is not a layer of this document.", nameof(groups)));
            }

            outer.Add(PdfValue.FromArray(inner));
            KeepFirstOn(edits, configuration, inner);
        }

        if (outer.Count == 0)
        {
            _ = configuration.Remove(KnownName.RBGroups);
            return;
        }

        configuration.Set(KnownName.RBGroups, PdfValue.FromArray(outer));
    }

    /// <summary>Finds a layer's reference in the optional content properties.</summary>
    /// <param name="properties">The properties, or <see langword="null"/>.</param>
    /// <param name="layerId">The layer's object number.</param>
    /// <param name="reference">Receives the reference.</param>
    /// <returns><see langword="true"/> when the layer is listed.</returns>
    private static bool TryFind(PdfDictionary? properties, int layerId, out PdfValue reference)
    {
        var groups = properties?.GetArray(KnownName.OCGs);
        var at = PdfEditDictionaries.IndexOf(groups, layerId);
        reference = at >= 0 ? groups!.GetRaw(at) : default;
        return at >= 0;
    }

    /// <summary>Gets a layer's reference, which the layer must have.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="layerId">The layer's object number.</param>
    /// <param name="id">Receives the layer's object.</param>
    /// <returns>The reference value.</returns>
    /// <exception cref="ArgumentException">The document has no such layer.</exception>
    private static PdfValue Require(PdfObjectEdits edits, int layerId, out PdfObjectId id)
    {
        if (!TryFind(edits.CatalogForRead.GetDictionary(KnownName.OCProperties), layerId, out var reference))
        {
            throw new ArgumentException($"Object {layerId} is not a layer of this document.", nameof(layerId));
        }

        id = reference.AsReference();
        return reference;
    }

    /// <summary>Maps object numbers to the references in the <c>/OCGs</c> array.</summary>
    /// <param name="groups">The array, or <see langword="null"/>.</param>
    /// <returns>The map.</returns>
    private static Dictionary<int, PdfValue> ReferencesByNumber(PdfArray? groups)
    {
        var map = new Dictionary<int, PdfValue>(groups?.Count ?? 0);
        for (var i = 0; groups is not null && i < groups.Count; i++)
        {
            var raw = groups.GetRaw(i);
            if (raw.AsReference() is { IsValid: true } id)
            {
                map[id.Number] = raw;
            }
        }

        return map;
    }

    /// <summary>Builds the usage dictionary (Table 100).</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="usage">The usage.</param>
    /// <returns>The dictionary.</returns>
    /// <exception cref="ArgumentException">Only one of the creator and its subtype is set.</exception>
    private static PdfDictionary BuildUsage(PdfObjectStore store, PdfLayerUsage usage)
    {
        var result = new PdfDictionary(store);
        AddState(store, result, "View"u8, "ViewState"u8, usage.ViewState);
        AddState(store, result, "Print"u8, "PrintState"u8, usage.PrintState);
        AddState(store, result, "Export"u8, "ExportState"u8, usage.ExportState);
        if (string.IsNullOrEmpty(usage.Creator) != string.IsNullOrEmpty(usage.CreatorSubtype))
        {
            throw new ArgumentException("The creator and its content kind must be given together.", nameof(usage));
        }

        if (!string.IsNullOrEmpty(usage.Creator))
        {
            var info = new PdfDictionary(store);
            info.Set(KnownName.Creator, PdfValue.FromString(PdfText.Encode(usage.Creator, store.Version)));
            info.Set(KnownName.Subtype, PdfValue.FromName(store.Names.Intern(usage.CreatorSubtype!)));
            result.Set(store.Names.Intern("CreatorInfo"u8), PdfValue.FromDictionary(info));
        }

        return result;
    }

    /// <summary>Adds a category with an ON or OFF state.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="usage">The usage dictionary.</param>
    /// <param name="category">The category key as UTF-8.</param>
    /// <param name="stateKey">The state key as UTF-8.</param>
    /// <param name="state">The state, or <see langword="null"/> to leave the category out.</param>
    private static void AddState(PdfObjectStore store, PdfDictionary usage, ReadOnlySpan<byte> category, ReadOnlySpan<byte> stateKey, bool? state)
    {
        if (state is not { } on)
        {
            return;
        }

        var inner = new PdfDictionary(store);
        inner.Set(store.Names.Intern(stateKey), PdfValue.FromName(on ? KnownName.ON : KnownName.OFF));
        usage.Set(store.Names.Intern(category), PdfValue.FromDictionary(inner));
    }

    /// <summary>Lists a new layer last in the default configuration's order; an order that does not exist yet starts with every layer.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="properties">The optional content properties copy, whose <c>/OCGs</c> already holds the new layer.</param>
    /// <param name="configuration">The default configuration copy.</param>
    /// <param name="reference">The new layer.</param>
    private static void AppendToOrder(PdfObjectEdits edits, PdfDictionary properties, PdfDictionary configuration, PdfValue reference)
    {
        // A missing order lists no layer in a viewer's panel (Table 99), so start from all of them, as every layer must appear (ISO 19005-2 6.9).
        if (configuration.GetArray(KnownName.Order) is null)
        {
            configuration.Set(KnownName.Order, PdfValue.FromArray(properties.GetArray(KnownName.OCGs)!.Clone()));
            return;
        }

        PdfEditDictionaries.ChildArray(edits, configuration, KnownName.Order).Add(reference);
    }

    /// <summary>Sets a layer's state in a configuration through its base state and the <c>/ON</c> and <c>/OFF</c> arrays.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="configuration">The configuration copy.</param>
    /// <param name="reference">The layer.</param>
    /// <param name="visible">Whether the layer is on.</param>
    private static void SetDefaultState(PdfObjectEdits edits, PdfDictionary configuration, PdfValue reference, bool visible)
    {
        var number = reference.AsReference().Number;
        var baseOff = configuration.IsName(KnownName.BaseState, KnownName.OFF);
        RemoveFrom(edits, configuration, KnownName.ON, number);
        RemoveFrom(edits, configuration, KnownName.OFF, number);
        if (visible == !baseOff)
        {
            return;
        }

        AddTo(edits, configuration, visible ? KnownName.ON : KnownName.OFF, reference);
    }

    /// <summary>Determines whether a configuration starts a layer shown.</summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="number">The layer's object number.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsOn(PdfDictionary configuration, int number) =>
        configuration.IsName(KnownName.BaseState, KnownName.OFF)
            ? PdfEditDictionaries.Contains(configuration.GetArray(KnownName.ON), number)
            : !PdfEditDictionaries.Contains(configuration.GetArray(KnownName.OFF), number);

    /// <summary>Turns off every layer of a radio group except one.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="configuration">The configuration copy.</param>
    /// <param name="group">The group.</param>
    /// <param name="keep">The object number of the layer that stays on.</param>
    private static void TurnOffOthers(PdfObjectEdits edits, PdfDictionary configuration, PdfArray group, int keep)
    {
        for (var i = 0; i < group.Count; i++)
        {
            var raw = group.GetRaw(i);
            if (raw.AsReference().Number != keep)
            {
                SetDefaultState(edits, configuration, raw, false);
            }
        }
    }

    /// <summary>Keeps the first layer of a group that starts on and turns off the rest.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="configuration">The configuration copy.</param>
    /// <param name="group">The group.</param>
    private static void KeepFirstOn(PdfObjectEdits edits, PdfDictionary configuration, PdfArray group)
    {
        for (var i = 0; i < group.Count; i++)
        {
            var number = group.GetRaw(i).AsReference().Number;
            if (!IsOn(configuration, number))
            {
                continue;
            }

            TurnOffOthers(edits, configuration, group, number);
            return;
        }
    }

    /// <summary>Adds a reference to one of a configuration's arrays when it is not there.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="configuration">The configuration copy.</param>
    /// <param name="key">The array's key.</param>
    /// <param name="reference">The layer.</param>
    private static void AddTo(PdfObjectEdits edits, PdfDictionary configuration, KnownName key, PdfValue reference)
    {
        if (!PdfEditDictionaries.Contains(configuration.GetArray(key), reference.AsReference().Number))
        {
            PdfEditDictionaries.ChildArray(edits, configuration, key).Add(reference);
        }
    }

    /// <summary>Removes a layer from one of a configuration's arrays, dropping the array when it ends up empty.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="configuration">The configuration copy.</param>
    /// <param name="key">The array's key.</param>
    /// <param name="number">The layer's object number.</param>
    private static void RemoveFrom(PdfObjectEdits edits, PdfDictionary configuration, KnownName key, int number)
    {
        if (!PdfEditDictionaries.Contains(configuration.GetArray(key), number))
        {
            return;
        }

        var array = PdfEditDictionaries.ChildArray(edits, configuration, key);
        _ = PdfEditDictionaries.RemoveAll(array, number);
        if (array.Count == 0)
        {
            _ = configuration.Remove(key);
        }
    }

    /// <summary>Removes a layer from every entry of a configuration.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="configuration">The configuration copy.</param>
    /// <param name="number">The layer's object number.</param>
    private static void StripConfiguration(PdfObjectEdits edits, PdfDictionary configuration, int number)
    {
        RemoveFrom(edits, configuration, KnownName.ON, number);
        RemoveFrom(edits, configuration, KnownName.OFF, number);
        RemoveFrom(edits, configuration, KnownName.Locked, number);
        if (configuration.GetArray(KnownName.Order) is { } order)
        {
            configuration.Set(KnownName.Order, PdfValue.FromArray(StripOrder(order, number, 0)));
        }

        StripGroups(edits, configuration, number);
        StripAutoStates(edits, configuration, number);
    }

    /// <summary>Removes a layer from the radio groups, dropping a group left with fewer than two layers.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="configuration">The configuration copy.</param>
    /// <param name="number">The layer's object number.</param>
    private static void StripGroups(PdfObjectEdits edits, PdfDictionary configuration, int number)
    {
        if (configuration.GetArray(KnownName.RBGroups) is not { } groups)
        {
            return;
        }

        var kept = new PdfArray(edits.Store, groups.Count);
        for (var i = 0; i < groups.Count; i++)
        {
            if (groups.GetArray(i) is not { } group)
            {
                continue;
            }

            var copy = group.Clone();
            _ = PdfEditDictionaries.RemoveAll(copy, number);
            if (copy.Count > 1)
            {
                kept.Add(PdfValue.FromArray(copy));
            }
        }

        configuration.Set(KnownName.RBGroups, PdfValue.FromArray(kept));
    }

    /// <summary>Removes a layer from the usage application dictionaries of <c>/AS</c> (Table 101).</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="configuration">The configuration copy.</param>
    /// <param name="number">The layer's object number.</param>
    private static void StripAutoStates(PdfObjectEdits edits, PdfDictionary configuration, int number)
    {
        if (configuration.GetArray(KnownName.AS) is not { } states)
        {
            return;
        }

        var copy = states.Clone();
        for (var i = 0; i < copy.Count; i++)
        {
            if (copy.GetDictionary(i) is not { } application || !PdfEditDictionaries.Contains(application.GetArray(KnownName.OCGs), number))
            {
                continue;
            }

            var changed = application.Clone();
            _ = PdfEditDictionaries.RemoveAll(PdfEditDictionaries.ChildArray(edits, changed, KnownName.OCGs), number);
            copy.SetAt(i, PdfValue.FromDictionary(changed));
        }

        configuration.Set(KnownName.AS, PdfValue.FromArray(copy));
    }

    /// <summary>Removes a layer from the alternate configurations.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="properties">The optional content properties copy.</param>
    /// <param name="number">The layer's object number.</param>
    private static void StripOtherConfigurations(PdfObjectEdits edits, PdfDictionary properties, int number)
    {
        if (properties.GetArray(KnownName.Configs) is null)
        {
            return;
        }

        var configurations = PdfEditDictionaries.ChildArray(edits, properties, KnownName.Configs);
        for (var i = 0; i < configurations.Count; i++)
        {
            var raw = configurations.GetRaw(i);
            if (raw.IsReference)
            {
                if (edits.Edit(raw.AsReference()) is { } indirect)
                {
                    StripConfiguration(edits, indirect, number);
                }

                continue;
            }

            if (raw.AsDictionary() is not { } direct)
            {
                continue;
            }

            var copy = direct.Clone();
            StripConfiguration(edits, copy, number);
            configurations.SetAt(i, PdfValue.FromDictionary(copy));
        }
    }

    /// <summary>Copies an order array without a layer. The layer's sublayers move up to its level, as they no longer have a parent.</summary>
    /// <param name="source">The array.</param>
    /// <param name="number">The layer's object number.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The new array.</returns>
    private static PdfArray StripOrder(PdfArray source, int number, int depth)
    {
        var result = new PdfArray(source.Owner, source.Count);
        for (var i = 0; i < source.Count; i++)
        {
            var raw = source.GetRaw(i);
            if (raw.AsReference().Number == number && raw.IsReference)
            {
                if (i + 1 < source.Count && IsChildList(source, i + 1) && depth < PdfLimits.MaxNesting)
                {
                    SpliceChildren(result, StripOrder(source.GetArray(i + 1)!, number, depth + 1));
                    i++;
                }

                continue;
            }

            result.Add(source.GetArray(i) is { } nested && depth < PdfLimits.MaxNesting ? PdfValue.FromArray(StripOrder(nested, number, depth + 1)) : raw);
        }

        return result;
    }

    /// <summary>Determines whether an order entry is a list of sublayers: an array with no label.</summary>
    /// <param name="array">The array holding the entry.</param>
    /// <param name="index">The entry's position.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsChildList(PdfArray array, int index) =>
        array.GetArray(index) is { } nested && !(nested.Count > 0 && nested.Get(0).Kind == PdfKind.String);

    /// <summary>Appends the entries of one array to another.</summary>
    /// <param name="target">The array that grows.</param>
    /// <param name="items">The entries.</param>
    private static void SpliceChildren(PdfArray target, PdfArray items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            target.Add(items.GetRaw(i));
        }
    }

    /// <summary>Builds the entries of an order array from a tree of layers and labels.</summary>
    /// <param name="target">The array that receives the entries.</param>
    /// <param name="nodes">The tree level.</param>
    /// <param name="references">The document's layers by object number.</param>
    /// <param name="seen">The layers listed so far.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <exception cref="ArgumentException">An entry names an unknown layer, names a layer twice, or nests too deep.</exception>
    private static void BuildOrder(PdfArray target, IReadOnlyList<PdfLayerNode> nodes, Dictionary<int, PdfValue> references, HashSet<int> seen, int depth)
    {
        if (depth > PdfLimits.MaxNesting)
        {
            throw new ArgumentException("The layer order is nested too deep.", nameof(nodes));
        }

        foreach (var node in nodes)
        {
            if (node.IsLabel)
            {
                var group = new PdfArray(target.Owner, node.Children.Length + 1);
                group.Add(PdfValue.FromString(PdfText.Encode(node.Name, target.Owner?.Version)));
                BuildOrder(group, node.Children, references, seen, depth + 1);
                target.Add(PdfValue.FromArray(group));
                continue;
            }

            if (!references.TryGetValue(node.LayerId, out var reference))
            {
                throw new ArgumentException($"Object {node.LayerId} is not a layer of this document.", nameof(nodes));
            }

            if (!seen.Add(node.LayerId))
            {
                throw new ArgumentException($"Layer '{node.Name}' is listed twice.", nameof(nodes));
            }

            target.Add(reference);
            if (node.Children.Length == 0)
            {
                continue;
            }

            var children = new PdfArray(target.Owner, node.Children.Length);
            BuildOrder(children, node.Children, references, seen, depth + 1);
            target.Add(PdfValue.FromArray(children));
        }
    }
}
