// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml;
using HyperPdfLibrary.Compat;

namespace HyperPdfLibrary.Metadata;

/// <summary>
/// Reads an XMP packet with <see cref="XmlReader"/> (trim and Native AOT safe; it detects UTF-8, UTF-16 and UTF-32 itself).
/// DTDs are refused and no external resource is ever loaded. Structure fields (<c>rdf:parseType="Resource"</c>, nested
/// <c>rdf:Description</c> and property attributes on a property element) are read as properties under their own names.
/// </summary>
internal static class XmpParser
{
    /// <summary>The RDF namespace.</summary>
    private const string RdfNamespace = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

    /// <summary>The XML namespace-declaration namespace.</summary>
    private const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";

    /// <summary>The XML namespace.</summary>
    private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";

    /// <summary>The depth of an item relative to its property.</summary>
    private const int ItemDepth = 2;

    /// <summary>The language that marks the default entry of a language alternative.</summary>
    private const string DefaultLanguage = "x-default";

    /// <summary>The <c>rdf:parseType</c> value that makes a property element a structure.</summary>
    private const string ResourceParseType = "Resource";

    /// <summary>The reader settings: no DTDs or resolver; whitespace remains available to structured text fields.</summary>
    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreWhitespace = false,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        MaxCharactersInDocument = PdfLimits.MaxDecodedLength,
    };

    /// <summary>Parses a packet; damage ends the read but keeps what was found.</summary>
    /// <param name="packet">The decoded packet.</param>
    /// <param name="cancellationToken">Cancels parsing and property traversal.</param>
    /// <returns>The metadata.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static XmpMetadata Parse(byte[] packet, CancellationToken cancellationToken) => Parse((ReadOnlyMemory<byte>)packet, cancellationToken);

    /// <summary>Parses leased packet bytes and detaches the retained snapshot before returning.</summary>
    /// <param name="packet">The bytes, unchanged throughout this call.</param>
    /// <param name="cancellationToken">Cancels parsing and property traversal.</param>
    /// <returns>The detached metadata snapshot.</returns>
    internal static XmpMetadata Parse(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var properties = new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal);
        var declarations = default(PdfDeclarationParser.Values<PdfDeclaration>);
        var wellFormed = true;
        try
        {
            try
            {
                using var stream = MemoryStreams.OpenRead(packet);
                using var reader = XmlReader.Create(stream, Settings);
                Walk(reader, properties, ref declarations, cancellationToken);
            }
            catch (XmlException)
            {
                wellFormed = false;
            }

            return new([.. packet.Span], new XmpPropertyDictionary(properties), wellFormed) { Declarations = PdfDeclarationParser.ToImmutable(declarations, cancellationToken) };
        }
        finally
        {
            PdfDeclarationParser.Release(ref declarations);
        }
    }

    /// <summary>Reads the property attributes of an element: the fields of an <c>rdf:Description</c> or of a structure.</summary>
    /// <param name="reader">The reader, on the element.</param>
    /// <param name="properties">The properties found.</param>
    /// <param name="nesting">How many structures deep the element is; a field of a structure adds to the values already read.</param>
    /// <param name="cancellationToken">Cancels parsing and property traversal.</param>
    internal static void ReadAttributes(XmlReader reader, Dictionary<string, ImmutableArray<string>> properties, int nesting, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!reader.MoveToFirstAttribute())
        {
            return;
        }

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NamespaceURI is XmlnsNamespace or XmlNamespace or RdfNamespace || reader.NamespaceURI.Length == 0)
            {
                continue;
            }

            Store(properties, XmpMetadata.Key(reader.NamespaceURI, reader.LocalName), [reader.Value], nesting > 0, cancellationToken);
        }
        while (reader.MoveToNextAttribute());

        _ = reader.MoveToElement();
    }

    /// <summary>Reads one property element: simple text, the items of an array, or the fields of a structure.</summary>
    /// <param name="reader">The reader, on the property element.</param>
    /// <param name="properties">The properties found.</param>
    /// <param name="nesting">How many structures deep the reader is.</param>
    /// <param name="cancellationToken">Cancels parsing and property traversal.</param>
    internal static void ReadProperty(XmlReader reader, Dictionary<string, ImmutableArray<string>> properties, int nesting, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (nesting >= PdfLimits.MaxNesting)
        {
            SkipElement(reader, cancellationToken);
            return;
        }

        var namespaceUri = reader.NamespaceURI;
        var localName = reader.LocalName;
        var isEmpty = reader.IsEmptyElement;
        var resource = reader.GetAttribute("resource", RdfNamespace);
        var isStructure = IsResourceType(reader);
        ReadAttributes(reader, properties, nesting + 1, cancellationToken);
        if (isStructure)
        {
            ReadStructure(reader, properties, nesting, cancellationToken);
            return;
        }

        var items = default(XmpPropertyValues);
        var direct = default(XmpTextValue);
        try
        {
            if (!isEmpty)
            {
                ReadContent(reader, ref items, ref direct, properties, nesting, cancellationToken);
            }

            if (items.Count > 0)
            {
                Store(properties, XmpMetadata.Key(namespaceUri, localName), XmpPropertyValueCollection.ToArray(items), nesting > 0, cancellationToken);
                return;
            }

            var text = XmpTextValues.GetText(direct).Trim();
            var value = text.Length > 0 ? text : resource;
            if (!string.IsNullOrEmpty(value))
            {
                Store(properties, XmpMetadata.Key(namespaceUri, localName), [value], nesting > 0, cancellationToken);
            }
        }
        finally
        {
            XmpTextValues.Release(ref direct);
            XmpPropertyValueCollection.Release(ref items);
        }
    }

    /// <summary>Stores a property, either replacing earlier values or adding to them (the same field in several structures).</summary>
    /// <param name="properties">The properties found.</param>
    /// <param name="key">The property key.</param>
    /// <param name="values">The values.</param>
    /// <param name="append">Whether to add to the values already read.</param>
    /// <param name="cancellationToken">Cancels storing property values.</param>
    internal static void Store(Dictionary<string, ImmutableArray<string>> properties, string key, string[] values, bool append, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Parsing owns each array exclusively; appending creates a new array without changing earlier values.
        properties[key] = append && properties.TryGetValue(key, out var existing)
            ? existing.AddRange(values.AsSpan())
            : ImmutableCollectionsMarshal.AsImmutableArray(values);
    }

    /// <summary>Reads properties and associated declarations from each RDF description.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="properties">The flattened property values.</param>
    /// <param name="declarations">The declarations completed before any XML damage.</param>
    /// <param name="cancellationToken">Cancels traversal.</param>
    private static void Walk(XmlReader reader, Dictionary<string, ImmutableArray<string>> properties, ref PdfDeclarationParser.Values<PdfDeclaration> declarations, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var descriptionDepth = -1;
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.Element && IsRdf(reader, "Description"))
            {
                ReadAttributes(reader, properties, 0, cancellationToken);
                descriptionDepth = reader.IsEmptyElement ? -1 : reader.Depth;
            }
            else if (reader.NodeType == XmlNodeType.Element && descriptionDepth >= 0 && reader.Depth == descriptionDepth + 1)
            {
                ReadPacketProperty(reader, properties, ref declarations, cancellationToken);
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == descriptionDepth)
            {
                descriptionDepth = -1;
            }
        }
    }

    /// <summary>Dispatches a packet property without flattening declaration associations.</summary>
    /// <param name="reader">The reader on the property.</param>
    /// <param name="properties">The flattened properties.</param>
    /// <param name="declarations">The declaration storage.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    private static void ReadPacketProperty(
        XmlReader reader,
        Dictionary<string, ImmutableArray<string>> properties,
        ref PdfDeclarationParser.Values<PdfDeclaration> declarations,
        CancellationToken cancellationToken)
    {
        if (reader.NamespaceURI == XmpMetadata.PdfDeclarationsNamespace && reader.LocalName == PdfDeclarationParser.DeclarationsName)
        {
            PdfDeclarationParser.Read(reader, properties, ref declarations, cancellationToken);
        }
        else
        {
            ReadProperty(reader, properties, 0, cancellationToken);
        }
    }

    /// <summary>Reads the child properties of a structure element as properties of their own.</summary>
    /// <param name="reader">The reader, on the structure element.</param>
    /// <param name="properties">The properties found.</param>
    /// <param name="nesting">How many structures deep the reader is.</param>
    /// <param name="cancellationToken">Cancels parsing and property traversal.</param>
    private static void ReadStructure(XmlReader reader, Dictionary<string, ImmutableArray<string>> properties, int nesting, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (reader.IsEmptyElement)
        {
            return;
        }

        var depth = reader.Depth;
        while (reader.Read() && reader.Depth > depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.Element && reader.Depth == depth + 1)
            {
                ReadProperty(reader, properties, nesting + 1, cancellationToken);
            }
        }
    }

    /// <summary>Moves past an element without reading its content.</summary>
    /// <param name="reader">The reader, on the element.</param>
    /// <param name="cancellationToken">Cancels parsing and property traversal.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SkipElement(XmlReader reader, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (reader.IsEmptyElement)
        {
            return;
        }

        var depth = reader.Depth;
        while (reader.Read() && reader.Depth > depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>Determines whether an element is marked <c>rdf:parseType="Resource"</c>.</summary>
    /// <param name="reader">The reader, on the element.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsResourceType(XmlReader reader) =>
        string.Equals(reader.GetAttribute("parseType", RdfNamespace), ResourceParseType, StringComparison.Ordinal);

    /// <summary>Reads a property's content, collecting array items and direct text.</summary>
    /// <param name="reader">The reader on the property element.</param>
    /// <param name="items">The array items found.</param>
    /// <param name="direct">The text directly inside the property.</param>
    /// <param name="properties">The properties found, which receive the fields of structure items.</param>
    /// <param name="nesting">How many structures deep the reader is.</param>
    /// <param name="cancellationToken">Cancels parsing and property traversal.</param>
    private static void ReadContent(
        XmlReader reader,
        ref XmpPropertyValues items,
        ref XmpTextValue direct,
        Dictionary<string, ImmutableArray<string>> properties,
        int nesting,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var depth = reader.Depth;
        var itemText = default(XmpTextValue);
        var inItem = false;
        string? language = null;
        try
        {
            while (reader.Read() && reader.Depth > depth)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element when reader.Depth == depth + ItemDepth && IsRdf(reader, "li"):
                    {
                        XmpTextValues.Release(ref itemText);
                        var header = ReadItem(reader, ref items, properties, nesting, cancellationToken);
                        inItem = header.IsText;
                        language = header.Language;
                        break;
                    }

                    case XmlNodeType.Element when IsRdf(reader, "Description"):
                    {
                        inItem = false;
                        ReadAttributes(reader, properties, nesting + 1, cancellationToken);
                        ReadStructure(reader, properties, nesting, cancellationToken);
                        break;
                    }

                    case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace:
                    {
                        AppendText(reader, depth, inItem, ref direct, ref itemText, cancellationToken);
                        break;
                    }

                    case XmlNodeType.EndElement when reader.Depth == depth + ItemDepth && inItem:
                    {
                        AddItem(ref items, itemText, language);
                        XmpTextValues.Release(ref itemText);
                        inItem = false;
                        break;
                    }

                    default:
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            XmpTextValues.Release(ref itemText);
        }
    }

    /// <summary>Collects text directly in a property or its current array item.</summary>
    /// <param name="reader">The reader on the text.</param>
    /// <param name="depth">The property's depth.</param>
    /// <param name="inItem">Whether an array item is active.</param>
    /// <param name="direct">The property text.</param>
    /// <param name="itemText">The item text.</param>
    /// <param name="cancellationToken">Cancels text collection.</param>
    private static void AppendText(XmlReader reader, int depth, bool inItem, ref XmpTextValue direct, ref XmpTextValue itemText, CancellationToken cancellationToken)
    {
        if (reader.Depth == depth + 1)
        {
            XmpTextValues.Append(ref direct, reader.Value, cancellationToken);
        }
        else if (inItem && reader.Depth == depth + ItemDepth + 1)
        {
            XmpTextValues.Append(ref itemText, reader.Value, cancellationToken);
        }
    }

    /// <summary>Reads an array item's attributes and any structure fields.</summary>
    /// <param name="reader">The reader on the item.</param>
    /// <param name="items">Receives empty text items.</param>
    /// <param name="properties">Receives structure fields.</param>
    /// <param name="nesting">The property nesting level.</param>
    /// <param name="cancellationToken">Cancels structure reads.</param>
    /// <returns>Whether to collect item text and its language.</returns>
    private static ItemHeader ReadItem(XmlReader reader, ref XmpPropertyValues items, Dictionary<string, ImmutableArray<string>> properties, int nesting, CancellationToken cancellationToken)
    {
        ReadAttributes(reader, properties, nesting + 1, cancellationToken);
        var language = reader.GetAttribute("lang", XmlNamespace);
        if (IsResourceType(reader))
        {
            ReadStructure(reader, properties, nesting, cancellationToken);
            return default;
        }

        if (!reader.IsEmptyElement)
        {
            return new(true, language);
        }

        AddItem(ref items, default, language);
        return default;
    }

    /// <summary>Adds an item, placing a default-language entry first.</summary>
    /// <param name="items">The items.</param>
    /// <param name="text">The item's text.</param>
    /// <param name="language">The item's language, or null.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddItem(ref XmpPropertyValues items, in XmpTextValue text, string? language) =>
        XmpPropertyValueCollection.Add(ref items, XmpTextValues.GetText(text), string.Equals(language, DefaultLanguage, StringComparison.Ordinal));

    /// <summary>Determines whether the reader is on an RDF element.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="localName">The local name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsRdf(XmlReader reader, string localName) =>
        string.Equals(reader.NamespaceURI, RdfNamespace, StringComparison.Ordinal) && string.Equals(reader.LocalName, localName, StringComparison.Ordinal);

    /// <summary>The text facts of an array item.</summary>
    /// <param name="IsText">Whether the item has text content to collect.</param>
    /// <param name="Language">The item's language.</param>
    private readonly record struct ItemHeader(bool IsText, string? Language);
}
