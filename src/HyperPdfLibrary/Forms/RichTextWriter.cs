// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using System.Xml;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Writes plain text as the rich text value a field stores (<c>/RV</c>): the XHTML subset of ISO 32000-2 12.7.4.3, a
/// <c>body</c> in the XHTML namespace whose default style comes from the field and with one <c>p</c> for each line. The XML
/// is written forward only.
/// </summary>
internal static class RichTextWriter
{
    /// <summary>The XHTML namespace.</summary>
    private const string XhtmlNamespace = "http://www.w3.org/1999/xhtml";

    /// <summary>The characters of the XML that wraps a short value.</summary>
    private const int WrapperChars = 128;

    /// <summary>The default style a field with no <c>/DS</c> gets: 12 point Helvetica in black.</summary>
    private const string DefaultFontStyle = "font: Helvetica 12pt; color:#000000";

    /// <summary>The settings that write a fragment with no declaration, in UTF-8; shared because a writer only reads them.</summary>
    private static readonly XmlWriterSettings Settings = new()
    {
        OmitXmlDeclaration = true,
        ConformanceLevel = ConformanceLevel.Fragment,
        Encoding = new UTF8Encoding(false),
        CloseOutput = false,
        NewLineHandling = NewLineHandling.None,
    };

    /// <summary>Wraps plain text as rich text.</summary>
    /// <param name="text">The text; each line becomes a paragraph.</param>
    /// <param name="bodyStyle">The body's CSS style, from the field's <c>/DS</c>; <see langword="null"/> or empty for a default one.</param>
    /// <returns>The XHTML.</returns>
    internal static string Write(string text, string? bodyStyle)
    {
        using var stream = new PooledWriteStream(text.Length + WrapperChars);
        using (var writer = XmlWriter.Create(stream, Settings))
        {
            writer.WriteStartElement("body", XhtmlNamespace);
            writer.WriteAttributeString("style", Style(text, bodyStyle));
            foreach (var line in text.AsSpan().EnumerateLines())
            {
                writer.WriteStartElement("p", XhtmlNamespace);
                writer.WriteString(Clean(line));
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        return Encoding.UTF8.GetString(stream.WrittenSpan);
    }

    /// <summary>Builds the body's style, keeping runs of spaces when the text has them.</summary>
    /// <param name="text">The text.</param>
    /// <param name="bodyStyle">The field's default style, or <see langword="null"/>.</param>
    /// <returns>The CSS declarations.</returns>
    private static string Style(string text, string? bodyStyle)
    {
        var style = string.IsNullOrWhiteSpace(bodyStyle) ? DefaultFontStyle : bodyStyle.Trim().TrimEnd(';');
        var keepsSpaces = text.Contains("  ", StringComparison.Ordinal) || text.StartsWith(' ') || text.Contains(" \n", StringComparison.Ordinal) || text.Contains('\t');
        return keepsSpaces ? string.Create(CultureInfo.InvariantCulture, $"{style}; xfa-spacerun:yes") : style;
    }

    /// <summary>Removes the characters XML 1.0 cannot hold, which would make the value unreadable.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The line without control characters other than tab.</returns>
    private static string Clean(ReadOnlySpan<char> line)
    {
        var builder = new StringBuilder(line.Length);
        foreach (var rune in line.EnumerateRunes())
        {
            if (!rune.IsBmp || XmlConvert.IsXmlChar((char)rune.Value))
            {
                _ = builder.Append(rune.ToString());
            }
        }

        return builder.ToString();
    }
}
