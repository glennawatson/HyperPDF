// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Export;

/// <summary>
/// Writes a page's text as HTML or Markdown. Bold, italic and underlined runs keep their emphasis, a line break in the text is
/// kept as a line break, and a blank line starts a new paragraph. Characters the format treats as markup are escaped.
/// </summary>
public static class PdfTextExport
{
    /// <summary>The characters Markdown reads as markup, which are escaped with a backslash.</summary>
    private const string MarkdownSpecials = "\\`*_[]#<>";

    /// <summary>Writes a page's text as an HTML fragment.</summary>
    /// <param name="page">The page's text.</param>
    /// <returns>The HTML: one <c>p</c> element per paragraph.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToHtml(PdfTextPage page) => Write(page, TextExportFormat.Html);

    /// <summary>Writes a page's text as Markdown.</summary>
    /// <param name="page">The page's text.</param>
    /// <returns>The Markdown, paragraphs separated by a blank line.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToMarkdown(PdfTextPage page) => Write(page, TextExportFormat.Markdown);

    /// <summary>Writes every page's text as HTML, each page in its own <c>section</c>.</summary>
    /// <param name="document">The document.</param>
    /// <param name="output">Receives the HTML.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>A task that completes when every page is written.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task WriteHtmlAsync(PdfDocument document, TextWriter output, CancellationToken cancellationToken)
    {
        for (var index = 0; index < document.PageCount; index++)
        {
            var page = await PdfDocumentText.GetTextPageAsync(document, index, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync($"<section data-page=\"{index + 1}\">".AsMemory(), cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(ToHtml(page).AsMemory(), cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync("</section>".AsMemory(), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Writes every page's text as Markdown, the pages separated by a horizontal rule.</summary>
    /// <param name="document">The document.</param>
    /// <param name="output">Receives the Markdown.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>A task that completes when every page is written.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task WriteMarkdownAsync(PdfDocument document, TextWriter output, CancellationToken cancellationToken)
    {
        for (var index = 0; index < document.PageCount; index++)
        {
            var page = await PdfDocumentText.GetTextPageAsync(document, index, cancellationToken).ConfigureAwait(false);
            if (index > 0)
            {
                await output.WriteLineAsync("\n---\n".AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            await output.WriteLineAsync(ToMarkdown(page).AsMemory(), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Writes the text of a page in one format.</summary>
    /// <param name="page">The page's text.</param>
    /// <param name="format">The markup language to write.</param>
    /// <returns>The text.</returns>
    private static string Write(PdfTextPage page, TextExportFormat format)
    {
        var text = page.Text;
        var sink = new TextExportSink(text.Length + text.Length);
        try
        {
            return Write(page, format, ref sink);
        }
        finally
        {
            sink.Dispose();
        }
    }

    /// <summary>Writes the text of a page into a sink.</summary>
    /// <param name="page">The page's text.</param>
    /// <param name="format">The markup language to write.</param>
    /// <param name="sink">Receives the characters.</param>
    /// <returns>The text.</returns>
    private static string Write(PdfTextPage page, TextExportFormat format, ref TextExportSink sink)
    {
        var text = page.Text;
        var emphasis = TextEmphasis.None;
        var breaks = 0;
        var started = false;
        if (format == TextExportFormat.Html)
        {
            sink.Append("<p>");
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is '\r' or '\n')
            {
                breaks = Newline(text, ref i, breaks);
                continue;
            }

            if (breaks > 0 && started)
            {
                emphasis = Close(ref sink, emphasis, format);
                Break(ref sink, format, breaks);
            }

            breaks = 0;
            started = true;
            emphasis = Change(ref sink, emphasis, EmphasisOf(page, i), format);
            Append(ref sink, text[i], format);
        }

        _ = Close(ref sink, emphasis, format);
        return started ? Finish(ref sink, format) : string.Empty;
    }

    /// <summary>Consumes one line break, and counts it.</summary>
    /// <param name="text">The text.</param>
    /// <param name="i">The index of the break's first character; moves to its last.</param>
    /// <param name="count">The breaks so far since the last visible character.</param>
    /// <returns>The new count.</returns>
    private static int Newline(string text, ref int i, int count)
    {
        if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
        {
            i++;
        }

        return count + 1;
    }

    /// <summary>Gets the emphasis of the character at a text index.</summary>
    /// <param name="page">The page's text.</param>
    /// <param name="textIndex">The index into <see cref="PdfTextPage.Text"/>.</param>
    /// <returns>The emphasis; none for generated text.</returns>
    private static TextEmphasis EmphasisOf(PdfTextPage page, int textIndex)
    {
        var index = page.CharIndexFromTextIndex(textIndex);
        if (index < 0)
        {
            return TextEmphasis.None;
        }

        var style = page.GetCharStyle(index);
        var result = style.IsBold ? TextEmphasis.Bold : TextEmphasis.None;
        result |= (style.FontFlags & FontFlags.Italic) != 0 ? TextEmphasis.Italic : TextEmphasis.None;
        return result | ((style.Decorations & PdfTextDecorations.Underline) != 0 ? TextEmphasis.Underline : TextEmphasis.None);
    }

    /// <summary>Moves from one emphasis to another by closing and opening the tags that differ.</summary>
    /// <param name="sink">The output.</param>
    /// <param name="current">The emphasis now open.</param>
    /// <param name="next">The emphasis the next character needs.</param>
    /// <param name="format">The markup language being written.</param>
    /// <returns>The emphasis that is open once the tags are written.</returns>
    private static TextEmphasis Change(ref TextExportSink sink, TextEmphasis current, TextEmphasis next, TextExportFormat format)
    {
        if (current == next)
        {
            return current;
        }

        _ = Close(ref sink, current, format);
        Open(ref sink, next, format);
        return next;
    }

    /// <summary>Closes every open emphasis.</summary>
    /// <param name="sink">The output.</param>
    /// <param name="current">The emphasis now open.</param>
    /// <param name="format">The markup language being written.</param>
    /// <returns>No emphasis.</returns>
    private static TextEmphasis Close(ref TextExportSink sink, TextEmphasis current, TextExportFormat format)
    {
        if (format == TextExportFormat.Html)
        {
            AppendIf(ref sink, current, TextEmphasis.Underline, "</u>");
            AppendIf(ref sink, current, TextEmphasis.Italic, "</em>");
            AppendIf(ref sink, current, TextEmphasis.Bold, "</strong>");
            return TextEmphasis.None;
        }

        AppendIf(ref sink, current, TextEmphasis.Italic, "*");
        AppendIf(ref sink, current, TextEmphasis.Bold, "**");
        return TextEmphasis.None;
    }

    /// <summary>Opens every emphasis in a set.</summary>
    /// <param name="sink">The output.</param>
    /// <param name="next">The emphasis to open.</param>
    /// <param name="format">The markup language being written.</param>
    private static void Open(ref TextExportSink sink, TextEmphasis next, TextExportFormat format)
    {
        if (format == TextExportFormat.Html)
        {
            AppendIf(ref sink, next, TextEmphasis.Bold, "<strong>");
            AppendIf(ref sink, next, TextEmphasis.Italic, "<em>");
            AppendIf(ref sink, next, TextEmphasis.Underline, "<u>");
            return;
        }

        AppendIf(ref sink, next, TextEmphasis.Bold, "**");
        AppendIf(ref sink, next, TextEmphasis.Italic, "*");
    }

    /// <summary>Appends a tag when a flag is set.</summary>
    /// <param name="sink">The output.</param>
    /// <param name="set">The emphasis to look in.</param>
    /// <param name="flag">The flag that wants the tag.</param>
    /// <param name="tag">The tag text.</param>
    private static void AppendIf(ref TextExportSink sink, TextEmphasis set, TextEmphasis flag, ReadOnlySpan<char> tag)
    {
        if ((set & flag) != 0)
        {
            sink.Append(tag);
        }
    }

    /// <summary>Writes a line or paragraph break.</summary>
    /// <param name="sink">The output.</param>
    /// <param name="format">The markup language being written.</param>
    /// <param name="breaks">The line breaks in the text: one is a line break, more start a paragraph.</param>
    private static void Break(ref TextExportSink sink, TextExportFormat format, int breaks)
    {
        if (format == TextExportFormat.Html)
        {
            sink.Append(breaks > 1 ? "</p>\n<p>" : "<br>\n");
            return;
        }

        sink.Append(breaks > 1 ? "\n\n" : "  \n");
    }

    /// <summary>Appends a character, escaped for the format.</summary>
    /// <param name="sink">The output.</param>
    /// <param name="c">The character.</param>
    /// <param name="format">The markup language being written.</param>
    private static void Append(ref TextExportSink sink, char c, TextExportFormat format)
    {
        if (format == TextExportFormat.Html)
        {
            var entity = HtmlEntity(c);
            if (entity.IsEmpty)
            {
                sink.Append(c);
                return;
            }

            sink.Append(entity);
            return;
        }

        if (MarkdownSpecials.Contains(c, StringComparison.Ordinal))
        {
            sink.Append('\\');
        }

        sink.Append(c);
    }

    /// <summary>Gets the HTML entity for a character that HTML reads as markup.</summary>
    /// <param name="c">The character.</param>
    /// <returns>The entity; empty when the character needs none.</returns>
    private static ReadOnlySpan<char> HtmlEntity(char c) => c switch
    {
        '&' => "&amp;",
        '<' => "&lt;",
        '>' => "&gt;",
        _ => default,
    };

    /// <summary>Wraps the finished text in the format's outer markup and makes it a string.</summary>
    /// <param name="sink">The output.</param>
    /// <param name="format">The markup language being written.</param>
    /// <returns>The result.</returns>
    private static string Finish(ref TextExportSink sink, TextExportFormat format)
    {
        if (format == TextExportFormat.Html)
        {
            sink.Append("</p>");
        }

        return sink.Build();
    }
}
