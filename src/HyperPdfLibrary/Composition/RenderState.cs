// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Composition;

/// <summary>What drawing commands need while a page is written: the document and the structure elements made so far.</summary>
[DebuggerDisplay("RenderState")]
internal sealed class RenderState
{
    /// <summary>Initializes a new instance of the <see cref="RenderState"/> class.</summary>
    /// <param name="creator">The document being written.</param>
    internal RenderState(PdfDocumentCreator creator) => Creator = creator;

    /// <summary>Gets the document being written.</summary>
    internal PdfDocumentCreator Creator { get; }

    /// <summary>Gets the links drawn on the page being written, whose annotations are added when the page ends.</summary>
    internal List<PendingLink> PendingLinks { get; } = [];

    /// <summary>Gets the named places found so far, by name.</summary>
    internal Dictionary<string, DestinationPoint> Destinations { get; } = [with(StringComparer.Ordinal)];

    /// <summary>Gets the numbers of the pages that links go to.</summary>
    internal SortedSet<int> LinkedPages { get; } = [];

    /// <summary>Gets the names of the bookmarks that links go to.</summary>
    internal HashSet<string> LinkedBookmarks { get; } = [with(StringComparer.Ordinal)];

    /// <summary>Gets the object of each page written so far, in order.</summary>
    internal List<PdfObjectId> PageIds { get; } = [];

    /// <summary>Gets or sets the number of the page being written, counted from 1.</summary>
    internal int PageNumber { get; set; }

    /// <summary>Records a named place on the page being written.</summary>
    /// <param name="name">The name.</param>
    /// <param name="x">The x of the place.</param>
    /// <param name="y">The y of the place.</param>
    /// <exception cref="InvalidOperationException">Two bookmarks have the same name.</exception>
    internal void AddDestination(string name, float x, float y)
    {
        if (!Destinations.TryAdd(name, new(PageNumber, x, y)))
        {
            throw new InvalidOperationException($"The bookmark '{name}' is used more than once.");
        }
    }

    /// <summary>Gets the element of a node, making it and its parents first when they do not exist yet.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The element.</returns>
    /// <exception cref="InvalidOperationException">The document is not tagged.</exception>
    internal PdfTagElement GetElement(StructNode node)
    {
        if (node.Element is { } known)
        {
            return known;
        }

        var tags = Creator.Tags ?? throw new InvalidOperationException("The document is not tagged.");
        var parent = node.Parent is { } above ? GetElement(above) : tags.Root;
        node.Element = tags.AddElement(parent, node.Type, node.Options);
        return node.Element;
    }
}
