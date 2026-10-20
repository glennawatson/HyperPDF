// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Reads the rich text value (<c>/RV</c>) of a text field as words and styles, for readers that cannot show the markup.</summary>
public static class PdfFormRichValues
{
    /// <summary>Summarises a text field's rich text value.</summary>
    /// <param name="field">The field or widget dictionary; the value is looked up through its parents.</param>
    /// <param name="cancellationToken">Cancels reading the XML.</param>
    /// <returns>The summary, or <see langword="null"/> when the field has no rich text value or it is not well formed XHTML.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> is <see langword="null"/>.</exception>
    public static PdfFormRichTextSummary? Describe(PdfDictionary field, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (field.Owner is not { } store)
        {
            return null;
        }

        var value = FieldAttributes.Find(field, store.Names.Intern("RV"u8));
        var text = default(RichText);
        try
        {
            return TryRead(value, field, ref text, cancellationToken) ? Summarise(ref text) : null;
        }
        finally
        {
            text.Dispose();
        }
    }

    /// <summary>Reads the value into rich text.</summary>
    /// <param name="value">The value: a string or a stream.</param>
    /// <param name="field">The field, which gives the default style.</param>
    /// <param name="text">Receives the paragraphs.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns><see langword="true"/> when it was read.</returns>
    private static bool TryRead(PdfValue value, PdfDictionary field, ref RichText text, CancellationToken cancellationToken)
    {
        if (value.AsStream() is { } stream)
        {
            return FormRichText.TryReadStream(stream, FormRichText.ReadDefaultStyle(field), ref text, cancellationToken);
        }

        var bytes = value.AsStringBytes();
        return !bytes.IsEmpty && RichTextReader.TryRead(PdfText.Decode(bytes), FormRichText.ReadDefaultStyle(field), ref text, cancellationToken);
    }

    /// <summary>Gathers the words and the styles used.</summary>
    /// <param name="text">The rich text.</param>
    /// <returns>The summary.</returns>
    private static PdfFormRichTextSummary Summarise(ref RichText text)
    {
        var bold = false;
        var italic = false;
        var underline = false;
        var strike = false;
        for (var i = 0; i < text.RunCount; i++)
        {
            var style = text.GetRun(i).Style;
            bold |= style.Bold;
            italic |= style.Italic;
            underline |= style.Underline;
            strike |= style.StrikeOut;
        }

        return new(text.ToPlainText(), bold, italic, underline, strike, text.ParagraphCount);
    }
}
