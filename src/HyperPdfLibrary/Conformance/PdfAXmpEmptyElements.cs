// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>Reads the attributes of an empty RDF property element (XMP Specification Part 1, 7.9.2.5 and Annex C).</summary>
internal static class PdfAXmpEmptyElements
{
    /// <summary>Works out what an empty property element's attributes make it, rejecting conflicting attributes.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The shape.</returns>
    /// <exception cref="InvalidDataException">The attributes conflict or one is not allowed.</exception>
    internal static PdfAXmpEmptyShape Classify(PdfAXmlNode element)
    {
        PdfAXmlAttribute? holder = null;
        var hasValue = false;
        var hasResource = false;
        var hasNodeId = false;
        var hasProperties = false;
        foreach (var attribute in element.Attributes)
        {
            if (PdfAXmpTreeBuilder.IsNamespaceDeclaration(attribute))
            {
                continue;
            }

            switch (PdfAXmpTreeBuilder.TermOf(attribute, element))
            {
                case PdfARdfTerm.Id:
                {
                    break;
                }

                case PdfARdfTerm.Resource:
                {
                    Require(!hasNodeId && !hasValue, "rdf:resource conflicts with rdf:nodeID or rdf:value.");
                    hasResource = true;
                    holder = attribute;
                    break;
                }

                case PdfARdfTerm.NodeId:
                {
                    Require(!hasResource, "rdf:nodeID conflicts with rdf:resource.");
                    hasNodeId = true;
                    break;
                }

                case PdfARdfTerm.Other when PdfAXmpTreeBuilder.IsRdf(attribute, "value"):
                {
                    Require(!hasResource, "rdf:value conflicts with rdf:resource.");
                    hasValue = true;
                    holder = attribute;
                    break;
                }

                case PdfARdfTerm.Other:
                {
                    hasProperties |= !PdfAXmpTreeBuilder.IsLanguage(attribute);
                    break;
                }

                default:
                {
                    throw new InvalidDataException("Unrecognized attribute of empty property element.");
                }
            }
        }

        return new(holder, hasValue, hasProperties);
    }

    /// <summary>Adds the element's other attributes to the node: qualifiers on a value, fields on a structure.</summary>
    /// <param name="tree">The model.</param>
    /// <param name="node">The node.</param>
    /// <param name="element">The element.</param>
    /// <param name="shape">What the attributes make the element.</param>
    /// <exception cref="InvalidDataException">A field or qualifier breaks a data model rule.</exception>
    internal static void ReadAttributes(PdfAXmpTree tree, PdfAXmpNode node, PdfAXmlNode element, PdfAXmpEmptyShape shape)
    {
        var isStruct = shape.Value is null && shape.HasProperties;
        foreach (var attribute in element.Attributes)
        {
            if (ReferenceEquals(attribute, shape.Value) || PdfAXmpTreeBuilder.IsNamespaceDeclaration(attribute))
            {
                continue;
            }

            switch (PdfAXmpTreeBuilder.TermOf(attribute, element))
            {
                case PdfARdfTerm.Id or PdfARdfTerm.NodeId:
                {
                    break;
                }

                case PdfARdfTerm.Resource:
                {
                    PdfAXmpTreeBuilder.AddQualifier(node, "rdf:resource", attribute.Value);
                    break;
                }

                case PdfARdfTerm.Other:
                {
                    ReadOther(tree, node, attribute, isStruct);
                    break;
                }

                default:
                {
                    throw new InvalidDataException("Unrecognized attribute of empty property element.");
                }
            }
        }
    }

    /// <summary>Adds an ordinary attribute: a qualifier of a value, or a field of a structure.</summary>
    /// <param name="tree">The model.</param>
    /// <param name="node">The node.</param>
    /// <param name="attribute">The attribute.</param>
    /// <param name="isStruct">Whether the node is a structure.</param>
    private static void ReadOther(PdfAXmpTree tree, PdfAXmpNode node, PdfAXmlAttribute attribute, bool isStruct)
    {
        if (!isStruct)
        {
            PdfAXmpTreeBuilder.AddQualifier(node, attribute.QualifiedName, attribute.Value);
        }
        else if (PdfAXmpTreeBuilder.IsLanguage(attribute))
        {
            PdfAXmpTreeBuilder.AddQualifier(node, PdfAXmpNode.LanguageQualifier, attribute.Value);
        }
        else
        {
            _ = PdfAXmpTreeBuilder.AddChild(tree, node, PdfAXmpTreeBuilder.NameOf(attribute), attribute.Value, false);
        }
    }

    /// <summary>Raises a malformed-packet error when a condition fails.</summary>
    /// <param name="holds">The condition.</param>
    /// <param name="message">The reason.</param>
    /// <exception cref="InvalidDataException">The condition failed.</exception>
    private static void Require(bool holds, string message)
    {
        if (!holds)
        {
            throw new InvalidDataException(message);
        }
    }
}
