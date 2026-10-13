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
/// group's /Usage (8.11.4.4). It also reads /Order, /ListMode, /Locked and /Usage /Zoom.
/// </summary>
internal sealed class OptionalContentReader
{
    /// <summary>The objects <see cref="Witness"/> always lists: the properties, the default configuration, and the two arrays.</summary>
    private const int WitnessFixedSlots = 4;

    /// <summary>The usage dictionary key of each event, in event order.</summary>
    private static readonly KnownName[] Categories = [KnownName.View, KnownName.Print, KnownName.Export];

    /// <summary>The spellings of each event's state key, in event order.</summary>
    private static readonly byte[][] StateSpellings = ["ViewState"u8.ToArray(), "PrintState"u8.ToArray(), "ExportState"u8.ToArray()];

    /// <summary>The document's /OCProperties dictionary.</summary>
    private readonly PdfDictionary _properties;

    /// <summary>The configuration that applies, if any.</summary>
    private readonly PdfDictionary? _configuration;

    /// <summary>The state key of each event, in event order.</summary>
    private readonly PdfName[] _stateKeys;

    /// <summary>The key of a zoom dictionary's minimum.</summary>
    private readonly PdfName _min;

    /// <summary>The key of a zoom dictionary's maximum.</summary>
    private readonly PdfName _max;

    /// <summary>The /ListMode value that lists only the layers of visible pages.</summary>
    private readonly PdfName _visiblePages;

    /// <summary>Whether groups are shown unless listed in /OFF.</summary>
    private readonly bool _baseOn;

    /// <summary>The object numbers listed in /ON.</summary>
    private readonly HashSet<int> _on;

    /// <summary>The object numbers listed in /OFF.</summary>
    private readonly HashSet<int> _off;

    /// <summary>The object numbers listed in /Locked.</summary>
    private readonly HashSet<int> _locked;

    /// <summary>The /AS array, if any.</summary>
    private readonly PdfArray? _autoStates;

    /// <summary>Initializes a new instance of the <see cref="OptionalContentReader"/> class.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="properties">The /OCProperties dictionary.</param>
    /// <param name="configuration">The configuration that applies.</param>
    private OptionalContentReader(PdfObjectStore objects, PdfDictionary properties, PdfDictionary? configuration)
    {
        _properties = properties;
        _configuration = configuration;
        _stateKeys = [objects.Names.Intern(StateSpellings[0]), objects.Names.Intern(StateSpellings[1]), objects.Names.Intern(StateSpellings[2])];
        _min = objects.Names.Intern("min"u8);
        _max = objects.Names.Intern("max"u8);
        _visiblePages = objects.Names.Intern("VisiblePages"u8);
        _baseOn = configuration is null || !configuration.IsName(KnownName.BaseState, KnownName.OFF);
        _on = ReferencedNumbers(configuration?.GetArray(KnownName.ON));
        _off = ReferencedNumbers(configuration?.GetArray(KnownName.OFF));
        _locked = ReferencedNumbers(configuration?.GetArray(KnownName.Locked));
        _autoStates = configuration?.GetArray(KnownName.AS);
    }

    /// <summary>Reads the layers of a document under its default configuration /D.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <returns>The layers and their state.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static OptionalContentSet Read(PdfObjectStore objects) => Read(objects, PdfLayerConfiguration.DefaultIndex);

    /// <summary>Reads the layers of a document under a chosen configuration.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="configurationIndex">The position in /Configs, <see cref="PdfLayerConfiguration.DefaultIndex"/> for /D.</param>
    /// <returns>The layers and their state.</returns>
    internal static OptionalContentSet Read(PdfObjectStore objects, int configurationIndex)
    {
        var properties = objects.Catalog.GetDictionary(KnownName.OCProperties);
        if (properties is null)
        {
            return OptionalContentSet.Empty;
        }

        var reader = new OptionalContentReader(objects, properties, ConfigurationAt(properties, configurationIndex));
        var groups = reader.CollectGroups();
        var states = new LayerStates();
        var layers = new PdfLayer[groups.Count];
        for (var i = 0; i < layers.Length; i++)
        {
            layers[i] = reader.ReadLayer(StoreReading.GetDictionary(objects, groups[i]), groups[i].Number, i, states);
        }

        return reader.Finish(layers, states, configurationIndex);
    }

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

    /// <summary>Gets a configuration's /Name, or a fallback.</summary>
    /// <param name="configuration">The configuration, if it resolves.</param>
    /// <param name="fallback">The name to use when there is none.</param>
    /// <returns>The name.</returns>
    private static string ConfigurationName(PdfDictionary? configuration, string fallback)
    {
        var name = configuration?.GetText(KnownName.Name);
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
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

    /// <summary>Collects the object numbers an array refers to.</summary>
    /// <param name="array">The array.</param>
    /// <returns>The numbers.</returns>
    private static HashSet<int> ReferencedNumbers(PdfArray? array)
    {
        var numbers = new HashSet<int>();
        for (var i = 0; array is not null && i < array.Count; i++)
        {
            var id = array.GetRaw(i).AsReference();
            if (id.IsValid)
            {
                _ = numbers.Add(id.Number);
            }
        }

        return numbers;
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

    /// <summary>Adds each distinct group an array refers to.</summary>
    /// <param name="array">The array.</param>
    /// <param name="groups">The groups so far.</param>
    /// <param name="seen">The object numbers already added.</param>
    private static void AddGroups(PdfArray? array, List<PdfObjectId> groups, HashSet<int> seen)
    {
        for (var i = 0; array is not null && i < array.Count; i++)
        {
            var id = array.GetRaw(i).AsReference();
            if (id.IsValid && seen.Add(id.Number))
            {
                groups.Add(id);
            }
        }
    }

    /// <summary>Lists each group once, in /OCGs order; without /OCGs, the groups the configuration names.</summary>
    /// <returns>The groups.</returns>
    private List<PdfObjectId> CollectGroups()
    {
        var groups = new List<PdfObjectId>();
        var seen = new HashSet<int>();
        var listed = _properties.GetArray(KnownName.OCGs);
        AddGroups(listed, groups, seen);
        if (listed is null)
        {
            AddGroups(_configuration?.GetArray(KnownName.ON), groups, seen);
            AddGroups(_configuration?.GetArray(KnownName.OFF), groups, seen);
        }

        return groups;
    }

    /// <summary>Reads one layer and records its state.</summary>
    /// <param name="group">The group dictionary, if it resolves.</param>
    /// <param name="id">The group's object number.</param>
    /// <param name="position">The layer's position.</param>
    /// <param name="states">Collects the hidden groups of each event.</param>
    /// <returns>The layer.</returns>
    private PdfLayer ReadLayer(PdfDictionary? group, int id, int position, LayerStates states)
    {
        var visible = IsDefaultOn(PdfOptionalContentEvent.View, group, id);
        var printed = IsDefaultOn(PdfOptionalContentEvent.Print, group, id);
        var exported = IsDefaultOn(PdfOptionalContentEvent.Export, group, id);
        var forView = group is null || HasIntent(group, true);
        var zoom = forView ? ReadZoom(group, id) : ZoomLimit.Unlimited;
        states.Record(id, visible, printed, exported, forView, zoom);
        return new(id, LayerName(group, position), visible, _locked.Contains(id), printed, exported, zoom.Min, zoom.Max);
    }

    /// <summary>Builds the set once every layer is read.</summary>
    /// <param name="layers">The layers.</param>
    /// <param name="states">The collected state.</param>
    /// <param name="active">The configuration in use.</param>
    /// <returns>The set.</returns>
    private OptionalContentSet Finish(PdfLayer[] layers, LayerStates states, int active)
    {
        var limits = OptionalContentZoom.CollectLimits(states.Zoom);
        var ranges = new Dictionary<int, ZoomClassRange>();
        foreach (var (id, limit) in states.Zoom)
        {
            ranges[id] = OptionalContentZoom.ToClasses(limit, limits);
        }

        return new(
            layers,
            states.Hidden,
            states.NonView,
            ReadRadioGroups(),
            states.PrintHidden,
            states.ExportHidden,
            _locked,
            ranges,
            limits,
            ReadTree(layers),
            ReadListMode(),
            ListConfigurations(active),
            active);
    }

    /// <summary>Lists the default configuration and the alternates.</summary>
    /// <param name="active">The configuration in use.</param>
    /// <returns>The configurations.</returns>
    private PdfLayerConfiguration[] ListConfigurations(int active)
    {
        var configs = _properties.GetArray(KnownName.Configs);
        var result = new PdfLayerConfiguration[(configs?.Count ?? 0) + 1];
        var defaults = _properties.GetDictionary(KnownName.D);
        result[0] = new(PdfLayerConfiguration.DefaultIndex, ConfigurationName(defaults, "Default"), defaults is null || HasIntent(defaults, true), active == PdfLayerConfiguration.DefaultIndex);
        for (var i = 1; i < result.Length; i++)
        {
            var candidate = configs!.GetDictionary(i - 1);
            var fallback = string.Create(CultureInfo.CurrentCulture, $"Configuration {i}");
            result[i] = new(i - 1, ConfigurationName(candidate, fallback), candidate is not null && HasIntent(candidate, true), active == i - 1);
        }

        return result;
    }

    /// <summary>Reads the layer order: the configuration's /Order, else the default configuration's, else every layer in turn.</summary>
    /// <param name="layers">The layers.</param>
    /// <returns>The top-level entries.</returns>
    private PdfLayerNode[] ReadTree(PdfLayer[] layers)
    {
        var order = _configuration?.GetArray(KnownName.Order) ?? _properties.GetDictionary(KnownName.D)?.GetArray(KnownName.Order);
        if (order is null)
        {
            return OptionalContentOrder.Flat(layers);
        }

        var names = new Dictionary<int, string>();
        foreach (var layer in layers)
        {
            names[layer.Id] = layer.Name;
        }

        return OptionalContentOrder.Build(order, names);
    }

    /// <summary>Reads /ListMode.</summary>
    /// <returns>The mode; all pages when missing or unknown.</returns>
    private PdfLayerListMode ReadListMode() =>
        _configuration?.GetName(KnownName.ListMode) == _visiblePages ? PdfLayerListMode.VisiblePages : PdfLayerListMode.AllPages;

    /// <summary>Reads the /RBGroups: sets of groups of which only one is shown, from the configuration or else the default configuration.</summary>
    /// <returns>The object numbers of each set.</returns>
    private int[][] ReadRadioGroups()
    {
        var sets = _configuration?.GetArray(KnownName.RBGroups) ?? _properties.GetDictionary(KnownName.D)?.GetArray(KnownName.RBGroups);
        var result = new List<int[]>();
        for (var i = 0; sets is not null && i < sets.Count; i++)
        {
            if (sets.GetArray(i) is { } set)
            {
                result.Add([.. ReferencedNumbers(set)]);
            }
        }

        return [.. result];
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
        var key = _stateKeys[(int)ev];
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
        var shown = (_baseOn || _on.Contains(id)) && !_off.Contains(id);
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

    /// <summary>Collects the object numbers of the groups that are off for each event, while the layers are read.</summary>
    private sealed class LayerStates
    {
        /// <summary>Gets the groups hidden when viewing.</summary>
        internal HashSet<int> Hidden { get; } = [];

        /// <summary>Gets the groups hidden when printing.</summary>
        internal HashSet<int> PrintHidden { get; } = [];

        /// <summary>Gets the groups hidden when exporting.</summary>
        internal HashSet<int> ExportHidden { get; } = [];

        /// <summary>Gets the groups that are not for viewing.</summary>
        internal HashSet<int> NonView { get; } = [];

        /// <summary>Gets the zoom range of each zoom-managed group.</summary>
        internal Dictionary<int, ZoomLimit> Zoom { get; } = [];

        /// <summary>Records a layer's state.</summary>
        /// <param name="id">The group's object number.</param>
        /// <param name="visible">Whether it is shown when viewing.</param>
        /// <param name="printed">Whether it prints.</param>
        /// <param name="exported">Whether it is kept when exporting.</param>
        /// <param name="forView">Whether its intent includes View.</param>
        /// <param name="zoom">Its zoom range.</param>
        internal void Record(int id, bool visible, bool printed, bool exported, bool forView, ZoomLimit zoom)
        {
            AddUnless(NonView, id, forView);
            AddUnless(Hidden, id, visible);
            AddUnless(PrintHidden, id, printed);
            AddUnless(ExportHidden, id, exported);
            if (zoom.IsLimited)
            {
                Zoom[id] = zoom;
            }
        }

        /// <summary>Adds a group to a set unless a condition holds.</summary>
        /// <param name="set">The set.</param>
        /// <param name="id">The group's object number.</param>
        /// <param name="condition">Whether the group is left out.</param>
        private static void AddUnless(HashSet<int> set, int id, bool condition)
        {
            if (!condition)
            {
                _ = set.Add(id);
            }
        }
    }
}
