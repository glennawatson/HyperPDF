// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Document;

/// <summary>Reads and searches document text.</summary>
public static class PdfDocumentText
{
    /// <summary>The most text pages kept per document.</summary>
    private const int TextPageCapacity = 16;

    /// <summary>Gets a page's text, extracting it on first use and stopping at the token's cancellation.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name = "cancellationToken">Cancels the loading and the extraction.</param>
    /// <returns>The text page.</returns>
    /// <exception cref = "ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref = "ObjectDisposedException">The document has been disposed.</exception>
    /// <exception cref = "OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<PdfTextPage> GetTextPageAsync(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        var load = PdfDocumentPages.PrefetchPageAsync(document, pageIndex, cancellationToken);
        return load.IsCompletedSuccessfully ? PdfDocumentText.TextPageReady(document, pageIndex, cancellationToken) : PdfDocumentText.TextPageAfterAsync(document, load, pageIndex, cancellationToken);
    }

    /// <summary>Walks the text of every page in order, loading each page just ahead of its extraction.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "cancellationToken">Cancels the walk.</param>
    /// <returns>The text pages.</returns>
    /// <exception cref = "OperationCanceledException">The token was cancelled.</exception>
    public static async IAsyncEnumerable<PdfTextPage> GetTextPagesAsync(PdfDocument document, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 0; i < document.PageCount; i++)
        {
            yield return await PdfDocumentText.GetTextPageAsync(document, i, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Searches every page for text, yielding the pages that match.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "query">The text to find.</param>
    /// <param name = "options">The search options.</param>
    /// <param name = "cancellationToken">Cancels the search.</param>
    /// <returns>One entry per page with at least one match, in page order.</returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "query"/> is <see langword="null"/>.</exception>
    /// <exception cref = "OperationCanceledException">The token was cancelled.</exception>
    public static IAsyncEnumerable<PdfPageMatches> FindAsync(PdfDocument document, string query, PdfTextSearchOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return PdfDocumentText.FindPagesAsync(document, query, options, cancellationToken);
    }

    /// <summary>Gets a page's text, extracting it on first use and keeping recently used pages.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <returns>The text page.</returns>
    /// <exception cref = "ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref = "ObjectDisposedException">The document has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfTextPage GetTextPage(PdfDocument document, int pageIndex) =>
        GetTextPagePrepared(document, pageIndex, CancellationToken.None);

    /// <summary>Gets the recently used text pages.</summary>
    /// <param name = "document">The document.</param>
    /// <returns>The recently extracted text page cache.</returns>
    internal static PdfTextPageCache GetTextPages(PdfDocument document)
    {
        if (Volatile.Read(ref document.State.TextPages) is { } existing)
        {
            return existing;
        }

        _ = Interlocked.CompareExchange(ref document.State.TextPages, new(PdfDocumentText.TextPageCapacity), null);
        return Volatile.Read(ref document.State.TextPages)!;
    }

    /// <summary>Extracts a page's text without caching it.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The text page.</returns>
    internal static PdfTextPage ExtractText(PdfDocument document, int pageIndex, CancellationToken cancellationToken = default)
    {
        var page = PdfDocumentPages.GetPage(document, pageIndex);
        var state = TextPageBuild.Rent(document);
        var device = state.Device;
        try
        {
            device.Reset(page);
            PdfDocumentText.RunContent(document, page, device, cancellationToken);
            device.Finish();
            return TextPageBuild.Build(state, page, PdfDocumentText.IsRightToLeft(document));
        }
        finally
        {
            device.Clear();
            TextPageBuild.Return(document, state);
        }
    }

    /// <summary>Gets a text page while passing the caller's token to first extraction.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <param name="cancellationToken">Cancels first extraction.</param>
    /// <returns>The text page.</returns>
    private static PdfTextPage GetTextPagePrepared(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, document.PageCount);
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);
        var request = new TextExtractionRequest(document, cancellationToken);
        return PdfDocumentText.GetTextPages(document).GetOrAdd(
            pageIndex,
            request,
            static (index, state) => PdfDocumentText.ExtractText(state.Document, index, state.CancellationToken));
    }

    /// <summary>Walks the pages, yielding those with matches.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "query">The text to find.</param>
    /// <param name = "options">The search options.</param>
    /// <param name = "cancellationToken">Cancels the walk.</param>
    /// <returns>One entry per page with at least one match.</returns>
    private static async IAsyncEnumerable<PdfPageMatches> FindPagesAsync(PdfDocument document, string query, PdfTextSearchOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var found = new List<PdfTextMatch>();
        for (var i = 0; i < document.PageCount; i++)
        {
            var text = await PdfDocumentText.GetTextPageAsync(document, i, cancellationToken).ConfigureAwait(false);
            found.Clear();
            text.Find(query, options, found);
            if (found.Count > 0)
            {
                yield return new(i, [.. found]);
            }
        }
    }

    /// <summary>Extracts a page's text once its objects are loaded, reporting failures through the task.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "pageIndex">The page index.</param>
    /// <param name = "cancellationToken">The token.</param>
    /// <returns>The completed text page, or a faulted task.</returns>
    private static ValueTask<PdfTextPage> TextPageReady(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        try
        {
            return new(PdfDocumentText.ExtractTextScoped(document, pageIndex, cancellationToken));
        }
        catch (Exception ex) when (PdfDocumentAsyncTasks.IsTaskFault(ex))
        {
            return ValueTask.FromException<PdfTextPage>(ex);
        }
    }

    /// <summary>Waits for a load, then extracts a page's text.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "load">The load.</param>
    /// <param name = "pageIndex">The page index.</param>
    /// <param name = "cancellationToken">The token.</param>
    /// <returns>The text page.</returns>
    private static async ValueTask<PdfTextPage> TextPageAfterAsync(PdfDocument document, ValueTask load, int pageIndex, CancellationToken cancellationToken)
    {
        await load.ConfigureAwait(false);
        return PdfDocumentText.ExtractTextScoped(document, pageIndex, cancellationToken);
    }

    /// <summary>Extracts a page's text with the token in force.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "pageIndex">The page index.</param>
    /// <param name = "cancellationToken">The token.</param>
    /// <returns>The text page.</returns>
    private static PdfTextPage ExtractTextScoped(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return PdfDocumentText.GetTextPagePrepared(document, pageIndex, cancellationToken);
    }

    /// <summary>Runs a page's content into the text device, keeping the text read before damaged content stops it, as PDFium does.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "page">The page.</param>
    /// <param name = "device">The text device.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void RunContent(PdfDocument document, PdfPage page, TextDevice device, CancellationToken cancellationToken)
    {
        using var interpreter = new ContentInterpreter(PdfDocumentRendering.GetRenderCache(document), device, 0, cancellationToken: cancellationToken);
        try
        {
            ContentExecution.RunPage(interpreter, page);
        }
        catch (Exception ex) when (ex is InvalidDataException or
        PdfException or
        ArgumentException or
        InvalidOperationException or
        IndexOutOfRangeException or
        NotSupportedException or
        FormatException or
        OverflowException)
        {
            // The runs collected so far still make a text page.
        }
    }

    /// <summary>Determines whether the viewer preferences ask for right-to-left reading order.</summary>
    /// <param name = "document">The document.</param>
    /// <returns><see langword="true"/> when /Direction is /R2L.</returns>
    private static bool IsRightToLeft(PdfDocument document)
    {
        if (document.Catalog.GetDictionary(KnownName.ViewerPreferences) is not { } preferences)
        {
            return false;
        }

        var names = document.Objects.Names;
        var direction = preferences.GetName(names.Intern("Direction"u8));
        return names.NameEquals(direction, "R2L"u8);
    }

    /// <summary>The document and token passed to a first text extraction without a closure.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="CancellationToken">The extraction token.</param>
    private readonly record struct TextExtractionRequest(PdfDocument Document, CancellationToken CancellationToken);
}
