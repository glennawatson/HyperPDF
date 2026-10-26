// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Tests.Fakes;

namespace HyperPdf.Core.Tests.Rendering;

/// <summary>Checks which pages are prepared ahead, in what order, and when preparation stands aside.</summary>
public sealed class NeighbourPagePreparerTests
{
    /// <summary>The pages in the sample document.</summary>
    private const int Pages = 10;

    /// <summary>The page the reader is on.</summary>
    private const int Current = 4;

    /// <summary>The page after the current one.</summary>
    private const int Next = 5;

    /// <summary>The page two after the current one.</summary>
    private const int Further = 6;

    /// <summary>The page before the current one.</summary>
    private const int Previous = 3;

    /// <summary>The page two before the current one, outside the window; also page 2 of the document.</summary>
    private const int TwoBefore = 2;

    /// <summary>The second page after the first one.</summary>
    private const int Two = 2;

    /// <summary>The width and height of each sample page.</summary>
    private const int PageSide = 100;

    /// <summary>The page three after the current one, outside the window.</summary>
    private const int ThreeAfter = 7;

    /// <summary>The scale used for the requests.</summary>
    private const float Scale = 1;

    /// <summary>The settle wait, in milliseconds, a quick page turn must beat; a manual time provider holds it, so the test never waits it out.</summary>
    private const int LongWaitMilliseconds = 1000;

    /// <summary>The pages prepared in the sample runs.</summary>
    private const int NeighbourCount = 3;

    /// <summary>Reading forward, the next two pages come first, then the one behind.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ForwardOrderIsNextThenFurtherThenBehind() =>
        await Assert.That(NeighbourPages.Order(Current, Pages, false)).IsEquivalentTo([Next, Further, Previous]);

    /// <summary>Reading backward, the page behind comes first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BackwardOrderStartsBehind() =>
        await Assert.That(NeighbourPages.Order(Current, Pages, true)).IsEquivalentTo([Previous, Next, Further]);

    /// <summary>Pages outside the document are dropped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrderStaysInsideTheDocument()
    {
        await Assert.That(NeighbourPages.Order(0, Pages, false)).IsEquivalentTo([1, Two]);
        await Assert.That(NeighbourPages.Order(Pages - 1, Pages, false)).IsEquivalentTo([Pages - Two]);
    }

    /// <summary>The window runs from one page behind to two ahead and leaves out the current page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WindowCoversOneBehindAndTwoAhead()
    {
        await Assert.That(NeighbourPages.IsInWindow(Previous, Current)).IsTrue();
        await Assert.That(NeighbourPages.IsInWindow(Further, Current)).IsTrue();
        await Assert.That(NeighbourPages.IsInWindow(TwoBefore, Current)).IsFalse();
        await Assert.That(NeighbourPages.IsInWindow(ThreeAfter, Current)).IsFalse();
        await Assert.That(NeighbourPages.IsInWindow(Current, Current)).IsFalse();
    }

    /// <summary>After the reader settles, the neighbours are prepared in the order of travel.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SettledReaderGetsNeighboursPrepared(CancellationToken cancellationToken)
    {
        using var preparer = Create(static () => false, static () => false);
        var document = Document();

        preparer.Visit(document, Current, Scale, PageRotation.None, RenderFlags.None);
        await preparer.Latest.WaitAsync(cancellationToken);

        await Assert.That(document.PreparedAhead).IsEquivalentTo([Next, Further, Previous]);
    }

    /// <summary>The page being read and its neighbours have their links read away from the UI thread, so the first hover finds them ready.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SettledReaderGetsLinksRead(CancellationToken cancellationToken)
    {
        using var preparer = Create(static () => false, static () => false);
        var document = Document();

        preparer.Visit(document, Current, Scale, PageRotation.None, RenderFlags.None);
        await preparer.Latest.WaitAsync(cancellationToken);

        await Assert.That(document.LinksRead).IsEquivalentTo([Current, Next, Further, Previous]);
    }

    /// <summary>Moving to an earlier page puts the page behind first.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task BackwardMoveChangesTheOrder(CancellationToken cancellationToken)
    {
        using var preparer = Create(static () => false, static () => false);
        var document = Document();
        preparer.Visit(document, Next, Scale, PageRotation.None, RenderFlags.None);
        await preparer.Latest.WaitAsync(cancellationToken);
        document.PreparedAhead.Clear();

        preparer.Visit(document, Current, Scale, PageRotation.None, RenderFlags.None);
        await preparer.Latest.WaitAsync(cancellationToken);

        await Assert.That(document.PreparedAhead[0]).IsEqualTo(Previous);
    }

    /// <summary>Under memory pressure nothing is prepared.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task PressureSkipsPreparation(CancellationToken cancellationToken)
    {
        using var preparer = Create(static () => false, static () => true);
        var document = Document();

        preparer.Visit(document, Current, Scale, PageRotation.None, RenderFlags.None);
        await preparer.Latest.WaitAsync(cancellationToken);

        await Assert.That(document.PreparedAhead.Count).IsEqualTo(0);
    }

    /// <summary>Asking for the same page again does not start another run.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SamePageDoesNotRestart(CancellationToken cancellationToken)
    {
        using var preparer = Create(static () => false, static () => false);
        var document = Document();
        preparer.Visit(document, Current, Scale, PageRotation.None, RenderFlags.None);
        var first = preparer.Latest;

        preparer.Visit(document, Current, Scale, PageRotation.None, RenderFlags.None);

        await Assert.That(ReferenceEquals(preparer.Latest, first)).IsTrue();
        await first.WaitAsync(cancellationToken);
    }

    /// <summary>A page turn before the wait ends cancels the earlier run, so only the new page's neighbours are prepared.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task QuickTurnCancelsTheEarlierRun(CancellationToken cancellationToken)
    {
        var time = new ManualTimeProvider();
        using var preparer = new NeighbourPagePreparer(static () => false, static () => false, TimeSpan.FromMilliseconds(LongWaitMilliseconds), time);
        var document = Document();
        preparer.Visit(document, 0, Scale, PageRotation.None, RenderFlags.None);
        var first = preparer.Latest;

        // Both settle waits are still held when the second turn comes, so the first run cannot have prepared anything.
        preparer.Visit(document, Current, Scale, PageRotation.None, RenderFlags.None);
        time.Release();
        await preparer.Latest.WaitAsync(cancellationToken);
        await first.WaitAsync(cancellationToken);

        await Assert.That(document.PreparedAhead).IsEquivalentTo([Next, Further, Previous]);
    }

    /// <summary>When something on screen needs drawing mid-preparation, the page in hand is cut short, and is prepared again once the screen is served.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task VisibleWorkPreemptsAndThenPreparationResumes(CancellationToken cancellationToken)
    {
        var screen = new Screen();
        using var preparer = Create(screen.HasWork, static () => false);
        var document = Document();
        document.Preparation = screen.HoldFirstPreparationOf(Next);

        screen.Busy = true;
        preparer.Visit(document, Current, Scale, PageRotation.None, RenderFlags.None);
        await screen.Started.WaitAsync(cancellationToken);
        preparer.Preempt();
        screen.Busy = false;
        await preparer.Latest.WaitAsync(cancellationToken);

        await Assert.That(document.PreparedAhead).IsEquivalentTo([Next, Further, Previous]);
    }

    /// <summary>Resetting stops the run and a later visit to the same page starts a new one.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResetAllowsTheSamePageAgain(CancellationToken cancellationToken)
    {
        using var preparer = Create(static () => false, static () => false);
        var document = Document();
        preparer.Visit(document, Current, Scale, PageRotation.None, RenderFlags.None);
        await preparer.Latest.WaitAsync(cancellationToken);
        document.PreparedAhead.Clear();

        preparer.Reset();
        preparer.Visit(document, Current, Scale, PageRotation.None, RenderFlags.None);
        await preparer.Latest.WaitAsync(cancellationToken);

        await Assert.That(document.PreparedAhead.Count).IsEqualTo(NeighbourCount);
    }

    /// <summary>Makes a preparer with no wait.</summary>
    /// <param name="hasVisibleWork">Tells whether the screen has work.</param>
    /// <param name="underPressure">Tells whether memory is short.</param>
    /// <returns>The preparer.</returns>
    private static NeighbourPagePreparer Create(Func<bool> hasVisibleWork, Func<bool> underPressure) =>
        new(hasVisibleWork, underPressure, TimeSpan.Zero, TimeProvider.System);

    /// <summary>Makes a document of equal pages.</summary>
    /// <returns>The document.</returns>
    private static FakeDocument Document()
    {
        var sizes = new PageSize[Pages];
        Array.Fill(sizes, new(PageSide, PageSide));
        return new("neighbours.pdf", sizes);
    }

    /// <summary>A stand-in for the screen's render queue.</summary>
    private sealed class Screen
    {
        /// <summary>Whether the screen has work waiting.</summary>
        private volatile bool _busy;

        /// <summary>Whether the page that is held has been held already.</summary>
        private bool _held;

        /// <summary>Gets a task that completes when a preparation is being held.</summary>
        internal TaskCompletionSource StartedSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets a task that completes when a preparation is being held.</summary>
        internal Task Started => StartedSource.Task;

        /// <summary>Gets or sets a value indicating whether the screen has work waiting.</summary>
        internal bool Busy
        {
            get => _busy;
            set => _busy = value;
        }

        /// <summary>Tells whether the screen has work waiting.</summary>
        /// <returns>The state.</returns>
        internal bool HasWork() => _busy;

        /// <summary>Makes preparation of one page wait until it is cancelled, the first time only.</summary>
        /// <param name="page">The page to hold.</param>
        /// <returns>The preparation to give the document.</returns>
        internal Func<int, CancellationToken, ValueTask> HoldFirstPreparationOf(int page) => async (candidate, token) =>
        {
            if (_held || candidate != page)
            {
                return;
            }

            _held = true;
            _ = StartedSource.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
        };
    }
}
