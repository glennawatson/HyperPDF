// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Collects the file specifications the document associates with its parts through /AF arrays (ISO 19005-3, 6.8): the
/// catalog, structure elements, pages, annotations and their appearances, and the forms, graphics states, patterns and
/// Type 3 fonts in the pages' resources. Only indirect file specifications can be associated.
/// </summary>
internal static class PdfAAssociatedFiles
{
    /// <summary>The appearance entries searched: normal, down and rollover.</summary>
    private static readonly KnownName[] AppearanceKeys = [KnownName.N, KnownName.D, KnownName.R];

    /// <summary>Collects the object numbers of the associated file specifications.</summary>
    /// <param name="context">The validation.</param>
    /// <returns>The object numbers.</returns>
    internal static HashSet<int> Collect(PdfAValidationContext context)
    {
        var document = context.Document;
        var walk = new PdfAAssociatedWalk(document.Objects);
        var catalog = document.Catalog;
        Register(walk, catalog);
        if (catalog.GetDictionary(KnownName.StructTreeRoot) is { } root)
        {
            foreach (var element in PdfAElementWalk.Elements(document.Objects, root, context.CancellationToken))
            {
                Register(walk, element);
            }
        }

        for (var index = 0; index < document.PageCount; index++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var page = PdfDocumentPages.GetPage(document, index);
            Register(walk, page.Dictionary);
            CollectAnnotations(walk, page.Dictionary.GetArray(KnownName.Annots));
            CollectResources(walk, page.Resources);
        }

        return walk.Keys;
    }

    /// <summary>Notes the indirect file specifications a dictionary's /AF array holds.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="dictionary">The dictionary.</param>
    private static void Register(PdfAAssociatedWalk walk, PdfDictionary dictionary)
    {
        if (dictionary.Get(KnownName.AF).AsArray() is not { } files)
        {
            return;
        }

        for (var i = 0; i < files.Count; i++)
        {
            if (files.GetRaw(i) is { IsReference: true } reference)
            {
                _ = walk.Keys.Add(reference.AsReference().Number);
            }
        }
    }

    /// <summary>Collects from annotations and their appearance forms.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="annotations">The page's /Annots array.</param>
    private static void CollectAnnotations(PdfAAssociatedWalk walk, PdfArray? annotations)
    {
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is not { } annotation)
            {
                continue;
            }

            Register(walk, annotation);
            if (annotation.GetDictionary(KnownName.AP) is { } appearances)
            {
                CollectAppearances(walk, appearances);
            }
        }
    }

    /// <summary>Collects from an annotation's appearance forms, including each state's.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="appearances">The appearance dictionary.</param>
    private static void CollectAppearances(PdfAAssociatedWalk walk, PdfDictionary appearances)
    {
        foreach (var key in AppearanceKeys)
        {
            var entry = appearances.Get(key);
            if (entry.AsStream() is { } form)
            {
                CollectXObject(walk, form);
                continue;
            }

            var states = entry.AsDictionary();
            for (var i = 0; states is not null && i < states.Count; i++)
            {
                if (StoreReading.Resolve(walk.Store, states.GetValueAt(i)).AsStream() is { } state)
                {
                    CollectXObject(walk, state);
                }
            }
        }
    }

    /// <summary>Collects from an XObject and, for a form, its resources.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="xobject">The XObject.</param>
    private static void CollectXObject(PdfAAssociatedWalk walk, PdfStream xobject)
    {
        var dictionary = xobject.Dictionary;
        if (!walk.Visited.Add(dictionary))
        {
            return;
        }

        Register(walk, dictionary);
        if (dictionary.IsName(KnownName.Subtype, KnownName.Form))
        {
            CollectResources(walk, dictionary.GetDictionary(KnownName.Resources));
        }
    }

    /// <summary>Collects from the forms, graphics states, patterns and Type 3 fonts in a resource dictionary.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="resources">The resources.</param>
    private static void CollectResources(PdfAAssociatedWalk walk, PdfDictionary? resources)
    {
        if (resources is null || !walk.Visited.Add(resources))
        {
            return;
        }

        ForEachValue(walk, resources.GetDictionary(KnownName.XObject), static (walk, value) =>
        {
            if (value.AsStream() is { } xobject)
            {
                CollectXObject(walk, xobject);
            }
        });
        ForEachValue(walk, resources.GetDictionary(KnownName.ExtGState), static (walk, value) => CollectGraphicsState(walk, value.AsDictionary()));
        ForEachValue(walk, resources.GetDictionary(KnownName.Pattern), CollectPattern);
        ForEachValue(walk, resources.GetDictionary(KnownName.Font), static (walk, value) => CollectFont(walk, value.AsDictionary()));
    }

    /// <summary>Collects from a graphics state's font.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The graphics state.</param>
    private static void CollectGraphicsState(PdfAAssociatedWalk walk, PdfDictionary? state)
    {
        if (state is not null && walk.Visited.Add(state))
        {
            CollectFont(walk, state.GetArray(KnownName.Font)?.GetDictionary(0));
        }
    }

    /// <summary>Collects from a tiling pattern's resources or a shading pattern's graphics state.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="pattern">The pattern.</param>
    private static void CollectPattern(PdfAAssociatedWalk walk, PdfValue pattern)
    {
        if (pattern.AsStream() is { } tiling)
        {
            if (walk.Visited.Add(tiling.Dictionary))
            {
                CollectResources(walk, tiling.Dictionary.GetDictionary(KnownName.Resources));
            }

            return;
        }

        if (pattern.AsDictionary() is { } shading && walk.Visited.Add(shading))
        {
            CollectGraphicsState(walk, shading.GetDictionary(KnownName.ExtGState));
        }
    }

    /// <summary>Collects from a Type 3 font's resources.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="font">The font.</param>
    private static void CollectFont(PdfAAssociatedWalk walk, PdfDictionary? font)
    {
        if (font is not null && font.IsName(KnownName.Subtype, KnownName.Type3) && walk.Visited.Add(font))
        {
            CollectResources(walk, font.GetDictionary(KnownName.Resources));
        }
    }

    /// <summary>Calls an action for each resolved value of a resource category.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="category">The category dictionary.</param>
    /// <param name="action">The action.</param>
    private static void ForEachValue(PdfAAssociatedWalk walk, PdfDictionary? category, Action<PdfAAssociatedWalk, PdfValue> action)
    {
        for (var i = 0; category is not null && i < category.Count; i++)
        {
            action(walk, StoreReading.Resolve(walk.Store, category.GetValueAt(i)));
        }
    }
}
