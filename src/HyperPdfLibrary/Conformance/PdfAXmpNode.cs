// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>A node of the XMP data model: a property, a structure field or an array item.</summary>
[DebuggerDisplay("PdfAXmpNode: {Prefix}:{Name} = {Value}")]
internal sealed class PdfAXmpNode
{
    /// <summary>The name of an array item.</summary>
    internal const string ItemName = "[]";

    /// <summary>The name of the language qualifier.</summary>
    internal const string LanguageQualifier = "xml:lang";

    /// <summary>Initializes a new instance of the <see cref="PdfAXmpNode"/> class.</summary>
    /// <param name="namespaceUri">The namespace URI; <see langword="null"/> for array items.</param>
    /// <param name="name">The local name, or <see cref="ItemName"/>.</param>
    /// <param name="prefix">The prefix as written in the packet, or <see langword="null"/>.</param>
    /// <param name="value">The value.</param>
    internal PdfAXmpNode(string? namespaceUri, string name, string? prefix, string value)
    {
        Namespace = namespaceUri;
        Name = name;
        Prefix = prefix;
        Value = value;
    }

    /// <summary>Gets the namespace URI; <see langword="null"/> for array items.</summary>
    internal string? Namespace { get; }

    /// <summary>Gets the local name, or <see cref="ItemName"/> for an array item.</summary>
    internal string Name { get; }

    /// <summary>Gets the prefix as written in the packet, or <see langword="null"/> when there was none.</summary>
    internal string? Prefix { get; }

    /// <summary>Gets or sets the value of a simple node.</summary>
    internal string Value { get; set; }

    /// <summary>Gets or sets the node's form.</summary>
    internal PdfAXmpForm Form { get; set; }

    /// <summary>Gets or sets a value indicating whether a structure holds an <c>rdf:value</c> field still to be folded in.</summary>
    internal bool HasValueChild { get; set; }

    /// <summary>Gets the fields of a structure or the items of an array.</summary>
    internal List<PdfAXmpNode> Children { get; } = [];

    /// <summary>Gets the qualifiers; <c>xml:lang</c> is always first.</summary>
    internal List<PdfAXmpQualifier> Qualifiers { get; } = [];

    /// <summary>Gets a value indicating whether the node is a simple value, neither an array nor a structure.</summary>
    internal bool IsSimple => (Form & (PdfAXmpForm.Array | PdfAXmpForm.Struct)) == 0;

    /// <summary>Gets a value indicating whether the node has an <c>xml:lang</c> qualifier.</summary>
    internal bool HasLanguage => (Form & PdfAXmpForm.Language) != 0;

    /// <summary>Gets the node's <c>xml:lang</c> value.</summary>
    /// <returns>The language, or <see langword="null"/> when there is none.</returns>
    internal string? Language() => HasLanguage && Qualifiers.Count > 0 ? Qualifiers[0].Value : null;

    /// <summary>Determines whether the node has a namespace and local name.</summary>
    /// <param name="namespaceUri">The namespace URI.</param>
    /// <param name="name">The local name.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    internal bool Is(string namespaceUri, string name) =>
        string.Equals(Namespace, namespaceUri, StringComparison.Ordinal) && string.Equals(Name, name, StringComparison.Ordinal);

    /// <summary>Finds the first child with a namespace and local name.</summary>
    /// <param name="namespaceUri">The namespace URI.</param>
    /// <param name="name">The local name.</param>
    /// <returns>The child, or <see langword="null"/>.</returns>
    internal PdfAXmpNode? Child(string namespaceUri, string name)
    {
        foreach (var child in Children)
        {
            if (child.Is(namespaceUri, name))
            {
                return child;
            }
        }

        return null;
    }
}
