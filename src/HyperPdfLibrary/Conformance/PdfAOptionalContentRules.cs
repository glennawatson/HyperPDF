// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// PDF/A rules on optional content configuration dictionaries: the default <c>/D</c> and each entry of <c>/Configs</c>
/// (ISO 19005-2/3 6.9, ISO 19005-4 6.10).
/// </summary>
internal static class PdfAOptionalContentRules
{
    /// <summary>The report context of an optional content configuration.</summary>
    private const string ConfigContext = "optional content configuration";

    /// <summary>Gets the rules this area checks.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.OptionalContentAutoState,
        PdfARule.OptionalContentName,
        PdfARule.OptionalContentNamePdfA4,
        PdfARule.OptionalContentOrder,
        PdfARule.OptionalContentDuplicateName,
    ];

    /// <summary>Records the failures of this area's rules.</summary>
    /// <param name="context">The validation.</param>
    internal static void Check(PdfAValidationContext context)
    {
        if (context.Document.Catalog.GetDictionary(KnownName.OCProperties) is not { } properties)
        {
            return;
        }

        var configs = ReadConfigs(properties);
        if (configs.Count == 0)
        {
            return;
        }

        var groups = ReadGroups(properties);
        var names = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var config in configs)
        {
            if (config.GetText(KnownName.Name) is { } name)
            {
                names[name] = names.GetValueOrDefault(name) + 1;
            }
        }

        var autoState = context.Document.Objects.Names.Intern("AS"u8);
        foreach (var config in configs)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var name = config.GetText(KnownName.Name);
            context.Require(!config.ContainsKey(autoState), PdfARule.OptionalContentAutoState, ConfigContext);
            context.Require(name is { Length: > 0 }, PdfARule.OptionalContentName, ConfigContext);
            context.Require(name is not null, PdfARule.OptionalContentNamePdfA4, ConfigContext);
            context.Require(name is null || names[name] == 1, PdfARule.OptionalContentDuplicateName, ConfigContext);
            context.Require(OrderListsAll(config, groups), PdfARule.OptionalContentOrder, ConfigContext);
        }
    }

    /// <summary>Lists the configuration dictionaries: <c>/D</c>, then each dictionary in <c>/Configs</c>.</summary>
    /// <param name="properties">The <c>/OCProperties</c> dictionary.</param>
    /// <returns>The configurations.</returns>
    private static List<PdfDictionary> ReadConfigs(PdfDictionary properties)
    {
        var configs = new List<PdfDictionary>();
        if (properties.Get(KnownName.D) is { Kind: PdfKind.Dictionary } defaults)
        {
            configs.Add(defaults.AsDictionary()!);
        }

        for (var i = 0; properties.GetArray(KnownName.Configs) is { } list && i < list.Count; i++)
        {
            if (list.Get(i) is { Kind: PdfKind.Dictionary } config)
            {
                configs.Add(config.AsDictionary()!);
            }
        }

        return configs;
    }

    /// <summary>Reads the optional content groups the document declares in <c>/OCGs</c>.</summary>
    /// <param name="properties">The <c>/OCProperties</c> dictionary.</param>
    /// <returns>The group dictionaries.</returns>
    private static HashSet<PdfDictionary> ReadGroups(PdfDictionary properties)
    {
        var groups = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        for (var i = 0; properties.GetArray(KnownName.OCGs) is { } list && i < list.Count; i++)
        {
            if (list.GetDictionary(i) is { } group)
            {
                _ = groups.Add(group);
            }
        }

        return groups;
    }

    /// <summary>Determines whether a configuration's <c>/Order</c>, when present, lists every declared group.</summary>
    /// <param name="config">The configuration.</param>
    /// <param name="groups">The declared groups.</param>
    /// <returns><see langword="true"/> when there is no order or it lists them all.</returns>
    private static bool OrderListsAll(PdfDictionary config, HashSet<PdfDictionary> groups)
    {
        if (config.GetArray(KnownName.Order) is not { } order)
        {
            return true;
        }

        var listed = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        var seen = new HashSet<PdfArray>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<PdfArray>();
        pending.Push(order);
        while (pending.TryPop(out var array))
        {
            if (!seen.Add(array))
            {
                continue;
            }

            for (var i = 0; i < array.Count; i++)
            {
                var item = array.Get(i);
                if (item.AsArray() is { } nested)
                {
                    pending.Push(nested);
                }
                else if (item.AsDictionary() is { } group)
                {
                    _ = listed.Add(group);
                }
            }
        }

        return listed.IsSupersetOf(groups);
    }
}
