// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Services;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer.Chrome.Forms;
using HyperPdf.Viewer.Icons;
using HyperPdf.Viewer.Text;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// The window's own controls around the pages: the tab strip, the toolbar, the status bar, and the menus and dialogs that open over
/// them. It lays them out only when what they show changes, draws them into each frame and takes the input that lands on them.
/// Everything here runs on the window thread.
/// </summary>
[DebuggerDisplay("ViewerChrome: {_bars.Count} controls")]
public sealed partial class ViewerChrome : IDisposable
{
    /// <summary>The window.</summary>
    private readonly IPlatformWindow _window;

    /// <summary>The main view model.</summary>
    private readonly MainViewModel _main;

    /// <summary>The drawn text.</summary>
    private readonly TextRuns _runs;

    /// <summary>The drawn icons of the controls.</summary>
    private readonly IconRuns _icons;

    /// <summary>The controls of the bars, back to front.</summary>
    private readonly List<ChromeItem> _bars = [];

    /// <summary>The controls of the overlay, back to front.</summary>
    private readonly List<ChromeItem> _overlayItems = [];

    /// <summary>The controls of the preview of the tab the pointer is over, made again each frame it is shown.</summary>
    private readonly List<ChromeItem> _previewItems = [];

    /// <summary>Carries the command of each shortcut key pressed to whatever runs it.</summary>
    private readonly Signal<MenuEntry> _shortcuts = new();

    /// <summary>Carries the window events the controls did not use on to the pages.</summary>
    private readonly Signal<WindowEvent> _pageEvents = new();

    /// <summary>Carries every event of the window to the controls, whether it came from the window or was put in by hand.</summary>
    private readonly Signal<WindowEvent> _incoming = new();

    /// <summary>What the controls follow for as long as they live: the window's events, the view models' changes and the presses joined to commands.</summary>
    private readonly MultipleDisposable _owned = [];

    /// <summary>The presses joined to the tab shown, replaced when another tab is shown.</summary>
    private readonly SwapDisposable _tabBindings = new();

    /// <summary>The dialogs that wait under the open one, the last one opened on top. A dialog opened over another one keeps it, and gives the window back to it when it closes.</summary>
    private readonly List<OverlayFrame> _under = [];

    /// <summary>What closed overlays were joined to. An overlay closes while the key or press that closed it is still passing through its input, so it is let go after.</summary>
    private readonly List<IDisposable> _retired = [];

    /// <summary>Which control the keyboard is on.</summary>
    private ChromeFocus _focus = ChromeFocus.None;

    /// <summary>The surface that shows the sheet the print window shows.</summary>
    private SheetFrame _sheetFrame = SheetFrame.Empty;

    /// <summary>The surface that shows the latest picture of the film the media window plays.</summary>
    private MediaFrame _mediaFrame = MediaFrame.Empty;

    /// <summary>What was asked for the pictures of the pages panel.</summary>
    private ThumbnailAsks _thumbnails = ThumbnailAsks.None;

    /// <summary>What the side panel lists, as it was last laid out.</summary>
    private SidebarView _sidebar = SidebarView.Empty with { PageRows = [] };

    /// <summary>What the open overlay is joined to: its changes, its wish to close and everything the overlay itself follows and owns.</summary>
    private IDisposable? _overlayLinks;

    /// <summary>The commands of the controls that act on the window itself and not on a view model, or <see langword="null"/> before they are first needed.</summary>
    private ChromeActions? _actions;

    /// <summary>What the controls are drawn with.</summary>
    private ChromeLook _look;

    /// <summary>The menu, dialog or search bar that is open, or <see langword="null"/>.</summary>
    private Overlay? _overlay;

    /// <summary>The part of the window left for the pages.</summary>
    private LayoutRect _canvasBounds;

    /// <summary>Where the pointer last was, in device pixels.</summary>
    private ScreenPoint _pointer;

    /// <summary>A number that goes up each time the controls change, which the accessibility tree compares.</summary>
    private int _version;

    /// <summary>Whether what the bars show changed since they were laid out. They start out needing a layout.</summary>
    private bool _barsChanged = true;

    /// <summary>Whether what the open overlay shows changed since it was laid out.</summary>
    private bool _overlayChanged;

    /// <summary>Whether the pointer has been inside the window yet.</summary>
    private bool _pointerSeen;

    /// <summary>Whether a stroke is being drawn on the dialog's drawing pad, so the pointer moving and being let go belong to it.</summary>
    private bool _padHeld;

    /// <summary>Whether F11 has put the window into full screen.</summary>
    private bool _fullScreen;

    /// <summary>The tab whose preview is shown, or <see langword="null"/> when none is.</summary>
    private DocumentTabViewModel? _previewTab;

    /// <summary>The pictures asked for the preview.</summary>
    private ThumbnailAsks _previewAsks = ThumbnailAsks.None;

    /// <summary>What the controls of the preview were made from, or <see langword="null"/> before the first.</summary>
    private TabPreviewKey? _previewKey;

    /// <summary>The presses on the empty part of the top bar, which tell a second press from a first.</summary>
    private ClickCounter _framePresses;

    /// <summary>The presses on the controls of the open menu or dialog, which tell a double click from a single one.</summary>
    private ClickCounter _overlayPresses;

    /// <summary>The place of the tab the pointer was pressed on, or -1 when it was not pressed on a tab.</summary>
    private int _tabDragFrom = -1;

    /// <summary>Initializes a new instance of the <see cref="ViewerChrome"/> class.</summary>
    /// <param name="window">The window.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="theme">The colours to start with.</param>
    /// <param name="events">The window's events, on the window thread.</param>
    public ViewerChrome(IPlatformWindow window, MainViewModel main, ChromeTheme theme, IObservable<WindowEvent> events)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(events);
        _window = window;
        Pointer = new(window);
        _main = main;
        _runs = new(window.Backend.Surfaces);
        _icons = new(window.Backend.Surfaces);
        _look = new(_runs, theme, ChromeMetrics.For(window.Scale), ThumbnailOf, InkOf) { Icons = _icons };

        // The view models say when something the controls show has changed, on whichever thread changed it. The layout runs on the
        // window's thread, so the signal is brought there first.
        _owned.Add(main.ControlChanges.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(_ => InvalidateBars(), ErrorTrace.Write));
        _owned.Add(main.WhenChanged(static m => m.SelectedTab).SubscribeSafe(JoinTab, ErrorTrace.Write));
        _owned.Add(_shortcuts.SubscribeSafe(RunEntry, ErrorTrace.Write));
        _owned.Add(events.SubscribeSafe(_incoming.OnNext, ErrorTrace.Write));
        _owned.Add(RouteEvents());
    }

    /// <summary>Gets what takes the window's events into the controls. An event they use goes no further; any other goes on to <see cref="PageEvents"/>.</summary>
    public IObserver<WindowEvent> Input => _incoming;

    /// <summary>Gets the window events the controls did not use, which belong to the pages.</summary>
    public IObservable<WindowEvent> PageEvents => _pageEvents;

    /// <summary>Gets or sets the colours the controls are drawn with.</summary>
    public ChromeTheme Theme
    {
        get => _look.Theme;
        set
        {
            _look = _look with { Theme = value };
            if (_overlay is not null)
            {
                _overlayChanged = true;
            }

            _window.RequestFrame();
        }
    }

    /// <summary>Gets or sets a value indicating whether tool bar buttons show their names beside their icons.</summary>
    public bool ShowLabels
    {
        get => _look.ShowLabels;
        set
        {
            if (_look.ShowLabels == value)
            {
                return;
            }

            _look = _look with { ShowLabels = value };
            InvalidateBars();
        }
    }

    /// <summary>Gets or sets the interface text size in points, or <see langword="null"/> for the default.</summary>
    public double? FontSizePoints
    {
        get => _look.FontSizePoints;
        set
        {
            if (Nullable.Equals(_look.FontSizePoints, value))
            {
                return;
            }

            _look = _look with { FontSizePoints = value };
            InvalidateBars();
        }
    }

    /// <summary>Gets the part of the window left for the pages, in device pixels.</summary>
    public LayoutRect CanvasBounds
    {
        get
        {
            Layout();
            return _canvasBounds;
        }
    }

    /// <summary>Gets a number that changes whenever the controls change: what the bars show, or which menu or dialog is open and what it shows.</summary>
    public int ControlsVersion => _version;

    /// <summary>Gets the place of the control the keyboard is on in <see cref="Bars"/> or <see cref="OverlayItems"/>, or -1 while the keyboard is on the page.</summary>
    public int FocusedControl => _focus.Index;

    /// <summary>Gets a value indicating whether the control the keyboard is on is in the open menu, dialog or search bar rather than the bars.</summary>
    public bool FocusIsInOverlay => _focus.InOverlay;

    /// <summary>Gets the window's drawn text, which the page overlays share for their labels.</summary>
    public TextRuns Text => _runs;

    /// <summary>Gets a value indicating whether a menu, a dialog or the search bar is open.</summary>
    public bool HasOverlay => _overlay is not null;

    /// <summary>Gets a value indicating whether F11 has put the window into full screen.</summary>
    public bool IsFullScreen => _fullScreen;

    /// <summary>Gets the controls of the bars, back to front.</summary>
    public List<ChromeItem> Bars
    {
        get
        {
            Layout();
            return _bars;
        }
    }

    /// <summary>Gets the controls of the open menu, dialog or search bar, back to front. Empty when none is open.</summary>
    public List<ChromeItem> OverlayItems
    {
        get
        {
            Layout();
            return _overlayItems;
        }
    }

    /// <summary>Gets the pointer feedback for this window.</summary>
    internal ChromePointer Pointer { get; }

    /// <summary>Gets the commands of the controls that act on the window itself, made when first asked for, once the controls exist to run them.</summary>
    private ChromeActions Actions => _actions ??= new(this, _main, _window);

    /// <summary>Presses a control, as a screen reader or a switch device does through the accessibility tree.</summary>
    /// <param name="inOverlay">Whether the control is in the open menu, dialog or search bar rather than the bars.</param>
    /// <param name="index">The control's place in <see cref="OverlayItems"/> or <see cref="Bars"/>.</param>
    /// <returns><see langword="true"/> when the control was pressed.</returns>
    public bool PressControl(bool inOverlay, int index)
    {
        Layout();
        var items = inOverlay ? _overlayItems : _bars;
        if ((uint)index >= (uint)items.Count || items[index] is not { Enabled: true, IsPressable: true } item)
        {
            return false;
        }

        if (!inOverlay)
        {
            Run(item);
            return true;
        }

        if (_overlay is null)
        {
            return false;
        }

        PressOverlayItem(item, 1);
        return true;
    }

    /// <summary>Puts the keyboard on a control, as a screen reader or a magnifier does when it follows or sets focus through the accessibility tree.</summary>
    /// <param name="inOverlay">Whether the control is in the open menu, dialog or search bar rather than the bars.</param>
    /// <param name="index">The control's place in <see cref="OverlayItems"/> or <see cref="Bars"/>.</param>
    /// <returns><see langword="true"/> when the control can take the keyboard and now has it.</returns>
    public bool FocusControl(bool inOverlay, int index)
    {
        Layout();
        var items = inOverlay ? _overlayItems : _bars;
        if ((uint)index >= (uint)items.Count || !ChromeFocus.CanStopOn(items[index]))
        {
            return false;
        }

        _focus = ChromeFocus.At(items, inOverlay, index);
        _version++;
        _window.RequestFrame();
        return true;
    }

    /// <summary>Draws the controls over the pages.</summary>
    /// <param name="pass">The frame.</param>
    public void Draw(IFramePass pass)
    {
        ArgumentNullException.ThrowIfNull(pass);
        ReleaseRetired();
        Layout();
        ChromePainter.DrawKept(pass, _bars, _look);
        if (_overlay is not { } overlay)
        {
            DrawTabPreview(pass);
        }
        else
        {
            if (overlay.IsModal)
            {
                var size = pass.Size;
                pass.FillRect(new(0, 0, size.Width, size.Height), _look.Theme.Scrim);
            }

            ChromePainter.Draw(pass, _overlayItems, _look);
        }

        Pointer.DrawHover(pass, _look);
        DrawFocus(pass, _overlay is null ? _bars : _overlayItems, _overlay is not null);
        Pointer.DrawTooltip(pass, _look);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _owned.Dispose();
        _tabBindings.Dispose();
        var open = _overlay;
        _overlay = null;
        open?.Input.OnNext(OverlayInput.Dismiss());
        _overlayLinks?.Dispose();
        _overlayLinks = null;
        for (var i = _under.Count - 1; i >= 0; i--)
        {
            var waiting = _under[i];
            waiting.Overlay.Input.OnNext(OverlayInput.Dismiss());
            waiting.Links.Dispose();
        }

        _under.Clear();
        ReleaseRetired();
        _pageEvents.Dispose();
        _incoming.Dispose();
        _shortcuts.Dispose();
        SheetFrames.Release(_sheetFrame);
        MediaFrames.Release(_mediaFrame);
        _runs.Dispose();
        _icons.Dispose();
        _actions?.Dispose();
    }

    /// <summary>Shows the side panel's list from its start again, as when another list is chosen.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ResetSidebarScroll() => _sidebar = _sidebar with { First = 0, FollowedTab = null, FollowedPage = -1 };

    /// <summary>Marks the bars as changed and asks for a frame, which lays them out again.</summary>
    internal void InvalidateBars()
    {
        _barsChanged = true;
        _version++;
        _window.RequestFrame();
    }

    /// <summary>Tells whether a key goes to the open overlay. One that does not take every key still takes Enter, Escape and Backspace.</summary>
    /// <param name="overlay">The overlay.</param>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the overlay takes the key.</returns>
    private static bool Reaches(Overlay overlay, ViewerKey key) => overlay.TakesAllKeys || key is ViewerKey.Enter or ViewerKey.Escape or ViewerKey.Back;

    /// <summary>Tells whether a control is a row of a dialog's list, rather than one of its buttons.</summary>
    /// <param name="item">The control.</param>
    /// <returns><see langword="true"/> for a row.</returns>
    private static bool IsListRow(ChromeItem item) =>
        item.Action is OverlayAction.PickRow or OverlayAction.PickOption || item is { Kind: ChromeItemKind.Row, Command: not null };

    /// <summary>Runs the command of a menu entry the user chose, when it can run.</summary>
    /// <param name="entry">The entry.</param>
    private static void RunEntry(MenuEntry entry)
    {
        if (entry.Command is { } command && command.CanExecute(entry.Parameter))
        {
            command.Execute(entry.Parameter);
        }
    }

    /// <summary>Joins what the controls follow of the tab shown.</summary>
    /// <param name="tab">The tab, or <see langword="null"/> when none is open.</param>
    private void JoinTab(DocumentTabViewModel? tab) =>
        _tabBindings.Disposable = tab is null ? null : ChromeBindings.Tab(Actions, tab, this);

    /// <summary>
    /// Joins the events of the window to the controls, one stream for each kind of event. An event the controls use goes no further. One
    /// they do not use goes on to the pages, and so does a new size, which moves every control and which the pages need too. What closed an overlay
    /// while an event passed through its input is let go once the event is done with.
    /// </summary>
    /// <returns>What ends the joins.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MultipleDisposable RouteEvents() => new(
        Signal.Merge(
                _incoming.WhereEventKind(WindowEventKind.PointerMoved).WhereUnused(this, static (chrome, e) => chrome.MovePointer(e.X, e.Y)),
                _incoming.WhereEventKind(WindowEventKind.PointerLeft).Do(_ =>
                {
                    _pointerSeen = false;
                    Pointer.Leave();
                }),
                _incoming.WhereEventKind(WindowEventKind.PointerDown).WhereUnused(this, static (chrome, e) => chrome.Press(e)),
                _incoming.WhereEventKind(WindowEventKind.PointerUp).WhereUnused(this, static (chrome, e) => chrome.Release(e.X, e.Y)),
                _incoming.WhereEventKind(WindowEventKind.Scrolled).WhereUnused(this, static (chrome, e) => chrome.Scroll(e)),
                _incoming.WhereEventKind(WindowEventKind.KeyDown).WhereUnused(this, static (chrome, e) => chrome.PressKey(e.Key, e.Held)),
                _incoming.WhereEventKind(WindowEventKind.KeyUp).WhereUnused(this, static (chrome, _) => chrome._overlay is { TakesAllKeys: true }),
                _incoming.WhereEventKind(WindowEventKind.TextInput).WhereUnused(this, static (chrome, e) => chrome.Type((int)e.Code)),
                _incoming.WhereEventKind(WindowEventKind.Resized).Do(_ => InvalidateBars()),
                _incoming.Where(static e => e.Kind is not (WindowEventKind.PointerMoved or WindowEventKind.PointerDown or WindowEventKind.PointerUp or WindowEventKind.Scrolled
                    or WindowEventKind.KeyDown or WindowEventKind.KeyUp or WindowEventKind.TextInput or WindowEventKind.Resized or WindowEventKind.PointerLeft)))
            .SubscribeSafe(_pageEvents.OnNext, ErrorTrace.Write),
        _incoming.SubscribeSafe(_ => ReleaseRetired(), ErrorTrace.Write));

    /// <summary>Keeps what a closed overlay was joined to until nothing passes through its input.</summary>
    /// <param name="links">What the overlay was joined to, or <see langword="null"/> for none.</param>
    private void Retire(IDisposable? links)
    {
        if (links is not null)
        {
            _retired.Add(links);
        }
    }

    /// <summary>Lets go of everything closed overlays were joined to.</summary>
    private void ReleaseRetired()
    {
        for (var i = 0; i < _retired.Count; i++)
        {
            _retired[i].Dispose();
        }

        _retired.Clear();
    }

    /// <summary>Runs the command of a control that was pressed, when it can run now.</summary>
    /// <param name="item">The control.</param>
    private void Run(ChromeItem item)
    {
        if (item.Command is { } command && command.CanExecute(item.Parameter))
        {
            command.Execute(item.Parameter);
        }

        _window.RequestFrame();
    }

    /// <summary>
    /// Presses a control of the open menu or dialog. The dialog hears of the press first, while it is still the one that is open,
    /// and then the control's command runs, which may close the dialog or open another over it.
    /// </summary>
    /// <param name="item">The control.</param>
    /// <param name="clicks">The presses counted close together, this one included.</param>
    private void PressOverlayItem(ChromeItem item, int clicks)
    {
        SendToOverlay(OverlayInput.Press(item, clicks));
        Run(item);
    }

    /// <summary>Sends what the user did to the open overlay, which is then laid out again.</summary>
    /// <param name="input">What the user did.</param>
    private void SendToOverlay(in OverlayInput input)
    {
        _overlay?.Input.OnNext(input);
        InvalidateOverlay();
    }

    /// <summary>Draws a ring round the control the keyboard is on, when it is in the list being drawn.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="items">The controls being drawn.</param>
    /// <param name="inOverlay">Whether they are the controls of the open menu, dialog or search bar.</param>
    private void DrawFocus(IFramePass pass, List<ChromeItem> items, bool inOverlay)
    {
        if (_focus.InOverlay == inOverlay && _focus.Item(items) is { } focused)
        {
            ChromePainter.DrawFocus(pass, focused, _look);
        }
    }

    /// <summary>Updates the pointer target after a move or layout.</summary>
    /// <returns>Whether the target changed.</returns>
    private bool FollowPointer()
    {
        var overlay = _overlay is not null && IsOverOverlay(_pointer.X, _pointer.Y);
        var reachable = _pointerSeen && (overlay || _overlay is not { IsModal: true });
        return Pointer.Move(reachable ? Pointer.Hit(overlay, _pointer.X, _pointer.Y) : null);
    }

    /// <summary>Marks the open overlay as changed and asks for a frame, which lays it out again.</summary>
    private void InvalidateOverlay()
    {
        _overlayChanged = true;
        _version++;
        _window.RequestFrame();
    }

    /// <summary>Lays the controls out again when what they show changed.</summary>
    private void Layout()
    {
        var barsChanged = _barsChanged;
        if (barsChanged)
        {
            _barsChanged = false;
            _look = WindowFrame.Follow(_look with { Metrics = ChromeMetrics.For(_window.Scale, _look.FontSizePoints) }, _window, _fullScreen);
            var laid = ChromeBars.Build(_bars, _main, _look, _sidebar, _window.ClientSize, Actions);
            Pointer.SetTargets(_bars, false);
            _canvasBounds = laid.Canvas;
            _sidebar = laid.Sidebar;
            if (!_focus.InOverlay)
            {
                _focus = _focus.Follow(_bars);
            }
        }

        if (_overlay is { } overlay && (barsChanged || _overlayChanged || overlay.HasMoved?.Invoke() == true))
        {
            _overlayChanged = false;
            BuildOverlay();
        }
        else if (barsChanged && _pointerSeen)
        {
            // Nothing is under a pointer that has not been seen in the window yet.
            _ = FollowPointer();
        }
    }

    /// <summary>Lays the overlay out and finds the control under the pointer again.</summary>
    private void BuildOverlay()
    {
        if (_overlay is not { } overlay)
        {
            return;
        }

        _overlayItems.Clear();
        overlay.Layout(_overlayItems, new(_runs, _look.Metrics, _window.ClientSize, _canvasBounds) { Theme = _look.Theme });
        Pointer.SetTargets(_overlayItems, true);
        _ = FollowPointer();
        if (_focus.InOverlay)
        {
            _focus = _focus.Follow(_overlayItems);
        }
    }

    /// <summary>Tells whether a point is over the controls rather than the pages.</summary>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <returns><see langword="true"/> when input at the point belongs to the controls.</returns>
    private bool IsOverChrome(int x, int y)
    {
        Layout();
        return _overlay is { IsModal: true } || IsOverOverlay(x, y) || !_canvasBounds.Contains(x, y);
    }

    /// <summary>Tells whether a point is inside the open menu, dialog or search bar: its first control, or any panel it lays out.</summary>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsOverOverlay(int x, int y) => ChromePointer.OverOverlay(_overlayItems, x, y);

    /// <summary>Follows the pointer, so the control under it is marked.</summary>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <returns><see langword="true"/> when the pointer is over the controls.</returns>
    private bool MovePointer(int x, int y)
    {
        var moved = !_pointerSeen || _pointer.X != x || _pointer.Y != y;
        _pointer = new(x, y);
        _pointerSeen = true;
        if (_padHeld)
        {
            SendToOverlay(OverlayInput.Pad(OverlayInputKind.PadMove, x, y));
        }

        var over = IsOverChrome(x, y);
        if (moved && (over ? FollowPointer() : Pointer.Move(null)))
        {
            _window.RequestFrame();
        }

        if (over)
        {
            _window.SetCursor(ViewerCursor.Arrow);
        }

        return over;
    }

    /// <summary>Handles a pointer press that is inside the open menu, dialog or search bar: it presses the control under it, or starts a stroke on a drawing pad.</summary>
    /// <param name="overlay">The open overlay.</param>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <param name="buttons">The buttons held.</param>
    private void PressInOverlay(Overlay overlay, int x, int y, ViewerButtons buttons)
    {
        if ((buttons & ViewerButtons.Left) == 0)
        {
            return;
        }

        if (ChromePointer.Find(_overlayItems, x, y) is { } pressed)
        {
            // Presses close together on the same place are a double click, which a list of files takes as "open this one".
            _overlayPresses = _overlayPresses.Press(x, y, Stopwatch.GetTimestamp());
            PressOverlayItem(pressed, _overlayPresses.Count);
            return;
        }

        if (!overlay.TakesPointer)
        {
            return;
        }

        _padHeld = true;
        SendToOverlay(OverlayInput.Pad(OverlayInputKind.PadDown, x, y));
    }

    /// <summary>Handles the pointer being let go: it ends a stroke on a drawing pad, and otherwise belongs to whatever it is over.</summary>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <returns><see langword="true"/> when the pointer is over the controls or ended a stroke.</returns>
    private bool Release(int x, int y)
    {
        Pointer.Release();

        // A tab let go over another tab takes that tab's place, which is how tabs are put in a new order.
        var dragged = _tabDragFrom;
        _tabDragFrom = -1;
        if (dragged >= 0 && ChromePointer.Find(_bars, x, y) is { Kind: ChromeItemKind.Tab } target && target.Index != dragged)
        {
            _main.MoveTab(dragged, target.Index);
            return true;
        }

        if (!_padHeld)
        {
            return IsOverChrome(x, y);
        }

        _padHeld = false;
        SendToOverlay(OverlayInput.Pad(OverlayInputKind.PadUp, x, y));
        return true;
    }

    /// <summary>Draws the preview of the tab the pointer is over: the page it shows, its name and where it is in the document.</summary>
    /// <param name="pass">The frame.</param>
    private void DrawTabPreview(IFramePass pass)
    {
        _previewTab = null;
        if (Pointer.Hover is not { Kind: ChromeItemKind.Tab } hovered || (uint)hovered.Index >= (uint)_main.Tabs.Count)
        {
            return;
        }

        var tab = _main.Tabs[hovered.Index];
        _previewTab = tab;

        // The controls are made again only when something they show has changed, so a hover that rests costs no allocation.
        var key = new TabPreviewKey(tab, hovered, TabPreviews.PageOf(tab), tab.PageCount, _window.ClientSize.Width, _window.Scale);
        if (!key.IsSameAs(_previewKey))
        {
            _previewKey = key;
            _previewItems.Clear();
            TabPreviews.Add(_previewItems, tab, hovered, _runs, _look.Metrics, key.Scale, key.WindowWidth);
        }

        ChromePainter.Draw(pass, _previewItems, _look);
    }

    /// <summary>Handles a press on a tab with the middle or right button: the middle button closes the tab and the right opens its menu.</summary>
    /// <param name="buttons">The buttons held.</param>
    /// <param name="tabItem">The tab.</param>
    /// <returns><see langword="true"/> when the press was one of these.</returns>
    private bool PressTabWithAnotherButton(ViewerButtons buttons, ChromeItem tabItem)
    {
        if ((buttons & ViewerButtons.Middle) != 0)
        {
            Run(tabItem with { Command = _main.CloseTabCommand });
            return true;
        }

        if ((buttons & ViewerButtons.Right) == 0 || (uint)tabItem.Index >= (uint)_main.Tabs.Count)
        {
            return false;
        }

        ShowMenu([..MainMenu.ForTab(_main, _main.Tabs[tabItem.Index])], new((int)tabItem.Bounds.X, (int)tabItem.Bounds.Bottom));
        return true;
    }

    /// <summary>Handles a right press on a row of the side panel, which opens the menu of what can be done to that row.</summary>
    /// <param name="buttons">The buttons held.</param>
    /// <param name="item">The control pressed.</param>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <returns><see langword="true"/> when the press opened a row's menu.</returns>
    private bool PressRowWithRightButton(ViewerButtons buttons, ChromeItem item, int x, int y)
    {
        if ((buttons & ViewerButtons.Right) == 0 || _main.SelectedTab is not { } shown || SidebarContextMenus.ForRow(shown, item.Parameter) is not { } menu)
        {
            return false;
        }

        ShowMenu(menu, new(x, y));
        return true;
    }

    /// <summary>Handles a pointer press.</summary>
    /// <param name="windowEvent">The press.</param>
    /// <returns><see langword="true"/> when the press was on the controls.</returns>
    private bool Press(in WindowEvent windowEvent)
    {
        var x = windowEvent.X;
        var y = windowEvent.Y;
        _pointer = new(x, y);
        _pointerSeen = true;
        Layout();
        _ = FollowPointer();
        Pointer.Press((windowEvent.Buttons & ViewerButtons.Left) != 0);
        if (WindowFrame.EdgesAt(_look, _window.ClientSize, x, y) is not WindowEdges.None and var edges)
        {
            _window.BeginResize(edges);
            return true;
        }

        if (_overlay is { } overlay)
        {
            if (IsOverOverlay(x, y))
            {
                PressInOverlay(overlay, x, y, windowEvent.Buttons);
                return true;
            }

            if (overlay.ClosesOnOutsidePress)
            {
                // Being sent away makes the overlay ask to close, which closes it. One that did not ask is closed here.
                overlay.Input.OnNext(OverlayInput.Dismiss());
                Close(overlay);
                return true;
            }

            if (overlay.IsModal)
            {
                return true;
            }
        }

        if (_canvasBounds.Contains(x, y))
        {
            return false;
        }

        PressBars(windowEvent.Buttons, x, y);
        return true;
    }

    /// <summary>Handles a press on the bars: a left press runs the control, and on a tab the other buttons close it or open its menu.</summary>
    /// <param name="buttons">The buttons held.</param>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    private void PressBars(ViewerButtons buttons, int x, int y)
    {
        if (ChromePointer.Find(_bars, x, y) is not { } item)
        {
            // The empty part of the bar at the top is the title bar of a window the system gives none: it moves the window, and a second press sizes it.
            if (_look.OwnsFrame && y < _look.Metrics.TabHeight && (buttons & ViewerButtons.Left) != 0)
            {
                _framePresses = _framePresses.Press(x, y, Stopwatch.GetTimestamp());
                WindowFrame.PressTitle(_window, _framePresses.Count);
            }

            return;
        }

        if (item.Kind == ChromeItemKind.Tab && PressTabWithAnotherButton(buttons, item))
        {
            return;
        }

        if (PressRowWithRightButton(buttons, item, x, y))
        {
            return;
        }

        if ((buttons & ViewerButtons.Left) == 0)
        {
            return;
        }

        _tabDragFrom = item.Kind == ChromeItemKind.Tab ? item.Index : -1;
        Run(item);
    }

    /// <summary>Handles a scroll.</summary>
    /// <param name="windowEvent">The scroll.</param>
    /// <returns><see langword="true"/> when the scroll belongs to the controls.</returns>
    private bool Scroll(in WindowEvent windowEvent)
    {
        if (_overlay is not { IsModal: true })
        {
            // A scroll carries no place, so it goes where the pointer last was. Before the pointer has moved, that is the pages.
            if (_overlay is not null && _pointerSeen && IsOverOverlay(_pointer.X, _pointer.Y))
            {
                SendToOverlay(OverlayInput.Scroll(-Math.Sign(windowEvent.Y)));
                return true;
            }

            var overChrome = _pointerSeen && IsOverChrome(_pointer.X, _pointer.Y);
            if (overChrome && _sidebar.Bounds.Contains(_pointer.X, _pointer.Y))
            {
                ScrollSidebar(-Math.Sign(windowEvent.Y));
            }
            else if (overChrome && _pointer.Y < _look.Metrics.TabHeight)
            {
                ScrollTabs(-Math.Sign(windowEvent.Y));
            }

            return overChrome;
        }

        // A notch up scrolls a list towards its start.
        SendToOverlay(OverlayInput.Scroll(-Math.Sign(windowEvent.Y)));
        return true;
    }

    /// <summary>Moves to a neighbouring tab, which is how the tab strip scrolls: it always keeps the chosen tab in view.</summary>
    /// <param name="step">The tabs to move on, negative to move back.</param>
    private void ScrollTabs(int step)
    {
        if (_main.SelectedTab is not { } current)
        {
            return;
        }

        var next = Math.Clamp(_main.Tabs.IndexOf(current) + step, 0, _main.Tabs.Count - 1);
        _main.SelectedTab = _main.Tabs[next];
    }

    /// <summary>Scrolls the list of bookmarks in the side panel.</summary>
    /// <param name="rows">The rows to move down, negative to move up.</param>
    private void ScrollSidebar(int rows)
    {
        var first = Math.Clamp(_sidebar.First + rows, 0, Math.Max(0, _sidebar.Rows.Length - _sidebar.VisibleRows));
        if (first == _sidebar.First)
        {
            return;
        }

        _sidebar = _sidebar with { First = first };
        InvalidateBars();
    }

    /// <summary>Gets what is drawn on the drawing pad of the open dialog.</summary>
    /// <returns>The ink, or <see langword="null"/> when the open overlay has no pad.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PadInk? InkOf() => _overlay?.Ink?.Invoke();

    /// <summary>Gets the picture of the page a row of the pages panel shows.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The picture, or <see langword="null"/> while it is being made.</returns>
    private IRenderSurface? ThumbnailOf(int row)
    {
        // A dialog's own picture, such as the sheet a print makes, has a number no row of the pages panel has.
        if (row == Overlay.PictureIndex)
        {
            return _overlay?.Picture?.Invoke();
        }

        // So has the picture of the page a tab's preview shows.
        if (row == TabPreviews.PictureIndex)
        {
            return _previewTab is { } previewed ? PageThumbnails.Get(ref _previewAsks, previewed, TabPreviews.PageOf(previewed)) : null;
        }

        return _main.SelectedTab is { } tab && (uint)row < (uint)_sidebar.Rows.Length ? PageThumbnails.Get(ref _thumbnails, tab, _sidebar.Rows[row].Page) : null;
    }

    /// <summary>Handles a key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/> when the key was used.</returns>
    private bool PressKey(ViewerKey key, ViewerModifiers modifiers)
    {
        if (Pointer.Dismiss() && key == ViewerKey.Escape)
        {
            return true;
        }

        Layout();
        if (_overlay is { IsModal: false } && (RunShortcut(key, modifiers) || OpenMenuByKey(key, modifiers)))
        {
            // A shortcut and the menu key still work over the search bar, focus mode or the start view. A dialog keeps every key to itself.
            return true;
        }

        return _overlay is { } overlay ? PressOverlayKey(overlay, key, modifiers) : PressWindowKey(key, modifiers);
    }

    /// <summary>Handles a key while no menu or dialog is open: a shortcut, the keys that move the keyboard to the controls, or a key for the control it is on.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/> when the key was used.</returns>
    private bool PressWindowKey(ViewerKey key, ViewerModifiers modifiers)
    {
        if (RunShortcut(key, modifiers))
        {
            return true;
        }

        if (key == ViewerKey.F6)
        {
            // F6 goes from the page to the first control, and from the controls back to the page.
            if (_focus.Index >= 0)
            {
                LeaveFocus();
            }
            else
            {
                MoveFocus(_bars, false, 1);
            }

            return true;
        }

        return OpenMenuByKey(key, modifiers) || (_focus.Index >= 0 && PressFocusKey(key, modifiers));
    }

    /// <summary>Opens a menu of the menu bar: File with F10, or the menu whose access letter is pressed with Alt.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/> when a menu was opened.</returns>
    private bool OpenMenuByKey(ViewerKey key, ViewerModifiers modifiers)
    {
        var index = key == ViewerKey.F10 && modifiers == ViewerModifiers.None ? MenuBar.FileMenu : -1;
        if (modifiers == ViewerModifiers.Alt)
        {
            index = MenuBar.ForKey(key);
        }

        if (index < 0 || !MenuBar.CanOpen(_main, index) || !ShowsMenuBar())
        {
            return false;
        }

        Actions.OpenBarMenu(index);
        return true;
    }

    /// <summary>Tells whether the menu bar is among the controls shown; a presentation and reading mode hide it.</summary>
    /// <returns><see langword="true"/> when it is shown.</returns>
    private bool ShowsMenuBar()
    {
        var open = Actions.ShowBarMenu;
        foreach (var item in _bars)
        {
            if (ReferenceEquals(item.Command, open))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Runs the command of a shortcut key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/> when the key is a shortcut that has something to act on now.</returns>
    private bool RunShortcut(ViewerKey key, ViewerModifiers modifiers)
    {
        // Delete and F2 edit text where a field, a note or the search bar is being typed in, so they belong to that while it is open.
        if (_overlay is not null && key is ViewerKey.Delete or ViewerKey.F2)
        {
            return false;
        }

        // Full screen belongs to the window, not to a view model, so F11 acts on the window itself.
        if (key == ViewerKey.F11 && modifiers == ViewerModifiers.None)
        {
            _fullScreen = !_fullScreen;
            _window.SetFullScreen(_fullScreen);
            return true;
        }

        if (Shortcuts.Find(_main, key, modifiers) is not { } shortcut)
        {
            return false;
        }

        _shortcuts.OnNext(shortcut);
        _window.RequestFrame();
        return true;
    }

    /// <summary>Handles a key while the keyboard is on a control of the bars. The page gets no keys until the keyboard goes back to it.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/>, as the key belongs to the controls.</returns>
    private bool PressFocusKey(ViewerKey key, ViewerModifiers modifiers)
    {
        switch (key)
        {
            case ViewerKey.Tab:
                {
                    MoveFocus(_bars, false, (modifiers & ViewerModifiers.Shift) != 0 ? -1 : 1);
                    break;
                }

            case ViewerKey.Right or ViewerKey.Down:
                {
                    MoveFocus(_bars, false, 1);
                    break;
                }

            case ViewerKey.Left or ViewerKey.Up:
                {
                    MoveFocus(_bars, false, -1);
                    break;
                }

            case ViewerKey.Enter or ViewerKey.Space when _focus.Item(_bars) is { } item:
                {
                    Run(item);
                    break;
                }

            case ViewerKey.Escape:
                {
                    LeaveFocus();
                    break;
                }

            default:
                {
                    break;
                }
        }

        return true;
    }

    /// <summary>Handles a key while a menu, a dialog or the search bar is open.</summary>
    /// <param name="overlay">The overlay.</param>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/> when the key was used.</returns>
    private bool PressOverlayKey(Overlay overlay, ViewerKey key, ViewerModifiers modifiers)
    {
        if (key == ViewerKey.V && modifiers == ViewerModifiers.Control)
        {
            Paste();
            return true;
        }

        if (!Reaches(overlay, key))
        {
            return false;
        }

        if (key == ViewerKey.Tab && !overlay.UsesTab)
        {
            TabThroughOverlay((modifiers & ViewerModifiers.Shift) != 0 ? -1 : 1);
            return true;
        }

        if (!PressFocusedInOverlay(key))
        {
            SendToOverlay(OverlayInput.KeyDown(key, modifiers));
        }

        return true;
    }

    /// <summary>Moves the keyboard from one control of the dialog to the next. Landing on a typing box also starts typing in it.</summary>
    /// <param name="step">1 for the next control, -1 for the one before.</param>
    private void TabThroughOverlay(int step)
    {
        if (!TryRevealRow(step))
        {
            MoveFocus(_overlayItems, true, step);
        }

        if (_focus.Item(_overlayItems) is { Kind: ChromeItemKind.Field } box)
        {
            SendToOverlay(OverlayInput.Press(box));
        }
    }

    /// <summary>
    /// Scrolls a long dialog by one row when the keyboard is on its last row in view and moving down, or on its first and moving
    /// up. The keyboard stays in the same place on screen, which is now the next row.
    /// </summary>
    /// <param name="step">1 when the keyboard is moving down, -1 when it is moving up.</param>
    /// <returns><see langword="true"/> when another row came into view and has the keyboard.</returns>
    private bool TryRevealRow(int step)
    {
        var slot = _focus.Index;
        if (!_focus.InOverlay || _focus.Item(_overlayItems) is not { } row || !IsListRow(row) || NextIsListRow(slot, step))
        {
            return false;
        }

        var before = OverlaySignature();
        _overlay?.Input.OnNext(OverlayInput.Scroll(step));
        _overlayChanged = true;
        Layout();
        if (OverlaySignature() == before || (uint)slot >= (uint)_overlayItems.Count || !ChromeFocus.CanStopOn(_overlayItems[slot]))
        {
            // The dialog was already at its end, so the keyboard moves on to the buttons as usual.
            return false;
        }

        _focus = ChromeFocus.At(_overlayItems, true, slot);
        _version++;
        _window.RequestFrame();
        return true;
    }

    /// <summary>Tells whether the next control the keyboard can stop on, in one direction, is another row of the dialog's list.</summary>
    /// <param name="from">The place of the control the keyboard is on.</param>
    /// <param name="step">1 to look down, -1 to look up.</param>
    /// <returns><see langword="true"/> when there is another row in view that way.</returns>
    private bool NextIsListRow(int from, int step)
    {
        for (var i = from + step; (uint)i < (uint)_overlayItems.Count; i += step)
        {
            if (ChromeFocus.CanStopOn(_overlayItems[i]))
            {
                return IsListRow(_overlayItems[i]);
            }
        }

        return false;
    }

    /// <summary>Makes a number out of what the open dialog shows, which changes when the dialog scrolls.</summary>
    /// <returns>The number.</returns>
    private int OverlaySignature()
    {
        var signature = default(HashCode);
        foreach (var item in _overlayItems)
        {
            signature.Add(item.Index);
            signature.Add(item.Text);
        }

        return signature.ToHashCode();
    }

    /// <summary>Presses the control of the dialog the keyboard is on, when Enter or Space is the key and the control is not a typing box.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the control was pressed.</returns>
    private bool PressFocusedInOverlay(ViewerKey key)
    {
        if (key is not (ViewerKey.Enter or ViewerKey.Space) || !_focus.InOverlay || _focus.Item(_overlayItems) is not { Kind: not ChromeItemKind.Field } focused)
        {
            return false;
        }

        PressOverlayItem(focused, 1);
        return true;
    }

    /// <summary>Moves the keyboard to the next or the previous control.</summary>
    /// <param name="items">The controls.</param>
    /// <param name="inOverlay">Whether the controls are those of the open menu, dialog or search bar.</param>
    /// <param name="step">1 for the next control, -1 for the one before.</param>
    private void MoveFocus(List<ChromeItem> items, bool inOverlay, int step)
    {
        if (_focus.Move(items, inOverlay, step) is not { } moved)
        {
            return;
        }

        _focus = moved;
        _version++;
        _window.RequestFrame();
    }

    /// <summary>Puts the keyboard back on the page.</summary>
    private void LeaveFocus()
    {
        _focus = ChromeFocus.None;
        _version++;
        _window.RequestFrame();
    }

    /// <summary>Types the clipboard's text into the open menu, dialog or search bar. A text box takes one line, so the paste stops at the first line break.</summary>
    private void Paste()
    {
        if (_window.GetClipboardText() is not { Length: > 0 } text)
        {
            return;
        }

        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value is '\n' or '\r')
            {
                break;
            }

            _overlay?.Input.OnNext(OverlayInput.Text(rune.Value));
        }

        InvalidateOverlay();
    }

    /// <summary>Handles a typed character.</summary>
    /// <param name="scalar">The Unicode scalar value.</param>
    /// <returns><see langword="true"/> when the character was used.</returns>
    private bool Type(int scalar)
    {
        if (_overlay is null)
        {
            return false;
        }

        if (scalar == ' ' && _focus.InOverlay && _focus.Item(_overlayItems) is { Kind: not ChromeItemKind.Field })
        {
            // Space pressed the control the keyboard is on. It is not also typed into a box elsewhere in the dialog.
            return true;
        }

        SendToOverlay(OverlayInput.Text(scalar));
        return true;
    }
}
