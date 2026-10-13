// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>Adds optional content groups (ISO 32000-2 8.11) to an open document, so a stamp can be switched on and off.</summary>
internal static class PdfOptionalContentWriter
{
    /// <summary>The name given to a default configuration this writer creates.</summary>
    private const string DefaultConfigurationName = "Default";

    /// <summary>Finds the group with a name, or adds one that is visible by default.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction the edit belongs to.</param>
    /// <param name="name">The group's name, shown to the user.</param>
    /// <returns>The group's object.</returns>
    internal static PdfObjectId EnsureGroup(PdfDocument document, PdfEditTransaction transaction, string name)
    {
        var store = document.Objects;
        var catalog = store.Catalog;
        var properties = catalog.GetDictionary(KnownName.OCProperties);
        var found = Find(properties, name);
        if (found.IsValid)
        {
            return found;
        }

        var group = new PdfDictionary(store);
        group.Set(KnownName.Type, PdfValue.FromName(KnownName.OCG));
        group.Set(KnownName.Name, PdfValue.FromString(PdfText.Encode(name, store.Version)));
        var id = transaction.Add(PdfValue.FromDictionary(group));
        var reference = PdfValue.FromReference(id);
        var changed = properties?.Clone() ?? new PdfDictionary(store);
        var groups = changed.GetArray(KnownName.OCGs)?.Clone() ?? new PdfArray(store);
        groups.Add(reference);
        changed.Set(KnownName.OCGs, PdfValue.FromArray(groups));
        changed.Set(KnownName.D, PdfValue.FromDictionary(Configure(store, changed.GetDictionary(KnownName.D), reference)));
        var edited = catalog.Clone();
        edited.Set(KnownName.OCProperties, PdfValue.FromDictionary(changed));
        PdfPageTreeWriter.ReplaceCatalog(transaction, store, edited);
        return id;
    }

    /// <summary>Finds a group by name among the groups the document lists.</summary>
    /// <param name="properties">The optional content properties, or <see langword="null"/>.</param>
    /// <param name="name">The name.</param>
    /// <returns>The group's object, or an invalid id.</returns>
    private static PdfObjectId Find(PdfDictionary? properties, string name)
    {
        var groups = properties?.GetArray(KnownName.OCGs);
        for (var i = 0; groups is not null && i < groups.Count; i++)
        {
            if (groups.GetDictionary(i) is { } group && string.Equals(group.GetText(KnownName.Name), name, StringComparison.Ordinal))
            {
                return groups.GetRaw(i).AsReference();
            }
        }

        return default;
    }

    /// <summary>Lists a new group in the default configuration, creating the configuration when there is none.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="configuration">The default configuration, or <see langword="null"/>.</param>
    /// <param name="group">A reference to the new group.</param>
    /// <returns>The configuration to store.</returns>
    private static PdfDictionary Configure(PdfObjectStore store, PdfDictionary? configuration, PdfValue group)
    {
        if (configuration is null)
        {
            var created = new PdfDictionary(store);
            created.Set(KnownName.Name, PdfValue.FromString(PdfText.Encode(DefaultConfigurationName, store.Version)));
            created.Set(KnownName.Order, PdfValue.FromArray(new(store, [group])));
            return created;
        }

        var copy = configuration.Clone();

        // A configuration that orders its groups must list every group (ISO 19005-2 6.9).
        if (copy.GetArray(KnownName.Order) is { } order)
        {
            var extended = order.Clone();
            extended.Add(group);
            copy.Set(KnownName.Order, PdfValue.FromArray(extended));
        }

        return copy;
    }
}
