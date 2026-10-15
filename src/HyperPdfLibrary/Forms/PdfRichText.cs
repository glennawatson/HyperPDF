// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Reads the rich text of fields (<c>/RV</c>) and annotations (<c>/RC</c>), the XHTML subset of ISO 32000-2 12.7.4.3.</summary>
public static class PdfRichText
{
    /// <summary>Gets the plain text of rich text: its paragraphs and line breaks as lines, without the styling.</summary>
    /// <param name="xhtml">The XHTML, such as the value of an annotation's <c>/RC</c>.</param>
    /// <returns>The text; <see langword="null"/> when the value is not well formed rich text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="xhtml"/> is <see langword="null"/>.</exception>
    public static string? GetPlainText(string xhtml)
    {
        ArgumentNullException.ThrowIfNull(xhtml);
        var text = default(RichText);
        try
        {
            return RichTextReader.TryRead(xhtml, RichStyle.None, ref text, CancellationToken.None) ? text.ToPlainText() : null;
        }
        finally
        {
            text.Dispose();
        }
    }

    /// <summary>
    /// Gets the text a pop-up window shows for a markup annotation: the plain text of its rich text (<c>/RC</c>) when that is
    /// well formed, otherwise its <c>/Contents</c>.
    /// </summary>
    /// <param name="annotation">The annotation dictionary.</param>
    /// <returns>The text; <see langword="null"/> when the annotation has neither.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static string? GetPopupText(PdfDictionary annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return ReadRichContents(annotation.Get(KnownName.RC)) ?? annotation.GetText(KnownName.Contents);
    }

    /// <summary>Reads the plain text of an <c>/RC</c> value, which is a text string or a text stream.</summary>
    /// <param name="rich">The value.</param>
    /// <returns>The text; <see langword="null"/> when the value is missing or not well formed.</returns>
    private static string? ReadRichContents(PdfValue rich)
    {
        if (rich.AsStream() is { } stream)
        {
            var text = default(RichText);
            try
            {
                return FormRichText.TryReadStream(stream, RichStyle.None, ref text, CancellationToken.None) ? text.ToPlainText() : null;
            }
            finally
            {
                text.Dispose();
            }
        }

        return rich.AsStringBytes() is { Length: > 0 } bytes ? GetPlainText(PdfText.Decode(bytes)) : null;
    }
}
