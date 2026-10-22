// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Rendering;

/// <summary>
/// Prepares the pages next to the one being read, so a page turn finds its content already recorded. It waits for the reader
/// to settle, prepares at most the pages in <see cref="NeighbourPages"/>, nearest in the direction of travel first, and
/// stands aside the moment something on screen needs drawing or memory runs short. It draws no pixels.
/// </summary>
[DebuggerDisplay("NeighbourPagePreparer: page {_page}")]
public sealed class NeighbourPagePreparer : IDisposable
{
    /// <summary>The default wait, in milliseconds, before neighbours are prepared; inside the 100 to 150 ms that tolerates a quick page turn.</summary>
    private const int DefaultDebounceMilliseconds = 125;

    /// <summary>The wait, in milliseconds, between checks that the screen is served.</summary>
    private const int QuietCheckMilliseconds = 50;

    /// <summary>The wait for a visible request to clear, between checks.</summary>
    private static readonly TimeSpan QuietCheck = TimeSpan.FromMilliseconds(QuietCheckMilliseconds);

    /// <summary>Guards the current run.</summary>
    private readonly Lock _gate = new();

    /// <summary>Tells whether something on screen is queued or rendering.</summary>
    private readonly Func<bool> _hasVisibleWork;

    /// <summary>Tells whether memory is short, in which case nothing is prepared.</summary>
    private readonly Func<bool> _underPressure;

    /// <summary>How long the reader must stay on a page before its neighbours are prepared.</summary>
    private readonly TimeSpan _debounce;

    /// <summary>The clock the waits use.</summary>
    private readonly TimeProvider _time;

    /// <summary>The document being read.</summary>
    private IDocument? _document;

    /// <summary>The page being read, or -1.</summary>
    private int _page = -1;

    /// <summary>Counts the runs started, so a run can tell whether a newer one replaced it.</summary>
    private long _generation;

    /// <summary>Cancels the current run when the reader moves on or the preparer closes.</summary>
    private Action? _cancelRun;

    /// <summary>Cancels the page being prepared when something on screen needs drawing.</summary>
    private Action? _cancelAttempt;

    /// <summary>The task of the latest run.</summary>
    private Task _latest = Task.CompletedTask;

    /// <summary>Whether the preparer is closed.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="NeighbourPagePreparer"/> class.</summary>
    /// <param name="hasVisibleWork">Tells whether something on screen is queued or rendering.</param>
    /// <param name="underPressure">Tells whether memory is short.</param>
    /// <param name="debounce">How long the reader must stay on a page before its neighbours are prepared.</param>
    /// <param name="time">The clock the waits use.</param>
    public NeighbourPagePreparer(Func<bool> hasVisibleWork, Func<bool> underPressure, TimeSpan debounce, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(hasVisibleWork);
        ArgumentNullException.ThrowIfNull(underPressure);
        ArgumentNullException.ThrowIfNull(time);
        _hasVisibleWork = hasVisibleWork;
        _underPressure = underPressure;
        _debounce = debounce;
        _time = time;
    }

    /// <summary>Gets the delay before neighbours are prepared.</summary>
    public static TimeSpan DefaultDebounce { get; } = TimeSpan.FromMilliseconds(DefaultDebounceMilliseconds);

    /// <summary>Gets a task that completes when the latest run has finished or stopped. Meant for tests.</summary>
    public Task Latest
    {
        get
        {
            lock (_gate)
            {
                return _latest;
            }
        }
    }

    /// <summary>Notes the page being read. A new page cancels the earlier run and starts a fresh one; the same page changes nothing.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page the reader is on.</param>
    /// <param name="scale">The scale pages are drawn at, so the recording matches what will be drawn.</param>
    /// <param name="rotation">The rotation pages are drawn with.</param>
    /// <param name="flags">The render flags pages are drawn with.</param>
    public void Visit(IDocument document, int page, float scale, PageRotation rotation, RenderFlags flags)
    {
        ArgumentNullException.ThrowIfNull(document);
        lock (_gate)
        {
            if (_disposed || (ReferenceEquals(_document, document) && _page == page))
            {
                return;
            }

            var movedBackward = ReferenceEquals(_document, document) && page < _page;
            _document = document;
            _page = page;
            _cancelRun?.Invoke();
            var source = new CancellationTokenSource();
            _cancelRun = source.Cancel;
            _generation++;
            _latest = RunAsync(document, new(page, scale, rotation, 0, 0, flags), movedBackward, source, _generation);
        }
    }

    /// <summary>Stops preparing the page in hand because something on screen needs drawing. The run waits, then carries on.</summary>
    public void Preempt()
    {
        lock (_gate)
        {
            _cancelAttempt?.Invoke();
        }
    }

    /// <summary>Stops preparing and forgets the document.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _cancelRun?.Invoke();
            _document = null;
            _page = -1;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cancelRun?.Invoke();
        }
    }

    /// <summary>Waits for the reader to settle, then prepares each neighbour in turn.</summary>
    /// <param name="document">The document.</param>
    /// <param name="template">The current page's render information; each neighbour reuses its scale, rotation and flags.</param>
    /// <param name="movedBackward">Whether the reader arrived from a later page.</param>
    /// <param name="source">Cancels the run; the run disposes it.</param>
    /// <param name="generation">The number of this run.</param>
    /// <returns>A task that completes when the run ends.</returns>
    private async Task RunAsync(IDocument document, PageRenderInfo template, bool movedBackward, CancellationTokenSource source, long generation)
    {
        var token = source.Token;
        try
        {
            await Task.Delay(_debounce, _time, token).ConfigureAwait(false);
            foreach (var page in NeighbourPages.Order(template.PageIndex, document.PageCount, movedBackward))
            {
                if (_underPressure() || document.IsDisposed)
                {
                    return;
                }

                await PrepareOneAsync(document, template with { PageIndex = page }, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The reader moved on, or the preparer closed.
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or AccessViolationException))
        {
            // A damaged page is not worth a failure the reader would see; it is drawn, and reported, when they reach it.
            Debug.WriteLine($"Neighbour preparation failed for page {template.PageIndex}: {exception.Message}");
        }
        finally
        {
            lock (_gate)
            {
                if (_generation == generation)
                {
                    _cancelRun = null;
                }
            }

            source.Dispose();
        }
    }

    /// <summary>Prepares a page, and when something on screen cuts it short, waits for the screen to be served and tries again.</summary>
    /// <param name="document">The document.</param>
    /// <param name="info">The page to prepare.</param>
    /// <param name="runToken">Cancels the run.</param>
    /// <returns>A task that completes when the page is prepared.</returns>
    private async Task PrepareOneAsync(IDocument document, PageRenderInfo info, CancellationToken runToken)
    {
        var prepared = false;
        while (!prepared)
        {
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(runToken);
            lock (_gate)
            {
                _cancelAttempt = attempt.Cancel;
            }

            try
            {
                await document.PreparePageAsync(info.PageIndex, attempt.Token).ConfigureAwait(false);
                _ = document.PrepareAhead(info, attempt.Token);
                prepared = true;
            }
            catch (OperationCanceledException) when (!runToken.IsCancellationRequested)
            {
                await WaitForQuietAsync(runToken).ConfigureAwait(false);
            }
            finally
            {
                lock (_gate)
                {
                    _cancelAttempt = null;
                }
            }
        }
    }

    /// <summary>Waits until nothing on screen is waiting to be drawn.</summary>
    /// <param name="runToken">Cancels the wait.</param>
    /// <returns>A task that completes when the screen is served.</returns>
    private async Task WaitForQuietAsync(CancellationToken runToken)
    {
        while (_hasVisibleWork())
        {
            await Task.Delay(QuietCheck, _time, runToken).ConfigureAwait(false);
        }
    }
}
