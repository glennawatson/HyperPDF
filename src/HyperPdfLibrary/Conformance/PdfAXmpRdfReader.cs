// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads the <c>rdf:RDF</c> element of a packet into the XMP data model, following the RDF serialization rules of XMP
/// Specification Part 1, clause 7 and Annex C. Node elements and their attributes are read here; property elements are
/// read by <see cref="PdfAXmpPropertyElements"/>. Malformed RDF raises <see cref="InvalidDataException"/>.
/// </summary>
internal static class PdfAXmpRdfReader
{
    /// <summary>Reads the descriptions inside <c>rdf:RDF</c>.</summary>
    /// <param name="rdf">The <c>rdf:RDF</c> element.</param>
    /// <returns>The model.</returns>
    /// <exception cref="InvalidDataException">The RDF is malformed.</exception>
    internal static PdfAXmpTree Read(PdfAXmlNode rdf)
    {
        var tree = new PdfAXmpTree();
        var root = new PdfAXmpNode(null, string.Empty, null, string.Empty);
        foreach (var child in rdf.Children)
        {
            if (!PdfAXmpTreeBuilder.IsWhitespace(child))
            {
                NodeElement(tree, root, child, true);
            }
        }

        return tree;
    }

    /// <summary>Reads a node element: <c>rdf:Description</c> or a typed node.</summary>
    /// <param name="tree">The model.</param>
    /// <param name="parent">The node the element describes.</param>
    /// <param name="element">The element.</param>
    /// <param name="isTopLevel">Whether the element is directly inside <c>rdf:RDF</c>.</param>
    /// <exception cref="InvalidDataException">The element is not a node element.</exception>
    internal static void NodeElement(PdfAXmpTree tree, PdfAXmpNode parent, PdfAXmlNode element, bool isTopLevel)
    {
        var term = PdfAXmpTreeBuilder.TermOf(element);
        if (term is not (PdfARdfTerm.Description or PdfARdfTerm.Other))
        {
            throw new InvalidDataException("Node element must be rdf:Description or typed node.");
        }

        if (isTopLevel && term == PdfARdfTerm.Other)
        {
            throw new InvalidDataException("Top level typed node not allowed.");
        }

        tree.Depth++;
        if (tree.Depth > PdfAXmpTree.MaxDepth)
        {
            throw new InvalidDataException("The metadata nests too deeply.");
        }

        NodeElementAttributes(tree, parent, element, isTopLevel);
        PropertyElementList(tree, parent, element, isTopLevel);
        tree.Depth--;
    }

    /// <summary>Reads the property elements inside a node element.</summary>
    /// <param name="tree">The model.</param>
    /// <param name="parent">The node the properties belong to.</param>
    /// <param name="element">The node element.</param>
    /// <param name="isTopLevel">Whether the properties are top-level.</param>
    /// <exception cref="InvalidDataException">A child is not an element.</exception>
    internal static void PropertyElementList(PdfAXmpTree tree, PdfAXmpNode parent, PdfAXmlNode element, bool isTopLevel)
    {
        foreach (var child in element.Children)
        {
            if (PdfAXmpTreeBuilder.IsWhitespace(child))
            {
                continue;
            }

            if (child.Kind != PdfAXmlNodeKind.Element)
            {
                throw new InvalidDataException("Expected property element node not found.");
            }

            PdfAXmpPropertyElements.Read(tree, parent, child, isTopLevel);
        }
    }

    /// <summary>Reads a node element's attributes: <c>rdf:about</c> at the top level, and property attributes.</summary>
    /// <param name="tree">The model.</param>
    /// <param name="parent">The node the element describes.</param>
    /// <param name="element">The element.</param>
    /// <param name="isTopLevel">Whether the element is directly inside <c>rdf:RDF</c>.</param>
    /// <exception cref="InvalidDataException">An attribute is not allowed or the descriptions name different resources.</exception>
    private static void NodeElementAttributes(PdfAXmpTree tree, PdfAXmpNode parent, PdfAXmlNode element, bool isTopLevel)
    {
        var identified = false;
        foreach (var attribute in element.Attributes)
        {
            if (PdfAXmpTreeBuilder.IsNamespaceDeclaration(attribute))
            {
                continue;
            }

            switch (PdfAXmpTreeBuilder.TermOf(attribute, element))
            {
                case PdfARdfTerm.Id or PdfARdfTerm.NodeId or PdfARdfTerm.About:
                {
                    if (identified)
                    {
                        throw new InvalidDataException("Mutually exclusive about, ID, nodeID attributes.");
                    }

                    identified = true;
                    if (isTopLevel && PdfAXmpTreeBuilder.TermOf(attribute, element) == PdfARdfTerm.About)
                    {
                        CheckAbout(tree, attribute.Value);
                    }

                    break;
                }

                case PdfARdfTerm.Other:
                {
                    _ = PdfAXmpTreeBuilder.AddChild(tree, parent, PdfAXmpTreeBuilder.NameOf(attribute), attribute.Value, isTopLevel);
                    break;
                }

                default:
                {
                    throw new InvalidDataException("Invalid nodeElement attribute.");
                }
            }
        }
    }

    /// <summary>Checks a top-level <c>rdf:about</c> value against the one already read.</summary>
    /// <param name="tree">The model.</param>
    /// <param name="about">The value.</param>
    /// <exception cref="InvalidDataException">The descriptions name different resources.</exception>
    private static void CheckAbout(PdfAXmpTree tree, string about)
    {
        if (tree.About.Length == 0)
        {
            tree.About = about;
        }
        else if (!string.Equals(tree.About, about, StringComparison.Ordinal) && about.Length > 0)
        {
            throw new InvalidDataException("Mismatched top level rdf:about values.");
        }
    }
}
