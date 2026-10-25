// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets.Shaping;

/// <summary>Builds a GSUB or GPOS table from lookups, features and scripts (OpenType, "OpenType Layout common table formats").</summary>
public sealed class LayoutTableBuilder
{
    /// <summary>The lookups, each as its type, flags, mark filtering set and subtables.</summary>
    private readonly List<LookupSpec> _lookups = [];

    /// <summary>The features, each as its tag and lookups.</summary>
    private readonly List<FeatureSpec> _features = [];

    /// <summary>The scripts.</summary>
    private readonly List<ScriptSpec> _scripts = [];

    /// <summary>Gets a value indicating whether no lookup has been added.</summary>
    public bool IsEmpty => _lookups.Count == 0;

    /// <summary>Adds a lookup.</summary>
    /// <param name="type">The lookup type.</param>
    /// <param name="flags">The lookup flags.</param>
    /// <param name="subtables">The subtables.</param>
    /// <returns>The lookup's index.</returns>
    public int AddLookup(int type, int flags, params byte[][] subtables) => AddLookupWithMarkSet(type, flags, -1, subtables);

    /// <summary>Adds a lookup that uses a mark filtering set.</summary>
    /// <param name="type">The lookup type.</param>
    /// <param name="flags">The lookup flags, without the mark filtering set bit.</param>
    /// <param name="markSet">The mark filtering set, or -1 for none.</param>
    /// <param name="subtables">The subtables.</param>
    /// <returns>The lookup's index.</returns>
    public int AddLookupWithMarkSet(int type, int flags, int markSet, byte[][] subtables)
    {
        _lookups.Add(new(type, markSet >= 0 ? flags | 0x10 : flags, markSet, subtables));
        return _lookups.Count - 1;
    }

    /// <summary>Adds a feature.</summary>
    /// <param name="tag">The feature tag.</param>
    /// <param name="lookups">The lookups it uses.</param>
    /// <returns>The feature's index.</returns>
    public int AddFeature(string tag, params int[] lookups)
    {
        _features.Add(new(tag, lookups));
        return _features.Count - 1;
    }

    /// <summary>Adds a script whose default language system uses some features.</summary>
    /// <param name="script">The script tag.</param>
    /// <param name="features">The indexes of the features.</param>
    public void AddScript(string script, params int[] features) => _scripts.Add(new(script, features, []));

    /// <summary>Adds a language system to a script that was added with <see cref="AddScript"/>.</summary>
    /// <param name="script">The script tag.</param>
    /// <param name="language">The language tag.</param>
    /// <param name="features">The indexes of the features.</param>
    public void AddLanguage(string script, string language, params int[] features)
    {
        var index = _scripts.FindIndex(s => string.Equals(s.Code, script, StringComparison.Ordinal));
        _scripts[index] = _scripts[index] with { Languages = [.. _scripts[index].Languages, new LanguageSpec(language, features)] };
    }

    /// <summary>Writes the table.</summary>
    /// <returns>The table.</returns>
    public byte[] Build()
    {
        var buffer = new ByteBuffer();
        buffer.Write(1, 0);
        var scriptList = buffer.Reserve();
        var featureList = buffer.Reserve();
        var lookupList = buffer.Reserve();
        buffer.AttachChild(scriptList, WriteScripts());
        buffer.AttachChild(featureList, WriteFeatures());
        buffer.AttachChild(lookupList, WriteLookups());
        return buffer.ToArray();
    }

    /// <summary>Makes a four-byte tag.</summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The tag as a number.</returns>
    private static uint Tag(string tag) => SfntTables.Tag(tag.PadRight(0x4));

    /// <summary>Writes a Script table.</summary>
    /// <param name="script">The script.</param>
    /// <returns>The table.</returns>
    private static byte[] WriteScript(ScriptSpec script)
    {
        var buffer = new ByteBuffer();
        var defaultSlot = buffer.Reserve();
        buffer.Write(script.Languages.Length);
        var slots = new List<int>();
        foreach (var language in script.Languages)
        {
            buffer.Write32(Tag(language.Code));
            slots.Add(buffer.Reserve());
        }

        buffer.AttachChild(defaultSlot, LangSys(script.Features));
        for (var i = 0; i < script.Languages.Length; i++)
        {
            buffer.AttachChild(slots[i], LangSys(script.Languages[i].Features));
        }

        return buffer.ToArray();
    }

    /// <summary>Writes a LangSys table.</summary>
    /// <param name="features">The indexes of the features.</param>
    /// <returns>The table.</returns>
    private static byte[] LangSys(int[] features) => ByteBuffer.Words([0, 0xFFFF, features.Length, .. features]);

    /// <summary>Writes a Lookup table.</summary>
    /// <param name="lookup">The lookup.</param>
    /// <returns>The table.</returns>
    private static byte[] WriteLookup(LookupSpec lookup)
    {
        var buffer = new ByteBuffer();
        buffer.Write(lookup.Type, lookup.Flags, lookup.Subtables.Length);
        var slots = lookup.Subtables.Select(_ => buffer.Reserve()).ToArray();
        if (lookup.MarkSet >= 0)
        {
            buffer.Write(lookup.MarkSet);
        }

        for (var i = 0; i < slots.Length; i++)
        {
            buffer.AttachChild(slots[i], lookup.Subtables[i]);
        }

        return buffer.ToArray();
    }

    /// <summary>Writes a Script List table.</summary>
    /// <returns>The table.</returns>
    private byte[] WriteScripts()
    {
        var ordered = _scripts.OrderBy(static s => s.Code, StringComparer.Ordinal).ToArray();
        var buffer = new ByteBuffer();
        buffer.Write(ordered.Length);
        var slots = new List<int>();
        foreach (var script in ordered)
        {
            buffer.Write32(Tag(script.Code));
            slots.Add(buffer.Reserve());
        }

        for (var i = 0; i < ordered.Length; i++)
        {
            buffer.AttachChild(slots[i], WriteScript(ordered[i]));
        }

        return buffer.ToArray();
    }

    /// <summary>Writes a Feature List table.</summary>
    /// <returns>The table.</returns>
    private byte[] WriteFeatures()
    {
        var buffer = new ByteBuffer();
        buffer.Write(_features.Count);
        var slots = new List<int>();
        foreach (var feature in _features)
        {
            buffer.Write32(Tag(feature.Code));
            slots.Add(buffer.Reserve());
        }

        for (var i = 0; i < _features.Count; i++)
        {
            buffer.AttachChild(slots[i], ByteBuffer.Words([0, _features[i].Lookups.Length, .. _features[i].Lookups]));
        }

        return buffer.ToArray();
    }

    /// <summary>Writes a Lookup List table.</summary>
    /// <returns>The table.</returns>
    private byte[] WriteLookups()
    {
        var buffer = new ByteBuffer();
        buffer.Write(_lookups.Count);
        var slots = _lookups.Select(_ => buffer.Reserve()).ToArray();
        for (var i = 0; i < _lookups.Count; i++)
        {
            buffer.AttachChild(slots[i], WriteLookup(_lookups[i]));
        }

        return buffer.ToArray();
    }

    /// <summary>A lookup.</summary>
    /// <param name="Type">The lookup type.</param>
    /// <param name="Flags">The lookup flags.</param>
    /// <param name="MarkSet">The mark filtering set, or -1.</param>
    /// <param name="Subtables">The subtables.</param>
    private readonly record struct LookupSpec(int Type, int Flags, int MarkSet, byte[][] Subtables);

    /// <summary>A feature.</summary>
    /// <param name="Code">The feature tag.</param>
    /// <param name="Lookups">The lookups.</param>
    private readonly record struct FeatureSpec(string Code, int[] Lookups);

    /// <summary>A language system.</summary>
    /// <param name="Code">The language tag.</param>
    /// <param name="Features">The feature indexes.</param>
    private readonly record struct LanguageSpec(string Code, int[] Features);

    /// <summary>A script.</summary>
    /// <param name="Code">The script tag.</param>
    /// <param name="Features">The feature indexes of the default language system.</param>
    /// <param name="Languages">The other language systems.</param>
    private readonly record struct ScriptSpec(string Code, int[] Features, LanguageSpec[] Languages);
}
