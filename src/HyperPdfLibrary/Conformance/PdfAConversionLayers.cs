// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Makes optional content (layers) meet PDF/A. PDF/A-1 has no layers, so they are removed when every layer is shown by
/// default, which leaves the pages looking the same (ISO 19005-1 §6.1.13). Later parts keep them, with a unique name on
/// every configuration, no automatic state changes, and every layer listed in each order array (ISO 19005-2 and -3
/// §6.9; ISO 19005-4:2020 §6.8).
/// </summary>
internal static class PdfAConversionLayers
{
    /// <summary>The name given to the default configuration when it has none.</summary>
    private const string DefaultName = "Default";

    /// <summary>Applies the fixes.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="catalog">The catalog copy.</param>
    /// <returns><see langword="true"/> when the catalog changed.</returns>
    internal static bool Apply(PdfAConversionSession session, PdfDictionary catalog)
    {
        if (catalog.GetDictionary(KnownName.OCProperties) is not { } original)
        {
            return false;
        }

        if (session.Part == PdfAClaim.Part1)
        {
            return RemoveForPdfA1(session, catalog, original);
        }

        var properties = original.Clone();
        var store = session.Store;
        var groups = properties.GetArray(KnownName.OCGs);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var changed = false;
        if (properties.GetDictionary(KnownName.D) is { } defaults)
        {
            var fixedDefaults = FixConfiguration(store, defaults, groups, used, DefaultName, ref changed);
            properties.Set(KnownName.D, PdfValue.FromDictionary(fixedDefaults));
        }

        if (properties.GetArray(KnownName.Configs) is { } configs)
        {
            var fixedConfigs = new PdfArray(store, configs.Count);
            for (var i = 0; i < configs.Count; i++)
            {
                var name = string.Create(CultureInfo.InvariantCulture, $"Configuration {i + 1}");
                fixedConfigs.Add(configs.GetDictionary(i) is { } config ? PdfValue.FromDictionary(FixConfiguration(store, config, groups, used, name, ref changed)) : configs.GetRaw(i));
            }

            properties.Set(KnownName.Configs, PdfValue.FromArray(fixedConfigs));
        }

        if (changed)
        {
            catalog.Set(KnownName.OCProperties, PdfValue.FromDictionary(properties));
            session.Changed(
                "Gave each layer configuration a unique name and listed every layer in its order.",
                PdfARule.OptionalContentName,
                PdfARule.OptionalContentOrder,
                PdfARule.OptionalContentAutoState);
        }

        return changed;
    }

    /// <summary>Removes optional content for PDF/A-1, or refuses when a layer starts hidden.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="catalog">The catalog copy.</param>
    /// <param name="properties">The optional content properties.</param>
    /// <returns><see langword="true"/> when the catalog changed.</returns>
    private static bool RemoveForPdfA1(PdfAConversionSession session, PdfDictionary catalog, PdfDictionary properties)
    {
        var defaults = properties.GetDictionary(KnownName.D);
        if (defaults is not null && (defaults.GetArray(KnownName.OFF) is { Count: > 0 } || defaults.IsName(KnownName.BaseState, KnownName.OFF)))
        {
            session.Refuse("The document has layers that start hidden, and PDF/A-1 has no layers. Choose PDF/A-2 or later.", PdfARule.OptionalContentForbidden);
            return false;
        }

        _ = catalog.Remove(KnownName.OCProperties);
        session.Changed("Removed the layer list, as PDF/A-1 has no layers; every layer was shown, so the pages look the same.", PdfARule.OptionalContentForbidden);
        return true;
    }

    /// <summary>Fixes one configuration: a unique /Name, no /AS, and every layer in /Order.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="groups">The document's layers, or <see langword="null"/>.</param>
    /// <param name="used">The names already given.</param>
    /// <param name="fallback">The name to give when it has none or repeats one.</param>
    /// <param name="changed">Set when the configuration changed.</param>
    /// <returns>The configuration, or a fixed copy.</returns>
    private static PdfDictionary FixConfiguration(PdfObjectStore store, PdfDictionary config, PdfArray? groups, HashSet<string> used, string fallback, ref bool changed)
    {
        var copy = config.Clone();
        var name = copy.GetText(KnownName.Name);
        if (name is null || !used.Add(name))
        {
            var candidate = fallback;
            for (var n = 2; !used.Add(candidate); n++)
            {
                candidate = string.Create(CultureInfo.InvariantCulture, $"{fallback} {n}");
            }

            copy.Set(KnownName.Name, PdfValue.FromString(PdfText.Encode(candidate)));
            changed = true;
        }

        if (copy.Remove(store.Names.Intern("AS"u8)))
        {
            changed = true;
        }

        if (groups is not null && copy.GetArray(KnownName.Order) is { } order && CompleteOrder(store, order, groups) is { } completed)
        {
            copy.Set(KnownName.Order, PdfValue.FromArray(completed));
            changed = true;
        }

        return copy;
    }

    /// <summary>Appends the layers an order array leaves out.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="order">The order array.</param>
    /// <param name="groups">The document's layers.</param>
    /// <returns>The completed copy, or <see langword="null"/> when nothing was missing.</returns>
    private static PdfArray? CompleteOrder(PdfObjectStore store, PdfArray order, PdfArray groups)
    {
        var listed = new HashSet<int>();
        Collect(store, order, listed, 0);
        PdfArray? copy = null;
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups.GetRaw(i);
            if (!group.IsReference || listed.Contains(group.AsReference().Number))
            {
                continue;
            }

            copy ??= order.Clone();
            copy.Add(group);
        }

        return copy;
    }

    /// <summary>Collects the layer references in an order array and its nested arrays.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="array">The array.</param>
    /// <param name="listed">The object numbers found.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void Collect(PdfObjectStore store, PdfArray array, HashSet<int> listed, int depth)
    {
        if (depth > PdfLimits.MaxNesting)
        {
            return;
        }

        for (var i = 0; i < array.Count; i++)
        {
            var item = array.GetRaw(i);
            if (item.IsReference)
            {
                _ = listed.Add(item.AsReference().Number);
            }

            if (StoreReading.Resolve(store, item).AsArray() is { } nested)
            {
                Collect(store, nested, listed, depth + 1);
            }
        }
    }
}
