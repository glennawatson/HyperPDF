// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// Turns the features a script needs into the lookups of one GSUB or GPOS table: it picks the script and language system
/// (OpenType, "Script and language system selection"), finds each feature's lookups and sorts them into lookup list order,
/// which is the order lookups apply in (OpenType, "Lookup Processing").
/// </summary>
internal static class PlanCompiler
{
    /// <summary>Compiles the stages of a plan for one table.</summary>
    /// <param name="table">The GSUB or GPOS table.</param>
    /// <param name="scripts">The script tags to try, best first; the default script follows them.</param>
    /// <param name="language">The language tag, or zero for the script's default.</param>
    /// <param name="stages">The features of each stage.</param>
    /// <returns>The lookups of each stage in lookup list order; the stages are empty when the table has no matching script.</returns>
    internal static LookupEntry[][] Compile(OtLayoutTable table, ReadOnlySpan<uint> scripts, uint language, FeatureRequest[][] stages)
    {
        var compiled = new LookupEntry[stages.Length][];
        var langSys = table.IsPresent ? FindLangSys(table, scripts, language) : -1;
        var features = new List<int>();
        var lookups = new List<int>();
        for (var stage = 0; stage < stages.Length; stage++)
        {
            var merged = new SortedDictionary<int, LookupEntry>();
            if (langSys >= 0)
            {
                AddRequired(table, langSys, stage == 0, merged, lookups);
                foreach (var request in stages[stage])
                {
                    features.Clear();
                    table.FindFeatures(langSys, request.Tag, features);
                    AddFeatures(table, features, request, merged, lookups);
                }
            }

            compiled[stage] = [.. merged.Values];
        }

        return compiled;
    }

    /// <summary>Finds the language system for the first script the table has.</summary>
    /// <param name="table">The table.</param>
    /// <param name="scripts">The script tags to try, best first.</param>
    /// <param name="language">The language tag, or zero.</param>
    /// <returns>The offset of the LangSys table, or -1.</returns>
    private static int FindLangSys(OtLayoutTable table, ReadOnlySpan<uint> scripts, uint language)
    {
        foreach (var script in scripts)
        {
            if (table.TryFindScript(script, out var offset) && table.FindLangSys(offset, language) is var found and >= 0)
            {
                return found;
            }
        }

        // A font without the script is used with its default script, then its Latin script (OpenType, "Script tags").
        var hasFallback = table.TryFindScript(OtTags.DefaultScript, out var fallback) || table.TryFindScript(OtTags.Latin, out fallback);
        return hasFallback ? table.FindLangSys(fallback, 0) : -1;
    }

    /// <summary>Adds the lookups of the feature a language system requires.</summary>
    /// <param name="table">The table.</param>
    /// <param name="langSys">The language system.</param>
    /// <param name="first">Whether this is the first stage, which carries the required feature.</param>
    /// <param name="merged">Receives the lookups.</param>
    /// <param name="lookups">A scratch list.</param>
    private static void AddRequired(OtLayoutTable table, int langSys, bool first, SortedDictionary<int, LookupEntry> merged, List<int> lookups)
    {
        var feature = first ? table.RequiredFeature(langSys) : -1;
        if (feature < 0)
        {
            return;
        }

        lookups.Clear();
        table.FeatureLookups(feature, lookups);
        foreach (var lookup in lookups)
        {
            Merge(merged, new(lookup, ShapeMasks.Global, 1));
        }
    }

    /// <summary>Adds the lookups of some features.</summary>
    /// <param name="table">The table.</param>
    /// <param name="features">The offsets of the Feature tables.</param>
    /// <param name="request">The request that asked for them.</param>
    /// <param name="merged">Receives the lookups.</param>
    /// <param name="lookups">A scratch list.</param>
    private static void AddFeatures(OtLayoutTable table, List<int> features, in FeatureRequest request, SortedDictionary<int, LookupEntry> merged, List<int> lookups)
    {
        foreach (var feature in features)
        {
            lookups.Clear();
            table.FeatureLookups(feature, lookups);
            foreach (var lookup in lookups)
            {
                Merge(merged, new(lookup, request.Mask, request.Value));
            }
        }
    }

    /// <summary>Adds a lookup, joining the masks when features share it.</summary>
    /// <param name="merged">The lookups so far.</param>
    /// <param name="entry">The lookup to add.</param>
    private static void Merge(SortedDictionary<int, LookupEntry> merged, LookupEntry entry)
    {
        if (merged.TryGetValue(entry.Lookup, out var existing))
        {
            merged[entry.Lookup] = existing with { Mask = existing.Mask | entry.Mask, Value = Math.Max(existing.Value, entry.Value) };
            return;
        }

        merged[entry.Lookup] = entry;
    }
}
