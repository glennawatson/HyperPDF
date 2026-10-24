// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>
/// A node of the document's structure as laid out. Drawing commands name the node they belong to; when the pages are written
/// the nodes become structure elements, parents first, in the order they are first drawn on.
/// </summary>
[DebuggerDisplay("StructNode: {Type}")]
internal sealed class StructNode
{
    /// <summary>Initializes a new instance of the <see cref="StructNode"/> class.</summary>
    /// <param name="type">The structure type, such as <c>P</c> or <c>LI</c>.</param>
    /// <param name="parent">The parent node, or <see langword="null"/> for a child of the document.</param>
    /// <param name="options">What the element says besides its type.</param>
    internal StructNode(string type, StructNode? parent, PdfTagOptions options)
    {
        Type = type;
        Parent = parent;
        Options = options;
    }

    /// <summary>Gets the structure type.</summary>
    internal string Type { get; }

    /// <summary>Gets the parent node, or <see langword="null"/> for a child of the document.</summary>
    internal StructNode? Parent { get; }

    /// <summary>Gets what the element says besides its type.</summary>
    internal PdfTagOptions Options { get; }

    /// <summary>Gets or sets the structure element made for the node, once it is first drawn on.</summary>
    internal PdfTagElement? Element { get; set; }
}
