// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Layers;

/// <summary>
/// Finds the optional content groups a page refers to, for a layer panel whose /ListMode is VisiblePages (ISO 32000-2,
/// 8.11.4.3, Table 99). It follows the page's /Properties resources, the /OC entries of its XObjects (and of the forms
/// they hold) and of its annotations. It does not read the content stream, so a group named only by a resource the
/// page never uses still counts.
/// </summary>
internal static class OptionalContentPages
{
    /// <summary>Collects the object numbers of the groups a page refers to.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="page">The page.</param>
    /// <returns>The object numbers.</returns>
    internal static HashSet<int> Collect(PdfObjectStore objects, PdfPage page)
    {
        var ids = new HashSet<int>();
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        ScanResources(objects, page.Resources, ids, visited, 0);
        var annotations = page.Dictionary.GetArray(KnownName.Annots);
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is { } annotation)
            {
                AddMembership(objects, annotation.GetRaw(KnownName.OC), ids, 0);
            }
        }

        return ids;
    }

    /// <summary>Scans a resource dictionary.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="resources">The resources, if any.</param>
    /// <param name="ids">Receives the group numbers.</param>
    /// <param name="visited">The resource dictionaries already scanned, so a cycle ends.</param>
    /// <param name="depth">The form nesting depth.</param>
    private static void ScanResources(PdfObjectStore objects, PdfDictionary? resources, HashSet<int> ids, HashSet<PdfDictionary> visited, int depth)
    {
        if (resources is null || depth >= PdfLimits.MaxDrawDepth || !visited.Add(resources))
        {
            return;
        }

        var properties = resources.GetDictionary(KnownName.Properties);
        for (var i = 0; properties is not null && i < properties.Count; i++)
        {
            AddMembership(objects, properties.GetValueAt(i), ids, 0);
        }

        var xobjects = resources.GetDictionary(KnownName.XObject);
        for (var i = 0; xobjects is not null && i < xobjects.Count; i++)
        {
            if (StoreReading.Resolve(objects, xobjects.GetValueAt(i)).AsStream() is { } stream)
            {
                ScanXObject(objects, stream, ids, visited, depth);
            }
        }
    }

    /// <summary>Scans an XObject's /OC entry and, for a form, its resources.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="stream">The XObject.</param>
    /// <param name="ids">Receives the group numbers.</param>
    /// <param name="visited">The resource dictionaries already scanned.</param>
    /// <param name="depth">The form nesting depth.</param>
    private static void ScanXObject(PdfObjectStore objects, PdfStream stream, HashSet<int> ids, HashSet<PdfDictionary> visited, int depth)
    {
        AddMembership(objects, stream.Dictionary.GetRaw(KnownName.OC), ids, 0);
        if (stream.Dictionary.IsName(KnownName.Subtype, KnownName.Form))
        {
            ScanResources(objects, stream.Dictionary.GetDictionary(KnownName.Resources), ids, visited, depth + 1);
        }
    }

    /// <summary>Adds the groups an /OC value refers to: one group, or the groups of a membership dictionary and its expression.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="value">The value.</param>
    /// <param name="ids">Receives the group numbers.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void AddMembership(PdfObjectStore objects, PdfValue value, HashSet<int> ids, int depth)
    {
        if (value.IsNull || depth > PdfLimits.MaxNesting)
        {
            return;
        }

        if (value.AsReference() is { IsValid: true } id && StoreReading.Resolve(objects, value).AsDictionary() is { } group && !PdfOptionalContent.IsMembership(group))
        {
            _ = ids.Add(id.Number);
            return;
        }

        var resolved = StoreReading.Resolve(objects, value);
        if (resolved.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                AddMembership(objects, array.GetRaw(i), ids, depth + 1);
            }
        }
        else if (resolved.AsDictionary() is { } membership && PdfOptionalContent.IsMembership(membership))
        {
            AddMembership(objects, membership.GetRaw(KnownName.OCGs), ids, depth + 1);
            AddMembership(objects, membership.GetRaw(KnownName.VE), ids, depth + 1);
        }
    }
}
