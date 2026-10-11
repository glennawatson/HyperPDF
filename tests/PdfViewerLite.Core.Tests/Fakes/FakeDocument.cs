// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.Core.Tests.Fakes;

/// <summary>A document whose pages are filled with a grey level equal to the page index, with optional render gating.</summary>
internal sealed class FakeDocument : IDocument
{
    /// <summary>The page sizes.</summary>
    private readonly PageSize[] _sizes;

    /// <summary>Completes when the first render begins.</summary>
    private readonly TaskCompletionSource _firstRenderStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The number of renders performed.</summary>
    private int _renderCount;

    /// <summary>Initializes a new instance of the <see cref="FakeDocument"/> class.</summary>
    /// <param name="filePath">The file path.</param>
    /// <param name="sizes">The page sizes.</param>
    internal FakeDocument(string filePath, params PageSize[] sizes)
    {
        FilePath = filePath;
        _sizes = sizes;
    }

    /// <inheritdoc/>
    public string FilePath { get; }

    /// <inheritdoc/>
    public int PageCount => _sizes.Length;

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <summary>Gets the number of renders performed.</summary>
    internal int RenderCount => Volatile.Read(ref _renderCount);

    /// <summary>Gets an event set when the first render starts.</summary>
    internal ManualResetEventSlim Started { get; } = new(false);

    /// <summary>Gets a task completed when the first render begins.</summary>
    internal Task FirstRenderStarted => _firstRenderStarted.Task;

    /// <summary>Gets or sets an event every render waits on before drawing.</summary>
    internal ManualResetEventSlim? Gate { get; set; }

    /// <summary>Gets or sets asynchronous page preparation.</summary>
    internal Func<int, CancellationToken, ValueTask>? Preparation { get; set; }

    /// <summary>Gets or sets the text of every page.</summary>
    internal string PageText { get; set; } = "alpha beta alpha";

    /// <inheritdoc/>
    public PageSize[] GetPageSizes() => (PageSize[])_sizes.Clone();

    /// <inheritdoc/>
    public DocumentMetadata GetMetadata() => new() { Title = Path.GetFileName(FilePath) };

    /// <inheritdoc/>
    public string? GetPageLabel(int pageIndex) => null;

    /// <inheritdoc/>
    public IReadOnlyList<OutlineNode> GetOutline() => [];

    /// <inheritdoc/>
    public ValueTask PreparePageAsync(int pageIndex, CancellationToken cancellationToken) =>
        Preparation?.Invoke(pageIndex, cancellationToken) ?? ValueTask.CompletedTask;

    /// <inheritdoc/>
    public bool Render(in PageRenderInfo info, RenderTarget target)
    {
        Started.Set();
        _ = _firstRenderStarted.TrySetResult();
        Gate?.Wait();
        if (IsDisposed)
        {
            return false;
        }

        _ = Interlocked.Increment(ref _renderCount);
        target.Pixels.Fill((byte)info.PageIndex);
        return true;
    }

    /// <inheritdoc/>
    public int GetCharacterCount(int pageIndex) => PageText.Length;

    /// <inheritdoc/>
    public int GetCharacterIndexAt(int pageIndex, PagePoint point, float tolerance) => -1;

    /// <inheritdoc/>
    public string GetText(int pageIndex, int start, int count) => PageText.Substring(start, count);

    /// <inheritdoc/>
    public void GetTextBounds(int pageIndex, int start, int count, List<PageRect> output) => output.Add(new(start, 0, count, 1));

    /// <inheritdoc/>
    public IReadOnlyList<PageLink> GetLinks(int pageIndex) => [];

    /// <inheritdoc/>
    public void Find(int pageIndex, string query, SearchOptions options, List<TextMatch> output)
    {
        var comparison = (options & SearchOptions.MatchCase) != 0 ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var index = PageText.IndexOf(query, comparison);
        while (index >= 0)
        {
            output.Add(new(pageIndex, index, query.Length));
            index = PageText.IndexOf(query, index + query.Length, comparison);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        IsDisposed = true;
        Started.Dispose();
    }
}
