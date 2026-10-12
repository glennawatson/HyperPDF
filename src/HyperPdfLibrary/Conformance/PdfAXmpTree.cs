// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>The XMP data model being built from a packet's RDF: one schema node per namespace, holding its properties.</summary>
[DebuggerDisplay("PdfAXmpTree: {Schemas.Count} schemas")]
internal sealed class PdfAXmpTree
{
    /// <summary>The deepest node nesting read; deeper packets are treated as malformed.</summary>
    internal const int MaxDepth = 512;

    /// <summary>Gets the schema nodes in the order their namespaces first appear.</summary>
    internal List<PdfAXmpNode> Schemas { get; } = [];

    /// <summary>Gets or sets the <c>rdf:about</c> value shared by the top-level descriptions.</summary>
    internal string About { get; set; } = string.Empty;

    /// <summary>Gets or sets the current node nesting.</summary>
    internal int Depth { get; set; }

    /// <summary>Gets the schema node for a namespace, adding it when it is new.</summary>
    /// <param name="namespaceUri">The namespace URI.</param>
    /// <returns>The schema node.</returns>
    internal PdfAXmpNode Schema(string namespaceUri)
    {
        foreach (var schema in Schemas)
        {
            if (string.Equals(schema.Namespace, namespaceUri, StringComparison.Ordinal))
            {
                return schema;
            }
        }

        var added = new PdfAXmpNode(namespaceUri, namespaceUri, null, string.Empty);
        Schemas.Add(added);
        return added;
    }
}
