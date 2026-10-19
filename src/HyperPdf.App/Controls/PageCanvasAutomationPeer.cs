// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.Core.Reading;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Controls;

/// <summary>
/// Tells assistive technology which page the reader is on, for example "Page 5 of 40", and gives it that page's text
/// as a read-only value. Avalonia has no text pattern, so the value is how a screen reader reaches the words.
/// </summary>
[DebuggerDisplay("PageCanvasAutomationPeer: {GetName()}")]
internal sealed class PageCanvasAutomationPeer : ControlAutomationPeer, IValueProvider
{
    /// <summary>The name when no document is shown.</summary>
    internal const string NoDocumentName = "No document";

    /// <summary>The page the cached text belongs to, or -1 when nothing is cached.</summary>
    private int _textPage = -1;

    /// <summary>The cached text of <see cref="_textPage"/>.</summary>
    private string _text = string.Empty;

    /// <summary>The page the cached blocks belong to, or -1 when nothing is cached.</summary>
    private int _blocksPage = -1;

    /// <summary>The cached peers of <see cref="_blocksPage"/>' blocks, in reading order.</summary>
    private ReadingBlockAutomationPeer[] _blocks = [];

    /// <summary>The name last reported.</summary>
    private string _name;

    /// <summary>Initializes a new instance of the <see cref="PageCanvasAutomationPeer"/> class.</summary>
    /// <param name="owner">The page canvas.</param>
    public PageCanvasAutomationPeer(PageCanvas owner)
        : base(owner)
    {
        Canvas = owner;
        _name = Describe(owner.Tab);

        // The peer lives as long as its canvas and only references it, so the subscription needs no owner. A new page,
        // page count or tab gives a new name, which is announced when it differs from the one last reported.
        _ = owner.WhenChanged(static x => x.Tab)
            .SwitchMap(static tab => tab is null
                ? Signal.Return(RxVoid.Default)
                : Signal.Merge(tab.WhenChanged(static x => x.CurrentPageIndex).AsVoid, tab.WhenChanged(static x => x.PageCount).AsVoid))

            // The steps read the peer's own name: a constructor cannot hand 'this' to a state-passing operator.
            .Select(_ => GetName())
            .Where(name => !string.Equals(name, _name, StringComparison.Ordinal))
            .SubscribeSafe(AnnouncePage, ErrorTrace.Write);
    }

    /// <inheritdoc/>
    public bool IsReadOnly => true;

    /// <inheritdoc/>
    public string? Value => GetPageText();

    /// <summary>Gets the canvas the peer describes.</summary>
    private PageCanvas Canvas { get; }

    /// <inheritdoc/>
    public void SetValue(string? value) => throw new InvalidOperationException("The page text cannot be changed.");

    /// <summary>Describes where the reader is, for example "Page 5 of 40", using the document's own page labels.</summary>
    /// <param name="tab">The tab shown, or <see langword="null"/>.</param>
    /// <returns>The description.</returns>
    internal static string Describe(DocumentTabViewModel? tab)
    {
        if (tab is not { PageCount: > 0 } shown)
        {
            return NoDocumentName;
        }

        var index = Math.Clamp(shown.CurrentPageIndex, 0, shown.PageCount - 1);
        return ItemAutomation.DescribePage(shown.GetPageDisplay(index), index, shown.PageCount);
    }

    /// <summary>Gets the canvas rectangle of a block on a page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="block">The block.</param>
    /// <returns>The rectangle on the canvas, or an empty rectangle when the page is not laid out.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Rect GetCanvasRect(int page, ReadingBlock block) => Canvas.GetCanvasRect(page, block.Bounds);

    /// <summary>
    /// Lists the children afresh each time, because the page's blocks are worked out in the background and a list kept before they were
    /// ready would stay empty. The blocks themselves are kept once they are known.
    /// </summary>
    /// <returns>The children.</returns>
    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => GetChildrenCore() ?? [];

    /// <summary>Lists the shown page's blocks in reading order after any children the canvas has, so a screen reader can move by heading, list item, table cell and figure.</summary>
    /// <returns>The children.</returns>
    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
    {
        var inherited = base.GetChildrenCore();
        var blocks = GetBlocks();
        if (blocks.Length == 0)
        {
            return inherited;
        }

        return inherited is not { Count: > 0 } ? blocks : [.. inherited, .. blocks];
    }

    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

    /// <inheritdoc/>
    protected override string GetLocalizedControlTypeCore() => "document";

    /// <inheritdoc/>
    protected override string? GetNameCore()
    {
        // A name set on the control wins, as it does for every other control.
        var name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? Describe(Canvas.Tab) : name;
    }

    /// <summary>Reads a page's text, in reading order when the document can describe its layout.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <returns>The text.</returns>
    private static string ReadPage(DocumentTabViewModel tab, int page)
    {
        if (tab.GetReadingDocument() is { } reading && page < reading.PageCount)
        {
            return ReadingDocument.Flatten(reading.GetPage(page), out _);
        }

        return tab.TryGetDocument() is { } document ? document.GetText(page, 0, document.GetCharacterCount(page)) : string.Empty;
    }

    /// <summary>Gets the current page's text in reading order, reading it once per page.</summary>
    /// <returns>The text, or an empty string when there is none.</returns>
    private string GetPageText()
    {
        if (Canvas.Tab is not { PageCount: > 0 } tab)
        {
            return string.Empty;
        }

        var page = Math.Clamp(tab.CurrentPageIndex, 0, tab.PageCount - 1);
        if (page != _textPage)
        {
            _text = ReadPage(tab, page);
            _textPage = page;
        }

        return _text;
    }

    /// <summary>Gets the peers of the current page's blocks, reading them once per page.</summary>
    /// <returns>The peers; none when the page has no reading order.</returns>
    private ReadingBlockAutomationPeer[] GetBlocks()
    {
        if (Canvas.Tab is not { PageCount: > 0 } tab)
        {
            return [];
        }

        var page = Math.Clamp(tab.CurrentPageIndex, 0, tab.PageCount - 1);
        if (page == _blocksPage)
        {
            return _blocks;
        }

        // The reading order is worked out in the background, so nothing is kept until it can describe the page.
        if (tab.GetReadingDocument() is not { } reading || page >= reading.PageCount)
        {
            return [];
        }

        _blocks = CreateBlocks(reading.GetPage(page));
        _blocksPage = page;
        return _blocks;
    }

    /// <summary>Makes a peer for each block of a page.</summary>
    /// <param name="page">The page's blocks.</param>
    /// <returns>The peers, in reading order.</returns>
    private ReadingBlockAutomationPeer[] CreateBlocks(ReadingPage page)
    {
        var peers = new ReadingBlockAutomationPeer[page.Blocks.Count];
        var runStart = 0;
        for (var index = 0; index < peers.Length; index++)
        {
            var block = page.Blocks[index];
            if (block.Kind != ReadingBlockKind.ListItem)
            {
                peers[index] = new(this, page.PageIndex, index, block, 0, 0);
                continue;
            }

            runStart = index > 0 && page.Blocks[index - 1].Kind == ReadingBlockKind.ListItem ? runStart : index;
            var runEnd = index;
            while (runEnd + 1 < peers.Length && page.Blocks[runEnd + 1].Kind == ReadingBlockKind.ListItem)
            {
                runEnd++;
            }

            peers[index] = new(this, page.PageIndex, index, block, index - runStart + 1, runEnd - runStart + 1);
        }

        return peers;
    }

    /// <summary>Tells assistive technology the page, and with it the name and the text, changed.</summary>
    /// <param name="name">The new name, for example "Page 6 of 40".</param>
    private void AnnouncePage(string name)
    {
        var oldName = _name;
        _name = name;
        var oldText = _textPage < 0 ? null : _text;
        _textPage = -1;
        _blocksPage = -1;
        InvalidateChildren();
        RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, oldName, name);
        RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, oldText, GetPageText());
    }
}
