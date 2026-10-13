// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Annotations;

/// <summary>Collects the objects of a page's description that a trap network's <c>/Version</c> array lists (ISO 32000-2 Table 403).</summary>
internal static class PdfPageDescriptionReader
{
    /// <summary>Collects a page's content streams, its resources and the resources of its form XObjects.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="page">The page dictionary.</param>
    /// <param name="description">Receives the objects.</param>
    internal static void Collect(PdfObjectStore store, PdfDictionary page, PdfPageDescription description)
    {
        AddContents(store, page.GetRaw(KnownName.Contents), description);
        AddResources(store, Resources(page), description, 0);
    }

    /// <summary>Gets a page's resources as written: its own, or the nearest ancestor's.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <returns>The <c>/Resources</c> value, or null.</returns>
    internal static PdfValue Resources(PdfDictionary page)
    {
        var node = page;
        for (var depth = 0; node is not null && depth < PdfLimits.MaxPageTreeDepth; depth++)
        {
            var raw = node.GetRaw(KnownName.Resources);
            if (!raw.IsNull)
            {
                return raw;
            }

            node = node.GetDictionary(KnownName.Parent);
        }

        return default;
    }

    /// <summary>Adds the content streams of a page's <c>/Contents</c>.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="contents">The entry as written.</param>
    /// <param name="description">Receives the objects.</param>
    private static void AddContents(PdfObjectStore store, PdfValue contents, PdfPageDescription description)
    {
        var resolved = StoreReading.Resolve(store, contents);
        if (resolved.AsArray() is not { } array)
        {
            _ = AddReference(description.Described, contents);
            return;
        }

        _ = AddReference(description.Containers, contents);
        foreach (var item in array.Items)
        {
            _ = AddReference(description.Described, item);
        }
    }

    /// <summary>Adds the objects of a resource dictionary, leaving out procedure sets, and follows its form XObjects.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="resources">The <c>/Resources</c> value as written.</param>
    /// <param name="description">Receives the objects.</param>
    /// <param name="depth">How many forms deep the dictionary is.</param>
    private static void AddResources(PdfObjectStore store, PdfValue resources, PdfPageDescription description, int depth)
    {
        if (depth > PdfLimits.MaxDrawDepth || StoreReading.Resolve(store, resources).AsDictionary() is not { } dictionary)
        {
            return;
        }

        _ = AddReference(description.Containers, resources);
        for (var i = 0; i < dictionary.Count; i++)
        {
            var key = dictionary.GetKeyAt(i);
            if (!key.Is(KnownName.ProcSet))
            {
                AddCategory(store, dictionary.GetValueAt(i), key.Is(KnownName.XObject), description, depth);
            }
        }
    }

    /// <summary>Adds the resources of one category, such as <c>/Font</c>.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="category">The category's dictionary as written.</param>
    /// <param name="isXObject">Whether the category is <c>/XObject</c>, whose forms have resources of their own.</param>
    /// <param name="description">Receives the objects.</param>
    /// <param name="depth">How many forms deep the category is.</param>
    private static void AddCategory(PdfObjectStore store, PdfValue category, bool isXObject, PdfPageDescription description, int depth)
    {
        if (StoreReading.Resolve(store, category).AsDictionary() is not { } dictionary)
        {
            return;
        }

        _ = AddReference(description.Containers, category);
        for (var i = 0; i < dictionary.Count; i++)
        {
            var raw = dictionary.GetValueAt(i);

            // An object seen before has had its resources added, which also ends a form that draws itself.
            if (AddReference(description.Described, raw) && isXObject)
            {
                AddXObject(store, raw, description, depth);
            }
        }
    }

    /// <summary>Adds an XObject's OPI dictionaries and, for a form, its resources.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="xobject">The XObject as written.</param>
    /// <param name="description">Receives the objects.</param>
    /// <param name="depth">How many forms deep the XObject is.</param>
    private static void AddXObject(PdfObjectStore store, PdfValue xobject, PdfPageDescription description, int depth)
    {
        if (StoreReading.Resolve(store, xobject).AsDictionary() is not { } dictionary)
        {
            return;
        }

        var opi = dictionary.GetRaw(KnownName.OPI);
        _ = AddReference(description.Described, opi);
        if (StoreReading.Resolve(store, opi).AsDictionary() is { } versions)
        {
            for (var i = 0; i < versions.Count; i++)
            {
                _ = AddReference(description.Described, versions.GetValueAt(i));
            }
        }

        if (dictionary.IsName(KnownName.Subtype, KnownName.Form))
        {
            AddResources(store, dictionary.GetRaw(KnownName.Resources), description, depth + 1);
        }
    }

    /// <summary>Adds a value's object number to a set when it is a reference.</summary>
    /// <param name="set">The set.</param>
    /// <param name="value">The value as written.</param>
    /// <returns><see langword="true"/> when the value is a reference not in the set before; a direct value also returns <see langword="true"/>.</returns>
    private static bool AddReference(HashSet<int> set, PdfValue value) => !value.IsReference || set.Add(value.AsReference().Number);
}
