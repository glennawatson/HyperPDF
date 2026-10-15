// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Layers;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Registers the optional content groups the copied pages use. Copying page content copies the groups it names, but only
/// <c>/OCProperties</c> makes them layers. Each copied group joins the target's <c>/OCGs</c> and its default
/// configuration, shown or hidden as the source showed it.
/// </summary>
[DebuggerDisplay("PdfCarryLayers")]
internal sealed class PdfCarryLayers
{
    /// <summary>The fewest members that make a radio-button group worth carrying.</summary>
    private const int MinRadioMembers = 2;

    /// <summary>The context.</summary>
    private readonly PdfCarryContext _context;

    /// <summary>The source object numbers of the copied groups.</summary>
    private readonly HashSet<int> _copied = [];

    /// <summary>The source configuration whose radio-button groups and automatic states are carried; made by <see cref="Finish"/>.</summary>
    private PdfDictionary? _sourceConfiguration;

    /// <summary>Initializes a new instance of the <see cref="PdfCarryLayers"/> class.</summary>
    /// <param name="context">The context.</param>
    internal PdfCarryLayers(PdfCarryContext context) => _context = context;

    /// <summary>Adds the copied optional content groups to the target's optional content properties.</summary>
    internal void Finish()
    {
        var groups = FindCopiedGroups();
        if (groups.Count == 0)
        {
            return;
        }

        foreach (var group in groups)
        {
            _ = _copied.Add(group.Number);
        }

        if (_context.Source.Catalog.GetDictionary(KnownName.OCProperties) is { } sourceProperties)
        {
            _sourceConfiguration = sourceProperties.GetDictionary(KnownName.D);
        }

        var catalog = _context.Catalog;
        var properties = catalog.GetDictionary(KnownName.OCProperties)?.Clone() ?? _context.NewDictionary();
        properties.Set(KnownName.OCGs, PdfValue.FromArray(ListGroups(properties.GetArray(KnownName.OCGs), groups)));
        properties.Set(KnownName.D, PdfValue.FromDictionary(Configure(properties.GetDictionary(KnownName.D), groups)));
        _context.SetEntry(catalog, KnownName.OCProperties, properties);
        _context.CatalogChanged = true;
    }

    /// <summary>Sets an array unless there is none or it is empty.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    private static void SetIfAny(PdfDictionary dictionary, KnownName key, PdfArray? array)
    {
        if (array is { Count: > 0 })
        {
            dictionary.Set(key, PdfValue.FromArray(array));
        }
    }

    /// <summary>Lists the copied groups after the groups the target already lists.</summary>
    /// <param name="listed">The target's /OCGs array, or <see langword="null"/>.</param>
    /// <param name="groups">The copied groups.</param>
    /// <returns>The new array.</returns>
    private PdfArray ListGroups(PdfArray? listed, List<CopiedGroup> groups)
    {
        var references = listed?.Clone() ?? new PdfArray(_context.TargetStore);
        foreach (var group in groups)
        {
            references.Add(Reference(group));
        }

        return references;
    }

    /// <summary>Gets a reference to the copy of a group.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The reference.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PdfValue Reference(CopiedGroup group) => PdfValue.FromReference(new(_context.Sink.GetMapped(group.Number), 0));

    /// <summary>Finds the optional content groups among the objects copied, with whether the source hides each.</summary>
    /// <returns>The groups.</returns>
    private List<CopiedGroup> FindCopiedGroups()
    {
        var source = _context.Source;
        var groups = new List<CopiedGroup>();
        OptionalContentSet? set = null;
        for (var number = 1; number < source.Size; number++)
        {
            if (!IsCopiedGroup(number))
            {
                continue;
            }

            set ??= new OptionalContentReader().Read(source);
            groups.Add(new(number, set.IsHiddenByDefault(number)));
        }

        return groups;
    }

    /// <summary>Determines whether a source object is an optional content group that was copied.</summary>
    /// <param name="number">The source object number.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private bool IsCopiedGroup(int number) =>
        _context.Sink.GetMapped(number) > 0
        && StoreReading.GetDictionary(
        _context.Source,
        new(
        number,
        0)) is { } group
        && group.IsName(
        KnownName.Type,
        KnownName.OCG);

    /// <summary>Makes a configuration list each group in the state the source showed it.</summary>
    /// <param name="configuration">The target's configuration, or <see langword="null"/>.</param>
    /// <param name="groups">The copied groups.</param>
    /// <returns>The changed copy.</returns>
    private PdfDictionary Configure(PdfDictionary? configuration, List<CopiedGroup> groups)
    {
        var copy = configuration?.Clone() ?? _context.NewDictionary();
        var baseHidden = copy.IsName(KnownName.BaseState, KnownName.OFF);
        var on = Extend(copy.GetArray(KnownName.ON));
        var off = Extend(copy.GetArray(KnownName.OFF));
        var order = copy.GetArray(KnownName.Order)?.Clone();
        foreach (var group in groups)
        {
            var reference = Reference(group);
            order?.Add(reference);
            if (group.IsHidden != baseHidden)
            {
                (group.IsHidden ? off : on).Add(reference);
            }
        }

        SetIfAny(copy, KnownName.ON, on);
        SetIfAny(copy, KnownName.OFF, off);
        SetIfAny(copy, KnownName.Order, order);
        AddRadioGroups(copy);
        AddAutomaticStates(copy);
        return copy;
    }

    /// <summary>
    /// Adds the source's radio-button groups (<c>/RBGroups</c>, ISO 32000-2 8.11.4.3, Table 100) to a configuration, cut to
    /// the copied groups. A group left with fewer than two copied members no longer limits anything and is left out.
    /// </summary>
    /// <param name="configuration">A copy of the target's configuration, changed in place.</param>
    private void AddRadioGroups(PdfDictionary configuration)
    {
        if (_sourceConfiguration?.GetArray(KnownName.RBGroups) is not { } sets)
        {
            return;
        }

        var added = new List<PdfValue>();
        for (var i = 0; i < sets.Count; i++)
        {
            if (sets.GetArray(i) is { } members && KeepCopied(members, MinRadioMembers) is { } kept)
            {
                added.Add(PdfValue.FromArray(kept));
            }
        }

        Append(configuration, KnownName.RBGroups, added);
    }

    /// <summary>Adds values to the end of a configuration's array, creating the array when the configuration has none.</summary>
    /// <param name="configuration">A copy of the target's configuration, changed in place.</param>
    /// <param name="key">The array's key.</param>
    /// <param name="added">The values to add; nothing changes when there are none.</param>
    private void Append(PdfDictionary configuration, KnownName key, List<PdfValue> added)
    {
        if (added.Count == 0)
        {
            return;
        }

        var merged = Extend(configuration.GetArray(key));
        foreach (var value in added)
        {
            merged.Add(value);
        }

        configuration.Set(key, PdfValue.FromArray(merged));
    }

    /// <summary>
    /// Adds the source's automatic state changes (<c>/AS</c>, ISO 32000-2 8.11.4.3, Table 100) to a configuration, cut to the
    /// copied groups. An entry with no copied group is left out.
    /// </summary>
    /// <param name="configuration">A copy of the target's configuration, changed in place.</param>
    private void AddAutomaticStates(PdfDictionary configuration)
    {
        if (_sourceConfiguration?.GetArray(KnownName.AS) is not { } states)
        {
            return;
        }

        var added = new List<PdfValue>();
        for (var i = 0; i < states.Count; i++)
        {
            if (states.GetDictionary(i) is { } state && CopyState(state) is { } entry)
            {
                added.Add(PdfValue.FromDictionary(entry));
            }
        }

        Append(configuration, KnownName.AS, added);
    }

    /// <summary>Copies one automatic state entry, cut to the copied groups.</summary>
    /// <param name="state">The entry in the source.</param>
    /// <returns>The copy, or <see langword="null"/> when none of its groups were copied.</returns>
    private PdfDictionary? CopyState(PdfDictionary state)
    {
        if (state.GetArray(KnownName.OCGs) is not { } members || KeepCopied(members, 1) is not { } kept)
        {
            return null;
        }

        var entry = _context.NewDictionary();
        entry.Set(KnownName.Event, _context.Sink.Import(state.GetRaw(KnownName.Event)));
        entry.Set(KnownName.Category, _context.Sink.Import(state.GetRaw(KnownName.Category)));
        entry.Set(KnownName.OCGs, PdfValue.FromArray(kept));
        return entry;
    }

    /// <summary>Lists the copies of the copied groups among some references.</summary>
    /// <param name="members">The source array of group references.</param>
    /// <param name="minimum">The fewest copied groups the result needs.</param>
    /// <returns>References to the copies, or <see langword="null"/> when fewer than <paramref name="minimum"/> were copied.</returns>
    private PdfArray? KeepCopied(PdfArray members, int minimum)
    {
        PdfArray? kept = null;
        for (var i = 0; i < members.Count; i++)
        {
            var group = members.GetRaw(i).AsReference();
            if (!group.IsValid || !_copied.Contains(group.Number))
            {
                continue;
            }

            kept ??= new PdfArray(_context.TargetStore);
            kept.Add(PdfValue.FromReference(new(_context.Sink.GetMapped(group.Number), 0)));
        }

        return kept is not null && kept.Count >= minimum ? kept : null;
    }

    /// <summary>Copies an array so groups can be added, or creates an empty one.</summary>
    /// <param name="array">The target's array, or <see langword="null"/>.</param>
    /// <returns>The array to add to.</returns>
    private PdfArray Extend(PdfArray? array) => array?.Clone() ?? new PdfArray(_context.TargetStore);

    /// <summary>An optional content group that was copied.</summary>
    /// <param name="Number">The group's object number in the source.</param>
    /// <param name="IsHidden">Whether the source's default configuration hides it.</param>
    [DebuggerDisplay("CopiedGroup: {Number}")]
    private readonly record struct CopiedGroup(int Number, bool IsHidden);
}
