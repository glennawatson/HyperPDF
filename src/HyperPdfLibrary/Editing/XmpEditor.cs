// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Sets and removes properties in an XMP packet by editing its bytes in place, so everything else in the packet
/// stays byte for byte. A property written as an element is replaced, one written as an attribute has its value
/// replaced, and a missing one is added to the first rdf:Description. A language alternative keeps its other
/// languages: only x-default and the change's language are replaced. Written elements declare their own namespace, so
/// they are well-formed wherever they land. UTF-16 and UTF-32 packets (either byte order) are edited as UTF-8 and written
/// back in their own encoding. The result is checked with an XML reader; a packet that is not well-formed, or that the edits
/// leave unchanged, gives no result and is left alone.
/// </summary>
internal static class XmpEditor
{
    /// <summary>The largest packet edited, in bytes; real XMP packets are a few kilobytes.</summary>
    private const int MaxPacketLength = 1 << 24;

    /// <summary>The length of <c>xmlns:</c>.</summary>
    private const int XmlnsLength = 6;

    /// <summary>The reader settings for the well-formed check, shared because a reader only reads them.</summary>
    private static readonly XmlReaderSettings WellFormedSettings = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxPacketLength, };

    /// <summary>Gets the RDF namespace.</summary>
    private static ReadOnlySpan<byte> RdfNamespace => "http://www.w3.org/1999/02/22-rdf-syntax-ns#"u8;

    /// <summary>Gets the attribute prefix of a namespace declaration.</summary>
    private static ReadOnlySpan<byte> Xmlns => "xmlns:"u8;

    /// <summary>Gets the bytes that may follow an element or attribute name.</summary>
    private static ReadOnlySpan<byte> NameEnds => " \t\r\n>/="u8;

    /// <summary>Gets the XML whitespace bytes.</summary>
    private static ReadOnlySpan<byte> Whitespace => " \t\r\n"u8;

    /// <summary>Applies changes to a packet.</summary>
    /// <param name="packet">The packet's bytes.</param>
    /// <param name="changes">The changes.</param>
    /// <returns>The new packet, or <see langword="null"/> when it could not be edited safely or the edits changed nothing.</returns>
    internal static byte[]? Apply(ReadOnlySpan<byte> packet, ReadOnlySpan<XmpChange> changes) =>
        TryApply(packet, changes, out var edited) ? edited : null;

    /// <summary>Applies changes to a packet, telling a packet that cannot be edited apart from one that already holds the values.</summary>
    /// <param name="packet">The packet's bytes.</param>
    /// <param name="changes">The changes.</param>
    /// <param name="edited">The new packet, or <see langword="null"/> when the edits changed nothing.</param>
    /// <returns><see langword="false"/> when the packet could not be edited safely.</returns>
    internal static bool TryApply(ReadOnlySpan<byte> packet, ReadOnlySpan<XmpChange> changes, out byte[]? edited)
    {
        edited = null;
        if (packet.IsEmpty || packet.Length > MaxPacketLength)
        {
            return false;
        }

        var format = XmpTranscoder.Detect(packet, out var markLength);
        var original = packet.ToArray();
        if (!IsWellFormed(original))
        {
            return false;
        }

        // UTF-16 and UTF-32 packets are edited as UTF-8 and written back in their own encoding and byte order mark.
        var utf8 = format == XmpEncoding.Utf8 ? original : XmpTranscoder.ToUtf8(packet[markLength..], format);
        if (utf8 is null)
        {
            return false;
        }

        var applied = ApplyAll(utf8, changes);
        var result = format == XmpEncoding.Utf8 ? applied : XmpTranscoder.FromUtf8(applied, format, packet[..markLength]);
        if (result is null || !IsWellFormed(result))
        {
            return false;
        }

        edited = packet.SequenceEqual(result) ? null : result;
        return true;
    }

    /// <summary>Determines whether bytes are well-formed XML.</summary>
    /// <param name="xml">The bytes.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    internal static bool IsWellFormed(byte[] xml)
    {
        if (xml.Length > MaxPacketLength)
        {
            return false;
        }

        using var stream = new MemoryStream(xml, false);
        try
        {
            using var reader = XmlReader.Create(stream, WellFormedSettings);
            while (reader.Read())
            {
                // Reading to the end is the check.
            }

            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    /// <summary>Finds the prefix a packet binds to a namespace.</summary>
    /// <param name="xmp">The packet.</param>
    /// <param name="ns">The namespace URI.</param>
    /// <returns>The prefix, or <see langword="null"/> when the namespace is not declared.</returns>
    internal static byte[]? FindPrefix(byte[] xmp, ReadOnlySpan<byte> ns)
    {
        var from = 0;
        while (from < xmp.Length)
        {
            var found = xmp.AsSpan(from).IndexOf(ns);
            if (found < 0)
            {
                return null;
            }

            var at = from + found;
            from = at + 1;
            if (at > 0 && at + ns.Length < xmp.Length && xmp[at - 1] is (byte)'"' or (byte)'\'' && xmp[at + ns.Length] == xmp[at - 1]
                && ReadDeclaredPrefix(xmp, at - 1) is { } prefix)
            {
                return prefix;
            }
        }

        return null;
    }

    /// <summary>Finds a name followed by a byte that ends names.</summary>
    /// <param name="xmp">The packet.</param>
    /// <param name="name">The name, with any leading <c>&lt;</c> or <c>&lt;/</c>.</param>
    /// <param name="from">The index to search from.</param>
    /// <returns>The index, or -1.</returns>
    internal static int FindName(byte[] xmp, byte[] name, int from)
    {
        while (from < xmp.Length)
        {
            var found = xmp.AsSpan(from).IndexOf(name);
            if (found < 0)
            {
                return -1;
            }

            var at = from + found;
            var after = at + name.Length;
            if (after < xmp.Length && NameEnds.Contains(xmp[after]))
            {
                return at;
            }

            from = at + 1;
        }

        return -1;
    }

    /// <summary>Replaces a range of bytes.</summary>
    /// <param name="source">The bytes.</param>
    /// <param name="start">The first index replaced.</param>
    /// <param name="end">The index after the last replaced.</param>
    /// <param name="insert">The new bytes.</param>
    /// <returns>The new array.</returns>
    internal static byte[] Splice(byte[] source, int start, int end, ReadOnlySpan<byte> insert)
    {
        var result = new byte[source.Length - (end - start) + insert.Length];
        source.AsSpan(0, start).CopyTo(result);
        insert.CopyTo(result.AsSpan(start));
        source.AsSpan(end).CopyTo(result.AsSpan(start + insert.Length));
        return result;
    }

    /// <summary>Applies every change to UTF-8 packet bytes.</summary>
    /// <param name="utf8">The packet.</param>
    /// <param name="changes">The changes.</param>
    /// <returns>The edited packet.</returns>
    private static byte[] ApplyAll(byte[] utf8, ReadOnlySpan<XmpChange> changes)
    {
        var current = utf8;
        var rdf = FindPrefix(current, RdfNamespace) ?? "rdf"u8.ToArray();
        foreach (var change in changes)
        {
            current = ApplyOne(current, change, rdf) ?? current;
        }

        return current;
    }

    /// <summary>Applies one change.</summary>
    /// <param name="xmp">The packet.</param>
    /// <param name="change">The change.</param>
    /// <param name="rdf">The packet's RDF prefix.</param>
    /// <returns>The new packet, or <see langword="null"/> when nothing changed.</returns>
    private static byte[]? ApplyOne(byte[] xmp, XmpChange change, byte[] rdf)
    {
        var property = change.Property;
        var prefix = FindPrefix(xmp, Encoding.UTF8.GetBytes(property.Namespace)) ?? Encoding.UTF8.GetBytes(property.Prefix);
        var name = Concat(prefix, ":"u8, Encoding.UTF8.GetBytes(property.LocalName));
        var remove = change.Value.Length == 0;
        if (TryFindElement(xmp, name, out var start, out var end))
        {
            if (!remove && property.Shape == XmpShape.Alternative && ReplaceAlternatives(xmp, start, end, change, rdf) is { } merged)
            {
                return merged;
            }

            return Splice(xmp, start, end, remove ? [] : BuildElement(change, prefix, name, rdf));
        }

        if (TryFindAttribute(xmp, name, out var attributeStart, out var valueStart, out var valueEnd))
        {
            return remove ? Splice(xmp, attributeStart, valueEnd + 1, []) : Splice(xmp, valueStart, valueEnd, Escape(change.Value));
        }

        return remove ? null : Insert(xmp, BuildElement(change, prefix, name, rdf), rdf);
    }

    /// <summary>Adds an element to the first rdf:Description, or in a new one when every description is empty.</summary>
    /// <param name="xmp">The packet.</param>
    /// <param name="element">The element.</param>
    /// <param name="rdf">The RDF prefix.</param>
    /// <returns>The new packet, or <see langword="null"/> when the packet has no RDF body.</returns>
    private static byte[]? Insert(byte[] xmp, byte[] element, byte[] rdf)
    {
        var close = xmp.AsSpan().IndexOf(Concat("</"u8, rdf, ":Description>"u8));
        if (close >= 0)
        {
            return Splice(xmp, close, close, element);
        }

        var end = xmp.AsSpan().IndexOf(Concat("</"u8, rdf, ":RDF>"u8));
        if (end < 0)
        {
            return null;
        }

        var description = Concat(Concat("<"u8, rdf, ":Description "u8), Concat(rdf, ":about=\"\">"u8, element), Concat("</"u8, rdf, ":Description>"u8));
        return Splice(xmp, end, end, description);
    }

    /// <summary>Writes a property element that declares its own namespace.</summary>
    /// <param name="change">The change.</param>
    /// <param name="prefix">The namespace prefix.</param>
    /// <param name="name">The qualified name.</param>
    /// <param name="rdf">The RDF prefix.</param>
    /// <returns>The element's bytes.</returns>
    private static byte[] BuildElement(XmpChange change, byte[] prefix, byte[] name, byte[] rdf)
    {
        var text = new StringBuilder();
        var rdfPrefix = Encoding.UTF8.GetString(rdf);
        var qualified = Encoding.UTF8.GetString(name);
        _ = text.Append('<').Append(qualified).Append(" xmlns:").Append(Encoding.UTF8.GetString(prefix)).Append("=\"")
            .Append(change.Property.Namespace).Append("\">");
        var value = EscapeText(change.Value);
        _ = change.Property.Shape switch
        {
            XmpShape.Alternative => text.Append('<').Append(rdfPrefix).Append(":Alt><").Append(rdfPrefix).Append(":li xml:lang=\"x-default\">")
                .Append(value).Append("</").Append(rdfPrefix).Append(":li></").Append(rdfPrefix).Append(":Alt>"),
            XmpShape.Sequence => text.Append('<').Append(rdfPrefix).Append(":Seq><").Append(rdfPrefix).Append(":li>")
                .Append(value).Append("</").Append(rdfPrefix).Append(":li></").Append(rdfPrefix).Append(":Seq>"),
            _ => text.Append(value),
        };
        _ = text.Append("</").Append(qualified).Append('>');
        return Encoding.UTF8.GetBytes(text.ToString());
    }

    /// <summary>
    /// Sets the x-default entry of a language alternative and the entry of the change's language, keeping every other
    /// language as it was. An x-default entry is added when the alternative has none.
    /// </summary>
    /// <param name="xmp">The packet.</param>
    /// <param name="start">The index of the property element's start tag.</param>
    /// <param name="end">The index after the property element.</param>
    /// <param name="change">The change.</param>
    /// <param name="rdf">The RDF prefix.</param>
    /// <returns>The new packet, or <see langword="null"/> when the property is not a written-out alternative.</returns>
    private static byte[]? ReplaceAlternatives(byte[] xmp, int start, int end, XmpChange change, byte[] rdf)
    {
        var value = Escape(change.Value);
        var language = change.Language is null ? [] : Encoding.UTF8.GetBytes(change.Language);
        var current = xmp;
        var limit = end;
        var cursor = start;
        var replacedDefault = false;
        while (TryFindEntry(current, cursor, limit, rdf, language, out var entry))
        {
            current = Splice(current, entry.ContentStart, entry.ContentEnd, value);
            limit += value.Length - (entry.ContentEnd - entry.ContentStart);
            cursor = entry.ContentStart + value.Length;
            replacedDefault |= entry.IsDefault;
        }

        return replacedDefault ? current : AddDefaultEntry(current, start, limit, rdf, value);
    }

    /// <summary>Adds an x-default entry at the start of an alternative.</summary>
    /// <param name="xmp">The packet.</param>
    /// <param name="start">The index of the property element's start tag.</param>
    /// <param name="limit">The index after the property element.</param>
    /// <param name="rdf">The RDF prefix.</param>
    /// <param name="value">The escaped text.</param>
    /// <returns>The new packet, or <see langword="null"/> when the property has no <c>rdf:Alt</c> start tag.</returns>
    private static byte[]? AddDefaultEntry(byte[] xmp, int start, int limit, byte[] rdf, byte[] value)
    {
        var alt = FindName(xmp, Concat("<"u8, rdf, ":Alt"u8), start);
        if (alt < 0 || alt >= limit)
        {
            return null;
        }

        var close = xmp.AsSpan(alt).IndexOf((byte)'>');
        if (close < 0 || xmp[alt + close - 1] == (byte)'/')
        {
            return null;
        }

        var entry = Concat(Concat("<"u8, rdf, ":li xml:lang=\"x-default\">"u8), value, Concat("</"u8, rdf, ":li>"u8));
        return Splice(xmp, alt + close + 1, alt + close + 1, entry);
    }

    /// <summary>Finds the next list entry whose language is x-default or the given language.</summary>
    /// <param name="xmp">The packet.</param>
    /// <param name="from">The index to search from.</param>
    /// <param name="limit">The index after the property element.</param>
    /// <param name="rdf">The RDF prefix.</param>
    /// <param name="language">An extra language to match, or empty.</param>
    /// <param name="entry">The entry found.</param>
    /// <returns><see langword="true"/> when found.</returns>
    private static bool TryFindEntry(byte[] xmp, int from, int limit, byte[] rdf, byte[] language, out AlternativeEntry entry)
    {
        var open = Concat("<"u8, rdf, ":li"u8);
        var closeTag = Concat("</"u8, rdf, ":li>"u8);
        entry = default;
        for (var at = FindName(xmp, open, from); at >= 0 && at < limit; at = FindName(xmp, open, at + 1))
        {
            var tagEnd = xmp.AsSpan(at).IndexOf((byte)'>');
            var close = tagEnd < 0 ? -1 : xmp.AsSpan(at + tagEnd).IndexOf(closeTag);
            if (close < 0 || xmp[at + tagEnd - 1] == (byte)'/')
            {
                continue;
            }

            var tag = xmp.AsSpan(at, tagEnd);
            var entryLanguage = ReadLanguage(tag);
            var isDefault = entryLanguage.SequenceEqual("x-default"u8);
            if (!isDefault && (language.Length == 0 || !Ascii.EqualsIgnoreCase(entryLanguage, language)))
            {
                continue;
            }

            entry = new(at + tagEnd + 1, at + tagEnd + close, isDefault);
            return true;
        }

        return false;
    }

    /// <summary>Reads the <c>xml:lang</c> value of a start tag.</summary>
    /// <param name="tag">The start tag's bytes.</param>
    /// <returns>The language, or empty when the tag has none.</returns>
    private static ReadOnlySpan<byte> ReadLanguage(ReadOnlySpan<byte> tag)
    {
        var at = tag.IndexOf("xml:lang"u8);
        if (at < 0)
        {
            return [];
        }

        var position = at + "xml:lang"u8.Length;
        while (position < tag.Length && (Whitespace.Contains(tag[position]) || tag[position] == (byte)'='))
        {
            position++;
        }

        if (position >= tag.Length || tag[position] is not ((byte)'"' or (byte)'\''))
        {
            return [];
        }

        var close = tag[(position + 1)..].IndexOf(tag[position]);
        return close < 0 ? [] : tag.Slice(position + 1, close);
    }

    /// <summary>Finds a property element: its start tag through its end tag, or a self-closing tag.</summary>
    /// <param name="xmp">The packet.</param>
    /// <param name="name">The qualified name.</param>
    /// <param name="start">The index of the start tag's <c>&lt;</c>.</param>
    /// <param name="end">The index after the element.</param>
    /// <returns><see langword="true"/> when found.</returns>
    private static bool TryFindElement(byte[] xmp, byte[] name, out int start, out int end)
    {
        start = FindName(xmp, Concat("<"u8, name, []), 0);
        end = -1;
        if (start < 0)
        {
            return false;
        }

        var close = xmp.AsSpan(start).IndexOf((byte)'>');
        if (close < 0)
        {
            return false;
        }

        close += start;
        if (xmp[close - 1] == (byte)'/')
        {
            end = close + 1;
            return true;
        }

        var endTag = FindName(xmp, Concat("</"u8, name, []), close);
        var endClose = endTag < 0 ? -1 : xmp.AsSpan(endTag).IndexOf((byte)'>');
        end = endClose < 0 ? -1 : endTag + endClose + 1;
        return end > 0;
    }

    /// <summary>Finds a property written as an attribute of rdf:Description.</summary>
    /// <param name="xmp">The packet.</param>
    /// <param name="name">The qualified name.</param>
    /// <param name="attributeStart">The index of the whitespace before the attribute.</param>
    /// <param name="valueStart">The index of the value's first byte.</param>
    /// <param name="valueEnd">The index of the closing quote.</param>
    /// <returns><see langword="true"/> when found.</returns>
    private static bool TryFindAttribute(byte[] xmp, byte[] name, out int attributeStart, out int valueStart, out int valueEnd)
    {
        attributeStart = -1;
        valueStart = -1;
        valueEnd = -1;
        for (var at = FindName(xmp, name, 0); at > 0; at = FindName(xmp, name, at + 1))
        {
            if (!Whitespace.Contains(xmp[at - 1]))
            {
                continue;
            }

            var position = SkipWhitespace(xmp, at + name.Length);
            if (position >= xmp.Length || xmp[position] != (byte)'=')
            {
                continue;
            }

            position = SkipWhitespace(xmp, position + 1);
            if (position >= xmp.Length || xmp[position] is not ((byte)'"' or (byte)'\''))
            {
                continue;
            }

            var close = xmp.AsSpan(position + 1).IndexOf(xmp[position]);
            if (close < 0)
            {
                return false;
            }

            attributeStart = at - 1;
            valueStart = position + 1;
            valueEnd = valueStart + close;
            return true;
        }

        return false;
    }

    /// <summary>Reads the prefix of an <c>xmlns:prefix=</c> attribute that ends just before a quote.</summary>
    /// <param name="xmp">The packet.</param>
    /// <param name="quote">The index of the value's opening quote.</param>
    /// <returns>The prefix, or <see langword="null"/> when the attribute is not a namespace declaration.</returns>
    private static byte[]? ReadDeclaredPrefix(byte[] xmp, int quote)
    {
        var position = SkipWhitespaceBack(xmp, quote - 1);
        if (position < 0 || xmp[position] != (byte)'=')
        {
            return null;
        }

        position = SkipWhitespaceBack(xmp, position - 1);
        var nameEnd = position + 1;
        while (position >= 0 && !Whitespace.Contains(xmp[position]) && xmp[position] != (byte)'<')
        {
            position--;
        }

        var attribute = xmp.AsSpan(position + 1, nameEnd - position - 1);
        return attribute.StartsWith(Xmlns) && attribute.Length > XmlnsLength ? attribute[XmlnsLength..].ToArray() : null;
    }

    /// <summary>Skips whitespace backwards.</summary>
    /// <param name="xmp">The packet.</param>
    /// <param name="position">The index to start at.</param>
    /// <returns>The last index at or before <paramref name="position"/> that is not whitespace, or -1.</returns>
    private static int SkipWhitespaceBack(byte[] xmp, int position)
    {
        while (position >= 0 && Whitespace.Contains(xmp[position]))
        {
            position--;
        }

        return position;
    }

    /// <summary>Skips whitespace.</summary>
    /// <param name="xmp">The packet.</param>
    /// <param name="position">The index to start at.</param>
    /// <returns>The first index that is not whitespace.</returns>
    private static int SkipWhitespace(byte[] xmp, int position)
    {
        while (position < xmp.Length && Whitespace.Contains(xmp[position]))
        {
            position++;
        }

        return position;
    }

    /// <summary>Joins three byte runs.</summary>
    /// <param name="first">The first run.</param>
    /// <param name="second">The second run.</param>
    /// <param name="third">The third run.</param>
    /// <returns>The joined bytes.</returns>
    private static byte[] Concat(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, ReadOnlySpan<byte> third) => [.. first, .. second, .. third];

    /// <summary>Escapes text for an attribute value or element content, as UTF-8.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] Escape(string value) => Encoding.UTF8.GetBytes(EscapeText(value));

    /// <summary>Escapes the characters XML reserves.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The escaped text.</returns>
    private static string EscapeText(string value)
    {
        if (value.AsSpan().IndexOfAny("&<>\"'") < 0)
        {
            return value;
        }

        var text = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            _ = c switch
            {
                '&' => text.Append("&amp;"),
                '<' => text.Append("&lt;"),
                '>' => text.Append("&gt;"),
                '"' => text.Append("&quot;"),
                '\'' => text.Append("&apos;"),
                _ => text.Append(c),
            };
        }

        return text.ToString();
    }
}
