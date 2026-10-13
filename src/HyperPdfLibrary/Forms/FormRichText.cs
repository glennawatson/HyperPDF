// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Finds and reads the rich text value (<c>/RV</c>) and default style (<c>/DS</c>) of a text field.</summary>
internal static class FormRichText
{
    /// <summary>Reads the rich text of a text field that has the rich text flag and a readable <c>/RV</c>.</summary>
    /// <param name="context">The widget.</param>
    /// <returns>The paragraphs; <see langword="null"/> when the field is not rich text or its <c>/RV</c> is missing or is not well formed XHTML, so the plain value is shown.</returns>
    internal static List<RichParagraph>? Read(in AppearanceContext context)
    {
        if (context.Type != PdfFieldType.Text || (context.Flags & PdfFieldFlags.RichTextOrRadiosInUnison) == 0)
        {
            return null;
        }

        var value = FieldAttributes.Find(context.Field, context.Store.Names.Intern("RV"u8));
        var root = ReadDefaultStyle(context.Field);
        var paragraphs = new List<RichParagraph>();
        if (value.AsStream() is { } stream)
        {
            return RichTextReader.TryRead(stream.DecodeToArray(), root, paragraphs) ? paragraphs : null;
        }

        return value.AsStringBytes() is { Length: > 0 } bytes && RichTextReader.TryRead(PdfText.Decode(bytes), root, paragraphs) ? paragraphs : null;
    }

    /// <summary>Reads the default style (<c>/DS</c>) of a field.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The style; none when the field has no <c>/DS</c>.</returns>
    internal static RichStyle ReadDefaultStyle(PdfDictionary field)
    {
        var value = FieldAttributes.Find(field, KnownName.DS);
        return value.AsStringBytes() is { Length: > 0 } bytes ? RichStyleReader.Apply(PdfText.Decode(bytes), RichStyle.None) : RichStyle.None;
    }

    /// <summary>Gets the text of a field's <c>/DS</c> default style.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The style text, or <see langword="null"/>.</returns>
    internal static string? GetDefaultStyleText(PdfDictionary field) =>
        FieldAttributes.Find(field, KnownName.DS).AsStringBytes() is { Length: > 0 } bytes ? PdfText.Decode(bytes) : null;
}
