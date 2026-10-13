// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Draws a text or image watermark on pages. The watermark is one form XObject (ISO 32000-2 8.10) drawn on every chosen
/// page, so it is stored once. A tagged document marks it as a pagination artifact of subtype Watermark (14.8.2.2), and an
/// optional content group (8.11) can let readers switch it off.
/// </summary>
/// <remarks>
/// A document that claims PDF/A keeps the claim or the call fails: built-in fonts, transparency and layers the level does
/// not allow are refused. Use a font that is already embedded in the document for text.
/// </remarks>
public static class PdfDocumentWatermarks
{
    /// <summary>Draws a text watermark on pages.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based indexes of the pages; repeats are ignored.</param>
    /// <param name="text">The text, drawn on one line.</param>
    /// <param name="style">The font, size and colour.</param>
    /// <param name="options">Where and how the watermark is drawn.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The text is empty, the font cannot show every character, or the style is out of range.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A page does not exist.</exception>
    /// <exception cref="InvalidOperationException">The document claims PDF/A and the watermark would break the claim.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddText(PdfDocument document, ReadOnlySpan<int> pages, string text, PdfTextWatermarkStyle style, PdfWatermarkOptions options) =>
        AddText(document, pages, text, style, options, CancellationToken.None);

    /// <summary>Draws a text watermark on pages, passing the token to the edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based indexes of the pages; repeats are ignored.</param>
    /// <param name="text">The text, drawn on one line.</param>
    /// <param name="style">The font, size and colour.</param>
    /// <param name="options">Where and how the watermark is drawn.</param>
    /// <param name="cancellationToken">Cancels the edit; the document is left unchanged.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The text is empty, the font cannot show every character, or the style is out of range.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A page does not exist.</exception>
    /// <exception cref="InvalidOperationException">The document claims PDF/A and the watermark would break the claim.</exception>
    public static void AddText(PdfDocument document, ReadOnlySpan<int> pages, string text, PdfTextWatermarkStyle style, PdfWatermarkOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        ArgumentNullException.ThrowIfNull(style);
        PdfWatermarkForms.ValidateStyle(style);
        Run(document, pages, options, new PdfWatermarkText(text, style), cancellationToken);
    }

    /// <summary>Draws a text watermark on pages after loading them with real asynchronous I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based indexes of the pages; repeats are ignored.</param>
    /// <param name="text">The text, drawn on one line.</param>
    /// <param name="style">The font, size and colour.</param>
    /// <param name="options">Where and how the watermark is drawn.</param>
    /// <param name="cancellationToken">Cancels loading and the edit; the document is left unchanged.</param>
    /// <returns>A task completing after the edit.</returns>
    public static async ValueTask AddTextAsync(
        PdfDocument document,
        ReadOnlyMemory<int> pages,
        string text,
        PdfTextWatermarkStyle style,
        PdfWatermarkOptions options,
        CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, pages, cancellationToken).ConfigureAwait(false);
        AddText(document, pages.Span, text, style, options, cancellationToken);
    }

    /// <summary>Draws an image watermark on pages.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based indexes of the pages; repeats are ignored.</param>
    /// <param name="image">The image and the width it is drawn at.</param>
    /// <param name="options">Where and how the watermark is drawn.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The size is not positive or the pixels do not fill the image.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A page does not exist.</exception>
    /// <exception cref="InvalidOperationException">The document claims PDF/A and the watermark would break the claim.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddImage(PdfDocument document, ReadOnlySpan<int> pages, PdfWatermarkBitmap image, PdfWatermarkOptions options) =>
        AddImage(document, pages, image, options, CancellationToken.None);

    /// <summary>Draws an image watermark on pages, passing the token to the edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based indexes of the pages; repeats are ignored.</param>
    /// <param name="image">The image and the width it is drawn at.</param>
    /// <param name="options">Where and how the watermark is drawn.</param>
    /// <param name="cancellationToken">Cancels the edit; the document is left unchanged.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The size is not positive or the pixels do not fill the image.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A page does not exist.</exception>
    /// <exception cref="InvalidOperationException">The document claims PDF/A and the watermark would break the claim.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddImage(PdfDocument document, ReadOnlySpan<int> pages, PdfWatermarkBitmap image, PdfWatermarkOptions options, CancellationToken cancellationToken) =>
        Run(document, pages, options, PdfWatermarkImage.Create(image), cancellationToken);

    /// <summary>Draws an image watermark on pages after loading them with real asynchronous I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based indexes of the pages; repeats are ignored.</param>
    /// <param name="image">The image and the width it is drawn at.</param>
    /// <param name="options">Where and how the watermark is drawn.</param>
    /// <param name="cancellationToken">Cancels loading and the edit; the document is left unchanged.</param>
    /// <returns>A task completing after the edit.</returns>
    public static async ValueTask AddImageAsync(PdfDocument document, ReadOnlyMemory<int> pages, PdfWatermarkBitmap image, PdfWatermarkOptions options, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, pages, cancellationToken).ConfigureAwait(false);
        AddImage(document, pages.Span, image, options, cancellationToken);
    }

    /// <summary>Checks the arguments and applies the watermark in one edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The pages.</param>
    /// <param name="options">The options.</param>
    /// <param name="content">What is drawn.</param>
    /// <param name="cancellationToken">Cancels the edit.</param>
    private static void Run(PdfDocument document, ReadOnlySpan<int> pages, PdfWatermarkOptions options, PdfWatermarkContent content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        PdfWatermarkForms.ValidateOptions(options);
        foreach (var index in pages)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)document.PageCount, nameof(pages));
        }

        PdfWatermarkForms.RequireConformance(document, options, content);
        PdfDocumentEditing.RunEdit(
            document,
            "Add watermark",
            PdfChangeKinds.PageChanges,
            new WatermarkEdit(document, pages.ToArray(), options, content, cancellationToken),
            Apply);
    }

    /// <summary>Loads the pages before the synchronous edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The pages.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>A task completing when the pages are loaded.</returns>
    private static async ValueTask PrefetchAsync(PdfDocument document, ReadOnlyMemory<int> pages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        for (var i = 0; i < pages.Length; i++)
        {
            _ = await PdfDocumentPages.GetPageAsync(document, pages.Span[i], cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Makes the watermark form and draws it on each page, inside the transaction.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="edit">The watermark.</param>
    private static void Apply(PdfEditTransaction transaction, WatermarkEdit edit)
    {
        var (document, pages, options, content, cancellationToken) = edit;
        var group = options.LayerName is { } name ? PdfOptionalContentWriter.EnsureGroup(document, transaction, name) : default;
        var form = content.Build(document, transaction, group);
        var state = PdfWatermarkForms.AddOpacity(document, transaction, options.Opacity);
        var marking = PdfStampConformance.GetMarking(document, true);
        var copies = new Dictionary<int, PdfDictionary>();
        foreach (var index in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = PdfDocumentPages.GetPage(document, index);
            if (copies.ContainsKey(index))
            {
                continue;
            }

            var copy = PdfDocumentPageOperations.CloneForEdit(transaction, page);
            copies[index] = copy;
            var placement = PdfWatermarkForms.GetPlacement(page, form.Width, form.Height, options);
            PdfPageStamping.Stamp(document, transaction, page, copy, new(form.Form, placement, options.Behind, state, marking));
        }

        PdfPageStamping.StoreAll(document, transaction, copies);
    }

    /// <summary>A watermark to apply.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Pages">The pages.</param>
    /// <param name="Options">Where and how it is drawn.</param>
    /// <param name="Content">What is drawn.</param>
    /// <param name="CancellationToken">Cancels the edit.</param>
    private sealed record WatermarkEdit(PdfDocument Document, int[] Pages, PdfWatermarkOptions Options, PdfWatermarkContent Content, CancellationToken CancellationToken);
}
