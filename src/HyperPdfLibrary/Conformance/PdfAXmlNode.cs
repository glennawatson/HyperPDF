// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// A node of an XMP packet read forward-only by <see cref="PdfAXmlTreeReader"/>. The RDF rules of the XMP
/// specification look at an element's children and attributes together, so the packet is held as a small tree.
/// </summary>
[DebuggerDisplay("PdfAXmlNode: {Kind} {QualifiedName}")]
internal sealed class PdfAXmlNode
{
    /// <summary>Initializes a new instance of the <see cref="PdfAXmlNode"/> class.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="name">The element's name; empty for other kinds.</param>
    /// <param name="value">The text, or the instruction's target and data.</param>
    internal PdfAXmlNode(PdfAXmlNodeKind kind, in PdfAXmlName name, string value)
    {
        Kind = kind;
        Namespace = name.Namespace;
        Prefix = name.Prefix;
        LocalName = name.LocalName;
        QualifiedName = name.QualifiedName;
        Value = value;
    }

    /// <summary>Gets the kind.</summary>
    internal PdfAXmlNodeKind Kind { get; }

    /// <summary>Gets the element's namespace URI, or <see langword="null"/> when it has none.</summary>
    internal string? Namespace { get; }

    /// <summary>Gets the element's prefix as written, or <see langword="null"/> when there is none.</summary>
    internal string? Prefix { get; }

    /// <summary>Gets the element's local name, or an instruction's target.</summary>
    internal string LocalName { get; }

    /// <summary>Gets the element's name as written.</summary>
    internal string QualifiedName { get; }

    /// <summary>Gets or sets the text of a text node, or an instruction's data.</summary>
    internal string Value { get; set; }

    /// <summary>Gets the element's attributes, sorted by written name as a DOM keeps them.</summary>
    internal List<PdfAXmlAttribute> Attributes { get; } = [];

    /// <summary>Gets the child nodes in document order.</summary>
    internal List<PdfAXmlNode> Children { get; } = [];

    /// <summary>Determines whether the node is an element with a given namespace and local name.</summary>
    /// <param name="namespaceUri">The namespace URI.</param>
    /// <param name="localName">The local name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal bool IsElement(string namespaceUri, string localName) =>
        Kind == PdfAXmlNodeKind.Element && string.Equals(Namespace, namespaceUri, StringComparison.Ordinal) && string.Equals(LocalName, localName, StringComparison.Ordinal);
}
