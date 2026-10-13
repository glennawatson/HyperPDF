// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Text;
using System.Xml;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Reads rich text, the XHTML subset of ISO 32000-2 12.7.4.3 (the <c>/RV</c> of a text field, the <c>/RC</c> of a markup
/// annotation), into paragraphs of styled runs. The XML is read forward only, with document type declarations prohibited
/// and no resolver, and its length and depth are bounded.
/// </summary>
internal static class RichTextReader
{
    /// <summary>The most characters of XML read.</summary>
    internal const int MaxCharacters = 1_048_576;

    /// <summary>The deepest element nesting read.</summary>
    private const int MaxDepth = 64;

    /// <summary>The name of the attribute that holds an element's CSS.</summary>
    private const string StyleAttribute = "style";

    /// <summary>The settings that read untrusted XML: no DTD, no resolver, bounded length. Creating a reader copies them, so they are shared.</summary>
    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = MaxCharacters,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        CloseInput = false,
    };

    /// <summary>The elements that start a new paragraph.</summary>
    private static readonly FrozenSet<string> ParagraphElements =
        new HashSet<string>(StringComparer.Ordinal) { "p", "div", "li", "h1", "h2", "h3", "h4", "h5", "h6" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The elements that change the style, by name.</summary>
    private static readonly FrozenDictionary<string, Func<RichStyle, RichStyle>> ElementStyles = new Dictionary<string, Func<RichStyle, RichStyle>>(StringComparer.Ordinal)
    {
        ["b"] = static style => style with { Bold = true },
        ["strong"] = static style => style with { Bold = true },
        ["i"] = static style => style with { Italic = true },
        ["em"] = static style => style with { Italic = true },
        ["u"] = static style => style with { Underline = true },
        ["s"] = static style => style with { StrikeOut = true },
        ["strike"] = static style => style with { StrikeOut = true },
        ["del"] = static style => style with { StrikeOut = true },
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Reads rich text held as a string.</summary>
    /// <param name="xhtml">The XHTML.</param>
    /// <param name="root">The style the text starts in, such as the field's default style.</param>
    /// <param name="paragraphs">Receives the paragraphs.</param>
    /// <returns><see langword="false"/> when the text is not well formed XML in the XHTML namespace, or is too long or too deeply nested.</returns>
    internal static bool TryRead(string xhtml, RichStyle root, List<RichParagraph> paragraphs)
    {
        if (xhtml.Length > MaxCharacters)
        {
            return false;
        }

        using var text = new StringReader(xhtml);
        return TryRead(XmlReader.Create(text, Settings), root, paragraphs);
    }

    /// <summary>Reads rich text held as a stream of XML bytes.</summary>
    /// <param name="xhtml">The XML bytes; a declared encoding is honoured.</param>
    /// <param name="root">The style the text starts in.</param>
    /// <param name="paragraphs">Receives the paragraphs.</param>
    /// <returns><see langword="false"/> when the text is not well formed XML in the XHTML namespace, or is too long or too deeply nested.</returns>
    internal static bool TryRead(ReadOnlyMemory<byte> xhtml, RichStyle root, List<RichParagraph> paragraphs)
    {
        if (xhtml.Length > MaxCharacters * sizeof(char))
        {
            return false;
        }

        using var stream = Compat.MemoryStreams.OpenRead(xhtml);
        return TryRead(XmlReader.Create(stream, Settings), root, paragraphs);
    }

    /// <summary>Joins the text of paragraphs, one line per paragraph.</summary>
    /// <param name="paragraphs">The paragraphs.</param>
    /// <returns>The plain text.</returns>
    internal static string ToPlainText(List<RichParagraph> paragraphs)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < paragraphs.Count; i++)
        {
            _ = i > 0 ? builder.Append('\n') : builder;
            foreach (var run in paragraphs[i].Runs)
            {
                _ = builder.Append(run.Text);
            }
        }

        return builder.ToString();
    }

    /// <summary>Reads the document with a reader, which it disposes.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="root">The style the text starts in.</param>
    /// <param name="paragraphs">Receives the paragraphs.</param>
    /// <returns><see langword="false"/> when the XML is damaged or not XHTML.</returns>
    private static bool TryRead(XmlReader reader, RichStyle root, List<RichParagraph> paragraphs)
    {
        using (reader)
        {
            try
            {
                var state = new RichTextState(root);
                return ReadNodes(reader, state, paragraphs);
            }
            catch (XmlException)
            {
                return false;
            }
        }
    }

    /// <summary>Walks the nodes of the document.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="state">The styles and the paragraph being built.</param>
    /// <param name="paragraphs">Receives the paragraphs.</param>
    /// <returns><see langword="false"/> when the document is not rich text.</returns>
    private static bool ReadNodes(XmlReader reader, RichTextState state, List<RichParagraph> paragraphs)
    {
        var sawBody = false;
        while (reader.Read())
        {
            if (reader.Depth > MaxDepth)
            {
                return false;
            }

            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                {
                    sawBody |= reader.LocalName.Equals("body", StringComparison.Ordinal);
                    StartElement(reader, state, paragraphs);
                    break;
                }

                case XmlNodeType.EndElement:
                {
                    state.EndElement(ParagraphElements.Contains(reader.LocalName), paragraphs);
                    break;
                }

                case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace:
                {
                    state.AddText(reader.Value);
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        state.EndParagraph(paragraphs);
        return sawBody;
    }

    /// <summary>Starts an element: pushes its style and, for a paragraph or line break, ends the line.</summary>
    /// <param name="reader">The reader, at the element.</param>
    /// <param name="state">The styles and the paragraph being built.</param>
    /// <param name="paragraphs">Receives paragraphs that an empty element ends.</param>
    private static void StartElement(XmlReader reader, RichTextState state, List<RichParagraph> paragraphs)
    {
        var name = reader.LocalName;
        var empty = reader.IsEmptyElement;
        var style = state.Style;
        if (ElementStyles.TryGetValue(name, out var change))
        {
            style = change(style);
        }

        var css = reader.GetAttribute(StyleAttribute);
        style = css is null ? style : RichStyleReader.Apply(css, style);
        if (name.Equals("br", StringComparison.Ordinal))
        {
            state.AddLineBreak();
            if (!empty)
            {
                state.Push(style);
            }

            return;
        }

        var paragraph = ParagraphElements.Contains(name);
        if (paragraph)
        {
            state.StartParagraph(style.Alignment, paragraphs);
        }

        if (empty)
        {
            state.EndParagraph(paragraphs);
            return;
        }

        state.Push(style);
    }
}
