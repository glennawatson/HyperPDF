// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Reactive;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;

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

    /// <summary>Times the waits.</summary>
    private readonly ISequencer _sequencer;

    /// <summary>The document being read.</summary>
    private IDocument? _document;

    /// <summary>The page being read, or -1.</summary>
    private int _page = -1;

    /// <summary>Ends the current run when the reader moves on or the preparer closes.</summary>
    private IDisposable? _run;

    /// <summary>Cancels the page being prepared when something on screen needs drawing.</summary>
    private Action? _cancelAttempt;

    /// <summary>Tells when the latest run has ended.</summary>
    private IObservable<RxVoid> _latest = Signal.Return(RxVoid.Default);

    /// <summary>Whether the preparer is closed.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="NeighbourPagePreparer"/> class.</summary>
    /// <param name="hasVisibleWork">Tells whether something on screen is queued or rendering.</param>
    /// <param name="underPressure">Tells whether memory is short.</param>
    /// <param name="debounce">How long the reader must stay on a page before its neighbours are prepared.</param>
    /// <param name="sequencer">Times the waits and runs the preparation off the interface thread.</param>
    public NeighbourPagePreparer(Func<bool> hasVisibleWork, Func<bool> underPressure, TimeSpan debounce, ISequencer sequencer)
    {
        ArgumentNullException.ThrowIfNull(hasVisibleWork);
        ArgumentNullException.ThrowIfNull(underPressure);
        ArgumentNullException.ThrowIfNull(sequencer);
        _hasVisibleWork = hasVisibleWork;
        _underPressure = underPressure;
        _debounce = debounce;
        _sequencer = sequencer;
    }

    /// <summary>Gets the delay before neighbours are prepared.</summary>
    public static TimeSpan DefaultDebounce { get; } = TimeSpan.FromMilliseconds(DefaultDebounceMilliseconds);

    /// <summary>Gets a signal that gives one value when the latest run has finished or stopped, even to a watcher that comes late. Meant for tests.</summary>
    public IObservable<RxVoid> Latest
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
            _run?.Dispose();
            var finished = new ReplaySignal<RxVoid>();
            _latest = finished;
            _run = Run(document, new(page, scale, rotation, 0, 0, flags), movedBackward)
                .Finally(() => Finish(finished))
                .SubscribeSafe(static _ => { }, static _ => { });
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
            _run?.Dispose();
            _run = null;
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
            _run?.Dispose();
            _run = null;
        }
    }

    /// <summary>Tells a run's watchers that it has ended.</summary>
    /// <param name="finished">The run's end marker.</param>
    private static void Finish(ReplaySignal<RxVoid> finished)
    {
        finished.OnNext(RxVoid.Default);
        finished.OnCompleted();
    }

    /// <summary>Notes a failed run, which is not worth a failure the reader would see.</summary>
    /// <param name="exception">The failure.</param>
    /// <param name="page">The page the run was for.</param>
    /// <returns>A signal that ends, or fails with the same error when it is one that must not be hidden.</returns>
    private static IObservable<RxVoid> Failed(Exception exception, int page)
    {
        if (exception is OutOfMemoryException or AccessViolationException)
        {
            return Signal.Throw<RxVoid>(exception);
        }

        // A damaged page is drawn, and reported, when the reader reaches it.
        AppLog.Warning(exception, $"Neighbour preparation failed for page {page}.");
        return Signal.Empty<RxVoid>();
    }

    /// <summary>
    /// Reads a page's links here, away from the UI thread. The first read scans the page's text for web addresses, and the
    /// document keeps the result, so the first pointer move over the page finds it ready.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    private static void ReadLinks(IDocument document, int page) => _ = document.GetLinks(page);

    /// <summary>Waits for the reader to settle, then prepares each neighbour in turn.</summary>
    /// <param name="document">The document.</param>
    /// <param name="template">The current page's render information; each neighbour reuses its scale, rotation and flags.</param>
    /// <param name="movedBackward">Whether the reader arrived from a later page.</param>
    /// <returns>A cold signal that ends when the run does; unsubscribing, because the reader moved on or the preparer closed, stops it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> Run(IDocument document, PageRenderInfo template, bool movedBackward) =>
        Signal.Timer(_debounce, _sequencer)
            .Do(_ => ReadLinks(document, template.PageIndex))
            .SwitchMap(_ => LazySequence.Concat(Neighbours(document, template, movedBackward)).Count().Select(static _ => RxVoid.Default))
            .Catch<RxVoid, Exception>(exception => Failed(exception, template.PageIndex));

    /// <summary>Makes the preparation of each neighbour, one at a time as the run moves along, stopping at memory pressure or a closed document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="template">The current page's render information.</param>
    /// <param name="movedBackward">Whether the reader arrived from a later page.</param>
    /// <returns>A cold signal for each neighbour.</returns>
    private IEnumerable<IObservable<RxVoid>> Neighbours(IDocument document, PageRenderInfo template, bool movedBackward)
    {
        foreach (var page in NeighbourPages.Order(template.PageIndex, document.PageCount, movedBackward))
        {
            if (_underPressure() || document.IsDisposed)
            {
                yield break;
            }

            yield return PrepareOne(document, template with { PageIndex = page });
        }
    }

    /// <summary>Prepares a page, and when something on screen cuts it short, waits for the screen to be served and tries again.</summary>
    /// <param name="document">The document.</param>
    /// <param name="info">The page to prepare.</param>
    /// <returns>A cold signal that ends once the page is prepared.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> PrepareOne(IDocument document, PageRenderInfo info) =>
        Signal.Defer(() => Attempt(document, info))
            .Catch<RxVoid, OperationCanceledException>(_ => WaitForQuiet().SwitchMap(_ => PrepareOne(document, info)));

    /// <summary>Makes one attempt at preparing a page, which <see cref="Preempt"/> can cut short.</summary>
    /// <param name="document">The document.</param>
    /// <param name="info">The page to prepare.</param>
    /// <returns>A cold signal that ends once the page is prepared, or fails with the cancellation when it was cut short.</returns>
    private IObservable<RxVoid> Attempt(IDocument document, PageRenderInfo info)
    {
        var attempt = new CancellationTokenSource();
        lock (_gate)
        {
            _cancelAttempt = attempt.Cancel;
        }

        var token = attempt.Token;
        return document.PreparePage(info.PageIndex).ThrowWhenCancelled(token)
            .Do(prepared =>
            {
                _ = document.PrepareAhead(info, token);
                ReadLinks(document, info.PageIndex);
            })
            .Finally(() => EndAttempt(attempt));
    }

    /// <summary>Forgets an attempt that ended.</summary>
    /// <param name="attempt">The attempt's cancellation.</param>
    private void EndAttempt(CancellationTokenSource attempt)
    {
        lock (_gate)
        {
            _cancelAttempt = null;
        }

        attempt.Dispose();
    }

    /// <summary>Waits until nothing on screen is waiting to be drawn.</summary>
    /// <returns>A cold signal that ends once the screen is served.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> WaitForQuiet() =>
        Signal.Defer(() => _hasVisibleWork()
            ? Signal.Every(QuietCheck, _sequencer).Where(_ => !_hasVisibleWork()).Take(1).Select(static _ => RxVoid.Default)
            : Signal.Return(RxVoid.Default));
}
