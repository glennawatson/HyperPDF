// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using HyperPdf.Core.Attachments;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Printing;
using HyperPdf.Core.Reading;
using HyperPdf.Engine;

namespace HyperPdf.App.Tests;

/// <summary>
/// An engine whose documents can be held in the middle of text work, so a test decides exactly when a search or a
/// read is in the middle of a page and when it carries on. A gate holds a page at one of three points.
/// </summary>
internal sealed class PausingEngine : IDocumentEngine
{
    /// <summary>The real engine every document comes from.</summary>
    private readonly HyperPdfEngine _inner = new();

    /// <summary>How many pages were prepared.</summary>
    private int _preparedPages;

    /// <summary>Where a gate holds a page.</summary>
    internal enum PausePoint
    {
        /// <summary>When the page is prepared; the hold can be awaited.</summary>
        Prepare = 0,

        /// <summary>When the page is searched; the hold is on a pool thread.</summary>
        Search = 1,

        /// <summary>When the page's characters are counted to read its text; the hold is on a pool thread.</summary>
        Text = 2,

        /// <summary>When a page is drawn; the hold is on the render thread.</summary>
        Render = 3,

        /// <summary>When a page's characters are laid out to read its text in order; the hold is on a pool thread.</summary>
        Layout = 4,

        /// <summary>When the print preview's exporter writes its pages; the hold is on a pool thread.</summary>
        Export = 5,
    }

    /// <inheritdoc/>
    public string Name => _inner.Name;

    /// <summary>Gets how many times a page was prepared, held or not.</summary>
    internal int PreparedPages => Volatile.Read(ref _preparedPages);

    /// <summary>Gets the gate pages wait at while it is armed, or <see langword="null"/> when pages run straight through.</summary>
    internal PauseGate? Gate { get; private set; }

    /// <summary>Gets or sets a value indicating whether documents pass on the real document's features, with page management, attachments and printing recording the thread they run on.</summary>
    internal bool ExposesFeatures { get; set; }

    /// <summary>Gets the library operations that ran, with their threads, in order.</summary>
    internal ConcurrentQueue<RecordedCall> Calls { get; } = new();

    /// <summary>Gets the pages whose open actions ran, in order. Features must be exposed.</summary>
    internal ConcurrentQueue<int> OpenedPages { get; } = new();

    /// <summary>Gets or sets the gate each page's open actions wait at, or <see langword="null"/> to run at once.</summary>
    internal TaskCompletionSource? FormGate { get; set; }

    /// <inheritdoc/>
    public bool CanOpen(string path) => _inner.CanOpen(path);

    /// <inheritdoc/>
    public IDocument Open(string path, string? password) => new PausingDocument(_inner.Open(path, password), this);

    /// <summary>Holds every page that is prepared from now on until the returned gate is released.</summary>
    /// <returns>The gate.</returns>
    internal PauseGate Arm() => Arm(PausePoint.Prepare);

    /// <summary>Holds the text search of every page from now on, after the page is prepared, until the returned gate is released.</summary>
    /// <returns>The gate.</returns>
    internal PauseGate ArmSearch() => Arm(PausePoint.Search);

    /// <summary>Holds the text read of every page from now on, after the page is prepared, until the returned gate is released.</summary>
    /// <returns>The gate.</returns>
    internal PauseGate ArmText() => Arm(PausePoint.Text);

    /// <summary>Holds the drawing of every page from now on until the returned gate is released.</summary>
    /// <returns>The gate.</returns>
    internal PauseGate ArmRender() => Arm(PausePoint.Render);

    /// <summary>Holds the character layout of one page, which Focus Mode reads, until the returned gate is released. Features must be exposed.</summary>
    /// <param name="page">The page to hold; other pages run straight through.</param>
    /// <returns>The gate.</returns>
    internal PauseGate ArmLayout(int page)
    {
        var gate = new PauseGate(PausePoint.Layout) { Page = page };
        Gate = gate;
        return gate;
    }

    /// <summary>Holds every page export (the print preview build) from now on, before it writes, until the returned gate is released. Features must be exposed.</summary>
    /// <returns>The gate.</returns>
    internal PauseGate ArmExport() => Arm(PausePoint.Export);

    /// <summary>Lets pages prepared from now on run straight through. Pages already held stay held until their gate is released.</summary>
    internal void Disarm() => Gate = null;

    /// <summary>Holds pages at a point.</summary>
    /// <param name="point">Where to hold.</param>
    /// <returns>The gate.</returns>
    private PauseGate Arm(PausePoint point)
    {
        var gate = new PauseGate(point);
        Gate = gate;
        return gate;
    }

    /// <summary>One hold: pages wait at it until a test releases it.</summary>
    /// <param name="point">Where the gate holds a page.</param>
    internal sealed class PauseGate(PausePoint point)
    {
        /// <summary>Gets where the gate holds a page.</summary>
        internal PausePoint Point => point;

        /// <summary>Gets the only page a layout gate holds.</summary>
        internal int Page { get; init; }

        /// <summary>Gets the signal that a page is waiting at the gate.</summary>
        internal TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the signal that a held page finished its search after the release.</summary>
        internal TaskCompletionSource Found { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the signal that lets the held pages carry on.</summary>
        internal TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the files an export wrote while the gate was armed, once each has finished.</summary>
        internal ConcurrentQueue<string> Exported { get; } = new();

        /// <summary>Lets the held pages carry on.</summary>
        internal void Release() => _ = Released.TrySetResult();
    }

    /// <summary>A document that holds page text work at the engine's gate and passes everything else to the real document.</summary>
    /// <param name="inner">The real document.</param>
    /// <param name="engine">The engine that owns the gate.</param>
    private sealed class PausingDocument(IDocument inner, PausingEngine engine) : IDocument
    {
        /// <summary>The longest a held page waits for its release on a pool thread; the engine's calls cannot be awaited.</summary>
        private static readonly TimeSpan HoldLimit = TimeSpan.FromSeconds(30);

        /// <summary>The gate the last prepared page met.</summary>
        private PauseGate? _held;

        /// <inheritdoc/>
        public string FilePath => inner.FilePath;

        /// <inheritdoc/>
        public int PageCount => inner.PageCount;

        /// <inheritdoc/>
        public bool IsDisposed => inner.IsDisposed;

        /// <inheritdoc/>
        public PageSize[] GetPageSizes() => inner.GetPageSizes();

        /// <inheritdoc/>
        public DocumentMetadata GetMetadata() => inner.GetMetadata();

        /// <inheritdoc/>
        public string? GetPageLabel(int pageIndex) => inner.GetPageLabel(pageIndex);

        /// <inheritdoc/>
        public IReadOnlyList<OutlineNode> GetOutline() => inner.GetOutline();

        /// <inheritdoc/>
        public async ValueTask PreparePageAsync(int pageIndex, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref engine._preparedPages);
            var gate = engine.Gate;
            Volatile.Write(ref _held, gate);
            if (gate is not { Point: PausePoint.Prepare })
            {
                return;
            }

            _ = gate.Reached.TrySetResult();

            // The gate ignores the token on purpose: a search stopped while held must still finish the page it is on.
            await gate.Released.Task.ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public bool Render(in PageRenderInfo info, RenderTarget target)
        {
            if (engine.Gate is { Point: PausePoint.Render } gate)
            {
                _ = gate.Reached.TrySetResult();

                // The call is synchronous, so the hold on the render thread polls the release with a bound.
                _ = SpinWait.SpinUntil(() => gate.Released.Task.IsCompleted, HoldLimit);
            }

            return inner.Render(info, target);
        }

        /// <inheritdoc/>
        public int GetCharacterCount(int pageIndex)
        {
            _ = Hold(PausePoint.Text);
            return inner.GetCharacterCount(pageIndex);
        }

        /// <inheritdoc/>
        public int GetCharacterIndexAt(int pageIndex, PagePoint point, float tolerance) => inner.GetCharacterIndexAt(pageIndex, point, tolerance);

        /// <inheritdoc/>
        public string GetText(int pageIndex, int start, int count) => inner.GetText(pageIndex, start, count);

        /// <inheritdoc/>
        public void GetTextBounds(int pageIndex, int start, int count, List<PageRect> output) => inner.GetTextBounds(pageIndex, start, count, output);

        /// <inheritdoc/>
        public IReadOnlyList<PageLink> GetLinks(int pageIndex) => inner.GetLinks(pageIndex);

        /// <inheritdoc/>
        public void Find(int pageIndex, string query, SearchOptions options, List<TextMatch> output)
        {
            var gate = Hold(PausePoint.Search);
            inner.Find(pageIndex, query, options, output);
            _ = gate?.Found.TrySetResult();
        }

        /// <inheritdoc/>
        public IDocumentFeature? GetFeature(Type featureType)
        {
            if (!engine.ExposesFeatures)
            {
                return null;
            }

            var real = inner.GetFeature(featureType);
            if (real is null)
            {
                return null;
            }

            if (featureType == typeof(IPageManagementSource))
            {
                return new RecordingPageManagement((IPageManagementSource)real, this, engine);
            }

            if (featureType == typeof(IAttachmentSource))
            {
                return new RecordingAttachments((IAttachmentSource)real, this, engine);
            }

            if (featureType == typeof(IFormActions))
            {
                return new RecordingFormActions((IFormActions)real, this, engine);
            }

            if (featureType == typeof(ITextLayoutSource))
            {
                return new HoldingLayout((ITextLayoutSource)real, this, engine);
            }

            return featureType == typeof(IPageExporter) ? new RecordingExporter((IPageExporter)real, this, engine) : real;
        }

        /// <inheritdoc/>
        public void Dispose() => inner.Dispose();

        /// <summary>Holds the calling thread at the gate the last prepared page met, when that gate holds at a point.</summary>
        /// <param name="point">The point being passed.</param>
        /// <returns>The gate the page met, or <see langword="null"/>.</returns>
        private PauseGate? Hold(PausePoint point)
        {
            var gate = Volatile.Read(ref _held);
            if (gate?.Point == point)
            {
                _ = gate.Reached.TrySetResult();

                // The call is synchronous, so the hold on a pool thread polls the release with a bound.
                _ = SpinWait.SpinUntil(() => gate.Released.Task.IsCompleted, HoldLimit);
            }

            return gate;
        }
    }

    /// <summary>Remembers the thread a library operation ran on.</summary>
    /// <param name="Name">The operation.</param>
    /// <param name="ThreadId">The managed thread that ran it.</param>
    internal sealed record RecordedCall(string Name, int ThreadId);
}
