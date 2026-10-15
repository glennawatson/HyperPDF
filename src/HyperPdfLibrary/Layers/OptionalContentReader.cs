// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Layers;

/// <summary>
/// Reads the layers and their default state for the View, Print and Export events (ISO 32000-2 8.11.4). The default
/// configuration /D, or a configuration the caller chooses from /Configs, supplies /BaseState, /ON, /OFF, /AS and
/// /RBGroups; groups whose /Intent is not View or All are always shown. /AS entries read each named category from the
/// group's /Usage (8.11.4.4). It also reads /Order, /ListMode, /Locked and /Usage /Zoom. A reader keeps its working
/// arrays between reads, so reading makes only the set it returns.
/// </summary>
internal sealed class OptionalContentReader
{
    /// <summary>The objects <see cref="Witness"/> always lists: the properties, the default configuration, and the two arrays.</summary>
    private const int WitnessFixedSlots = 4;

    /// <summary>The most elements a working array keeps while the reader sits idle.</summary>
    private const int MaxIdleElements = 4096;

    /// <summary>The usage dictionary key of each event, in event order.</summary>
    private static readonly KnownName[] Categories = [KnownName.View, KnownName.Print, KnownName.Export];

    /// <summary>The spellings of each event's state key, in event order.</summary>
    private static readonly byte[][] StateSpellings = ["ViewState"u8.ToArray(), "PrintState"u8.ToArray(), "ExportState"u8.ToArray()];

    /// <summary>The entries of the /Order levels being read.</summary>
    private readonly LayerNodeStack _nodes = new();

    /// <summary>The groups found, in document order.</summary>
    private PdfObjectId[] _groups = [];

    /// <summary>Sort keys that find a group listed twice.</summary>
    private long[] _keys = [];

    /// <summary>The object numbers of the groups, ascending.</summary>
    private int[] _sorted = [];

    /// <summary>For each entry of <see cref="_sorted"/>, the position of the layer in document order.</summary>
    private int[] _positions = [];

    /// <summary>The object numbers listed in /ON, ascending.</summary>
    private int[] _on = [];

    /// <summary>The object numbers listed in /OFF, ascending.</summary>
    private int[] _off = [];

    /// <summary>The object numbers listed in /Locked, ascending.</summary>
    private int[] _locked = [];

    /// <summary>The object numbers of one /RBGroups set, ascending.</summary>
    private int[] _members = [];

    /// <summary>The zoom range of each layer, by bit.</summary>
    private ZoomLimit[] _zoom = [];

    /// <summary>Room for the zoom limits before they are sorted.</summary>
    private double[] _limits = [];

    /// <summary>The number of groups found.</summary>
    private int _groupCount;

    /// <summary>The number of entries in <see cref="_on"/>.</summary>
    private int _onCount;

    /// <summary>The number of entries in <see cref="_off"/>.</summary>
    private int _offCount;

    /// <summary>The number of entries in <see cref="_locked"/>.</summary>
    private int _lockedCount;

    /// <summary>The document's /OCProperties dictionary.</summary>
    private PdfDictionary? _properties;

    /// <summary>The configuration that applies, if any.</summary>
    private PdfDictionary? _configuration;

    /// <summary>The /AS array, if any.</summary>
    private PdfArray? _autoStates;

    /// <summary>The state key of the View event.</summary>
    private PdfName _viewState;

    /// <summary>The state key of the Print event.</summary>
    private PdfName _printState;

    /// <summary>The state key of the Export event.</summary>
    private PdfName _exportState;

    /// <summary>The key of a zoom dictionary's minimum.</summary>
    private PdfName _min;

    /// <summary>The key of a zoom dictionary's maximum.</summary>
    private PdfName _max;

    /// <summary>The /ListMode value that lists only the layers of visible pages.</summary>
    private PdfName _visiblePages;

    /// <summary>Whether groups are shown unless listed in /OFF.</summary>
    private bool _baseOn;

    /// <summary>Whether any layer has a zoom range that limits it.</summary>
    private bool _anyZoom;

    /// <summary>
    /// Gets the objects a read of the layers depends on, so a later call can tell whether an edit changed them. Edits replace
    /// objects rather than change them, so the same objects mean the same layers.
    /// </summary>
    /// <param name="objects">The document's objects.</param>
    /// <returns>The /OCProperties dictionary, its default configuration, its /OCGs and /Configs arrays, and the groups and configurations in them; empty when there are no layers.</returns>
    internal static object?[] Witness(PdfObjectStore objects)
    {
        if (objects.Catalog.GetDictionary(KnownName.OCProperties) is not { } properties)
        {
            return [];
        }

        var groups = properties.GetArray(KnownName.OCGs);
        var configurations = properties.GetArray(KnownName.Configs);
        var witness = new object?[WitnessFixedSlots + (groups?.Count ?? 0) + (configurations?.Count ?? 0)];
        var next = 0;
        witness[next] = properties;
        next++;
        witness[next] = properties.GetDictionary(KnownName.D);
        next++;
        witness[next] = groups;
        next++;
        witness[next] = configurations;
        next++;
        for (var i = 0; groups is not null && i < groups.Count; i++)
        {
            witness[next] = groups.GetDictionary(i);
            next++;
        }

        for (var i = 0; configurations is not null && i < configurations.Count; i++)
        {
            witness[next] = configurations.GetDictionary(i);
            next++;
        }

        return witness;
    }

    /// <summary>Determines whether the objects a witness lists are still the document's, without making a second witness.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="witness">A witness taken earlier.</param>
    /// <returns><see langword="true"/> when no listed object changed.</returns>
    internal static bool IsWitnessCurrent(PdfObjectStore objects, object?[] witness)
    {
        if (objects.Catalog.GetDictionary(KnownName.OCProperties) is not { } properties)
        {
            return witness.Length == 0;
        }

        var groups = properties.GetArray(KnownName.OCGs);
        var configurations = properties.GetArray(KnownName.Configs);
        var groupCount = groups?.Count ?? 0;
        return witness.Length == WitnessFixedSlots + groupCount + (configurations?.Count ?? 0)
            && HeaderMatches(witness, properties, groups, configurations)
            && DictionariesMatch(witness, WitnessFixedSlots, groups)
            && DictionariesMatch(witness, WitnessFixedSlots + groupCount, configurations);
    }

    /// <summary>Determines whether a group is meant for viewing: its /Intent is View or All, or missing when the default is View.</summary>
    /// <param name="dictionary">The group or configuration.</param>
    /// <param name="defaultResult">The answer when there is no /Intent.</param>
    /// <returns><see langword="true"/> when the intent includes View.</returns>
    internal static bool HasIntent(PdfDictionary dictionary, bool defaultResult)
    {
        var intent = dictionary.Get(KnownName.Intent);
        if (intent.IsNull)
        {
            return defaultResult;
        }

        if (intent.AsArray() is { } list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (IsViewIntent(list.GetName(i)))
                {
                    return true;
                }
            }

            return false;
        }

        return IsViewIntent(intent.AsName());
    }

    /// <summary>Gets a configuration by position.</summary>
    /// <param name="properties">The /OCProperties dictionary.</param>
    /// <param name="index">The position in /Configs, or <see cref="PdfLayerConfiguration.DefaultIndex"/> for /D.</param>
    /// <returns>The configuration, or <see langword="null"/> when there is none at that position.</returns>
    internal static PdfDictionary? ConfigurationAt(PdfDictionary properties, int index) =>
        index < 0 ? properties.GetDictionary(KnownName.D) : properties.GetArray(KnownName.Configs)?.GetDictionary(index);

    /// <summary>Reads the layers of a document under its default configuration /D.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <returns>The layers and their state.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal OptionalContentSet Read(PdfObjectStore objects) => Read(objects, PdfLayerConfiguration.DefaultIndex);

    /// <summary>Reads the layers of a document under a chosen configuration.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="configurationIndex">The position in /Configs, <see cref="PdfLayerConfiguration.DefaultIndex"/> for /D.</param>
    /// <returns>The layers and their state.</returns>
    internal OptionalContentSet Read(PdfObjectStore objects, int configurationIndex)
    {
        var properties = objects.Catalog.GetDictionary(KnownName.OCProperties);
        if (properties is null)
        {
            return OptionalContentSet.Empty;
        }

        try
        {
            Begin(objects, properties, ConfigurationAt(properties, configurationIndex));
            return ReadSet(objects, configurationIndex);
        }
        finally
        {
            Release();
        }
    }

    /// <summary>Determines whether the fixed slots of a witness list the properties, the default configuration and the two arrays.</summary>
    /// <param name="witness">A witness taken earlier.</param>
    /// <param name="properties">The /OCProperties dictionary.</param>
    /// <param name="groups">Its /OCGs array, if any.</param>
    /// <param name="configurations">Its /Configs array, if any.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool HeaderMatches(object?[] witness, PdfDictionary properties, PdfArray? groups, PdfArray? configurations) =>
        ReferenceEquals(witness[0], properties)
        && ReferenceEquals(witness[1], properties.GetDictionary(KnownName.D))
        && ReferenceEquals(witness[2], groups)
        && ReferenceEquals(witness[3], configurations);

    /// <summary>Determines whether a run of a witness lists the dictionaries an array holds now.</summary>
    /// <param name="witness">A witness taken earlier.</param>
    /// <param name="start">The slot of the first dictionary.</param>
    /// <param name="array">The array.</param>
    /// <returns><see langword="true"/> when every slot holds the dictionary now at its position.</returns>
    private static bool DictionariesMatch(object?[] witness, int start, PdfArray? array)
    {
        for (var i = 0; array is not null && i < array.Count; i++)
        {
            if (!ReferenceEquals(witness[start + i], array.GetDictionary(i)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets a configuration's /Name, or a fallback.</summary>
    /// <param name="configuration">The configuration, if it resolves.</param>
    /// <param name="position">Zero for the default configuration; otherwise the alternate configuration's one-based position.</param>
    /// <returns>The name.</returns>
    private static string ConfigurationName(PdfDictionary? configuration, int position)
    {
        var name = configuration?.GetText(KnownName.Name);
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return position == 0 ? "Default" : string.Create(CultureInfo.CurrentCulture, $"Configuration {position}");
    }

    /// <summary>Determines whether an intent name is View or All.</summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> for View and All.</returns>
    private static bool IsViewIntent(PdfName name) => name.ToKnownName() is KnownName.View or KnownName.All;

    /// <summary>Gets a group's name, or "Layer n" when it has none.</summary>
    /// <param name="group">The group dictionary.</param>
    /// <param name="position">The layer's position.</param>
    /// <returns>The name.</returns>
    private static string LayerName(PdfDictionary? group, int position)
    {
        var name = group?.GetText(KnownName.Name);
        return string.IsNullOrWhiteSpace(name) ? string.Create(CultureInfo.CurrentCulture, $"Layer {position + 1}") : name;
    }

    /// <summary>Gets the event an /AS entry is for; an entry without /Event is for View.</summary>
    /// <param name="entry">The /AS entry.</param>
    /// <returns>The event, or <see langword="null"/> when the entry names an unknown event.</returns>
    private static PdfOptionalContentEvent? EntryEvent(PdfDictionary entry) =>
        entry.ContainsKey(KnownName.Event) ? EventOf(entry.GetName(KnownName.Event).ToKnownName()) : PdfOptionalContentEvent.View;

    /// <summary>Maps a usage category name to its event.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The event, or <see langword="null"/> when the name is not View, Print or Export.</returns>
    private static PdfOptionalContentEvent? EventOf(KnownName name) => name switch
    {
        KnownName.View => PdfOptionalContentEvent.View,
        KnownName.Print => PdfOptionalContentEvent.Print,
        KnownName.Export => PdfOptionalContentEvent.Export,
        _ => null,
    };

    /// <summary>Determines whether an /AS entry's /OCGs lists a group.</summary>
    /// <param name="entry">The /AS entry.</param>
    /// <param name="id">The group's object number.</param>
    /// <returns><see langword="true"/> when listed.</returns>
    private static bool Lists(PdfDictionary entry, int id)
    {
        var groups = entry.GetArray(KnownName.OCGs);
        for (var i = 0; groups is not null && i < groups.Count; i++)
        {
            if (groups.GetRaw(i).AsReference().Number == id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether an /AS entry's /Category lists a usage category.</summary>
    /// <param name="entry">The /AS entry.</param>
    /// <param name="category">The category.</param>
    /// <returns><see langword="true"/> when listed.</returns>
    private static bool HasCategory(PdfDictionary entry, KnownName category)
    {
        var names = entry.GetArray(KnownName.Category);
        for (var i = 0; names is not null && i < names.Count; i++)
        {
            if (names.GetName(i).ToKnownName() == category)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether an /AS entry manages a group by zoom: it is for the View event, lists the group and names the Zoom category.</summary>
    /// <param name="entry">The /AS entry, if it resolves.</param>
    /// <param name="id">The group's object number.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool ManagesZoom(PdfDictionary? entry, int id) =>
        entry is not null && EntryEvent(entry) == PdfOptionalContentEvent.View && Lists(entry, id) && HasCategory(entry, KnownName.Zoom);

    /// <summary>Doubles an array until it holds a count of elements, keeping what it holds.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The array.</param>
    /// <param name="count">The elements needed.</param>
    private static void Reserve<T>(ref T[] array, int count)
    {
        if (array.Length >= count)
        {
            return;
        }

        Array.Resize(ref array, Math.Max(count, array.Length << 1));
    }

    /// <summary>Drops an array that grew past what an idle reader keeps.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The array.</param>
    private static void TrimIdle<T>(ref T[] array)
    {
        if (array.Length <= MaxIdleElements)
        {
            return;
        }

        array = [];
    }

    /// <summary>Adds a group to a bit set unless a condition holds.</summary>
    /// <param name="words">The bit set.</param>
    /// <param name="bit">The group's bit.</param>
    /// <param name="condition">Whether the group is left out.</param>
    private static void BuildUnless(int[] words, int bit, bool condition)
    {
        if (condition)
        {
            return;
        }

        OptionalContentSet.Build(words, bit);
    }

    /// <summary>Determines whether a sorted run of object numbers holds one.</summary>
    /// <param name="numbers">The numbers, ascending.</param>
    /// <param name="count">How many are in use.</param>
    /// <param name="id">The object number.</param>
    /// <returns><see langword="true"/> when listed.</returns>
    private static bool Has(int[] numbers, int count, int id) => Array.BinarySearch(numbers, 0, count, id) >= 0;

    /// <summary>Collects the sorted object numbers an array refers to into a working array.</summary>
    /// <param name="array">The array.</param>
    /// <param name="numbers">The working array, grown to fit.</param>
    /// <returns>The number of distinct object numbers.</returns>
    private static int ReferencedNumbers(PdfArray? array, ref int[] numbers)
    {
        var count = 0;
        Reserve(ref numbers, array?.Count ?? 0);
        for (var i = 0; array is not null && i < array.Count; i++)
        {
            var id = array.GetRaw(i).AsReference();
            if (!id.IsValid)
            {
                continue;
            }

            numbers[count] = id.Number;
            count++;
        }

        return SortDistinct(numbers, count);
    }

    /// <summary>Sorts a run of numbers and drops repeats.</summary>
    /// <param name="numbers">The numbers.</param>
    /// <param name="count">How many are in use.</param>
    /// <returns>How many remain.</returns>
    private static int SortDistinct(int[] numbers, int count)
    {
        Array.Sort(numbers, 0, count);
        var distinct = 0;
        for (var i = 0; i < count; i++)
        {
            if (i > 0 && numbers[i] == numbers[i - 1])
            {
                continue;
            }

            numbers[distinct] = numbers[i];
            distinct++;
        }

        return distinct;
    }

    /// <summary>Gets the key of an event's usage state.</summary>
    /// <param name="ev">The event.</param>
    /// <returns>The key.</returns>
    private PdfName StateKey(PdfOptionalContentEvent ev) => ev switch
    {
        PdfOptionalContentEvent.Print => _printState,
        PdfOptionalContentEvent.Export => _exportState,
        _ => _viewState,
    };

    /// <summary>Points the reader at a document's properties and a configuration.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="properties">The /OCProperties dictionary.</param>
    /// <param name="configuration">The configuration that applies.</param>
    private void Begin(PdfObjectStore objects, PdfDictionary properties, PdfDictionary? configuration)
    {
        _properties = properties;
        _configuration = configuration;
        _viewState = objects.Names.Intern(StateSpellings[0]);
        _printState = objects.Names.Intern(StateSpellings[1]);
        _exportState = objects.Names.Intern(StateSpellings[2]);
        _min = objects.Names.Intern("min"u8);
        _max = objects.Names.Intern("max"u8);
        _visiblePages = objects.Names.Intern("VisiblePages"u8);
        _baseOn = configuration is null || !configuration.IsName(KnownName.BaseState, KnownName.OFF);
        _onCount = ReferencedNumbers(configuration?.GetArray(KnownName.ON), ref _on);
        _offCount = ReferencedNumbers(configuration?.GetArray(KnownName.OFF), ref _off);
        _lockedCount = ReferencedNumbers(configuration?.GetArray(KnownName.Locked), ref _locked);
        _autoStates = configuration?.GetArray(KnownName.AS);
        _anyZoom = false;
    }

    /// <summary>Drops the document and shrinks oversized arrays, then keeps the reader for the next read.</summary>
    private void Release()
    {
        _properties = null;
        _configuration = null;
        _autoStates = null;
        _nodes.Clear();
        TrimIdle(ref _groups);
        TrimIdle(ref _keys);
        TrimIdle(ref _sorted);
        TrimIdle(ref _positions);
        TrimIdle(ref _on);
        TrimIdle(ref _off);
        TrimIdle(ref _locked);
        TrimIdle(ref _members);
        TrimIdle(ref _zoom);
        TrimIdle(ref _limits);
    }

    /// <summary>Reads the layers once the reader is pointed at a configuration.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="active">The configuration in use.</param>
    /// <returns>The set.</returns>
    private OptionalContentSet ReadSet(PdfObjectStore objects, int active)
    {
        var count = CollectGroups();
        var sortedIds = _sorted.AsSpan(0, count).ToArray();
        var words = OptionalContentSet.WordCount(count);
        var bits = new LayerBits(new int[words], new int[words], new int[words], new int[words]);
        Reserve(ref _positions, count);
        var layers = new PdfLayer[count];
        for (var i = 0; i < count; i++)
        {
            var id = _groups[i];
            var bit = Array.BinarySearch(sortedIds, id.Number);
            _positions[bit] = i;
            layers[i] = ReadLayer(StoreReading.GetDictionary(objects, id), id.Number, i, bit, bits);
        }

        return Finish(layers, sortedIds, bits, active);
    }

    /// <summary>Lists each group once, in /OCGs order; without /OCGs, the groups the configuration names.</summary>
    /// <returns>The number of groups; <see cref="_groups"/> holds them in order and <see cref="_sorted"/> their numbers, ascending.</returns>
    private int CollectGroups()
    {
        _groupCount = 0;
        var listed = _properties!.GetArray(KnownName.OCGs);
        AddGroups(listed);
        if (listed is null)
        {
            AddGroups(_configuration?.GetArray(KnownName.ON));
            AddGroups(_configuration?.GetArray(KnownName.OFF));
        }

        var found = _groupCount;
        Reserve(ref _keys, found);
        Reserve(ref _sorted, found);
        for (var i = 0; i < found; i++)
        {
            _keys[i] = ((long)_groups[i].Number << 32) | (uint)i;
        }

        Array.Sort(_keys, 0, found);
        var distinct = 0;
        var previous = 0;
        for (var i = 0; i < found; i++)
        {
            var number = (int)(_keys[i] >> 32);
            if (i > 0 && number == previous)
            {
                _groups[(int)(_keys[i] & uint.MaxValue)] = default;
                continue;
            }

            _sorted[distinct] = number;
            distinct++;
            previous = number;
        }

        var kept = 0;
        for (var i = 0; i < found; i++)
        {
            if (_groups[i].Number == 0)
            {
                continue;
            }

            _groups[kept] = _groups[i];
            kept++;
        }

        _groupCount = kept;
        return kept;
    }

    /// <summary>Adds each group an array refers to; a group listed twice is dropped afterwards.</summary>
    /// <param name="array">The array.</param>
    private void AddGroups(PdfArray? array)
    {
        Reserve(ref _groups, _groupCount + (array?.Count ?? 0));
        for (var i = 0; array is not null && i < array.Count; i++)
        {
            var id = array.GetRaw(i).AsReference();
            if (!id.IsValid)
            {
                continue;
            }

            _groups[_groupCount] = id;
            _groupCount++;
        }
    }

    /// <summary>Reads one layer and records its state.</summary>
    /// <param name="group">The group dictionary, if it resolves.</param>
    /// <param name="id">The group's object number.</param>
    /// <param name="position">The layer's position.</param>
    /// <param name="bit">The layer's bit.</param>
    /// <param name="bits">Collects the groups that are off for each event, and the groups that are not for viewing.</param>
    /// <returns>The layer.</returns>
    private PdfLayer ReadLayer(PdfDictionary? group, int id, int position, int bit, in LayerBits bits)
    {
        var visible = IsDefaultOn(PdfOptionalContentEvent.View, group, id);
        var printed = IsDefaultOn(PdfOptionalContentEvent.Print, group, id);
        var exported = IsDefaultOn(PdfOptionalContentEvent.Export, group, id);
        var forView = group is null || HasIntent(group, true);
        var zoom = forView ? ReadZoom(group, id) : ZoomLimit.Unlimited;
        BuildUnless(bits.NonView, bit, forView);
        BuildUnless(bits.Hidden, bit, visible);
        BuildUnless(bits.PrintHidden, bit, printed);
        BuildUnless(bits.ExportHidden, bit, exported);
        if (zoom.IsLimited)
        {
            if (!_anyZoom)
            {
                Reserve(ref _zoom, _groupCount);
                _zoom.AsSpan(0, _groupCount).Fill(ZoomLimit.Unlimited);
                _anyZoom = true;
            }

            _zoom[bit] = zoom;
        }

        return new(id, LayerName(group, position), visible, Has(_locked, _lockedCount, id), printed, exported, zoom.Min, zoom.Max);
    }

    /// <summary>Builds the set once every layer is read.</summary>
    /// <param name="layers">The layers.</param>
    /// <param name="sortedIds">The layers' object numbers, ascending.</param>
    /// <param name="bits">The collected state.</param>
    /// <param name="active">The configuration in use.</param>
    /// <returns>The set.</returns>
    private OptionalContentSet Finish(PdfLayer[] layers, int[] sortedIds, in LayerBits bits, int active)
    {
        var count = layers.Length;
        double[] limits = [];
        ZoomClassRange[] ranges = [];
        if (_anyZoom)
        {
            Reserve(ref _limits, count << 1);
            limits = OptionalContentZoom.CollectLimits(_zoom.AsSpan(0, count), _limits);
            ranges = new ZoomClassRange[count];
            for (var i = 0; i < count; i++)
            {
                ranges[i] = _zoom[i].IsLimited ? OptionalContentZoom.ToClasses(_zoom[i], limits) : new(0, int.MaxValue);
            }
        }

        return new(
            layers,
            sortedIds,
            bits.Hidden,
            [.. bits.Hidden],
            bits.NonView,
            ReadRadioGroups(),
            bits.PrintHidden,
            bits.ExportHidden,
            _lockedCount == 0 ? [] : _locked.AsSpan(0, _lockedCount).ToArray(),
            ranges,
            limits,
            ReadTree(layers, sortedIds),
            ReadListMode(),
            ListConfigurations(active),
            active);
    }

    /// <summary>Lists the default configuration and the alternates.</summary>
    /// <param name="active">The configuration in use.</param>
    /// <returns>The configurations.</returns>
    private PdfLayerConfiguration[] ListConfigurations(int active)
    {
        var configs = _properties!.GetArray(KnownName.Configs);
        var result = new PdfLayerConfiguration[(configs?.Count ?? 0) + 1];
        var defaults = _properties.GetDictionary(KnownName.D);
        result[0] = new(PdfLayerConfiguration.DefaultIndex, ConfigurationName(defaults, 0), defaults is null || HasIntent(defaults, true), active == PdfLayerConfiguration.DefaultIndex);
        for (var i = 1; i < result.Length; i++)
        {
            var candidate = configs!.GetDictionary(i - 1);
            result[i] = new(i - 1, ConfigurationName(candidate, i), candidate is not null && HasIntent(candidate, true), active == i - 1);
        }

        return result;
    }

    /// <summary>Reads the layer order: the configuration's /Order, else the default configuration's, else every layer in turn.</summary>
    /// <param name="layers">The layers.</param>
    /// <param name="sortedIds">The layers' object numbers, ascending.</param>
    /// <returns>The top-level entries.</returns>
    private PdfLayerNode[] ReadTree(PdfLayer[] layers, int[] sortedIds)
    {
        var order = _configuration?.GetArray(KnownName.Order) ?? _properties!.GetDictionary(KnownName.D)?.GetArray(KnownName.Order);
        return order is null
            ? OptionalContentOrder.Flat(layers)
            : OptionalContentOrder.Build(order, new(layers, sortedIds, _positions), _nodes);
    }

    /// <summary>Reads /ListMode.</summary>
    /// <returns>The mode; all pages when missing or unknown.</returns>
    private PdfLayerListMode ReadListMode() =>
        _configuration?.GetName(KnownName.ListMode) == _visiblePages ? PdfLayerListMode.VisiblePages : PdfLayerListMode.AllPages;

    /// <summary>Reads the /RBGroups: sets of groups of which only one is shown, from the configuration or else the default configuration.</summary>
    /// <returns>The object numbers of each set.</returns>
    private int[][] ReadRadioGroups()
    {
        var sets = _configuration?.GetArray(KnownName.RBGroups) ?? _properties!.GetDictionary(KnownName.D)?.GetArray(KnownName.RBGroups);
        var count = 0;
        for (var i = 0; sets is not null && i < sets.Count; i++)
        {
            count += sets.GetArray(i) is null ? 0 : 1;
        }

        if (count == 0)
        {
            return [];
        }

        var result = new int[count][];
        var next = 0;
        for (var i = 0; i < sets!.Count; i++)
        {
            if (sets.GetArray(i) is not { } set)
            {
                continue;
            }

            var members = ReferencedNumbers(set, ref _members);
            result[next] = _members.AsSpan(0, members).ToArray();
            next++;
        }

        return result;
    }

    /// <summary>
    /// Works out whether a group is on for an event under the configuration (8.11.4.3). A group that is missing or not
    /// for the View intent is always on.
    /// </summary>
    /// <param name="ev">The event.</param>
    /// <param name="group">The group dictionary, if it resolves.</param>
    /// <param name="id">The group's object number.</param>
    /// <returns><see langword="true"/> when on.</returns>
    private bool IsDefaultOn(PdfOptionalContentEvent ev, PdfDictionary? group, int id) =>
        group is null || !HasIntent(group, true) || _configuration is null || ConfigurationState(ev, group, id);

    /// <summary>
    /// Reads the state a usage application dictionary recommends for one category of a group (8.11.4.4). A category
    /// without its state entry recommends nothing, so the group stays as the configuration left it.
    /// </summary>
    /// <param name="usage">The group's /Usage dictionary.</param>
    /// <param name="ev">The category's event.</param>
    /// <returns>Whether the recommendation is on; <see langword="null"/> when there is none.</returns>
    private bool? Recommendation(PdfDictionary usage, PdfOptionalContentEvent ev)
    {
        var key = StateKey(ev);
        var state = usage.GetDictionary(Categories[(int)ev]);
        return state is not null && state.ContainsKey(key) ? !state.IsName(key, KnownName.OFF) : null;
    }

    /// <summary>Applies the configuration's base state, /ON, /OFF and the event's /AS entries to a group.</summary>
    /// <param name="ev">The event.</param>
    /// <param name="group">The group dictionary.</param>
    /// <param name="id">The group's object number.</param>
    /// <returns><see langword="true"/> when on.</returns>
    private bool ConfigurationState(PdfOptionalContentEvent ev, PdfDictionary group, int id)
    {
        var shown = (_baseOn || Has(_on, _onCount, id)) && !Has(_off, _offCount, id);
        var usage = group.GetDictionary(KnownName.Usage);
        var found = false;
        var automatic = true;
        for (var i = 0; usage is not null && _autoStates is not null && i < _autoStates.Count; i++)
        {
            if (_autoStates.GetDictionary(i) is not { } entry || EntryEvent(entry) != ev || !Lists(entry, id))
            {
                continue;
            }

            var result = ApplyCategories(entry, usage, ev);
            found |= result.Found;
            automatic &= result.Shown;
        }

        return found ? automatic : shown;
    }

    /// <summary>
    /// Combines the states an /AS entry's categories recommend. When the entry has no /Category, the event's own
    /// category applies. The Zoom category recommends ON here and limits the group at render time.
    /// </summary>
    /// <param name="entry">The /AS entry.</param>
    /// <param name="usage">The group's /Usage dictionary.</param>
    /// <param name="ev">The entry's event.</param>
    /// <returns>Whether any category gave a recommendation, and whether all of them are on.</returns>
    private AutoState ApplyCategories(PdfDictionary entry, PdfDictionary usage, PdfOptionalContentEvent ev)
    {
        var names = entry.GetArray(KnownName.Category);
        if (names is null || names.Count == 0)
        {
            return RecommendationOf(usage, ev);
        }

        var found = false;
        var shown = true;
        for (var i = 0; i < names.Count; i++)
        {
            var result = CategoryRecommendation(usage, names.GetName(i).ToKnownName());
            found |= result.Found;
            shown &= result.Shown;
        }

        return new(found, shown);
    }

    /// <summary>Gets what one usage category recommends for a group. View, Print and Export read their state; Zoom recommends ON; other categories recommend nothing.</summary>
    /// <param name="usage">The group's /Usage dictionary.</param>
    /// <param name="category">The category name.</param>
    /// <returns>The recommendation.</returns>
    private AutoState CategoryRecommendation(PdfDictionary usage, KnownName category)
    {
        if (category == KnownName.Zoom)
        {
            return new(usage.GetDictionary(KnownName.Zoom) is not null, true);
        }

        return EventOf(category) is { } categoryEvent ? RecommendationOf(usage, categoryEvent) : default;
    }

    /// <summary>Wraps a category's recommendation.</summary>
    /// <param name="usage">The group's /Usage dictionary.</param>
    /// <param name="ev">The category's event.</param>
    /// <returns>The recommendation.</returns>
    private AutoState RecommendationOf(PdfDictionary usage, PdfOptionalContentEvent ev) =>
        Recommendation(usage, ev) is { } recommended ? new(true, recommended) : new(false, true);

    /// <summary>Reads the zoom range /AS entries for the View event apply to a group.</summary>
    /// <param name="group">The group dictionary, if it resolves.</param>
    /// <param name="id">The group's object number.</param>
    /// <returns>The range; unlimited when no entry manages the group by zoom.</returns>
    private ZoomLimit ReadZoom(PdfDictionary? group, int id)
    {
        var zoom = group?.GetDictionary(KnownName.Usage)?.GetDictionary(KnownName.Zoom);
        var result = ZoomLimit.Unlimited;
        for (var i = 0; zoom is not null && _autoStates is not null && i < _autoStates.Count; i++)
        {
            if (ManagesZoom(_autoStates.GetDictionary(i), id))
            {
                result = result.Intersect(ZoomRangeOf(zoom));
            }
        }

        return result;
    }

    /// <summary>Reads a /Usage /Zoom dictionary: /min defaults to 0 and /max to infinity.</summary>
    /// <param name="zoom">The zoom dictionary.</param>
    /// <returns>The range.</returns>
    private ZoomLimit ZoomRangeOf(PdfDictionary zoom) =>
        new(zoom.GetNumber(_min, 0), zoom.GetNumber(_max, double.PositiveInfinity));

    /// <summary>The outcome of an /AS entry's categories for one group.</summary>
    /// <param name="Found">Whether any category recommended a state.</param>
    /// <param name="Shown">Whether every recommendation is on.</param>
    private readonly record struct AutoState(bool Found, bool Shown);

    /// <summary>The bit sets collected while the layers are read.</summary>
    /// <param name="Hidden">The groups hidden when viewing.</param>
    /// <param name="NonView">The groups that are not for viewing.</param>
    /// <param name="PrintHidden">The groups hidden when printing.</param>
    /// <param name="ExportHidden">The groups hidden when exporting.</param>
    private readonly record struct LayerBits(int[] Hidden, int[] NonView, int[] PrintHidden, int[] ExportHidden);
}
