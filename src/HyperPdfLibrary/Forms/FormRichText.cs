// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Finds and reads the rich text value (<c>/RV</c>) and default style (<c>/DS</c>) of a text field.</summary>
internal static class FormRichText
{
    /// <summary>Reads the rich text of a text field that has the rich text flag and a readable <c>/RV</c>.</summary>
    /// <param name="context">The widget.</param>
    /// <param name="text">Receives the paragraphs; the caller disposes it whether or not the read succeeds.</param>
    /// <returns><see langword="false"/> when the field is not rich text or its <c>/RV</c> is missing or is not well formed XHTML, so the plain value is shown.</returns>
    internal static bool TryRead(in AppearanceContext context, ref RichText text)
    {
        if (context.Type != PdfFieldType.Text || (context.Flags & PdfFieldFlags.RichTextOrRadiosInUnison) == 0)
        {
            return false;
        }

        var value = FieldAttributes.Find(context.Field, context.Store.Names.Intern("RV"u8));
        if (value.AsStream() is { } stream)
        {
            return TryReadStream(stream, ReadDefaultStyle(context.Field), ref text, context.CancellationToken);
        }

        var bytes = value.AsStringBytes();
        if (bytes.IsEmpty)
        {
            return false;
        }

        context.CancellationToken.ThrowIfCancellationRequested();
        var style = FieldAttributes.Find(context.Field, KnownName.DS).AsStringBytes();
        if (context.Fonts?.TryReadRichText(bytes, style, ref text) == true)
        {
            return true;
        }

        var decoded = ArrayPool<char>.Shared.Rent(PdfText.GetDecodedLength(bytes));
        try
        {
            var length = PdfText.Decode(bytes, decoded);
            var read = RichTextReader.TryRead(decoded.AsMemory(0, length), ReadDefaultStyle(context.Field), ref text, context.CancellationToken);
            if (read)
            {
                context.Fonts?.KeepRichText(bytes, style, ref text);
            }

            return read;
        }
        finally
        {
            ArrayPool<char>.Shared.Return(decoded);
        }
    }

    /// <summary>Reads XML from a decoded stream without copying its bytes.</summary>
    /// <param name="stream">The rich text stream.</param>
    /// <param name="root">The initial style.</param>
    /// <param name="text">Receives the paragraphs.</param>
    /// <param name="cancellationToken">Cancels decoding and reading.</param>
    /// <returns>Whether the XML can be read as rich text.</returns>
    internal static bool TryReadStream(PdfStream stream, RichStyle root, ref RichText text, CancellationToken cancellationToken)
    {
        var decoded = default(PooledBuffer);
        try
        {
            _ = stream.Decode(ref decoded, cancellationToken);
            return RichTextReader.TryRead(decoded.Array.AsMemory(0, decoded.Length), root, ref text, cancellationToken);
        }
        finally
        {
            decoded.Dispose();
        }
    }

    /// <summary>Reads the default style (<c>/DS</c>) of a field.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The style; none when the field has no <c>/DS</c>.</returns>
    internal static RichStyle ReadDefaultStyle(PdfDictionary field)
    {
        var bytes = FieldAttributes.Find(field, KnownName.DS).AsStringBytes();
        if (bytes.IsEmpty)
        {
            return RichStyle.None;
        }

        var decoded = ArrayPool<char>.Shared.Rent(PdfText.GetDecodedLength(bytes));
        try
        {
            var length = PdfText.Decode(bytes, decoded);
            return RichStyleReader.Apply(decoded.AsSpan(0, length), RichStyle.None);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(decoded);
        }
    }

    /// <summary>Gets the text of a field's <c>/DS</c> default style.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The style text, or <see langword="null"/>.</returns>
    internal static string? GetDefaultStyleText(PdfDictionary field) =>
        FieldAttributes.Find(field, KnownName.DS).AsStringBytes() is { Length: > 0 } bytes ? PdfText.Decode(bytes) : null;
}
