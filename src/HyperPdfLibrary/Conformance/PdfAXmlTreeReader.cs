// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Xml;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads an XMP packet forward-only with <see cref="XmlReader"/> into a <see cref="PdfAXmlNode"/> tree. DTDs are
/// refused, nothing external is loaded, comments are dropped and adjacent text is joined, as an XML DOM would hold it.
/// </summary>
internal static class PdfAXmlTreeReader
{
    /// <summary>The reader settings: no DTDs, no resolver, comments dropped, whitespace and instructions kept.</summary>
    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreWhitespace = false,
        IgnoreProcessingInstructions = false,
        MaxCharactersInDocument = PdfLimits.MaxDecodedLength,
    };

    /// <summary>Reads a packet from its bytes, letting the reader detect the encoding.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The document node.</returns>
    /// <exception cref="XmlException">The packet is not well-formed XML.</exception>
    internal static PdfAXmlNode Read(byte[] packet)
    {
        using var stream = new MemoryStream(packet, false);
        using var reader = XmlReader.Create(stream, Settings);
        return Read(reader);
    }

    /// <summary>Reads a packet already decoded to text; any encoding declaration is ignored.</summary>
    /// <param name="text">The packet text.</param>
    /// <returns>The document node.</returns>
    /// <exception cref="XmlException">The packet is not well-formed XML.</exception>
    internal static PdfAXmlNode Read(string text)
    {
        using var source = new StringReader(text);
        using var reader = XmlReader.Create(source, Settings);
        return Read(reader);
    }

    /// <summary>Builds the tree from a reader.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The document node.</returns>
    private static PdfAXmlNode Read(XmlReader reader)
    {
        var document = new PdfAXmlNode(PdfAXmlNodeKind.Document, PdfAXmlName.None, string.Empty);
        var open = new Stack<PdfAXmlNode>();
        open.Push(document);
        while (reader.Read())
        {
            Add(reader, open);
        }

        return document;
    }

    /// <summary>Adds the reader's current node to the tree.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="open">The open elements, innermost on top.</param>
    private static void Add(XmlReader reader, Stack<PdfAXmlNode> open)
    {
        switch (reader.NodeType)
        {
            case XmlNodeType.Element:
            {
                var element = ReadElement(reader);
                open.Peek().Children.Add(element);
                if (!reader.IsEmptyElement)
                {
                    open.Push(element);
                }

                break;
            }

            case XmlNodeType.EndElement:
            {
                _ = open.Pop();
                break;
            }

            case XmlNodeType.Text or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace:
            {
                AddText(open.Peek(), reader.Value);
                break;
            }

            default:
            {
                AddOther(reader, open.Peek());
                break;
            }
        }
    }

    /// <summary>Adds a CDATA section or processing instruction; declarations and other nodes are skipped.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="parent">The parent node.</param>
    private static void AddOther(XmlReader reader, PdfAXmlNode parent)
    {
        if (reader.NodeType == XmlNodeType.CDATA)
        {
            parent.Children.Add(new(PdfAXmlNodeKind.CData, PdfAXmlName.None, reader.Value));
        }
        else if (reader.NodeType == XmlNodeType.ProcessingInstruction)
        {
            var target = new PdfAXmlName(null, null, reader.Name, reader.Name);
            parent.Children.Add(new(PdfAXmlNodeKind.Instruction, target, reader.Value));
        }
    }

    /// <summary>Adds text, joining it to a text node directly before it.</summary>
    /// <param name="parent">The parent node.</param>
    /// <param name="text">The text.</param>
    private static void AddText(PdfAXmlNode parent, string text)
    {
        if (parent.Children.Count > 0 && parent.Children[^1] is { Kind: PdfAXmlNodeKind.Text } last)
        {
            last.Value += text;
            return;
        }

        parent.Children.Add(new(PdfAXmlNodeKind.Text, PdfAXmlName.None, text));
    }

    /// <summary>Reads an element and its attributes.</summary>
    /// <param name="reader">The reader, on the element.</param>
    /// <returns>The element.</returns>
    private static PdfAXmlNode ReadElement(XmlReader reader)
    {
        var element = new PdfAXmlNode(PdfAXmlNodeKind.Element, new(OrNull(reader.NamespaceURI), OrNull(reader.Prefix), reader.LocalName, reader.Name), string.Empty);
        if (reader.MoveToFirstAttribute())
        {
            do
            {
                element.Attributes.Add(new(OrNull(reader.NamespaceURI), OrNull(reader.Prefix), reader.LocalName, reader.Name, reader.Value));
            }
            while (reader.MoveToNextAttribute());

            _ = reader.MoveToElement();
            element.Attributes.Sort(static (left, right) => string.CompareOrdinal(left.QualifiedName, right.QualifiedName));
        }

        return element;
    }

    /// <summary>Turns an empty reader string into <see langword="null"/>, as a DOM reports a missing namespace or prefix.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value, or <see langword="null"/> when empty.</returns>
    private static string? OrNull(string value) => value.Length == 0 ? null : value;
}
