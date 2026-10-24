// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Documents;

/// <summary>An opened document. Implementations are safe to call from any thread.</summary>
public interface IDocument : IDisposable
{
    /// <summary>Gets the file path the document was opened from.</summary>
    string FilePath { get; }

    /// <summary>Gets the number of pages.</summary>
    int PageCount { get; }

    /// <summary>Gets a value indicating whether the document has been disposed.</summary>
    bool IsDisposed { get; }

    /// <summary>Gets an optional document feature when the engine supports it. <see cref="DocumentFeature{TFeature}"/> gives it its interface type.</summary>
    /// <param name="featureType">The feature interface type.</param>
    /// <returns>The feature, or <see langword="null"/> when it is unsupported.</returns>
    IDocumentFeature? GetFeature(Type featureType)
    {
        ArgumentNullException.ThrowIfNull(featureType);
        return featureType.IsInstanceOfType(this) ? this as IDocumentFeature : null;
    }

    /// <summary>Gets the size of every page, in points, without loading the pages.</summary>
    /// <returns>The page sizes, indexed by page.</returns>
    PageSize[] GetPageSizes();

    /// <summary>Gets the size of one page, in points, without loading the page or copying the other sizes.</summary>
    /// <param name="page">The zero-based page index.</param>
    /// <returns>The page size.</returns>
    PageSize GetPageSize(int page) => GetPageSizes()[page];

    /// <summary>Gets the document metadata.</summary>
    /// <returns>The metadata.</returns>
    DocumentMetadata GetMetadata();

    /// <summary>Gets the display label of a page, for example "iv", when the document defines one.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The label or <see langword="null"/>.</returns>
    string? GetPageLabel(int pageIndex);

    /// <summary>Gets the outline (bookmarks) tree.</summary>
    /// <returns>The root entries.</returns>
    IReadOnlyList<OutlineNode> GetOutline();

    /// <summary>Loads external page resources before rendering or reading text.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels resource I/O.</param>
    /// <returns>A task completing when page resources are ready.</returns>
    ValueTask PreparePageAsync(int pageIndex, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <summary>
    /// Records a page the reader is likely to turn to, without drawing any pixels, and keeps it out of the way of the pages on
    /// screen: if the picture memory runs out, the prepared page is the first to go. Call <see cref="PreparePageAsync"/> first.
    /// </summary>
    /// <param name="info">The page, scale, rotation and flags the page is likely to be drawn with.</param>
    /// <param name="cancellationToken">Cancels recording.</param>
    /// <returns><see langword="true"/> when the page was recorded; engines that cannot record ahead return <see langword="false"/>.</returns>
    bool PrepareAhead(in PageRenderInfo info, CancellationToken cancellationToken) => false;

    /// <summary>Renders part of a page into a pixel buffer. The buffer is filled with white first.</summary>
    /// <param name="info">What to render.</param>
    /// <param name="target">The destination buffer.</param>
    /// <returns><see langword="true"/> when the page rendered; <see langword="false"/> when the document is closed.</returns>
    bool Render(in PageRenderInfo info, RenderTarget target);

    /// <summary>Gets the number of text characters on a page.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The character count.</returns>
    int GetCharacterCount(int pageIndex);

    /// <summary>Gets the index of the character at a point.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="point">The point in page space.</param>
    /// <param name="tolerance">The hit tolerance in points.</param>
    /// <returns>The character index, or -1.</returns>
    int GetCharacterIndexAt(int pageIndex, PagePoint point, float tolerance);

    /// <summary>Gets the text of a run of characters.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="start">The first character.</param>
    /// <param name="count">The number of characters.</param>
    /// <returns>The text.</returns>
    string GetText(int pageIndex, int start, int count);

    /// <summary>Appends the line rectangles covering a run of characters.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="start">The first character.</param>
    /// <param name="count">The number of characters.</param>
    /// <param name="output">The list receiving the rectangles.</param>
    void GetTextBounds(int pageIndex, int start, int count, List<PageRect> output);

    /// <summary>Gets the links on a page.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The links.</returns>
    IReadOnlyList<PageLink> GetLinks(int pageIndex);

    /// <summary>Finds every occurrence of text on a page.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="output">The list receiving the matches.</param>
    void Find(int pageIndex, string query, SearchOptions options, List<TextMatch> output);
}
