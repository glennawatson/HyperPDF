// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Composition;

/// <summary>Writes the name tree of the document's named destinations (ISO 32000-2 7.9.6), the names that links jump to.</summary>
internal static class NameTreeWriter
{
    /// <summary>The most names in one leaf of the tree.</summary>
    private const int LeafNames = 64;

    /// <summary>The items in a pair of a name tree: a name and what it goes to.</summary>
    private const int PairLength = 2;

    /// <summary>The entries of a tree node dictionary.</summary>
    private const int NodeEntries = 2;

    /// <summary>Writes the tree.</summary>
    /// <param name="builder">The document.</param>
    /// <param name="destinations">The names and what they go to, in any order.</param>
    /// <param name="pageIds">The object of each page, in order.</param>
    /// <returns>The tree's root object.</returns>
    internal static PdfObjectId Write(PdfDocumentBuilder builder, Dictionary<string, DestinationPoint> destinations, List<PdfObjectId> pageIds)
    {
        var entries = new List<KeyValuePair<byte[], string>>(destinations.Count);
        foreach (var name in destinations.Keys)
        {
            entries.Add(new(Encoding.UTF8.GetBytes(name), name));
        }

        entries.Sort(static (a, b) => a.Key.AsSpan().SequenceCompareTo(b.Key));
        var leaves = new List<PdfObjectId>();
        for (var start = 0; start < entries.Count; start += LeafNames)
        {
            leaves.Add(WriteLeaf(builder, entries, start, Math.Min(LeafNames, entries.Count - start), destinations, pageIds));
        }

        if (leaves.Count == 1)
        {
            return leaves[0];
        }

        var kids = new PdfArray(builder.Owner, leaves.Count);
        foreach (var leaf in leaves)
        {
            kids.Add(PdfValue.FromReference(leaf));
        }

        var root = new PdfDictionary(builder.Owner, NodeEntries);
        root.Set(KnownName.Kids, PdfValue.FromArray(kids));
        return builder.Add(PdfValue.FromDictionary(root));
    }

    /// <summary>Writes one leaf of the tree.</summary>
    /// <param name="builder">The document.</param>
    /// <param name="entries">All the names, in order.</param>
    /// <param name="start">The first name of the leaf.</param>
    /// <param name="count">The number of names in the leaf.</param>
    /// <param name="destinations">What each name goes to.</param>
    /// <param name="pageIds">The object of each page, in order.</param>
    /// <returns>The leaf's object.</returns>
    private static PdfObjectId WriteLeaf(
        PdfDocumentBuilder builder,
        List<KeyValuePair<byte[], string>> entries,
        int start,
        int count,
        Dictionary<string, DestinationPoint> destinations,
        List<PdfObjectId> pageIds)
    {
        var names = new PdfArray(builder.Owner, count * PairLength);
        for (var i = start; i < start + count; i++)
        {
            var point = destinations[entries[i].Value];
            names.Add(PdfValue.FromString(entries[i].Key));
            names.Add(DestinationArrays.Create(builder, pageIds[point.Page - 1], point));
        }

        var leaf = new PdfDictionary(builder.Owner, NodeEntries + 1);
        leaf.Set(KnownName.Names, PdfValue.FromArray(names));
        var limits = new PdfArray(builder.Owner, NodeEntries);
        limits.Add(PdfValue.FromString(entries[start].Key));
        limits.Add(PdfValue.FromString(entries[start + count - 1].Key));
        leaf.Set(KnownName.Limits, PdfValue.FromArray(limits));
        return builder.Add(PdfValue.FromDictionary(leaf));
    }
}
