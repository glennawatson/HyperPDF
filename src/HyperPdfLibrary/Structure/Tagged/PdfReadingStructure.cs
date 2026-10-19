// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Reads a page's reading structure for screen readers and reflow: from the document's tags when the page has them,
/// otherwise from the layout, and from text recognition words for pages that are only images.
/// </summary>
public static class PdfReadingStructure
{
    /// <summary>Reads a page: its tagged structure when it has one, otherwise an order inferred from the layout.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The page's nodes; check <see cref="PdfSemanticPage.NeedsTextRecognition"/> for an image-only page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfSemanticPage Read(PdfDocument document, int pageIndex) => Read(document, pageIndex, CancellationToken.None);

    /// <summary>
    /// Reads a page like <see cref="Read(PdfDocument, int)"/>, and for a page that draws only images asks the recogniser for
    /// its words and builds the structure from them.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="recogniser">The host's text recogniser.</param>
    /// <param name="cancellationToken">Cancels the read and the recognition.</param>
    /// <returns>The page's nodes; the image-only page's words when the recogniser found some.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> or <paramref name="recogniser"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfSemanticPage> ReadAsync(PdfDocument document, int pageIndex, PdfPageRecogniser recogniser, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recogniser);
        Validate(document, pageIndex);
        await PdfPrefetcher.PrefetchAsync(document.Objects, PdfDocumentPages.GetPage(document, pageIndex).Dictionary, PdfPrefetchKind.Page, cancellationToken).ConfigureAwait(false);
        var page = Read(document, pageIndex, cancellationToken);
        if (!page.NeedsTextRecognition)
        {
            return page;
        }

        var words = await recogniser(pageIndex, cancellationToken).ConfigureAwait(false);
        return words is { Count: > 0 } ? FromOcrWords(pageIndex, words) : page;
    }

    /// <summary>Reads a page's tagged structure.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The page's nodes in logical order; none for an untagged document or a page with no tagged content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfSemanticPage ReadTagged(PdfDocument document, int pageIndex) => ReadTagged(document, pageIndex, CancellationToken.None);

    /// <summary>Works out a page's reading order from its layout, leaving out artifacts; every node is labelled as inferred.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The page's paragraphs and headings in reading order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfSemanticPage InferLayout(PdfDocument document, int pageIndex) =>
        InferLayout(document, pageIndex, PdfLayoutInference.Default, CancellationToken.None);

    /// <summary>
    /// Works out a page's reading order from its layout: tables, lists, figures with captions, sidebars, and
    /// running headers, footers and page numbers set aside as pagination artifacts. Every node is labelled as inferred.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="options">What inference may use beyond the page's text.</param>
    /// <param name="cancellationToken">Stops the work between regions.</param>
    /// <returns>The page's nodes in reading order; the pagination artifacts are in <see cref="PdfSemanticPage.Artifacts"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfSemanticPage InferLayout(PdfDocument document, int pageIndex, PdfLayoutInference options, CancellationToken cancellationToken)
    {
        Validate(document, pageIndex);
        var content = PdfDocumentTagged.GetMarkedContent(document, pageIndex);
        var items = new List<LayoutItem>(content.GlyphCount);
        var glyphs = content.Glyphs;
        for (var i = 0; i < glyphs.Length; i++)
        {
            var glyph = glyphs[i];
            if (!glyph.IsArtifact && !glyph.Bounds.IsEmpty)
            {
                items.Add(new(glyph.Bounds, i, glyph.TextStart, glyph.TextLength));
            }
        }

        var nodes = new List<PdfSemanticNode>();
        var artifacts = new List<PdfSemanticNode>();
        new InferredLayoutBuilder(PdfNodeOrigin.Inferred, pageIndex, content.Text, items, content, options).Build(nodes, artifacts, cancellationToken);
        return new(pageIndex, PdfNodeOrigin.Inferred, nodes, content) { Artifacts = artifacts };
    }

    /// <summary>
    /// Builds a page's reading structure from text recognition words, for a page that is only an image. Every node is
    /// labelled as text recognition, and its items are indices into <paramref name="words"/>.
    /// </summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="words">The recognised words, in viewer space.</param>
    /// <returns>The page's paragraphs and headings in reading order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="words"/> is <see langword="null"/>.</exception>
    public static PdfSemanticPage FromOcrWords(int pageIndex, IReadOnlyList<PdfOcrWord> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        var text = new StringBuilder();
        var items = new List<LayoutItem>(words.Count);
        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];
            if (word.Bounds.IsEmpty || string.IsNullOrEmpty(word.Text))
            {
                continue;
            }

            items.Add(new(word.Bounds, i, text.Length, word.Text.Length));
            _ = text.Append(word.Text);
        }

        var nodes = new List<PdfSemanticNode>();
        new InferredLayoutBuilder(PdfNodeOrigin.Ocr, pageIndex, text.ToString(), items).Build(nodes, [], CancellationToken.None);
        return new(pageIndex, PdfNodeOrigin.Ocr, nodes, null);
    }

    /// <summary>Reads tagged nodes or infers their layout with the operation's cancellation token.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels structure traversal and layout inference.</param>
    /// <returns>The page's reading nodes.</returns>
    private static PdfSemanticPage Read(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        var tagged = ReadTagged(document, pageIndex, cancellationToken);
        return tagged.Nodes.Count > 0 ? tagged : InferLayout(document, pageIndex, PdfLayoutInference.Default, cancellationToken);
    }

    /// <summary>Builds tagged reading nodes using the operation's cancellation token.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels structure traversal.</param>
    /// <returns>The page's tagged reading nodes.</returns>
    private static PdfSemanticPage ReadTagged(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(document, pageIndex);
        var builder = new TaggedNodeBuilder(document, pageIndex);
        var nodes = new List<PdfSemanticNode>();
        if (PdfDocumentTagged.GetStructureTree(document) is { } tree)
        {
            builder.Build(tree, nodes, cancellationToken);
        }

        return new(pageIndex, PdfNodeOrigin.Tagged, nodes, builder.Content);
    }

    /// <summary>Checks the arguments.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    private static void Validate(PdfDocument document, int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, document.PageCount);
    }
}
