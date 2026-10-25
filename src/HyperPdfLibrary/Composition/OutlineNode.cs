// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Composition;

/// <summary>An outline entry as it is arranged into a tree before it is written.</summary>
[DebuggerDisplay("OutlineNode: {Entry}")]
internal sealed class OutlineNode
{
    /// <summary>Initializes a new instance of the <see cref="OutlineNode"/> class.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="id">The object the entry is written to.</param>
    /// <param name="parent">The entry's parent, or <see langword="null"/> for a child of the outline root.</param>
    internal OutlineNode(OutlineEntry entry, PdfObjectId id, OutlineNode? parent)
    {
        Entry = entry;
        Id = id;
        Parent = parent;
    }

    /// <summary>Gets the entry.</summary>
    internal OutlineEntry Entry { get; }

    /// <summary>Gets the object the entry is written to.</summary>
    internal PdfObjectId Id { get; }

    /// <summary>Gets the entry's parent, or <see langword="null"/> for a child of the outline root.</summary>
    internal OutlineNode? Parent { get; }

    /// <summary>Gets the entry's children, in order.</summary>
    internal List<OutlineNode> Children { get; } = [];

    /// <summary>Gets the number of entries below this one, counting all levels.</summary>
    internal int Descendants
    {
        get
        {
            var count = Children.Count;
            foreach (var child in Children)
            {
                count += child.Descendants;
            }

            return count;
        }
    }
}
