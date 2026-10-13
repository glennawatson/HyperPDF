// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Moves, scales or turns the content of pages with a matrix, optionally clipped, and moves the annotations with it. The
/// page's own content streams are kept: the transform is a <c>q</c>, a clip, a <c>cm</c> and a closing <c>Q</c> around
/// them (ISO 32000-2 8.3.4, 8.5.4). Destinations and the page boxes are not changed.
/// </summary>
public static class PdfDocumentContentTransforms
{
    /// <summary>Transforms the content of pages.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based indexes of the pages; repeats are ignored.</param>
    /// <param name="matrix">The matrix in the page's user space, as the six numbers of a <c>cm</c> operator.</param>
    /// <param name="options">The clip and what happens to the annotations.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A page does not exist.</exception>
    /// <exception cref="ArgumentException">The matrix is not finite or cannot be undone, or the clip is empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void TransformPages(PdfDocument document, ReadOnlySpan<int> pages, Matrix3x2 matrix, PdfContentTransformOptions options) =>
        TransformPages(document, pages, matrix, options, CancellationToken.None);

    /// <summary>Transforms the content of pages, passing the token to the edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based indexes of the pages; repeats are ignored.</param>
    /// <param name="matrix">The matrix in the page's user space, as the six numbers of a <c>cm</c> operator.</param>
    /// <param name="options">The clip and what happens to the annotations.</param>
    /// <param name="cancellationToken">Cancels the edit; the document is left unchanged.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A page does not exist.</exception>
    /// <exception cref="ArgumentException">The matrix is not finite or cannot be undone, or the clip is empty.</exception>
    public static void TransformPages(PdfDocument document, ReadOnlySpan<int> pages, Matrix3x2 matrix, PdfContentTransformOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(document, pages, matrix, options);
        PdfDocumentEditing.RunEdit(
            document,
            "Transform page content",
            PdfChangeKinds.PageChanges,
            new TransformEdit(document, pages.ToArray(), matrix, options, cancellationToken),
            Apply);
    }

    /// <summary>Transforms the content of pages after loading them with real asynchronous I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based indexes of the pages; repeats are ignored.</param>
    /// <param name="matrix">The matrix in the page's user space.</param>
    /// <param name="options">The clip and what happens to the annotations.</param>
    /// <param name="cancellationToken">Cancels loading and the edit; the document is left unchanged.</param>
    /// <returns>A task completing after the edit.</returns>
    public static async ValueTask TransformPagesAsync(PdfDocument document, ReadOnlyMemory<int> pages, Matrix3x2 matrix, PdfContentTransformOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        for (var i = 0; i < pages.Length; i++)
        {
            _ = await PdfDocumentPages.GetPageAsync(document, pages.Span[i], cancellationToken).ConfigureAwait(false);
        }

        TransformPages(document, pages.Span, matrix, options, cancellationToken);
    }

    /// <summary>Checks the arguments.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The pages.</param>
    /// <param name="matrix">The matrix.</param>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentOutOfRangeException">A page does not exist.</exception>
    /// <exception cref="ArgumentException">The matrix or the clip is not usable.</exception>
    private static void Validate(PdfDocument document, ReadOnlySpan<int> pages, Matrix3x2 matrix, PdfContentTransformOptions options)
    {
        foreach (var index in pages)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)document.PageCount, nameof(pages));
        }

        if (!IsUsable(matrix))
        {
            throw new ArgumentException("The matrix must be finite and must not collapse the page.", nameof(matrix));
        }

        if (options.Clip is { } clip && !PdfPage.IsUsableBox(clip))
        {
            throw new ArgumentException("The clip must be a finite rectangle with an area.", nameof(options));
        }
    }

    /// <summary>Determines whether a matrix is finite and can be undone.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <returns><see langword="true"/> when usable.</returns>
    private static bool IsUsable(Matrix3x2 matrix) =>
        float.IsFinite(matrix.M11) && float.IsFinite(matrix.M12) && float.IsFinite(matrix.M21)
        && float.IsFinite(matrix.M22) && float.IsFinite(matrix.M31) && float.IsFinite(matrix.M32)
        && Matrix3x2.Invert(matrix, out _);

    /// <summary>Writes the clip and matrix operators.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <param name="clip">The clip, or <see langword="null"/>.</param>
    /// <returns>The operators that open the transformed group.</returns>
    private static byte[] WritePrefix(Matrix3x2 matrix, PdfRectangle? clip)
    {
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            if (clip is { } box)
            {
                builder.Rectangle(box.Left, box.Bottom, box.Width, box.Height);
                builder.Clip();
                builder.EndPath();
            }

            builder.Transform(matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32);
            return builder.ToArray();
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Transforms the pages inside the transaction.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="edit">The transform.</param>
    private static void Apply(PdfEditTransaction transaction, TransformEdit edit)
    {
        var (document, pages, matrix, options, cancellationToken) = edit;
        var prefix = WritePrefix(matrix, options.Clip);
        var copies = new Dictionary<int, PdfDictionary>();
        foreach (var index in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (copies.ContainsKey(index))
            {
                continue;
            }

            var page = PdfDocumentPages.GetPage(document, index);
            var copy = PdfDocumentPageOperations.CloneForEdit(transaction, page);
            PdfPageStamping.Wrap(transaction, document.Objects, copy, prefix, "Q\n"u8);
            if (options.TransformAnnotations && page.Dictionary.GetArray(KnownName.Annots) is { Count: > 0 } annotations)
            {
                copy.Set(KnownName.Annots, PdfValue.FromArray(PdfAnnotationTransform.Transform(transaction, document.Objects, annotations, matrix)));
            }

            copies[index] = copy;
        }

        PdfPageStamping.StoreAll(document, transaction, copies);
    }

    /// <summary>A transform to apply.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Pages">The pages.</param>
    /// <param name="Matrix">The matrix.</param>
    /// <param name="Options">The clip and annotation choice.</param>
    /// <param name="CancellationToken">Cancels the edit.</param>
    private sealed record TransformEdit(PdfDocument Document, int[] Pages, Matrix3x2 Matrix, PdfContentTransformOptions Options, CancellationToken CancellationToken);
}
