// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Reads the XMP values and PDF strings the document information crosswalk compares, as veraPDF reads them.</summary>
internal static class PdfAXmpValues
{
    /// <summary>The generic language a localized text lookup accepts after <c>x-default</c>.</summary>
    private const string GenericLanguage = "x";

    /// <summary>The length of the UTF-16 byte order mark.</summary>
    private const int Utf16MarkLength = 2;

    /// <summary>The length of the UTF-8 byte order mark.</summary>
    private const int Utf8MarkLength = 3;

    /// <summary>Gets a simple property's value.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="namespaceUri">The namespace URI.</param>
    /// <param name="name">The local name.</param>
    /// <returns>The value, or <see langword="null"/> when missing or not simple.</returns>
    internal static string? Simple(PdfAXmpPacket packet, string namespaceUri, string name) =>
        packet.TryGetSimple(namespaceUri, name, out var value) ? value : null;

    /// <summary>Gets the default text of a Dublin Core language alternative, as the XMP toolkit chooses it for <c>x-default</c>.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="name">The Dublin Core property name.</param>
    /// <returns>The text, or <see langword="null"/> when there is none or the property is not a language alternative.</returns>
    internal static string? DefaultText(PdfAXmpPacket packet, string name)
    {
        if (packet.Property(PdfAXmpNames.DublinCore, name) is not { } property || (property.Form & PdfAXmpForm.AltText) == 0 || property.Children.Count == 0)
        {
            return null;
        }

        PdfAXmpNode? generic = null;
        foreach (var item in property.Children)
        {
            // The toolkit stops at an item that is not simple text with a language.
            if (LanguageOf(item) is not { } language)
            {
                return null;
            }

            if (string.Equals(language, PdfAXmpNames.DefaultLanguage, StringComparison.Ordinal))
            {
                return item.Value;
            }

            generic ??= language.StartsWith(GenericLanguage, StringComparison.Ordinal) ? item : null;
        }

        return (generic ?? property.Children[0]).Value;
    }

    /// <summary>Gets the entries of <c>dc:creator</c>.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The entries, or <see langword="null"/> when missing, not an ordered array, or holding an entry that is not simple.</returns>
    internal static List<string>? Creators(PdfAXmpPacket packet)
    {
        if (packet.Property(PdfAXmpNames.DublinCore, "creator") is not { } property || (property.Form & PdfAXmpForm.Ordered) == 0)
        {
            return null;
        }

        var creators = new List<string>(property.Children.Count);
        foreach (var item in property.Children)
        {
            if (!item.IsSimple)
            {
                return null;
            }

            creators.Add(item.Value);
        }

        return creators;
    }

    /// <summary>Decodes a PDF text string as veraPDF does: UTF-16BE or UTF-8 after a byte order mark, PDFDocEncoding otherwise.</summary>
    /// <param name="bytes">The string's bytes.</param>
    /// <returns>The text.</returns>
    internal static string DecodeText(ReadOnlySpan<byte> bytes)
    {
        if (bytes is [0xFE, 0xFF, ..])
        {
            return Encoding.BigEndianUnicode.GetString(bytes[Utf16MarkLength..]);
        }

        if (bytes is [0xEF, 0xBB, 0xBF, ..])
        {
            return Encoding.UTF8.GetString(bytes[Utf8MarkLength..]);
        }

        var builder = new StringBuilder(bytes.Length);
        foreach (var value in bytes)
        {
            _ = builder.Append(PdfText.DecodeDocByte(value));
        }

        return builder.ToString();
    }

    /// <summary>Gets the language of a language alternative item.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The language, or <see langword="null"/> when the item is not simple or has no <c>xml:lang</c> first.</returns>
    private static string? LanguageOf(PdfAXmpNode item) =>
        item.IsSimple && item.Qualifiers.Count > 0 && string.Equals(item.Qualifiers[0].Name, PdfAXmpNode.LanguageQualifier, StringComparison.Ordinal)
            ? item.Qualifiers[0].Value
            : null;
}
