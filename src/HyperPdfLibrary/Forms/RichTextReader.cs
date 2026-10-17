// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Xml;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Reads rich text, the XHTML subset of ISO 32000-2 12.7.4.3 (the <c>/RV</c> of a text field, the <c>/RC</c> of a markup
/// annotation), into paragraphs of styled runs. The XML is read forward only, with document type declarations prohibited
/// and no resolver, and its length and depth are bounded. Text and attribute values are read in chunks into pooled
/// buffers, so no string is made for them.
/// </summary>
internal static class RichTextReader
{
    /// <summary>The most characters of XML read.</summary>
    internal const int MaxCharacters = 1_048_576;

    /// <summary>The deepest element nesting read.</summary>
    private const int MaxDepth = 64;

    /// <summary>The factor by which a full buffer grows.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The characters read from a text node at a time.</summary>
    private const int ChunkSize = 512;

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

    /// <summary>Reads rich text held as a string.</summary>
    /// <param name="xhtml">The XHTML.</param>
    /// <param name="root">The style the text starts in, such as the field's default style.</param>
    /// <param name="text">Receives the paragraphs.</param>
    /// <param name="cancellationToken">Cancels reading XML.</param>
    /// <returns><see langword="false"/> when the text is not well formed XML in the XHTML namespace, or is too long or too deeply nested.</returns>
    internal static bool TryRead(string xhtml, RichStyle root, ref RichText text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (xhtml.Length > MaxCharacters)
        {
            return false;
        }

        using var reader = new StringReader(xhtml);
        return TryRead(XmlReader.Create(reader, Settings), root, ref text, cancellationToken);
    }

    /// <summary>Reads rich text held as characters.</summary>
    /// <param name="xhtml">The XHTML.</param>
    /// <param name="root">The style the text starts in, such as the field's default style.</param>
    /// <param name="text">Receives the paragraphs.</param>
    /// <param name="cancellationToken">Cancels reading XML.</param>
    /// <returns><see langword="false"/> when the text is not well formed XML in the XHTML namespace, or is too long or too deeply nested.</returns>
    internal static bool TryRead(ReadOnlyMemory<char> xhtml, RichStyle root, ref RichText text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (xhtml.Length > MaxCharacters)
        {
            return false;
        }

        using var reader = new MemoryTextReader(xhtml);
        return TryRead(XmlReader.Create(reader, Settings), root, ref text, cancellationToken);
    }

    /// <summary>Reads rich text held as a stream of XML bytes.</summary>
    /// <param name="xhtml">The XML bytes; a declared encoding is honoured.</param>
    /// <param name="root">The style the text starts in.</param>
    /// <param name="text">Receives the paragraphs.</param>
    /// <param name="cancellationToken">Cancels reading XML.</param>
    /// <returns><see langword="false"/> when the text is not well formed XML in the XHTML namespace, or is too long or too deeply nested.</returns>
    internal static bool TryRead(ReadOnlyMemory<byte> xhtml, RichStyle root, ref RichText text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (xhtml.Length > MaxCharacters * sizeof(char))
        {
            return false;
        }

        using var stream = Compat.MemoryStreams.OpenRead(xhtml);
        return TryRead(XmlReader.Create(stream, Settings), root, ref text, cancellationToken);
    }

    /// <summary>Reads the document with a reader, which it disposes.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="root">The style the text starts in.</param>
    /// <param name="text">Receives the paragraphs.</param>
    /// <param name="cancellationToken">Cancels reading XML.</param>
    /// <returns><see langword="false"/> when the XML is damaged or not XHTML.</returns>
    private static bool TryRead(XmlReader reader, RichStyle root, ref RichText text, CancellationToken cancellationToken)
    {
        var chunk = ArrayPool<char>.Shared.Rent(ChunkSize);
        var state = new RichTextState(root);
        try
        {
            using (reader)
            {
                return ReadNodes(reader, ref state, ref text, ref chunk, cancellationToken);
            }
        }
        catch (XmlException)
        {
            return false;
        }
        finally
        {
            state.Dispose();
            ArrayPool<char>.Shared.Return(chunk);
        }
    }

    /// <summary>Walks the nodes of the document.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="state">The styles and the paragraph being built.</param>
    /// <param name="text">Receives the paragraphs.</param>
    /// <param name="chunk">A pooled buffer to read values through; replaced by a larger one when a value needs it.</param>
    /// <param name="cancellationToken">Cancels reading XML.</param>
    /// <returns><see langword="false"/> when the document is not rich text.</returns>
    private static bool ReadNodes(XmlReader reader, ref RichTextState state, ref RichText text, ref char[] chunk, CancellationToken cancellationToken)
    {
        var sawBody = false;
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.Depth > MaxDepth)
            {
                return false;
            }

            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                {
                    sawBody |= reader.LocalName.Equals("body", StringComparison.Ordinal);
                    StartElement(reader, ref state, ref text, ref chunk, cancellationToken);
                    break;
                }

                case XmlNodeType.EndElement:
                {
                    state.EndElement(IsParagraph(reader.LocalName), ref text);
                    break;
                }

                case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace:
                {
                    AddValue(reader, ref state, ref text, chunk, cancellationToken);
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        state.EndParagraph(ref text);
        return sawBody;
    }

    /// <summary>Starts an element: pushes its style and, for a paragraph or line break, ends the line.</summary>
    /// <param name="reader">The reader, at the element.</param>
    /// <param name="state">The styles and the paragraph being built.</param>
    /// <param name="text">Receives paragraphs that an empty element ends.</param>
    /// <param name="chunk">A pooled buffer to read the style attribute through.</param>
    /// <param name="cancellationToken">Cancels reading attributes.</param>
    private static void StartElement(XmlReader reader, ref RichTextState state, ref RichText text, ref char[] chunk, CancellationToken cancellationToken)
    {
        var name = reader.LocalName;
        var empty = reader.IsEmptyElement;
        var style = ElementStyle(name, state.Style);

        if (reader.HasAttributes && reader.MoveToAttribute(StyleAttribute))
        {
            var length = ReadAttributeValue(reader, ref chunk, cancellationToken);
            _ = reader.MoveToElement();
            style = RichStyleReader.Apply(chunk.AsSpan(0, length), style);
        }

        if (name.Equals("br", StringComparison.Ordinal))
        {
            state.AddLineBreak(ref text);
            if (!empty)
            {
                state.Push(style);
            }

            return;
        }

        var paragraph = IsParagraph(name);
        if (paragraph)
        {
            state.StartParagraph(style.Alignment, ref text);
        }

        if (empty)
        {
            state.EndParagraph(ref text);
            return;
        }

        state.Push(style);
    }

    /// <summary>Adds the value of the text node the reader is at, a chunk at a time.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="state">The styles and the paragraph being built.</param>
    /// <param name="text">Receives the text.</param>
    /// <param name="chunk">A pooled buffer to read through.</param>
    /// <param name="cancellationToken">Cancels reading text.</param>
    private static void AddValue(XmlReader reader, ref RichTextState state, ref RichText text, char[] chunk, CancellationToken cancellationToken)
    {
        if (!reader.CanReadValueChunk)
        {
            state.AddText(reader.Value, ref text);
            return;
        }

        int read;
        while ((read = reader.ReadValueChunk(chunk, 0, chunk.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            state.AddText(chunk.AsSpan(0, read), ref text);
        }
    }

    /// <summary>Reads the value of the attribute the reader is at into a pooled buffer.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="buffer">The buffer; replaced by a larger pooled one when the value does not fit.</param>
    /// <param name="cancellationToken">Cancels reading attributes.</param>
    /// <returns>The number of characters in the value.</returns>
    private static int ReadAttributeValue(XmlReader reader, ref char[] buffer, CancellationToken cancellationToken)
    {
        if (!reader.CanReadValueChunk)
        {
            var value = reader.Value;
            Grow(ref buffer, value.Length);
            value.CopyTo(buffer);
            return value.Length;
        }

        var length = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (length == buffer.Length)
            {
                Grow(ref buffer, length + 1);
            }

            var read = reader.ReadValueChunk(buffer, length, buffer.Length - length);
            if (read == 0)
            {
                return length;
            }

            length += read;
        }
    }

    /// <summary>Replaces a pooled buffer with a larger one, keeping what it holds.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="minimum">The least length needed.</param>
    private static void Grow(ref char[] buffer, int minimum)
    {
        if (buffer.Length >= minimum)
        {
            return;
        }

        var larger = ArrayPool<char>.Shared.Rent(Math.Max(minimum, buffer.Length * GrowthFactor));
        buffer.AsSpan().CopyTo(larger);
        ArrayPool<char>.Shared.Return(buffer);
        buffer = larger;
    }

    /// <summary>Identifies elements that start paragraphs.</summary>
    /// <param name="name">The element's local name.</param>
    /// <returns>Whether it starts a paragraph.</returns>
    private static bool IsParagraph(string name) => name is "p" or "div" or "li" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6";

    /// <summary>Applies an element's text style.</summary>
    /// <param name="name">The element's local name.</param>
    /// <param name="style">The inherited style.</param>
    /// <returns>The element's style.</returns>
    private static RichStyle ElementStyle(string name, RichStyle style) => name switch
    {
        "b" or "strong" => style with { Bold = true },
        "i" or "em" => style with { Italic = true },
        "u" => style with { Underline = true },
        "s" or "strike" or "del" => style with { StrikeOut = true },
        _ => style,
    };
}
