// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads RDF property elements into the XMP data model: literal values, resources (arrays and structures),
/// <c>rdf:parseType="Resource"</c> structures and empty elements with property attributes (XMP Specification Part 1,
/// 7.9.2). The form is chosen as the XMP toolkit chooses it, including the order in which attributes are looked at.
/// </summary>
internal static class PdfAXmpPropertyElements
{
    /// <summary>The attribute count above which a property element is read as an empty element.</summary>
    private const int MaxDispatchAttributes = 3;

    /// <summary>Reads one property element.</summary>
    /// <param name="tree">The model.</param>
    /// <param name="parent">The node the property belongs to.</param>
    /// <param name="element">The property element.</param>
    /// <param name="isTopLevel">Whether the property is top-level.</param>
    /// <exception cref="InvalidDataException">The element is malformed.</exception>
    internal static void Read(PdfAXmpTree tree, PdfAXmpNode parent, PdfAXmlNode element, bool isTopLevel)
    {
        var term = PdfAXmpTreeBuilder.TermOf(element);
        if (term is not (PdfARdfTerm.Other or PdfARdfTerm.Item))
        {
            throw new InvalidDataException("Invalid property element name.");
        }

        var attributes = element.Attributes.FindAll(static attribute => !PdfAXmpTreeBuilder.IsNamespaceDeclaration(attribute));
        if (attributes.Count > MaxDispatchAttributes)
        {
            Empty(tree, parent, element, isTopLevel);
            return;
        }

        // The first attribute other than xml:lang, in name order, decides the element's form.
        if (attributes.Find(static attribute => !PdfAXmpTreeBuilder.IsLanguage(attribute)) is { } decisive)
        {
            ReadByAttribute(tree, parent, element, decisive, isTopLevel);
        }
        else if (element.Children.Count == 0)
        {
            Empty(tree, parent, element, isTopLevel);
        }
        else if (element.Children.TrueForAll(static child => child.Kind == PdfAXmlNodeKind.Text))
        {
            Literal(tree, parent, element, isTopLevel);
        }
        else
        {
            Resource(tree, parent, element, isTopLevel);
        }
    }

    /// <summary>Reads a property element whose form its first attribute decides.</summary>
    /// <param name="tree">The model.</param>
    /// <param name="parent">The node the property belongs to.</param>
    /// <param name="element">The property element.</param>
    /// <param name="attribute">The first attribute other than <c>xml:lang</c>.</param>
    /// <param name="isTopLevel">Whether the property is top-level.</param>
    /// <exception cref="InvalidDataException">The element is malformed or uses an unsupported parse type.</exception>
    private static void ReadByAttribute(PdfAXmpTree tree, PdfAXmpNode parent, PdfAXmlNode element, PdfAXmlAttribute attribute, bool isTopLevel)
    {
        if (PdfAXmpTreeBuilder.IsRdf(attribute, "datatype"))
        {
            Literal(tree, parent, element, isTopLevel);
        }
        else if (!PdfAXmpTreeBuilder.IsRdf(attribute, "parseType"))
        {
            Empty(tree, parent, element, isTopLevel);
        }
        else if (string.Equals(attribute.Value, PdfAXmpTreeBuilder.ParseTypeResource, StringComparison.Ordinal))
        {
            ParseTypeResource(tree, parent, element, isTopLevel);
        }
        else
        {
            throw new InvalidDataException("Only rdf:parseType=\"Resource\" is allowed.");
        }
    }

    /// <summary>Reads a resource property element: an array or a structure in a nested node element.</summary>
    /// <param name="tree">The model.</param>
    /// <param name="parent">The node the property belongs to.</param>
    /// <param name="element">The property element.</param>
    /// <param name="isTopLevel">Whether the property is top-level.</param>
    /// <exception cref="InvalidDataException">The element is malformed.</exception>
    private static void Resource(PdfAXmpTree tree, PdfAXmpNode parent, PdfAXmlNode element, bool isTopLevel)
    {
        // The XMP toolkit skips iX:changes, an old Adobe change log, at the top level.
        if (isTopLevel && string.Equals(element.QualifiedName, "iX:changes", StringComparison.Ordinal))
        {
            return;
        }

        var compound = PdfAXmpTreeBuilder.AddChild(tree, parent, PdfAXmpTreeBuilder.NameOf(element), string.Empty, isTopLevel);
        ReadQualifierAttributes(compound, element, "ID");
        var found = false;
        foreach (var child in element.Children)
        {
            if (PdfAXmpTreeBuilder.IsWhitespace(child))
            {
                continue;
            }

            if (found || child.Kind != PdfAXmlNodeKind.Element)
            {
                throw new InvalidDataException("A resource property element must hold exactly one element.");
            }

            ReadCompound(tree, compound, child);
            found = true;
        }

        if (!found)
        {
            throw new InvalidDataException("Missing child of resource property element.");
        }
    }

    /// <summary>Reads the node element inside a resource property element, setting the property's form from it.</summary>
    /// <param name="tree">The model.</param>
    /// <param name="compound">The property.</param>
    /// <param name="child">The node element.</param>
    /// <exception cref="InvalidDataException">The node element is malformed.</exception>
    private static void ReadCompound(PdfAXmpTree tree, PdfAXmpNode compound, PdfAXmlNode child)
    {
        var isRdf = string.Equals(child.Namespace, PdfAXmpNames.Rdf, StringComparison.Ordinal);
        compound.Form |= isRdf ? ArrayForm(child.LocalName) : PdfAXmpForm.None;
        if ((compound.Form & PdfAXmpForm.Array) == 0)
        {
            compound.Form |= PdfAXmpForm.Struct;
            if (!isRdf && !string.Equals(child.LocalName, "Description", StringComparison.Ordinal))
            {
                var typeNamespace = child.Namespace ?? throw new InvalidDataException("All XML elements must be in a namespace.");
                PdfAXmpTreeBuilder.AddQualifier(compound, PdfAXmpNames.TypeQualifier, $"{typeNamespace}:{child.LocalName}");
            }
        }

        PdfAXmpRdfReader.NodeElement(tree, compound, child, false);
        if (compound.HasValueChild)
        {
            PdfAXmpTreeBuilder.FoldValue(compound);
        }
        else
        {
            PdfAXmpTreeBuilder.DetectAltText(compound);
        }
    }

    /// <summary>Reads a literal property element: text only.</summary>
    /// <param name="tree">The model.</param>
    /// <param name="parent">The node the property belongs to.</param>
    /// <param name="element">The property element.</param>
    /// <param name="isTopLevel">Whether the property is top-level.</param>
    /// <exception cref="InvalidDataException">The element holds more than text or has another attribute.</exception>
    private static void Literal(PdfAXmpTree tree, PdfAXmpNode parent, PdfAXmlNode element, bool isTopLevel)
    {
        var node = PdfAXmpTreeBuilder.AddChild(tree, parent, PdfAXmpTreeBuilder.NameOf(element), string.Empty, isTopLevel);
        ReadQualifierAttributes(node, element, "datatype");
        var text = string.Empty;
        foreach (var child in element.Children)
        {
            text = child.Kind == PdfAXmlNodeKind.Text ? text + child.Value : throw new InvalidDataException("Invalid child of literal property element.");
        }

        node.Value = text;
    }

    /// <summary>Reads an <c>rdf:parseType="Resource"</c> property element: a structure whose fields are the child elements.</summary>
    /// <param name="tree">The model.</param>
    /// <param name="parent">The node the property belongs to.</param>
    /// <param name="element">The property element.</param>
    /// <param name="isTopLevel">Whether the property is top-level.</param>
    /// <exception cref="InvalidDataException">The element is malformed.</exception>
    private static void ParseTypeResource(PdfAXmpTree tree, PdfAXmpNode parent, PdfAXmlNode element, bool isTopLevel)
    {
        var node = PdfAXmpTreeBuilder.AddChild(tree, parent, PdfAXmpTreeBuilder.NameOf(element), string.Empty, isTopLevel);
        node.Form |= PdfAXmpForm.Struct;
        ReadQualifierAttributes(node, element, "parseType");
        PdfAXmpRdfReader.PropertyElementList(tree, node, element, false);
        if (node.HasValueChild)
        {
            PdfAXmpTreeBuilder.FoldValue(node);
        }
    }

    /// <summary>Reads an empty property element: a value from <c>rdf:value</c> or <c>rdf:resource</c>, or a structure from property attributes.</summary>
    /// <param name="tree">The model.</param>
    /// <param name="parent">The node the property belongs to.</param>
    /// <param name="element">The property element.</param>
    /// <param name="isTopLevel">Whether the property is top-level.</param>
    /// <exception cref="InvalidDataException">The element has content or conflicting attributes.</exception>
    private static void Empty(PdfAXmpTree tree, PdfAXmpNode parent, PdfAXmlNode element, bool isTopLevel)
    {
        if (element.Children.Count > 0)
        {
            throw new InvalidDataException("Nested content not allowed with rdf:resource or property attributes.");
        }

        var shape = PdfAXmpEmptyElements.Classify(element);
        var node = PdfAXmpTreeBuilder.AddChild(tree, parent, PdfAXmpTreeBuilder.NameOf(element), string.Empty, isTopLevel);
        if (shape.Value is { } value)
        {
            node.Value = value.Value;
            node.Form |= shape.HasValue ? PdfAXmpForm.None : PdfAXmpForm.Uri;
        }
        else if (shape.HasProperties)
        {
            node.Form |= PdfAXmpForm.Struct;
        }

        PdfAXmpEmptyElements.ReadAttributes(tree, node, element, shape);
    }

    /// <summary>Reads the attributes a literal, resource or structure element may carry: <c>xml:lang</c>, <c>rdf:ID</c> and one more RDF attribute.</summary>
    /// <param name="node">The node.</param>
    /// <param name="element">The element.</param>
    /// <param name="ignored">The RDF attribute, besides <c>rdf:ID</c>, the form allows and ignores.</param>
    /// <exception cref="InvalidDataException">Another attribute is present.</exception>
    private static void ReadQualifierAttributes(PdfAXmpNode node, PdfAXmlNode element, string ignored)
    {
        foreach (var attribute in element.Attributes)
        {
            if (PdfAXmpTreeBuilder.IsNamespaceDeclaration(attribute) || PdfAXmpTreeBuilder.IsRdf(attribute, "ID") || PdfAXmpTreeBuilder.IsRdf(attribute, ignored))
            {
                continue;
            }

            if (!PdfAXmpTreeBuilder.IsLanguage(attribute))
            {
                throw new InvalidDataException("Invalid attribute for the property element.");
            }

            PdfAXmpTreeBuilder.AddQualifier(node, PdfAXmpNode.LanguageQualifier, attribute.Value);
        }
    }

    /// <summary>Gets the array form an RDF container element names.</summary>
    /// <param name="localName">The local name.</param>
    /// <returns>The form, or <see cref="PdfAXmpForm.None"/> when the element is not a container.</returns>
    private static PdfAXmpForm ArrayForm(string localName) => localName switch
    {
        "Bag" => PdfAXmpForm.Array,
        "Seq" => PdfAXmpForm.Array | PdfAXmpForm.Ordered,
        "Alt" => PdfAXmpForm.Array | PdfAXmpForm.Ordered | PdfAXmpForm.Alternate,
        _ => PdfAXmpForm.None,
    };
}
