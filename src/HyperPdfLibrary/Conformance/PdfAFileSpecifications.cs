// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Finds the file specifications veraPDF checks: every dictionary typed /Filespec in any object or the trailer, and every
/// value of the catalog's /EmbeddedFiles name tree.
/// </summary>
internal static class PdfAFileSpecifications
{
    /// <summary>The deepest name tree walked.</summary>
    private const int MaxTreeDepth = 64;

    /// <summary>The objects read between cancellation checks.</summary>
    private const int ObjectsPerCheck = 64;

    /// <summary>The entries of a name tree's /Names array per name: the key and the value.</summary>
    private const int PairStep = 2;

    /// <summary>Finds the file specifications.</summary>
    /// <param name="context">The validation.</param>
    /// <returns>Each file specification once.</returns>
    internal static List<PdfAFileSpecification> Collect(PdfAValidationContext context)
    {
        var store = context.Document.Objects;
        var xref = store.XrefState;
        var found = new List<PdfAFileSpecification>();
        var seen = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        for (var number = 1; number < xref.Size; number++)
        {
            if (number % ObjectsPerCheck == 0)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
            }

            if (xref.GetType(number) != XrefEntryType.Free)
            {
                Search(StoreReading.GetObject(store, new(number, 0)), number, found, seen);
            }
        }

        Search(PdfValue.FromDictionary(store.Trailer), -1, found, seen);
        if (context.Document.Catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.EmbeddedFiles) is { } tree)
        {
            SearchNameTree(store, tree, 0, found);
        }

        return found;
    }

    /// <summary>Searches an object and the direct values inside it for file specification dictionaries.</summary>
    /// <param name="value">The object.</param>
    /// <param name="number">Its object number, or -1.</param>
    /// <param name="found">Receives the file specifications.</param>
    /// <param name="seen">The dictionaries already searched.</param>
    private static void Search(PdfValue value, int number, List<PdfAFileSpecification> found, HashSet<PdfDictionary> seen)
    {
        var pending = new Stack<PdfValue>();
        pending.Push(value);
        var top = true;
        while (pending.TryPop(out var current))
        {
            if (current.AsArray() is { } array)
            {
                PushChildren(pending, array.Items);
            }
            else if (current.AsDictionary() is { } dictionary && seen.Add(dictionary))
            {
                AddIfSpecification(found, dictionary, top ? number : -1);
                for (var i = 0; i < dictionary.Count; i++)
                {
                    PushChild(pending, dictionary.GetValueAt(i));
                }
            }

            top = false;
        }
    }

    /// <summary>Queues the arrays and dictionaries among some values; references lead to objects searched on their own.</summary>
    /// <param name="pending">The values still to search.</param>
    /// <param name="items">The values.</param>
    private static void PushChildren(Stack<PdfValue> pending, ReadOnlySpan<PdfValue> items)
    {
        foreach (var item in items)
        {
            PushChild(pending, item);
        }
    }

    /// <summary>Queues a value when it is a direct array or dictionary.</summary>
    /// <param name="pending">The values still to search.</param>
    /// <param name="value">The value.</param>
    private static void PushChild(Stack<PdfValue> pending, PdfValue value)
    {
        if (value.Kind is PdfKind.Array or PdfKind.Dictionary)
        {
            pending.Push(value);
        }
    }

    /// <summary>Adds a dictionary when its /Type is /Filespec.</summary>
    /// <param name="found">The file specifications.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="number">Its object number, or -1.</param>
    private static void AddIfSpecification(List<PdfAFileSpecification> found, PdfDictionary dictionary, int number)
    {
        if (dictionary.IsName(KnownName.Type, KnownName.Filespec))
        {
            found.Add(new(dictionary, number));
        }
    }

    /// <summary>Determines whether a dictionary has been found already.</summary>
    /// <param name="found">The file specifications.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    private static bool Contains(List<PdfAFileSpecification> found, PdfDictionary dictionary)
    {
        foreach (var entry in found)
        {
            if (ReferenceEquals(entry.Dictionary, dictionary))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Adds the dictionary values of an /EmbeddedFiles name tree, which are file specifications with or without /Type.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="node">The tree node.</param>
    /// <param name="depth">The node's depth.</param>
    /// <param name="found">Receives the file specifications not already found.</param>
    private static void SearchNameTree(PdfObjectStore store, PdfDictionary node, int depth, List<PdfAFileSpecification> found)
    {
        if (depth > MaxTreeDepth)
        {
            return;
        }

        var names = node.GetArray(KnownName.Names);
        for (var i = 1; names is not null && i < names.Count; i += PairStep)
        {
            var raw = names.GetRaw(i);
            if (StoreReading.Resolve(store, raw).AsDictionary() is { } specification && !Contains(found, specification))
            {
                found.Add(new(specification, raw.IsReference ? raw.AsReference().Number : -1));
            }
        }

        var kids = node.GetArray(KnownName.Kids);
        for (var i = 0; kids is not null && i < kids.Count; i++)
        {
            if (kids.GetDictionary(i) is { } kid)
            {
                SearchNameTree(store, kid, depth + 1, found);
            }
        }
    }
}
