// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Core.Rendering;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Markup;
using HyperPdf.ViewModels.Output;
using HyperPdf.ViewModels.Preferences;
using HyperPdf.ViewModels.Reading;
using HyperPdf.ViewModels.Services;
using HyperPdf.ViewModels.Sidebar;
using HyperPdf.Viewer.Chrome.Forms;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome;

/// <content>Opens and closes menus, dialogs, on-page bubbles and full-window views.</content>
public sealed partial class ViewerChrome
{
    /// <summary>Opens a menu.</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="at">Where the menu's top left corner is wanted, in device pixels.</param>
    public void ShowMenu(List<MenuEntry> entries, ScreenPoint at)
    {
        ArgumentNullException.ThrowIfNull(entries);

        // A menu entry carries its own command, so the entry chosen says which command to run.
        Show(MenuOverlays.Open(CollectionsMarshal.AsSpan(entries), at, RunEntry));
    }

    /// <summary>Shows a menu of the menu bar, which Left and Right leave for the menu beside it.</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="at">Where its top left corner goes, in device pixels.</param>
    /// <param name="step">Opens the menu beside it: 1 for the one to the right, -1 for the one to the left.</param>
    public void ShowMenu(List<MenuEntry> entries, ScreenPoint at, Action<int> step)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(step);
        Show(MenuOverlays.Open(CollectionsMarshal.AsSpan(entries), at, RunEntry, step));
    }

    /// <summary>Asks a yes or no question in a dialog. The dialog opens when the answer is subscribed to.</summary>
    /// <param name="request">What is asked.</param>
    /// <returns>One value when the dialog closes: <see langword="true"/> when the user agreed.</returns>
    public IObservable<bool> Confirm(ConfirmRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Signal.Defer(() =>
        {
            var dialog = ConfirmOverlays.Open(request);
            Show(dialog.Overlay);
            return dialog.Answer;
        });
    }

    /// <summary>Asks for a file in a dialog. The dialog opens when the answer is subscribed to.</summary>
    /// <param name="folder">The folder to start in.</param>
    /// <param name="saveName">The file name to offer when asking where to save, or <see langword="null"/> to ask what to open.</param>
    /// <returns>One value when the dialog closes: the path chosen, or <see langword="null"/> when it was cancelled.</returns>
    public IObservable<string?> PickFile(string folder, string? saveName)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return PickPaths(new(folder, saveName, false, false, UiText.Get(saveName is null ? "Open a file" : "Save a copy as"))).Select(static paths => paths.Length > 0 ? paths[0] : null);
    }

    /// <summary>Asks for a folder in a dialog. The dialog opens when the answer is subscribed to.</summary>
    /// <param name="folder">The folder to start in.</param>
    /// <returns>One value when the dialog closes: the folder chosen, or <see langword="null"/> when it was cancelled.</returns>
    public IObservable<string?> PickFolder(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return PickPaths(new(folder, null, false, true, UiText.Get("Choose a folder"))).Select(static paths => paths.Length > 0 ? paths[0] : null);
    }

    /// <summary>Asks for the files to open in a dialog where several can be ticked. The dialog opens when the answer is subscribed to.</summary>
    /// <param name="folder">The folder to start in.</param>
    /// <returns>One value when the dialog closes: the paths chosen, or none when it was cancelled.</returns>
    public IObservable<string[]> PickFiles(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return PickPaths(new(folder, null, true, false, UiText.Get("Open files")));
    }

    /// <summary>Asks for one line of text in a dialog. The dialog opens when the answer is subscribed to.</summary>
    /// <param name="request">What is asked.</param>
    /// <returns>One value when the dialog closes: the text typed, or <see langword="null"/> when it was cancelled.</returns>
    public IObservable<string?> Prompt(PromptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Signal.Defer(() =>
        {
            var dialog = PromptOverlays.Open(request);
            Show(dialog.Overlay);
            return dialog.Answer;
        });
    }

    /// <summary>Shows the preferences window when subscribed to.</summary>
    /// <param name="preferences">The preferences.</param>
    /// <returns>One value when the window closes.</returns>
    public IObservable<bool> ShowPreferences(PreferencesViewModel preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return ShowForm(PreferencesForm.For(preferences));
    }

    /// <summary>Shows the properties of a document when subscribed to.</summary>
    /// <param name="properties">The properties.</param>
    /// <returns>One value when the window closes.</returns>
    public IObservable<bool> ShowProperties(PropertiesViewModel properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return ShowForm(DocumentForms.Properties(properties));
    }

    /// <summary>Shows the licences of everything the app is built from when subscribed to.</summary>
    /// <param name="licences">The licences.</param>
    /// <returns>One value when the window closes.</returns>
    public IObservable<bool> ShowLicences(LicencesViewModel licences)
    {
        ArgumentNullException.ThrowIfNull(licences);
        return ShowForm(DocumentForms.Licences(licences));
    }

    /// <summary>Shows the print window when subscribed to.</summary>
    /// <param name="print">The print settings.</param>
    /// <returns>One value when the window closes: <see langword="true"/> when the user chose to print.</returns>
    public IObservable<bool> ShowPrint(PrintPreviewViewModel print)
    {
        ArgumentNullException.ThrowIfNull(print);
        return ShowForm(DocumentForms.Print(print, ShownSheet));
    }

    /// <summary>Shows the window that plays sound or film when subscribed to. The film stops when the window is put away.</summary>
    /// <param name="player">The player.</param>
    /// <returns>One value when the window closes.</returns>
    public IObservable<bool> ShowMedia(MediaPlayerViewModel player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return ShowForm(ToolForms.MediaPlayer(player, _ => ShownFilm(player))).Finally(() => Signal.Return(RxVoid.Default).InvokeCommand(player.StopCommand).Dispose());
    }

    /// <summary>Shows the window that makes a hand signature or initials when subscribed to, with a picture of a mark that came from a picture file.</summary>
    /// <param name="mark">The view model of the mark being made.</param>
    /// <returns>One value when the window closes: <see langword="true"/> to use the mark.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<bool> ShowSignatureMark(SignatureMarkViewModel mark) => MarkPreview.Show(ShowForm, _window.Backend.Surfaces, mark);

    /// <summary>Opens the search bar of the tab shown.</summary>
    public void ShowFind()
    {
        if (_main.SelectedTab is not { } tab)
        {
            return;
        }

        tab.Search.Open();
        ShowFindBar();
    }

    /// <summary>Shows the search bar of the tab shown, unless it is already showing.</summary>
    public void ShowFindBar()
    {
        if (_main.SelectedTab is not { } tab || _overlay is { Kind: OverlayKind.Find })
        {
            return;
        }

        Show(FindOverlays.Open(tab.Search));
    }

    /// <summary>Shows a window made of rows when subscribed to.</summary>
    /// <param name="request">What the window shows.</param>
    /// <returns>One value when the window closes: <see langword="true"/> when the user agreed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IObservable<bool> ShowForm(FormRequest request) => Signal.Defer(() =>
    {
        var dialog = FormOverlays.Open(request);
        Show(dialog.Overlay);
        return dialog.Answer;
    });

    /// <summary>Opens a typing box over a form field or a text box on the page, so its text is typed where it lies.</summary>
    /// <param name="target">What is typed into.</param>
    /// <param name="canvas">The page canvas, which knows where the page lies in the window.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ShowTyping(TypingTarget target, ViewerCanvas canvas) => Show(TypingOverlays.Open(target, canvas));

    /// <summary>Opens a comment bubble beside a note or marked passage on the page, without dimming the page.</summary>
    /// <param name="marks">The annotation tools of the tab shown.</param>
    /// <param name="canvas">The page canvas, which knows where the annotation lies in the window.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ShowNoteBubble(AnnotationsViewModel marks, ViewerCanvas canvas) => Show(NoteBubbleOverlays.Open(marks, canvas));

    /// <summary>Shows focus mode in place of the pages.</summary>
    /// <param name="focus">The focus mode of the tab shown.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ShowFocus(FocusModeViewModel focus) => Show(FocusOverlays.Open(focus));

    /// <summary>Shows the start view, which stands in for the pages while no document is open.</summary>
    /// <param name="main">The main view model.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ShowStart(MainViewModel main) => Show(StartOverlays.Open(main));

    /// <summary>Takes the start view away when a document opened.</summary>
    internal void CloseStart()
    {
        if (_overlay is { Kind: OverlayKind.Start })
        {
            CloseOverlay();
        }
    }

    /// <summary>Goes back to the pages when focus mode was turned off.</summary>
    internal void CloseFocus()
    {
        if (_overlay is { Kind: OverlayKind.Focus })
        {
            CloseOverlay();
        }
    }

    /// <summary>Closes the typing box when the view model ended the typing by itself.</summary>
    internal void CloseTyping()
    {
        if (_overlay is { Kind: OverlayKind.Typing })
        {
            CloseOverlay();
        }
    }

    /// <summary>Closes the comment bubble when the view model closed it.</summary>
    internal void CloseNoteBubble()
    {
        if (_overlay is { Kind: OverlayKind.NoteBubble })
        {
            CloseOverlay();
        }
    }

    /// <summary>Asks the head for a desktop picker, with the built-in chooser available for hosts without one.</summary>
    /// <param name="request">The selection requested.</param>
    /// <returns>The selected paths.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<string[]> PickPaths(FilePickerRequest request) =>
        Signal.Defer(() => _window.PickPaths(request)?.ObserveOn(RxSchedulers.MainThreadScheduler)
            .Catch<string[], IOException>(error =>
            {
                ErrorTrace.Write(error);
                return BuiltInPicker(request);
            }) ?? BuiltInPicker(request));

    /// <summary>Shows the file chooser inside the viewer.</summary>
    /// <param name="request">The selection requested.</param>
    /// <returns>The selected paths.</returns>
    private IObservable<string[]> BuiltInPicker(FilePickerRequest request)
    {
        var dialog = FileOverlays.Open(request.Folder, request.SaveName, request.Multiple, request.Directories);
        Show(dialog.Overlay);
        return dialog.Answer;
    }

    /// <summary>Shows an overlay that needs nothing besides itself, in place of the one that is open.</summary>
    /// <param name="overlay">The overlay.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Show(Overlay overlay) => Show(overlay, EmptyDisposable.Instance);

    /// <summary>Shows an overlay in place of the one that is open, and joins it to its input, its changes and its wish to close.</summary>
    /// <param name="overlay">The overlay.</param>
    /// <param name="extra">What else lives as long as the overlay is open: what made it, and what follows its answer.</param>
    private void Show(Overlay overlay, IDisposable extra)
    {
        if (_overlay is { } open && ((open.IsModal && overlay.IsModal) || open.StaysUnder) && _overlayLinks is { } links)
        {
            // A dialog opened by a dialog, such as the file dialog a tool window asks for, goes over it. So does anything opened over
            // focus mode or the start view. The first one waits, without input, and comes back when the second one closes.
            _under.Add(new(open, links));
        }
        else
        {
            // The overlay that is open answers whoever asked for it before it is let go.
            var old = _overlay;
            _overlay = null;
            old?.Input.OnNext(OverlayInput.Dismiss());
            Retire(_overlayLinks);
        }

        _overlay = overlay;
        _version++;

        // The keyboard starts outside the new overlay's controls, so Enter and Escape answer the overlay as a whole.
        _focus = ChromeFocus.None;

        // An overlay over a view model follows it: the search bar its matches, the print window its printers and sheets. The overlay
        // is let go last, after everything joined to it.
        _overlayLinks = new MultipleDisposable(
            overlay.Changes.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(_ => InvalidateOverlay(), ErrorTrace.Write),
            overlay.Closed.SubscribeSafe(_ => Close(overlay), ErrorTrace.Write),
            extra,
            overlay.Lifetime);
        BuildOverlay();
        _window.RequestFrame();
    }

    /// <summary>
    /// Closes an overlay that asked to close. One that waits under another, as a dialog does when its answer opened the next
    /// dialog, is let go where it waits, so it does not come back.
    /// </summary>
    /// <param name="overlay">The overlay.</param>
    private void Close(Overlay overlay)
    {
        if (ReferenceEquals(_overlay, overlay))
        {
            CloseOverlay();
            return;
        }

        for (var i = _under.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(_under[i].Overlay, overlay))
            {
                continue;
            }

            var links = _under[i].Links;
            _under.RemoveAt(i);
            Retire(links);
            return;
        }
    }

    /// <summary>Closes the overlay.</summary>
    private void CloseOverlay()
    {
        Retire(_overlayLinks);
        _overlayLinks = null;
        _padHeld = false;
        _version++;
        _overlay = null;
        _overlayItems.Clear();
        Pointer.SetTargets(_overlayItems, true);
        if (_focus.InOverlay)
        {
            _focus = ChromeFocus.None;
        }

        if (_under.Count > 0)
        {
            // The dialog that waited under this one takes the window and its input back.
            var waiting = _under[^1];
            _under.RemoveAt(_under.Count - 1);
            _overlay = waiting.Overlay;
            _overlayLinks = waiting.Links;
            InvalidateOverlay();
            return;
        }

        _ = FollowPointer();
        _window.RequestFrame();
    }

    /// <summary>Gets the picture of a sheet of the print window, drawn again only when another sheet or size is wanted.</summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="room">The room for it.</param>
    /// <returns>The picture, or <see langword="null"/> when it cannot be drawn.</returns>
    private IRenderSurface? ShownSheet(PrintPreviewPage sheet, SurfaceSize room)
    {
        _sheetFrame = SheetFrames.Get(_window.Backend.Surfaces, _sheetFrame, sheet, room);
        return _sheetFrame.Surface;
    }

    /// <summary>Gets the latest picture of the film the media window plays.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The picture, or <see langword="null"/> before the first one.</returns>
    private IRenderSurface? ShownFilm(MediaPlayerViewModel player)
    {
        _mediaFrame = MediaFrames.Get(_window.Backend.Surfaces, _mediaFrame, player);
        return _mediaFrame.Surface;
    }
}
