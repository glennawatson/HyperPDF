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

    /// <summary>The largest search builder kept after an enumeration.</summary>
    private const int MaxKeptMatches = 4096;

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

    /// <summary>Loads and extracts a page with explicit filtering, without changing the cached text.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="options">The opt-in filters.</param>
    /// <param name="cancellationToken">Cancels loading and extraction.</param>
    /// <returns>The filtered text page.</returns>
    public static async ValueTask<PdfTextPage> GetTextPageAsync(PdfDocument document, int pageIndex, PdfTextExtractionOptions options, CancellationToken cancellationToken)
    {
        await PdfDocumentPages.PrefetchPageAsync(document, pageIndex, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return ExtractText(document, pageIndex, options, cancellationToken);
    }

    /// <summary>
    /// Gets a page's text in article order: the text inside each article bead on the page, following the document's article
    /// threads in their order and each thread's beads in theirs (ISO 32000-2 12.4.3).
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="cancellationToken">Cancels the loading and the extraction.</param>
    /// <returns>The text of each bead one after the other; <see langword="null"/> when the page has no bead with a rectangle.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<string?> GetArticleTextAsync(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        var page = await GetTextPageAsync(document, pageIndex, cancellationToken).ConfigureAwait(false);
        return TextArticles.Read(PdfDocumentCatalog.GetThreads(document), pageIndex, page);
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

    /// <summary>
    /// Finds a query on one page. A page already in the text page cache is searched as it is; any other page is searched
    /// without making a text page, so searching many pages neither allocates their characters nor pushes the pages the
    /// reader is using out of the cache. The matches equal those of <see cref="PdfTextPage.Find(ReadOnlySpan{char}, PdfTextSearchOptions, List{PdfTextMatch})"/>.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="output">Receives the matches, as character indexes into the page's text page.</param>
    /// <param name="cancellationToken">Cancels the extraction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> or <paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static void FindInPage(PdfDocument document, int pageIndex, string query, PdfTextSearchOptions options, List<PdfTextMatch> output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        SearchLoadedPage(document, pageIndex, query, options, output, static (matches, start, count) => matches.Add(new(start, count)), cancellationToken);
    }

    /// <summary>Finds a query on one page as the list form does, without an intermediate list.</summary>
    /// <typeparam name="TState">The caller's state passed to each visit.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="state">The caller's state.</param>
    /// <param name="visit">Receives each match's first character and length.</param>
    /// <param name="cancellationToken">Cancels the extraction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> or <paramref name="visit"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FindInPage<TState>(
        PdfDocument document,
        int pageIndex,
        string query,
        PdfTextSearchOptions options,
        TState state,
        Action<TState, int, int> visit,
        CancellationToken cancellationToken) =>
        SearchLoadedPage(document, pageIndex, query, options, state, visit, cancellationToken);

    /// <summary>Loads a page, then finds a query on it as <see cref="FindInPage{TState}"/> does.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="output">Receives the matches, as character indexes into the page's text page.</param>
    /// <param name="cancellationToken">Cancels the loading and the extraction.</param>
    /// <returns>A task that completes when the matches are in <paramref name="output"/>.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask FindInPageAsync(PdfDocument document, int pageIndex, string query, PdfTextSearchOptions options, List<PdfTextMatch> output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        await PdfDocumentPages.PrefetchPageAsync(document, pageIndex, cancellationToken).ConfigureAwait(false);
        SearchLoadedPage(document, pageIndex, query, options, output, static (matches, start, count) => matches.Add(new(start, count)), cancellationToken);
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

    /// <summary>Extracts a page with explicit filtering, without changing the cached OCR and accessibility text.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="options">The opt-in filters.</param>
    /// <returns>The filtered text page.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfTextPage GetTextPage(PdfDocument document, int pageIndex, PdfTextExtractionOptions options) =>
        ExtractText(document, pageIndex, options, CancellationToken.None);

    /// <summary>Gets a page's text in article order, as <see cref="GetArticleTextAsync"/> does, without waiting for the page to load.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <returns>The text of each bead one after the other; <see langword="null"/> when the page has no bead with a rectangle.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? GetArticleText(PdfDocument document, int pageIndex) =>
        TextArticles.Read(PdfDocumentCatalog.GetThreads(document), pageIndex, GetTextPage(document, pageIndex));

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
    /// <returns>The text page.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfTextPage ExtractText(PdfDocument document, int pageIndex) =>
        ExtractText(document, pageIndex, default, CancellationToken.None);

    /// <summary>Extracts a page's text without caching it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The text page.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfTextPage ExtractText(PdfDocument document, int pageIndex, CancellationToken cancellationToken) =>
        ExtractText(document, pageIndex, default, cancellationToken);

    /// <summary>Extracts text with filters and an explicit cancellation token, without changing the default cache.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="options">The opt-in filters.</param>
    /// <param name="cancellationToken">Cancels extraction.</param>
    /// <returns>The extracted text page.</returns>
    private static PdfTextPage ExtractText(PdfDocument document, int pageIndex, PdfTextExtractionOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = PdfDocumentPages.GetPage(document, pageIndex);
        var state = TextPageBuild.Rent(document);
        var device = state.Device;
        try
        {
            device.Reset(page, options, cancellationToken);
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
        cancellationToken.ThrowIfCancellationRequested();
        if (query.Length == 0)
        {
            yield break;
        }

        var found = document.Objects.ScratchPools.Slots<List<PdfTextMatch>>().Take() ?? [];
        try
        {
            for (var i = 0; i < document.PageCount; i++)
            {
                found.Clear();
                await PdfDocumentText.FindInPageAsync(document, i, query, options, found, cancellationToken).ConfigureAwait(false);
                if (found.Count > 0)
                {
                    yield return new(i, [.. found]);
                }
            }
        }
        finally
        {
            found.Clear();
            if (found.Capacity <= MaxKeptMatches)
            {
                _ = document.Objects.ScratchPools.Slots<List<PdfTextMatch>>().Offer(found);
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

    /// <summary>Runs a page's content into the text device, keeping the text read before damaged content stops it.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "page">The page.</param>
    /// <param name = "device">The text device.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void RunContent(PdfDocument document, PdfPage page, TextDevice device, CancellationToken cancellationToken)
    {
        using var interpreter = ContentInterpreter.Rent(PdfDocumentRendering.GetRenderCache(document), device, 0, cancellationToken);
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

    /// <summary>Finds a query on a page whose objects are loaded: in the cached text page when there is one, else without making one.</summary>
    /// <typeparam name="TState">The caller's state passed to each visit.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="state">The caller's state.</param>
    /// <param name="visit">Receives each match's first character and length.</param>
    /// <param name="cancellationToken">Cancels the extraction.</param>
    private static void SearchLoadedPage<TState>(
        PdfDocument document,
        int pageIndex,
        string query,
        PdfTextSearchOptions options,
        TState state,
        Action<TState, int, int> visit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(visit);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, document.PageCount);
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);
        if (query.Length == 0)
        {
            return;
        }

        if (PdfDocumentText.GetTextPages(document).TryGet(pageIndex) is { } cached)
        {
            cached.Find(query, options, state, visit);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var page = PdfDocumentPages.GetPage(document, pageIndex);
        var build = TextPageBuild.Rent(document);
        var device = build.Device;
        try
        {
            device.Reset(page, default, cancellationToken);
            PdfDocumentText.RunContent(document, page, device, cancellationToken);
            device.Finish();
            TextPageBuild.Find(build, PdfDocumentText.IsRightToLeft(document), query, options, state, visit);
        }
        finally
        {
            device.Clear();
            TextPageBuild.Return(document, build);
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
