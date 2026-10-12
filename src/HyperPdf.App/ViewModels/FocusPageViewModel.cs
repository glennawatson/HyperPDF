// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Reading;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>One page of Focus Mode: its blocks in reading order, worked out off the UI thread when the page comes into view.</summary>
[DebuggerDisplay("FocusPageViewModel: Page {PageIndex}")]
public sealed partial class FocusPageViewModel : ReactiveObject
{
    /// <summary>The separator between blocks in a page's reading text.</summary>
    private const int SeparatorLength = 2;

    /// <summary>Gets the page's reading order.</summary>
    private readonly Func<ReadingDocument?> _reading;

    /// <summary>Loads the blocks once.</summary>
    private readonly Lazy<Task> _load;

    /// <summary>Initializes a new instance of the <see cref="FocusPageViewModel"/> class.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="label">The page's label, for example "Page 3" or "Page iv".</param>
    /// <param name="reading">Gets the document's reading order.</param>
    public FocusPageViewModel(int pageIndex, string label, Func<ReadingDocument?> reading)
    {
        PageIndex = pageIndex;
        Label = label;
        _reading = reading;
        _load = new(LoadCoreAsync);
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

    /// <summary>Works out the page's blocks once, off the UI thread; later calls wait for the same work.</summary>
    /// <returns>A task completing once the blocks are shown.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Task LoadAsync() => _load.Value;

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

    /// <summary>Works out the page's blocks off the UI thread.</summary>
    /// <returns>A task.</returns>
    private async Task LoadCoreAsync()
    {
        var reading = _reading();
        var page = reading is null ? null : await Task.Run(() => reading.GetPage(PageIndex)).ConfigureAwait(true);
        Blocks = page is null ? [] : Build(page);
        IsEmpty = Blocks.Count == 0;
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
