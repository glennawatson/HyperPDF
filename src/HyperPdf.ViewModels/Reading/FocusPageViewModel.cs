// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Reading;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Reading;

/// <summary>One page of Focus Mode: its blocks in reading order, worked out off the UI thread when the page comes into view.</summary>
[DebuggerDisplay("FocusPageViewModel: Page {PageIndex}")]
public sealed partial class FocusPageViewModel : ReactiveObject
{
    /// <summary>The separator between blocks in a page's reading text.</summary>
    private const int SeparatorLength = 2;

    /// <summary>The state before the page is read, or after a read found the document closed or failed.</summary>
    private const int Unloaded = 0;

    /// <summary>The state while the page is being read.</summary>
    private const int Loading = 1;

    /// <summary>The state once the blocks are shown.</summary>
    private const int Loaded = 2;

    /// <summary>Gets the page's reading order.</summary>
    private readonly Func<ReadingDocument?> _reading;

    /// <summary>Where the page is in being read: <see cref="Unloaded"/>, <see cref="Loading"/> or <see cref="Loaded"/>.</summary>
    private int _state;

    /// <summary>Initializes a new instance of the <see cref="FocusPageViewModel"/> class.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="label">The page's label, for example "Page 3" or "Page iv".</param>
    /// <param name="reading">Gets the document's reading order.</param>
    public FocusPageViewModel(int pageIndex, string label, Func<ReadingDocument?> reading)
    {
        PageIndex = pageIndex;
        Label = label;
        _reading = reading;
    }

    /// <summary>Gets the page.</summary>
    public int PageIndex { get; }

    /// <summary>Gets the page's label.</summary>
    public string Label { get; }

    /// <summary>Gets the blocks, empty until loaded.</summary>
    [Reactive]
    public partial IReadOnlyList<FocusBlockViewModel> Blocks { get; private set; } = [];

    /// <summary>Gets a value indicating whether the page has no text to show, once loaded.</summary>
    [Reactive]
    public partial bool IsEmpty { get; private set; }

    /// <summary>
    /// Works out the page's blocks on the pool and shows them on the UI thread. A page that is loading or loaded completes at
    /// once. A page asked for while the document is closed or reloading has nothing to read yet, so nothing is kept and it is
    /// asked again later.
    /// </summary>
    /// <returns>A cold signal that emits once the blocks are shown and completes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> Load() => Signal.Defer(StartLoad);

    /// <summary>Marks what is being read aloud on this page, or clears the marks.</summary>
    /// <param name="spoken">The sentence in the page's reading text, or none.</param>
    /// <param name="word">The word, or none.</param>
    /// <param name="focusBand">Whether blocks away from the sentence are dimmed.</param>
    public void Mark(TextRange spoken, TextRange word, bool focusBand)
    {
        foreach (var block in Blocks)
        {
            block.Mark(spoken, word, focusBand);
        }
    }

    /// <summary>Counts the consecutive list items starting at a block.</summary>
    /// <param name="blocks">The page's blocks.</param>
    /// <param name="start">The first item.</param>
    /// <returns>The number of items.</returns>
    private static int ListLength(IReadOnlyList<ReadingBlock> blocks, int start)
    {
        var end = start;
        while (end < blocks.Count && blocks[end].Kind == ReadingBlockKind.ListItem)
        {
            end++;
        }

        return end - start;
    }

    /// <summary>Claims the load and reads the page on the pool, or completes at once when there is nothing to do.</summary>
    /// <returns>The signal for this subscription.</returns>
    private IObservable<RxVoid> StartLoad() =>
        _reading() is not { } reading || Interlocked.CompareExchange(ref _state, Loading, Unloaded) != Unloaded
            ? Signal.Return(RxVoid.Default)
            : Signal.Start(() => reading.GetPage(PageIndex), TaskPoolSequencer.Default)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Select(Show)
                .Catch<RxVoid, Exception>(Release);

    /// <summary>Shows the page that was read.</summary>
    /// <param name="page">The page, or <see langword="null"/> when the document closed while it was read.</param>
    /// <returns>The completion value.</returns>
    private RxVoid Show(ReadingPage? page)
    {
        if (page is null)
        {
            // The document closed while the page was read; the page is not empty, it is not readable yet.
            Volatile.Write(ref _state, Unloaded);
            return RxVoid.Default;
        }

        Blocks = Build(page);
        IsEmpty = Blocks.Count == 0;
        Volatile.Write(ref _state, Loaded);
        return RxVoid.Default;
    }

    /// <summary>Lets the page be asked for again after a failed read.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A signal that fails with the same error.</returns>
    private IObservable<RxVoid> Release(Exception error)
    {
        Volatile.Write(ref _state, Unloaded);
        return Signal.Throw<RxVoid>(error);
    }

    /// <summary>Makes the block view models, with each block's place in the page's reading text.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The blocks.</returns>
    private FocusBlockViewModel[] Build(ReadingPage page)
    {
        var blocks = new FocusBlockViewModel[page.Blocks.Count];
        var offset = 0;
        var listStart = 0;
        for (var i = 0; i < blocks.Length; i++)
        {
            // Consecutive list items form one list, so each can say "item 2 of 5".
            var isItem = page.Blocks[i].Kind == ReadingBlockKind.ListItem;
            listStart = isItem && i > 0 && page.Blocks[i - 1].Kind == ReadingBlockKind.ListItem ? listStart : i;
            blocks[i] = new(PageIndex, page.Blocks[i], offset) { PositionInList = isItem ? i - listStart + 1 : 0, ListSize = isItem ? ListLength(page.Blocks, listStart) : 0 };
            offset += page.Blocks[i].Text.Length + SeparatorLength;
        }

        return blocks;
    }
}
