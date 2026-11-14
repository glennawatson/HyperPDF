// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.Core.Layout;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer.Text;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// The window's own controls around the pages: the tab strip, the toolbar, the status bar, and the menus and dialogs that open over
/// them. It lays them out only when what they show changes, draws them into each frame and takes the input that lands on them.
/// Everything here runs on the window thread.
/// </summary>
[DebuggerDisplay("ViewerChrome: {_bars.Count} controls")]
public sealed class ViewerChrome : IDisposable
{
    /// <summary>The window.</summary>
    private readonly IPlatformWindow _window;

    /// <summary>The main view model.</summary>
    private readonly MainViewModel _main;

    /// <summary>The drawn text.</summary>
    private readonly TextRuns _runs;

    /// <summary>The controls of the bars, back to front.</summary>
    private readonly List<ChromeItem> _bars = [];

    /// <summary>The controls of the overlay, back to front.</summary>
    private readonly List<ChromeItem> _overlayItems = [];

    /// <summary>The side panel's rows and scroll position.</summary>
    private readonly SidebarState _sidebar = new();

    /// <summary>What the controls are drawn with.</summary>
    private ChromeLook _look;

    /// <summary>The menu, dialog or search bar that is open, or <see langword="null"/>.</summary>
    private IChromeOverlay? _overlay;

    /// <summary>The control under the pointer, or <see langword="null"/>.</summary>
    private ChromeItem? _hover;

    /// <summary>The part of the window left for the pages.</summary>
    private LayoutRect _canvasBounds;

    /// <summary>Where the pointer last was, in device pixels.</summary>
    private ScreenPoint _pointer;

    /// <summary>The stamp of what the bars showed when they were laid out.</summary>
    private int _stamp;

    /// <summary>The stamp of what the bars show, as last read from the view models.</summary>
    private int _seenStamp;

    /// <summary>The version of the overlay when it was laid out.</summary>
    private int _overlayVersion;

    /// <summary>Whether the bars have been laid out at least once.</summary>
    private bool _laidOut;

    /// <summary>Whether the pointer has been inside the window yet.</summary>
    private bool _pointerSeen;

    /// <summary>Initializes a new instance of the <see cref="ViewerChrome"/> class.</summary>
    /// <param name="window">The window.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="theme">The colours to start with.</param>
    public ViewerChrome(IPlatformWindow window, MainViewModel main, ChromeTheme theme)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(main);
        _window = window;
        _main = main;
        _runs = new(window.Backend.Surfaces);
        _look = new(_runs, theme, ChromeMetrics.For(window.Scale));
    }

    /// <summary>Gets or sets the colours the controls are drawn with.</summary>
    public ChromeTheme Theme
    {
        get => _look.Theme;
        set
        {
            _look = _look with { Theme = value };
            _window.RequestFrame();
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
    /// <remarks>It reads what <see cref="RequestFrameIfChanged"/> last saw, so a loop turn checks the view models once for both.</remarks>
    public int ControlsVersion => HashCode.Combine(_seenStamp, _overlay is null ? 0 : RuntimeHelpers.GetHashCode(_overlay), _overlay?.Version ?? 0);

    /// <summary>Gets a value indicating whether a menu, a dialog or the search bar is open.</summary>
    public bool HasOverlay => _overlay is not null;

    /// <summary>Gets the controls of the bars, back to front.</summary>
    public IReadOnlyList<ChromeItem> Bars
    {
        get
        {
            Layout();
            return _bars;
        }
    }

    /// <summary>Gets the controls of the open menu, dialog or search bar, back to front. Empty when none is open.</summary>
    public IReadOnlyList<ChromeItem> OverlayItems
    {
        get
        {
            Layout();
            return _overlayItems;
        }
    }

    /// <summary>Opens a menu.</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="at">Where the menu's top left corner is wanted, in device pixels.</param>
    public void ShowMenu(IReadOnlyList<MenuEntry> entries, ScreenPoint at)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Show(new MenuOverlay(entries, at));
    }

    /// <summary>Asks a yes or no question in a dialog.</summary>
    /// <param name="request">What is asked.</param>
    /// <returns><see langword="true"/> when the user agreed.</returns>
    public Task<bool> ConfirmAsync(ConfirmRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var dialog = new ConfirmOverlay(request);
        Show(dialog);
        return dialog.Answer;
    }

    /// <summary>Asks for a file in a dialog.</summary>
    /// <param name="folder">The folder to start in.</param>
    /// <param name="saveName">The file name to offer when asking where to save, or <see langword="null"/> to ask what to open.</param>
    /// <returns>The path chosen, or <see langword="null"/> when the dialog was cancelled.</returns>
    public Task<string?> PickFileAsync(string folder, string? saveName)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var dialog = new FileOverlay(folder, saveName);
        Show(dialog);
        return dialog.Answer;
    }

    /// <summary>Opens the search bar of the tab shown.</summary>
    public void ShowFind()
    {
        if (_main.SelectedTab is not { } tab)
        {
            return;
        }

        tab.Search.Open();
        Show(new FindOverlay(tab.Search));
    }

    /// <summary>Presses a control, as a screen reader or a switch device does through the accessibility tree.</summary>
    /// <param name="inOverlay">Whether the control is in the open menu, dialog or search bar rather than the bars.</param>
    /// <param name="index">The control's place in <see cref="OverlayItems"/> or <see cref="Bars"/>.</param>
    /// <returns><see langword="true"/> when the control was pressed.</returns>
    public bool PressControl(bool inOverlay, int index)
    {
        Layout();
        var items = inOverlay ? _overlayItems : _bars;
        if ((uint)index >= (uint)items.Count || items[index] is not { Enabled: true } item || item.Command == ChromeCommand.None)
        {
            return false;
        }

        if (!inOverlay)
        {
            RunBar(item);
            return true;
        }

        if (_overlay is not { } overlay)
        {
            return false;
        }

        PressOverlay(overlay, item, ViewerButtons.Left);
        return true;
    }

    /// <summary>Asks for a frame when what the controls show changed since they were laid out. View models change after posted work, with no window event to draw for.</summary>
    public void RequestFrameIfChanged()
    {
        _seenStamp = ChromeBars.Stamp(_main, _window.ClientSize, _window.Scale, _sidebar.Version);
        if (!_laidOut || _seenStamp != _stamp || (_overlay is { } overlay && overlay.Version != _overlayVersion))
        {
            _window.RequestFrame();
        }
    }

    /// <summary>Takes a window event that lands on the controls.</summary>
    /// <param name="windowEvent">The event.</param>
    /// <returns><see langword="true"/> when the event was used and must not reach the pages.</returns>
    public bool Handle(in WindowEvent windowEvent)
    {
        switch (windowEvent.Kind)
        {
            case WindowEventKind.PointerMoved:
                {
                    return MovePointer(windowEvent.X, windowEvent.Y);
                }

            case WindowEventKind.PointerDown:
                {
                    return Press(windowEvent);
                }

            case WindowEventKind.PointerUp:
                {
                    return IsOverChrome(windowEvent.X, windowEvent.Y);
                }

            case WindowEventKind.Scrolled:
                {
                    return Scroll(windowEvent);
                }

            case WindowEventKind.KeyDown:
                {
                    return PressKey(windowEvent.Key, windowEvent.Held);
                }

            case WindowEventKind.KeyUp:
                {
                    return _overlay is { TakesAllKeys: true };
                }

            case WindowEventKind.TextInput:
                {
                    return Type((int)windowEvent.Code);
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>Draws the controls over the pages.</summary>
    /// <param name="pass">The frame.</param>
    public void Draw(IFramePass pass)
    {
        ArgumentNullException.ThrowIfNull(pass);
        Layout();
        ChromePainter.Draw(pass, _bars, _look, _hover);
        if (_overlay is not { } overlay)
        {
            return;
        }

        if (overlay.IsModal)
        {
            var size = pass.Size;
            pass.FillRect(new(0, 0, size.Width, size.Height), _look.Theme.Scrim);
        }

        ChromePainter.Draw(pass, _overlayItems, _look, _hover);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _overlay?.Dismiss();
        _overlay = null;
        _runs.Dispose();
    }

    /// <summary>Finds the control at a point that can be pressed.</summary>
    /// <param name="items">The controls, back to front.</param>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <returns>The control in front, or <see langword="null"/>.</returns>
    private static ChromeItem? Find(List<ChromeItem> items, int x, int y)
    {
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            if (item.Command != ChromeCommand.None && item.Enabled && item.Bounds.Contains(x, y))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Runs a command when it can run.</summary>
    /// <param name="command">The command.</param>
    /// <param name="parameter">Its parameter.</param>
    private static void Run(ICommand command, object? parameter)
    {
        if (command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    /// <summary>Shows an overlay in place of the one that is open.</summary>
    /// <param name="overlay">The overlay.</param>
    private void Show(IChromeOverlay overlay)
    {
        _overlay?.Dismiss();
        _overlay = overlay;
        BuildOverlay();
        _window.RequestFrame();
    }

    /// <summary>Closes the overlay.</summary>
    private void CloseOverlay()
    {
        _overlay = null;
        _overlayItems.Clear();
        _hover = Find(_bars, _pointer.X, _pointer.Y);
        _window.RequestFrame();
    }

    /// <summary>Lays the controls out again when what they show changed.</summary>
    private void Layout()
    {
        var size = _window.ClientSize;
        var stamp = ChromeBars.Stamp(_main, size, _window.Scale, _sidebar.Version);
        _seenStamp = stamp;
        var barsChanged = !_laidOut || stamp != _stamp;
        if (barsChanged)
        {
            _laidOut = true;
            _stamp = stamp;
            _look = _look with { Metrics = ChromeMetrics.For(_window.Scale) };
            _canvasBounds = ChromeBars.Build(_bars, _main, _look, _sidebar, size);
        }

        if (_overlay is { } overlay && (barsChanged || overlay.Version != _overlayVersion))
        {
            BuildOverlay();
        }
        else if (barsChanged)
        {
            _hover = Find(_bars, _pointer.X, _pointer.Y);
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
        _overlayVersion = overlay.Version;
        overlay.Build(_overlayItems, _runs, _look.Metrics, _window.ClientSize);
        _hover = Find(_overlayItems, _pointer.X, _pointer.Y);
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

    /// <summary>Tells whether a point is inside the open menu, dialog or search bar.</summary>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private bool IsOverOverlay(int x, int y) => _overlayItems.Count > 0 && _overlayItems[0].Bounds.Contains(x, y);

    /// <summary>Follows the pointer, so the control under it is marked.</summary>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <returns><see langword="true"/> when the pointer is over the controls.</returns>
    private bool MovePointer(int x, int y)
    {
        _pointer = new(x, y);
        _pointerSeen = true;
        var over = IsOverChrome(x, y);
        var hover = IsOverOverlay(x, y) ? Find(_overlayItems, x, y) : null;
        if (hover is null && _overlay is not { IsModal: true })
        {
            hover = Find(_bars, x, y);
        }

        if (!ReferenceEquals(hover, _hover))
        {
            _hover = hover;
            _window.RequestFrame();
        }

        if (over)
        {
            _window.SetCursor(ViewerCursor.Arrow);
        }

        return over;
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
        if (_overlay is { } overlay)
        {
            if (IsOverOverlay(x, y))
            {
                PressOverlay(overlay, Find(_overlayItems, x, y), windowEvent.Buttons);
                return true;
            }

            if (overlay.ClosesOnOutsidePress)
            {
                overlay.Dismiss();
                CloseOverlay();
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

        if ((windowEvent.Buttons & ViewerButtons.Left) != 0 && Find(_bars, x, y) is { } item)
        {
            RunBar(item);
        }

        return true;
    }

    /// <summary>Presses a control of the overlay.</summary>
    /// <param name="overlay">The overlay.</param>
    /// <param name="item">The control, or <see langword="null"/> for a press on the overlay's background.</param>
    /// <param name="buttons">The buttons pressed.</param>
    private void PressOverlay(IChromeOverlay overlay, ChromeItem? item, ViewerButtons buttons)
    {
        if (item is null || (buttons & ViewerButtons.Left) == 0)
        {
            return;
        }

        if (!overlay.Press(item) && ReferenceEquals(_overlay, overlay))
        {
            CloseOverlay();
            return;
        }

        _window.RequestFrame();
    }

    /// <summary>Handles a scroll.</summary>
    /// <param name="windowEvent">The scroll.</param>
    /// <returns><see langword="true"/> when the scroll belongs to the controls.</returns>
    private bool Scroll(in WindowEvent windowEvent)
    {
        if (_overlay is not { IsModal: true } overlay)
        {
            // A scroll carries no place, so it goes where the pointer last was. Before the pointer has moved, that is the pages.
            var overChrome = _pointerSeen && IsOverChrome(_pointer.X, _pointer.Y);
            if (overChrome && _sidebar.Bounds.Contains(_pointer.X, _pointer.Y) && !IsOverOverlay(_pointer.X, _pointer.Y))
            {
                ScrollSidebar(-Math.Sign(windowEvent.Y));
            }

            return overChrome;
        }

        // A notch up scrolls a list towards its start.
        overlay.Scroll(-Math.Sign(windowEvent.Y));
        _window.RequestFrame();
        return true;
    }

    /// <summary>Scrolls the list of bookmarks in the side panel.</summary>
    /// <param name="rows">The rows to move down, negative to move up.</param>
    private void ScrollSidebar(int rows)
    {
        var first = Math.Clamp(_sidebar.First + rows, 0, Math.Max(0, _sidebar.Rows.Count - _sidebar.VisibleRows));
        if (first == _sidebar.First)
        {
            return;
        }

        _sidebar.First = first;
        _sidebar.Version++;
        _window.RequestFrame();
    }

    /// <summary>Goes to a bookmark of the side panel, and shows or hides the bookmarks under it.</summary>
    /// <param name="index">The row.</param>
    /// <param name="tab">The tab shown.</param>
    private void PickBookmark(int index, DocumentTabViewModel tab)
    {
        if ((uint)index >= (uint)_sidebar.Rows.Count)
        {
            return;
        }

        var bookmark = _sidebar.Rows[index].Item;
        if (bookmark.Children.Count > 0)
        {
            bookmark.IsExpanded = !bookmark.IsExpanded;
            _sidebar.Version++;
        }

        if (ReferenceEquals(tab.SelectedOutlineItem, bookmark))
        {
            // Choosing the chosen bookmark again does not change the selection, so the tab is told to go there directly.
            tab.Navigate(bookmark.Target);
            return;
        }

        tab.SelectedOutlineItem = bookmark;
    }

    /// <summary>Handles a key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/> when the key was used.</returns>
    private bool PressKey(ViewerKey key, ViewerModifiers modifiers)
    {
        if (_overlay is not { } overlay)
        {
            return (modifiers & ViewerModifiers.Control) != 0 && RunShortcut(key, modifiers);
        }

        if (!overlay.TakesAllKeys && key is not (ViewerKey.Enter or ViewerKey.Escape or ViewerKey.Back))
        {
            return false;
        }

        if (!overlay.PressKey(key, modifiers) && ReferenceEquals(_overlay, overlay))
        {
            CloseOverlay();
            return true;
        }

        _window.RequestFrame();
        return true;
    }

    /// <summary>Runs the window command of a key pressed with Control.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/> when the key has a window command.</returns>
    private bool RunShortcut(ViewerKey key, ViewerModifiers modifiers)
    {
        switch (key)
        {
            case ViewerKey.O:
                {
                    Run(_main.OpenCommand, null);
                    return true;
                }

            case ViewerKey.F:
                {
                    ShowFind();
                    return true;
                }

            case ViewerKey.S when _main.SelectedTab is { } tab:
                {
                    Run(tab.SaveCommand, null);
                    return true;
                }

            case ViewerKey.W when _main.SelectedTab is { } tab:
                {
                    Run(_main.CloseTabCommand, tab);
                    return true;
                }

            case ViewerKey.Tab:
                {
                    Run((modifiers & ViewerModifiers.Shift) != 0 ? _main.PreviousTabCommand : _main.NextTabCommand, null);
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>Handles a typed character.</summary>
    /// <param name="scalar">The Unicode scalar value.</param>
    /// <returns><see langword="true"/> when the character was used.</returns>
    private bool Type(int scalar)
    {
        if (_overlay is not { } overlay)
        {
            return false;
        }

        overlay.Type(scalar);
        _window.RequestFrame();
        return true;
    }

    /// <summary>Does what a control of the bars is for.</summary>
    /// <param name="item">The control.</param>
    private void RunBar(ChromeItem item)
    {
        switch (item.Command)
        {
            case ChromeCommand.SelectTab when item.Index < _main.Tabs.Count:
                {
                    _main.SelectedTab = _main.Tabs[item.Index];
                    break;
                }

            case ChromeCommand.CloseTab when item.Index < _main.Tabs.Count:
                {
                    Run(_main.CloseTabCommand, _main.Tabs[item.Index]);
                    break;
                }

            case ChromeCommand.Open:
                {
                    Run(_main.OpenCommand, null);
                    break;
                }

            case ChromeCommand.Menu:
                {
                    ShowMenu(MainMenu.Build(_main), new((int)item.Bounds.X, (int)item.Bounds.Bottom));
                    break;
                }

            default:
                {
                    if (_main.SelectedTab is { } tab)
                    {
                        RunTab(item.Command, item.Index, tab);
                    }

                    break;
                }
        }

        _window.RequestFrame();
    }

    /// <summary>Does what a control is for on the tab shown.</summary>
    /// <param name="command">What the control does.</param>
    /// <param name="index">Which row the control stands for.</param>
    /// <param name="tab">The tab shown.</param>
    private void RunTab(ChromeCommand command, int index, DocumentTabViewModel tab)
    {
        if (command == ChromeCommand.Find)
        {
            ShowFind();
            return;
        }

        if (command == ChromeCommand.PickRow)
        {
            PickBookmark(index, tab);
            return;
        }

        ICommand? run = command switch
        {
            ChromeCommand.Save => tab.SaveCommand,
            ChromeCommand.PreviousPage => tab.PreviousPageCommand,
            ChromeCommand.NextPage => tab.NextPageCommand,
            ChromeCommand.ZoomOut => tab.ZoomOutCommand,
            ChromeCommand.ZoomIn => tab.ZoomInCommand,
            ChromeCommand.FitWidth => tab.FitWidthCommand,
            ChromeCommand.FitPage => tab.FitPageCommand,
            ChromeCommand.Sidebar => tab.ToggleSidebarCommand,
            _ => null,
        };
        if (run is not null)
        {
            Run(run, null);
        }
    }
}
