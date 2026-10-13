// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Moves large inline images out of a page's content into image XObjects (ISO 32000-2 §8.9.7). Each <c>BI ... EI</c> is
/// replaced by <c>/Name Do</c>; an inline image and an image XObject are painted into the unit square of the same current
/// transformation matrix, so the page draws the same. An image that cannot be expressed as an XObject, because it names a
/// colour space the page resources lack, stays inline.
/// </summary>
internal static class InlineImageExternaliser
{
    /// <summary>Gets the operator and line ends put around a replacement, so it is a token of its own.</summary>
    private static ReadOnlySpan<byte> DoOperator => " Do\n"u8;

    /// <summary>Replaces the inline images of a page's content that are larger than a threshold.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="content">The page's decoded content.</param>
    /// <param name="resources">The page's resources, which the new XObjects are added to.</param>
    /// <param name="threshold">The data size in bytes above which an image is moved.</param>
    /// <param name="transaction">The transaction the new objects are added in.</param>
    /// <param name="output">Receives the new content when at least one image was moved.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The number of images moved; the output is untouched when it is zero.</returns>
    internal static int Externalise(
        PdfObjectStore store,
        ReadOnlySpan<byte> content,
        PageResourceEdit resources,
        int threshold,
        PdfEditTransaction transaction,
        ref PooledBuffer output,
        CancellationToken cancellationToken)
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, store.Names, operands);
        var copied = 0;
        var start = 0;
        var moved = 0;
        while (reader.Next(out var op))
        {
            var end = reader.Position;
            if (op == ContentOperator.BeginInlineImage
                && TryMove(store, ref reader, resources, threshold, transaction, cancellationToken) is { } name)
            {
                output.Write(content[copied..start]);
                output.WriteByte((byte)'\n');
                PdfSyntax.WriteName(ref output, store.Names.GetSpelling(name));
                output.Write(DoOperator);
                copied = end;
                moved++;
            }

            start = end;
        }

        if (moved > 0)
        {
            output.Write(content[copied..]);
        }

        return moved;
    }

    /// <summary>Moves the inline image the reader has just read, when it is large enough and can be an XObject.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="reader">The reader, after the image's <c>EI</c>.</param>
    /// <param name="resources">The page's resources.</param>
    /// <param name="threshold">The data size in bytes above which an image is moved.</param>
    /// <param name="transaction">The transaction the new object is added in.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The XObject's resource name, or <see langword="null"/> when the image stays inline.</returns>
    private static PdfName? TryMove(PdfObjectStore store, ref ContentReader reader, PageResourceEdit resources, int threshold, PdfEditTransaction transaction, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = reader.Content[reader.InlineImageDictionary];
        var data = reader.Content[reader.InlineImageData];
        if (data.Length <= threshold || InlineImageDictionary.Build(store, entries, resources) is not { } dictionary)
        {
            return null;
        }

        // An unfiltered image has an exact size; the scan for EI can leave a trailing line end in the range.
        if (InlineImageSize.TryGetDataLength(entries, out var exact) && exact < data.Length)
        {
            data = data[..(int)exact];
        }

        var id = transaction.Add(PdfValue.FromStream(new(dictionary, data.ToArray())));
        return resources.AddImage(id);
    }
}
