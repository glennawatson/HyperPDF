// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Accessibility;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Reading;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Help;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Windowing.Windows;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Accessibility;

/// <summary>
/// What a screen reader or magnifier sees of the viewer: the application, its window, the document with the text of the page shown,
/// and that page's headings, paragraphs, list items, table cells and figures in reading order. The window thread builds an
/// unchanging snapshot and the platform bridges read it from any thread. Nothing is built until an assistive technology first asks.
/// </summary>
[DebuggerDisplay("ViewerAccessibleTree: {_current}")]
public sealed class ViewerAccessibleTree : IAccessibleTree, IDisposable
{
    /// <summary>The changes between snapshots.</summary>
    private readonly Signal<AccessibleChange> _changes = new();

    /// <summary>The window, woken when an assistive technology first asks for the tree.</summary>
    private readonly IPlatformWindow _window;

    /// <summary>The snapshot the bridges read.</summary>
    private AccessibleSnapshot _current = AccessibleSnapshot.Empty;

    /// <summary>The tab the cached page text belongs to.</summary>
    private DocumentTabViewModel? _textTab;

    /// <summary>The page the cached text belongs to, or -1 when nothing is cached.</summary>
    private int _textPage = -1;

    /// <summary>The cached text of the page.</summary>
    private string _text = string.Empty;

    /// <summary>One when the viewer changed since the snapshot was built.</summary>
    private int _stale = 1;

    /// <summary>One once an assistive technology has asked for the tree.</summary>
    private int _wanted;

    /// <summary>Initializes a new instance of the <see cref="ViewerAccessibleTree"/> class.</summary>
    /// <param name="window">The window the tree describes.</param>
    public ViewerAccessibleTree(IPlatformWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
    }

    /// <inheritdoc/>
    public int RootId => ViewerNodes.ApplicationId;

    /// <inheritdoc/>
    public int FocusedId => Volatile.Read(ref _current).FocusedId;

    /// <inheritdoc/>
    public IObservable<AccessibleChange> Changes => _changes;

    /// <inheritdoc/>
    public AccessibleNode? Get(int id)
    {
        Want();
        return Volatile.Read(ref _current).Nodes.GetValueOrDefault(id);
    }

    /// <inheritdoc/>
    public int HitTest(int x, int y)
    {
        Want();
        return Volatile.Read(ref _current).HitTest(x, y);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool DoAction(int id, int action) => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Focus(int id) => id == ViewerNodes.DocumentId;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetCaret(int id, int offset) => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _changes.Dispose();

    /// <summary>Notes that what the viewer shows changed, so the next refresh builds a new snapshot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invalidate() => Volatile.Write(ref _stale, 1);

    /// <summary>Builds a new snapshot when the viewer changed and an assistive technology is reading, and tells the bridges what differs. Runs on the window thread.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="canvas">The page canvas.</param>
    public void RefreshIfRead(MainViewModel main, ViewerCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(canvas);
        if (Volatile.Read(ref _wanted) == 0 || Interlocked.Exchange(ref _stale, 0) == 0)
        {
            return;
        }

        var previous = Volatile.Read(ref _current);
        var next = Build(main, canvas);
        Volatile.Write(ref _current, next);
        foreach (var change in SnapshotDiff.Compare(previous, next))
        {
            _changes.OnNext(change);
        }
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

    /// <summary>Finds where a list item is in its list: its place in the run of list items around it and the length of that run.</summary>
    /// <param name="blocks">The blocks of the page.</param>
    /// <param name="index">The block.</param>
    /// <returns>The place, or the default when the block is not a list item.</returns>
    private static ListPlace PlaceInList(IReadOnlyList<ReadingBlock> blocks, int index)
    {
        if (blocks[index].Kind != ReadingBlockKind.ListItem)
        {
            return default;
        }

        var first = index;
        while (first > 0 && blocks[first - 1].Kind == ReadingBlockKind.ListItem)
        {
            first--;
        }

        var last = index;
        while (last + 1 < blocks.Count && blocks[last + 1].Kind == ReadingBlockKind.ListItem)
        {
            last++;
        }

        return new(index - first + 1, last - first + 1);
    }

    /// <summary>Turns a canvas rectangle into a place on the screen.</summary>
    /// <param name="rect">The rectangle in canvas units.</param>
    /// <param name="offset">The scroll offset in canvas units.</param>
    /// <param name="scale">Device pixels per canvas unit.</param>
    /// <param name="origin">Where the window's drawing area is on the screen.</param>
    /// <returns>The rectangle in screen pixels.</returns>
    private static AccessibleBounds ToScreen(in LayoutRect rect, CanvasPoint offset, double scale, ScreenPoint origin) => new(
        origin.X + (int)Math.Round((rect.X - offset.X) * scale),
        origin.Y + (int)Math.Round((rect.Y - offset.Y) * scale),
        (int)Math.Round(rect.Width * scale),
        (int)Math.Round(rect.Height * scale));

    /// <summary>Notes that an assistive technology is reading, and wakes the window the first time so it builds the tree.</summary>
    private void Want()
    {
        if (Interlocked.Exchange(ref _wanted, 1) == 0)
        {
            _window.Wake();
        }
    }

    /// <summary>Builds the snapshot of what the viewer shows now.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="canvas">The page canvas.</param>
    /// <returns>The snapshot.</returns>
    private AccessibleSnapshot Build(MainViewModel main, ViewerCanvas canvas)
    {
        var size = _window.ClientSize;
        var origin = _window.ScreenOrigin;
        var area = new AccessibleBounds(origin.X, origin.Y, size.Width, size.Height);
        var nodes = new Dictionary<int, AccessibleNode>
        {
            [ViewerNodes.ApplicationId] = ViewerNodes.CreateApplication([ViewerNodes.WindowId]),
            [ViewerNodes.WindowId] = ViewerNodes.CreateWindow(main.WindowTitle, area, _window.NativeHandle),
        };
        var tab = main.SelectedTab;
        var name = ItemDescriptions.DescribeReader(tab);
        var help = ItemDescriptions.DescribeSelection(tab);
        if (tab is not { PageCount: > 0 })
        {
            nodes[ViewerNodes.DocumentId] = ViewerNodes.CreateDocument(name, help, area, string.Empty, []);
            return new(nodes, ViewerNodes.ApplicationId, ViewerNodes.DocumentId);
        }

        var page = Math.Clamp(tab.CurrentPageIndex, 0, tab.PageCount - 1);
        var children = AddBlocks(nodes, tab, canvas, page, origin);
        nodes[ViewerNodes.DocumentId] = ViewerNodes.CreateDocument(name, help, area, GetPageText(tab, page), children);
        return new(nodes, ViewerNodes.ApplicationId, ViewerNodes.DocumentId);
    }

    /// <summary>Adds the blocks of a page in reading order.</summary>
    /// <param name="nodes">The elements so far.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="page">The page shown.</param>
    /// <param name="origin">Where the window's drawing area is on the screen.</param>
    /// <returns>The numbers of the block elements; none until the reading order has been worked out.</returns>
    private int[] AddBlocks(Dictionary<int, AccessibleNode> nodes, DocumentTabViewModel tab, ViewerCanvas canvas, int page, ScreenPoint origin)
    {
        // The reading order is worked out in the background, so a page has no blocks until it is ready.
        if (tab.GetReadingDocument() is not { } reading || page >= reading.PageCount)
        {
            return [];
        }

        var blocks = reading.GetPage(page).Blocks;
        var children = new int[blocks.Count];
        var scale = _window.Scale;
        var offset = canvas.Offset;
        for (var i = 0; i < blocks.Count; i++)
        {
            var id = ViewerNodes.FirstBlockId + i;
            var rect = canvas.State.GetCanvasRect(page, blocks[i].Bounds);
            nodes[id] = ViewerNodes.CreateBlock(id, blocks[i], ToScreen(rect, offset, scale, origin), PlaceInList(blocks, i));
            children[i] = id;
        }

        return children;
    }

    /// <summary>Gets a page's text, reading it once for each page shown.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <returns>The text.</returns>
    private string GetPageText(DocumentTabViewModel tab, int page)
    {
        if (!ReferenceEquals(tab, _textTab) || page != _textPage)
        {
            _text = ReadPage(tab, page);
            _textTab = tab;
            _textPage = page;
        }

        return _text;
    }
}
