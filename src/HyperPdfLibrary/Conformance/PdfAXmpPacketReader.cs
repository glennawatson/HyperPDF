// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Xml;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads a metadata stream into an <see cref="PdfAXmpPacket"/> the way veraPDF reads one: XML first, then, when that
/// fails, once more after the repair in <see cref="PdfAXmpPacketRepair"/>; then the RDF inside <c>x:xmpmeta</c>.
/// </summary>
internal static class PdfAXmpPacketReader
{
    /// <summary>The name of a non-UTF-8 encoding the XML reader detected from the first bytes.</summary>
    private const string OtherEncoding = "other";

    /// <summary>
    /// The first bytes from which XML reading detects an encoding other than UTF-8: the UTF-16 and UTF-32 byte order
    /// marks, then the UTF-32, UTF-16 and EBCDIC forms of <c>&lt;</c> and <c>&lt;?</c>.
    /// </summary>
    private static readonly byte[][] OtherEncodingStarts =
    [
        [0xFE, 0xFF],
        [0xFF, 0xFE],
        [0x00, 0x00, 0xFE, 0xFF],
        [0x00, 0x00, 0x00, 0x3C],
        [0x3C, 0x00, 0x00, 0x00],
        [0x00, 0x00, 0x3C, 0x00],
        [0x00, 0x3C, 0x00, 0x00],
        [0x00, 0x3C, 0x00, 0x3F],
        [0x3C, 0x00, 0x3F, 0x00],
        [0x4C, 0x6F, 0xA7, 0x94],
    ];

    /// <summary>Reads a packet.</summary>
    /// <param name="bytes">The decoded stream data.</param>
    /// <returns>The packet; <see cref="PdfAXmpPacket.Malformed"/> when it cannot be read.</returns>
    internal static PdfAXmpPacket Read(byte[] bytes)
    {
        PdfAXmlNode document;
        string encoding;
        try
        {
            document = PdfAXmlTreeReader.Read(bytes);
            encoding = DetectedEncoding(bytes);
        }
        catch (XmlException)
        {
            encoding = PdfAXmpPacketRepair.GuessEncoding(bytes);
            if (!TryReadRepaired(bytes, encoding, out document))
            {
                return PdfAXmpPacket.Malformed;
            }
        }

        string? header = null;
        if (FindRdf(document, ref header) is not { } rdf)
        {
            // Without rdf:RDF the toolkit returns empty metadata that records neither header nor encoding.
            return new(true, null, null, [], null);
        }

        try
        {
            return Build(PdfAXmpRdfReader.Read(rdf), encoding, header);
        }
        catch (InvalidDataException)
        {
            return PdfAXmpPacket.Malformed;
        }
    }

    /// <summary>Reads a packet again after the toolkit's repair.</summary>
    /// <param name="bytes">The packet.</param>
    /// <param name="encoding">The encoding the toolkit guesses.</param>
    /// <param name="document">The document node.</param>
    /// <returns><see langword="true"/> when the repaired packet is well-formed.</returns>
    private static bool TryReadRepaired(byte[] bytes, string encoding, out PdfAXmlNode document)
    {
        try
        {
            document = PdfAXmlTreeReader.Read(PdfAXmpPacketRepair.Repair(bytes, encoding));
            return true;
        }
        catch (XmlException)
        {
            document = new(PdfAXmlNodeKind.Document, PdfAXmlName.None, string.Empty);
            return false;
        }
    }

    /// <summary>Splits the model's properties from the extension schema container.</summary>
    /// <param name="tree">The model.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="header">The packet header data.</param>
    /// <returns>The packet.</returns>
    private static PdfAXmpPacket Build(PdfAXmpTree tree, string encoding, string? header)
    {
        var properties = new List<PdfAXmpNode>();
        PdfAXmpNode? extension = null;
        foreach (var schema in tree.Schemas)
        {
            foreach (var property in schema.Children)
            {
                if (property.Is(PdfAXmpNames.PdfAExtension, "schemas"))
                {
                    extension = property;
                }
                else
                {
                    properties.Add(property);
                }
            }
        }

        return new(true, encoding, header, [.. properties], extension);
    }

    /// <summary>Finds <c>rdf:RDF</c> depth-first, inside <c>x:xmpmeta</c> when there is one, noting the last packet header seen.</summary>
    /// <param name="node">The node whose children are searched.</param>
    /// <param name="header">The data of the last <c>xpacket</c> instruction seen.</param>
    /// <returns>The element, or <see langword="null"/>.</returns>
    private static PdfAXmlNode? FindRdf(PdfAXmlNode node, ref string? header)
    {
        foreach (var child in node.Children)
        {
            if (child.Kind == PdfAXmlNodeKind.Instruction && string.Equals(child.LocalName, PdfAXmpNames.PacketInstruction, StringComparison.Ordinal))
            {
                header = child.Value;
                continue;
            }

            if (child.Kind != PdfAXmlNodeKind.Element)
            {
                continue;
            }

            if (child.IsElement(PdfAXmpNames.Meta, "xmpmeta") || child.IsElement(PdfAXmpNames.Meta, "xapmeta"))
            {
                return FindRdf(child, ref header);
            }

            if (child.IsElement(PdfAXmpNames.Rdf, "RDF"))
            {
                return child;
            }

            if (FindRdf(child, ref header) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Gets the encoding an XML reader detects from a packet's first bytes before any declaration is read.</summary>
    /// <param name="bytes">The packet.</param>
    /// <returns><see cref="PdfAXmpPacketRepair.Utf8"/>, or another name for UTF-16, UTF-32 or EBCDIC.</returns>
    private static string DetectedEncoding(ReadOnlySpan<byte> bytes)
    {
        foreach (var start in OtherEncodingStarts)
        {
            if (bytes.StartsWith(start))
            {
                return OtherEncoding;
            }
        }

        return PdfAXmpPacketRepair.Utf8;
    }
}
