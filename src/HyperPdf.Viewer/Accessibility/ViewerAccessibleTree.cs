// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Accessibility;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Reading;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Help;
using HyperPdf.ViewModels.Reading;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer.Chrome;
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

    /// <summary>Carries the controls an assistive technology asked to press, from the bridge's thread.</summary>
    private readonly Signal<int> _presses = new();

    /// <summary>Carries the controls an assistive technology asked to give the keyboard to, from the bridge's thread.</summary>
    private readonly Signal<int> _focusRequests = new();

    /// <summary>Carries each place in the page a screen reader asked to put the caret.</summary>
    private readonly Signal<CharacterPosition> _caretRequests = new();

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

    /// <summary>The page character each character of <see cref="_text"/> comes from, or <see langword="null"/> when they are the same.</summary>
    private int[]? _map;

    /// <summary>What the snapshot was built from.</summary>
    private TreeFacts _facts;

    /// <summary>Whether a snapshot has been built.</summary>
    private bool _built;

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

    /// <summary>Gets the ids of the controls an assistive technology asked to press. They arrive on the bridge's thread, so the window brings them to its own.</summary>
    public IObservable<int> Presses => _presses;

    /// <summary>Gets the ids of the controls an assistive technology asked to give the keyboard to. They arrive on the bridge's thread, so the window brings them to its own.</summary>
    public IObservable<int> FocusRequests => _focusRequests;

    /// <summary>Gets the places in the page a screen reader asked to put the caret. Whoever follows them moves the caret on the window thread.</summary>
    public IObservable<CharacterPosition> CaretRequests => _caretRequests;

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
    public bool DoAction(int id, int action)
    {
        if (id < ViewerNodes.FirstControlId || action != 0 || Volatile.Read(ref _current).Nodes.GetValueOrDefault(id) is not { Actions.Count: > 0 })
        {
            return false;
        }

        // A bridge asks from its own thread. Whoever follows the presses does them on the window thread.
        _presses.OnNext(id);
        return true;
    }

    /// <inheritdoc/>
    public bool Focus(int id)
    {
        if (id == ViewerNodes.DocumentId)
        {
            return true;
        }

        // A control takes the keyboard on the window thread; the answer says whether the control can take it.
        if (id is < ViewerNodes.FirstControlId or >= ViewerNodes.FirstPageItemId || Volatile.Read(ref _current).Nodes.GetValueOrDefault(id) is null)
        {
            return false;
        }

        _focusRequests.OnNext(id);
        return true;
    }

    /// <inheritdoc/>
    public bool SetCaret(int id, int offset)
    {
        var map = Volatile.Read(ref _map);
        var length = Volatile.Read(ref _text).Length;
        if (id != ViewerNodes.DocumentId || (uint)offset > (uint)length || _textPage < 0)
        {
            return false;
        }

        // A bridge asks from its own thread. Whoever follows the requests moves the caret on the window thread.
        _caretRequests.OnNext(new(_textPage, CharacterAt(map, offset)));
        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _changes.Dispose();
        _presses.Dispose();
        _focusRequests.Dispose();
        _caretRequests.Dispose();
    }

    /// <summary>Has a screen reader speak a message, such as how many matches a search found. Nothing is sent until an assistive technology is reading.</summary>
    /// <param name="message">The words to speak.</param>
    public void Announce(string message)
    {
        if (Volatile.Read(ref _wanted) != 0 && message is { Length: > 0 })
        {
            _changes.OnNext(new(AccessibleChangeKind.Announcement, ViewerNodes.WindowId, null, null) { Message = message });
        }
    }

    /// <summary>
    /// Builds a new snapshot when an assistive technology is reading and what the tree shows changed, and tells the bridges what
    /// differs. Runs on the window thread after every turn of its loop, so the check for a change reads a few values and allocates nothing.
    /// </summary>
    /// <param name="main">The main view model.</param>
    /// <param name="canvas">The page canvas.</param>
    public void RefreshIfRead(MainViewModel main, ViewerCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(canvas);
        if (Volatile.Read(ref _wanted) == 0)
        {
            return;
        }

        var facts = TreeFacts.Read(main.SelectedTab, canvas, _window);
        if (_built && facts == _facts)
        {
            return;
        }

        _facts = facts;
        _built = true;
        var previous = Volatile.Read(ref _current);
        var next = Build(main, canvas);
        Volatile.Write(ref _current, next);
        foreach (var change in SnapshotDiff.Compare(previous, next))
        {
            _changes.OnNext(change);
        }
    }

    /// <summary>Finds the page character a place in the text a screen reader reads comes from.</summary>
    /// <param name="map">The page character each character of the text comes from, or <see langword="null"/> when they are the same.</param>
    /// <param name="offset">The offset in the text.</param>
    /// <returns>The index of the character in the page.</returns>
    private static int CharacterAt(int[]? map, int offset)
    {
        if (map is null)
        {
            return offset;
        }

        // A character added between blocks has no place on the page, so the caret goes to the next one that has.
        for (var i = offset; i < map.Length; i++)
        {
            if (map[i] >= 0)
            {
                return map[i];
            }
        }

        for (var i = Math.Min(offset, map.Length) - 1; i >= 0; i--)
        {
            if (map[i] >= 0)
            {
                return map[i] + 1;
            }
        }

        return 0;
    }

    /// <summary>Reads a page's text, in reading order when the document can describe its layout.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="map">Receives the page character each character of the text comes from, or <see langword="null"/> when they are the same.</param>
    /// <returns>The text.</returns>
    private static string ReadPage(DocumentTabViewModel tab, int page, out int[]? map)
    {
        if (tab.GetReadingDocument() is { } reading && page < reading.PageCount)
        {
            return ReadingDocument.Flatten(reading.GetPage(page), out map);
        }

        map = null;
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

    /// <summary>Adds the window's controls that have text to read or something to press.</summary>
    /// <param name="nodes">The elements so far.</param>
    /// <param name="inWindow">The numbers of the window's children, which get the controls added.</param>
    /// <param name="items">The controls, back to front.</param>
    /// <param name="firstId">The number of the first control.</param>
    /// <param name="origin">Where the window's drawing area is on the screen.</param>
    /// <param name="focus">The place in the list of the control the keyboard is on, or -1.</param>
    /// <returns>The number of the text box being typed into, or -1 when there is none.</returns>
    private static int AddControls(Dictionary<int, AccessibleNode> nodes, List<int> inWindow, IReadOnlyList<ChromeItem> items, int firstId, ScreenPoint origin, int focus)
    {
        var typing = -1;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!ViewerNodes.IsPublished(item.Kind) || item.Text.Length == 0)
            {
                continue;
            }

            var bounds = item.Bounds;
            var id = firstId + i;
            nodes[id] = ViewerNodes.CreateControl(id, item, new(origin.X + (int)bounds.X, origin.Y + (int)bounds.Y, (int)bounds.Width, (int)bounds.Height), i == focus);
            inWindow.Add(id);
            if (item is { Kind: ChromeItemKind.Field, Selected: true })
            {
                typing = id;
            }
        }

        return typing;
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
        var pages = canvas.Bounds;
        var pageArea = new AccessibleBounds(origin.X + (int)pages.X, origin.Y + (int)pages.Y, (int)pages.Width, (int)pages.Height);
        var nodes = new Dictionary<int, AccessibleNode>();

        // The window's children go back to front, so a control over the pages is found before the document under it.
        var inWindow = new List<int> { ViewerNodes.DocumentId };
        var focused = ViewerNodes.DocumentId;
        if (canvas.Chrome is { } chrome)
        {
            _ = AddControls(nodes, inWindow, chrome.Bars, ViewerNodes.FirstControlId, origin, chrome.FocusIsInOverlay ? -1 : chrome.FocusedControl);
            var typing = AddControls(nodes, inWindow, chrome.OverlayItems, ViewerNodes.FirstOverlayId, origin, chrome.FocusIsInOverlay ? chrome.FocusedControl : -1);
            focused = typing >= 0 ? typing : focused;
            if (chrome.FocusedControl >= 0)
            {
                // The keyboard is on a control, which is where a screen reader reads from.
                focused = (chrome.FocusIsInOverlay ? ViewerNodes.FirstOverlayId : ViewerNodes.FirstControlId) + chrome.FocusedControl;
            }
        }

        nodes[ViewerNodes.ApplicationId] = ViewerNodes.CreateApplication([ViewerNodes.WindowId]);
        nodes[ViewerNodes.WindowId] = ViewerNodes.CreateWindow(main.WindowTitle, new(origin.X, origin.Y, size.Width, size.Height), _window.NativeHandle, inWindow);
        var tab = main.SelectedTab;
        var name = ItemDescriptions.DescribeReader(tab);
        var help = ItemDescriptions.DescribeSelection(tab);
        if (tab is not { PageCount: > 0 })
        {
            nodes[ViewerNodes.DocumentId] = ViewerNodes.CreateDocument(name, help, pageArea, AccessibleText.Plain(string.Empty), []);
            return new(nodes, ViewerNodes.ApplicationId, focused);
        }

        var page = Math.Clamp(tab.CurrentPageIndex, 0, tab.PageCount - 1);
        int[] children = [.. AddBlocks(nodes, tab, canvas, page, origin), .. AddPageItems(nodes, tab, canvas, page, origin)];
        nodes[ViewerNodes.DocumentId] = ViewerNodes.CreateDocument(name, help, pageArea, GetPageText(tab, page, canvas), children);
        return new(nodes, ViewerNodes.ApplicationId, focused);
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
        var offset = canvas.DrawOffset;
        for (var i = 0; i < blocks.Count; i++)
        {
            var id = ViewerNodes.FirstBlockId + i;
            var rect = canvas.State.GetCanvasRect(page, blocks[i].Bounds);
            nodes[id] = ViewerNodes.CreateBlock(id, blocks[i], ToScreen(rect, offset, scale, origin), PlaceInList(blocks, i));
            children[i] = id;
        }

        return children;
    }

    /// <summary>Adds the form fields, links and notes of a page, so a screen reader can find, read and press them.</summary>
    /// <param name="nodes">The elements so far.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="page">The page shown.</param>
    /// <param name="origin">Where the window's drawing area is on the screen.</param>
    /// <returns>The numbers of the elements added.</returns>
    private List<int> AddPageItems(Dictionary<int, AccessibleNode> nodes, DocumentTabViewModel tab, ViewerCanvas canvas, int page, ScreenPoint origin)
    {
        var added = new List<int>();
        if (DocumentFeature<IPageSemanticSource>.Get(tab.TryGetDocument())?.GetItems(page) is not { } items)
        {
            return added;
        }

        var scale = _window.Scale;
        var offset = canvas.DrawOffset;
        for (var i = 0; i < items.Count; i++)
        {
            // Empty table cells and page furniture are read with the page's text. Hidden layers are left out.
            var item = items[i];
            if (item.IsHidden || item.Kind is not (PageSemanticKind.FormField or PageSemanticKind.Annotation))
            {
                continue;
            }

            var id = ViewerNodes.PageItemId(page, i);
            nodes[id] = ViewerNodes.CreatePageItem(id, item, ToScreen(canvas.State.GetCanvasRect(page, item.Bounds), offset, scale, origin));
            added.Add(id);
        }

        return added;
    }

    /// <summary>Gets a page's text, reading it once for each page shown.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="canvas">The page canvas, which knows where the caret and the selection are.</param>
    /// <returns>The text, with the caret and the selection in it.</returns>
    private AccessibleText GetPageText(DocumentTabViewModel tab, int page, ViewerCanvas canvas)
    {
        if (!ReferenceEquals(tab, _textTab) || page != _textPage)
        {
            _text = ReadPage(tab, page, out var map);
            _map = map;
            _textTab = tab;
            _textPage = page;
        }

        var caret = canvas.State.Caret;
        if (caret.Page != page)
        {
            return AccessibleText.Plain(_text);
        }

        var at = OffsetOf(caret.Char);
        if (!canvas.State.Selection.TryGet(out var start, out var end) || start.Page > page || end.Page < page)
        {
            return new(_text, at, -1, -1);
        }

        // A selection that runs on from an earlier page or to a later one covers the page from its start or to its end.
        var from = start.Page < page ? 0 : OffsetOf(start.Char);
        var to = end.Page > page ? _text.Length : OffsetOf(end.Char);
        return new(_text, at, from, to);
    }

    /// <summary>Finds where a character of the page is in the text a screen reader reads.</summary>
    /// <param name="character">The character's index in the page.</param>
    /// <returns>The offset in the text.</returns>
    private int OffsetOf(int character)
    {
        if (_map is not { } map)
        {
            return Math.Clamp(character, 0, _text.Length);
        }

        var found = Array.IndexOf(map, character);
        return found >= 0 ? found : _text.Length;
    }
}
