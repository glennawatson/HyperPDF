// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Layers;

/// <summary>
/// Reads the layers and their default visibility, following PDFium: the first /Configs entry with a View intent (else
/// /D) supplies /BaseState, /ON, /OFF, /AS and /RBGroups; a group's own /Usage /View /ViewState wins over the
/// configuration; groups whose /Intent is not View or All are always shown.
/// </summary>
internal sealed class OptionalContentReader
{
    /// <summary>The configuration that applies, if any.</summary>
    private readonly PdfDictionary? _configuration;

    /// <summary>The key of a usage dictionary's view state.</summary>
    private readonly PdfName _viewState;

    /// <summary>The key of a usage dictionary's print state.</summary>
    private readonly PdfName _printState;

    /// <summary>Whether groups are shown unless listed in /OFF.</summary>
    private readonly bool _baseOn;

    /// <summary>The object numbers listed in /ON.</summary>
    private readonly HashSet<int> _on;

    /// <summary>The object numbers listed in /OFF.</summary>
    private readonly HashSet<int> _off;

    /// <summary>The /AS array, if any.</summary>
    private readonly PdfArray? _autoStates;

    /// <summary>Initializes a new instance of the <see cref="OptionalContentReader"/> class.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="configuration">The configuration that applies.</param>
    private OptionalContentReader(PdfObjectStore objects, PdfDictionary? configuration)
    {
        _configuration = configuration;
        _viewState = objects.Names.Intern(ViewStateSpelling);
        _printState = objects.Names.Intern(PrintStateSpelling);
        _baseOn = configuration is null || !configuration.IsName(KnownName.BaseState, KnownName.OFF);
        _on = ReferencedNumbers(configuration?.GetArray(KnownName.ON));
        _off = ReferencedNumbers(configuration?.GetArray(KnownName.OFF));
        _autoStates = configuration?.GetArray(KnownName.AS);
    }

    /// <summary>Gets the spelling of the /ViewState name.</summary>
    private static ReadOnlySpan<byte> ViewStateSpelling => "ViewState"u8;

    /// <summary>Gets the spelling of the /PrintState name.</summary>
    private static ReadOnlySpan<byte> PrintStateSpelling => "PrintState"u8;

    /// <summary>Reads the layers of a document.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <returns>The layers and their state.</returns>
    internal static OptionalContentSet Read(PdfObjectStore objects)
    {
        var properties = objects.Catalog.GetDictionary(KnownName.OCProperties);
        if (properties is null)
        {
            return new([], [], [], [], []);
        }

        var reader = new OptionalContentReader(objects, PickConfiguration(properties));
        var groups = reader.CollectGroups(properties);
        var layers = new PdfLayer[groups.Count];
        var hidden = new HashSet<int>();
        var nonView = new HashSet<int>();
        var printHidden = new HashSet<int>();
        for (var i = 0; i < layers.Length; i++)
        {
            var dictionary = StoreReading.GetDictionary(objects, groups[i]);
            var visible = reader.IsDefaultVisible(dictionary, groups[i].Number);
            if (dictionary is not null && !HasIntent(dictionary, true))
            {
                _ = nonView.Add(groups[i].Number);
            }

            if (!visible)
            {
                _ = hidden.Add(groups[i].Number);
            }

            if (!reader.IsPrintVisible(dictionary, groups[i].Number))
            {
                _ = printHidden.Add(groups[i].Number);
            }

            layers[i] = new(groups[i].Number, LayerName(dictionary, i), visible);
        }

        return new(layers, hidden, nonView, reader.ReadRadioGroups(), printHidden);
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

    /// <summary>Picks the configuration PDFium uses: the first /Configs entry with an explicit View intent, else /D.</summary>
    /// <param name="properties">The /OCProperties dictionary.</param>
    /// <returns>The configuration, or <see langword="null"/>.</returns>
    internal static PdfDictionary? PickConfiguration(PdfDictionary properties)
    {
        var configs = properties.GetArray(KnownName.Configs);
        for (var i = 0; configs is not null && i < configs.Count; i++)
        {
            if (configs.GetDictionary(i) is { } candidate && HasIntent(candidate, false))
            {
                return candidate;
            }
        }

        return properties.GetDictionary(KnownName.D);
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

    /// <summary>Determines whether an /AS entry is for the View event and lists a group.</summary>
    /// <param name="entry">The /AS entry.</param>
    /// <param name="id">The group's object number.</param>
    /// <returns><see langword="true"/> when it applies.</returns>
    private static bool AppliesTo(PdfDictionary? entry, int id) => entry is not null && (!entry.ContainsKey(KnownName.Event) || entry.IsName(KnownName.Event, KnownName.View)) && Lists(entry, id);

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
    /// <param name="properties">The /OCProperties dictionary.</param>
    /// <returns>The groups.</returns>
    private List<PdfObjectId> CollectGroups(PdfDictionary properties)
    {
        var groups = new List<PdfObjectId>();
        var seen = new HashSet<int>();
        var listed = properties.GetArray(KnownName.OCGs);
        AddGroups(listed, groups, seen);
        if (listed is null)
        {
            AddGroups(_configuration?.GetArray(KnownName.ON), groups, seen);
            AddGroups(_configuration?.GetArray(KnownName.OFF), groups, seen);
        }

        return groups;
    }

    /// <summary>Reads the /RBGroups: sets of groups of which only one is shown.</summary>
    /// <returns>The object numbers of each set.</returns>
    private int[][] ReadRadioGroups()
    {
        var sets = _configuration?.GetArray(KnownName.RBGroups);
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

    /// <summary>Works out whether a group is shown before the reader touches anything.</summary>
    /// <param name="group">The group dictionary, if it resolves.</param>
    /// <param name="id">The group's object number.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    private bool IsDefaultVisible(PdfDictionary? group, int id)
    {
        if (group is null || !HasIntent(group, true))
        {
            return true;
        }

        var view = group.GetDictionary(KnownName.Usage)?.GetDictionary(KnownName.View);
        return view is not null && view.ContainsKey(_viewState) ? !view.IsName(_viewState, KnownName.OFF) : _configuration is null || ConfigurationState(group, id);
    }

    /// <summary>
    /// Works out whether a group prints, as PDFium's print usage does: the group's /Usage /Print /PrintState, else its
    /// /Usage /View /ViewState, else the configuration's base state, /ON and /OFF, with /AS entries for the Print event
    /// applying the group's print state. The viewer's layer switches do not affect printing.
    /// </summary>
    /// <param name="group">The group dictionary, if it resolves.</param>
    /// <param name="id">The group's object number.</param>
    /// <returns><see langword="true"/> when printed.</returns>
    private bool IsPrintVisible(PdfDictionary? group, int id)
    {
        if (group is null || !HasIntent(group, true))
        {
            return true;
        }

        var usage = group.GetDictionary(KnownName.Usage);
        if (usage?.GetDictionary(KnownName.Print) is { } print && print.ContainsKey(_printState))
        {
            return !print.IsName(_printState, KnownName.OFF);
        }

        return usage?.GetDictionary(KnownName.View) is { } view
        && view.ContainsKey(_viewState) ? !view.IsName(
        _viewState,
        KnownName.OFF) : _configuration is null
        || PrintConfigurationState(
        group,
        id);
    }

    /// <summary>Applies the configuration's base state, /ON, /OFF and the Print event's /AS entries to a group.</summary>
    /// <param name="group">The group dictionary.</param>
    /// <param name="id">The group's object number.</param>
    /// <returns><see langword="true"/> when printed.</returns>
    private bool PrintConfigurationState(PdfDictionary group, int id)
    {
        var shown = (_baseOn || _on.Contains(id)) && !_off.Contains(id);
        var state = group.GetDictionary(KnownName.Usage)?.GetDictionary(KnownName.Print);
        for (var i = 0; state is not null && _autoStates is not null && i < _autoStates.Count; i++)
        {
            if (_autoStates.GetDictionary(i) is { } entry && entry.IsName(KnownName.Event, KnownName.Print) && Lists(entry, id))
            {
                shown = !state.IsName(_printState, KnownName.OFF);
            }
        }

        return shown;
    }

    /// <summary>Applies the configuration's base state, /ON, /OFF and /AS to a group.</summary>
    /// <param name="group">The group dictionary.</param>
    /// <param name="id">The group's object number.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    private bool ConfigurationState(PdfDictionary group, int id)
    {
        var shown = _baseOn;
        shown |= _on.Contains(id);
        shown &= !_off.Contains(id);
        var state = group.GetDictionary(KnownName.Usage)?.GetDictionary(KnownName.View);
        for (var i = 0; state is not null && _autoStates is not null && i < _autoStates.Count; i++)
        {
            if (AppliesTo(_autoStates.GetDictionary(i), id))
            {
                shown = !state.IsName(_viewState, KnownName.OFF);
            }
        }

        return shown;
    }
}
