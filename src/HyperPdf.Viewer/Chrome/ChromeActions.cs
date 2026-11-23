// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows.Input;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Services;
using HyperPdf.ViewModels.Sidebar;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Windowing.Windows;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// The commands of the controls that act on the window itself: which tab and which list of the side panel is shown, and the menus
/// and dialogs the window opens. Everything else a control does is a command of a view model, which the control runs directly.
/// </summary>
internal sealed class ChromeActions : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="ChromeActions"/> class.</summary>
    /// <param name="chrome">The window's controls, which open the menus and dialogs.</param>
    /// <param name="main">The main view model, which knows the tab shown.</param>
    /// <param name="window">The window, which its own buttons put away, size and close.</param>
    internal ChromeActions(ViewerChrome chrome, MainViewModel main, IPlatformWindow window)
    {
        SelectTab = ReactiveCommand.Create<DocumentTabViewModel>(tab => main.SelectedTab = tab);
        ShowBarMenu = ReactiveCommand.Create<int>(index => OpenBarMenu(main, chrome, index));
        ShowAddPages = ReactiveCommand.Create(() => OpenMenu(main, chrome, ShowAddPages, static (_, tab) => ToolMenus.AddPages(tab)));
        ShowChoosePages = ReactiveCommand.Create(() => OpenMenu(main, chrome, ShowChoosePages, static (_, tab) => ToolMenus.ChoosePages(tab)));
        ShowView = ReactiveCommand.Create(() => OpenMenu(main, chrome, ShowView, MainMenu.ReadingView));
        ShowZoom = ReactiveCommand.Create(() => OpenMenu(main, chrome, ShowZoom, static (_, tab) => ToolMenus.Zoom(tab)));
        ShowVoices = ReactiveCommand.Create(() => OpenMenu(main, chrome, ShowVoices, static (_, tab) => ToolMenus.Voices(tab.ReadAloud)));
        ShowSpeeds = ReactiveCommand.Create(() => OpenMenu(main, chrome, ShowSpeeds, static (_, tab) => ToolMenus.Speeds(tab.ReadAloud)));
        ShowMoreMarks = ReactiveCommand.Create(() => ShowMarksMenu(main, chrome, ShowMoreMarks));
        ShowFind = ReactiveCommand.Create(chrome.ShowFind);
        GoToPage = ReactiveCommand.Create(() => AskForPage(main, chrome));
        EditMeasureScale = ReactiveCommand.Create(() => AskForMeasureScale(main, chrome));
        ToggleAnnotate = ReactiveCommand.Create(() => ToggleMarking(main));
        ShowSidebarList = ReactiveCommand.Create<SidebarMode>(mode => ShowList(main, chrome, mode));
        PickSidebarRow = ReactiveCommand.Create<object, object>(static target => target);
        ToggleFollowPage = ReactiveCommand.Create(() =>
        {
            main.SidebarFollowsPage = !main.SidebarFollowsPage;
            chrome.InvalidateBars();
        });
        CheckSignatures = ReactiveCommand.Create(() => RunSignature(main, static s => s.CheckInlineCommand));
        ShowSignatureDetails = ReactiveCommand.Create(() => RunSignature(main, static s => s.ShowDetailsCommand));
        SignWithCertificate = ReactiveCommand.Create(() => RunSignature(main, static s => s.SignWithCertificateCommand));
        AddBookmark = ReactiveCommand.Create(() => RunBookmark(main, static b => b.AddCommand));
        RenameBookmark = ReactiveCommand.Create(() => RunBookmark(main, static b => b.RenameCommand));
        RemoveBookmark = ReactiveCommand.Create(() => RunBookmark(main, static b => b.RemoveCommand));
        MoveBookmarkUp = ReactiveCommand.Create(() => RunBookmark(main, static b => b.MoveUpCommand));
        MoveBookmarkDown = ReactiveCommand.Create(() => RunBookmark(main, static b => b.MoveDownCommand));
        IndentBookmark = ReactiveCommand.Create(() => RunBookmark(main, static b => b.IndentCommand));
        OutdentBookmark = ReactiveCommand.Create(() => RunBookmark(main, static b => b.OutdentCommand));
        Minimize = ReactiveCommand.Create(window.Minimize);
        ToggleMaximize = ReactiveCommand.Create(() =>
        {
            window.SetMaximized(!window.IsMaximized);

            // The button shows which way the next press goes, so it is laid out again even when the size stays the same.
            chrome.InvalidateBars();
        });
        CloseWindow = ReactiveCommand.Create(window.AskToClose);
    }

    /// <summary>Gets the command that puts the window away, for a window the system gives no title bar.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> Minimize { get; }

    /// <summary>Gets the command that makes the window as big as the screen, or puts it back to the size it had.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> ToggleMaximize { get; }

    /// <summary>Gets the command that asks to close the window, as the system's own close button does.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> CloseWindow { get; }

    /// <summary>Gets the command that shows a tab, which is its parameter.</summary>
    internal ReactiveCommand<DocumentTabViewModel, RxVoid> SelectTab { get; }

    /// <summary>Gets the command that opens a menu of the menu bar under its name. Its parameter is the menu's place on the bar.</summary>
    internal ReactiveCommand<int, RxVoid> ShowBarMenu { get; }

    /// <summary>Gets the command that opens the list of what can be added to the pages: a blank page, pages of files and pictures.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> ShowAddPages { get; }

    /// <summary>Gets the command that opens the list of ways to choose pages: every page, the odd ones, the even ones or the one being read.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> ShowChoosePages { get; }

    /// <summary>Gets the command that opens the reading and page layout choices.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> ShowView { get; }

    /// <summary>Gets the command that opens the zoom choices.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> ShowZoom { get; }

    /// <summary>Gets the command that opens the list of every voice that can read aloud.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> ShowVoices { get; }

    /// <summary>Gets the command that opens the list of reading speeds.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> ShowSpeeds { get; }

    /// <summary>Gets the command that opens the menu of the extra marking tools under its button.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> ShowMoreMarks { get; }

    /// <summary>Gets the command that opens the search bar.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> ShowFind { get; }

    /// <summary>Gets the command that asks which page to go to, and goes there.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> GoToPage { get; }

    /// <summary>Gets the command that edits the measuring scale.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> EditMeasureScale { get; }

    /// <summary>Gets the command that shows the marking bar, or puts it away when it is shown.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> ToggleAnnotate { get; }

    /// <summary>Gets the command that shows one list of the side panel, which is its parameter, from its start.</summary>
    internal ReactiveCommand<SidebarMode, RxVoid> ShowSidebarList { get; }

    /// <summary>Gets the command that makes the list of pages follow the page being read, or stops it.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> ToggleFollowPage { get; }

    /// <summary>Gets the command that verifies the open document's digital signatures in place on the signed-document bar.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> CheckSignatures { get; }

    /// <summary>Gets the command that opens the full signatures window for the open document.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> ShowSignatureDetails { get; }

    /// <summary>Gets the command that opens the certificate signing window for the open document.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> SignWithCertificate { get; }

    /// <summary>Gets the command that adds a bookmark at the current page.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> AddBookmark { get; }

    /// <summary>Gets the command that renames the chosen bookmark.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> RenameBookmark { get; }

    /// <summary>Gets the command that removes the chosen bookmark.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> RemoveBookmark { get; }

    /// <summary>Gets the command that moves the chosen bookmark earlier.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> MoveBookmarkUp { get; }

    /// <summary>Gets the command that moves the chosen bookmark later.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> MoveBookmarkDown { get; }

    /// <summary>Gets the command that nests the chosen bookmark under the one above it.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> IndentBookmark { get; }

    /// <summary>Gets the command that moves the chosen bookmark out of its parent.</summary>
    internal ReactiveCommand<RxVoid, RxVoid> OutdentBookmark { get; }

    /// <summary>
    /// Gets the command of a row of the side panel. Its parameter is what the row stands for: a page, a bookmark, a note, an attached
    /// file or a layer. The command hands it on, so each kind of row is one stream that the tab shown is joined to.
    /// </summary>
    internal ReactiveCommand<object, object> PickSidebarRow { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        SelectTab.Dispose();
        ShowBarMenu.Dispose();
        ShowAddPages.Dispose();
        ShowChoosePages.Dispose();
        ShowView.Dispose();
        ShowZoom.Dispose();
        ShowVoices.Dispose();
        ShowSpeeds.Dispose();
        ShowMoreMarks.Dispose();
        ShowFind.Dispose();
        GoToPage.Dispose();
        EditMeasureScale.Dispose();
        ToggleAnnotate.Dispose();
        ShowSidebarList.Dispose();
        PickSidebarRow.Dispose();
        ToggleFollowPage.Dispose();
        CheckSignatures.Dispose();
        ShowSignatureDetails.Dispose();
        SignWithCertificate.Dispose();
        AddBookmark.Dispose();
        RenameBookmark.Dispose();
        RemoveBookmark.Dispose();
        MoveBookmarkUp.Dispose();
        MoveBookmarkDown.Dispose();
        IndentBookmark.Dispose();
        OutdentBookmark.Dispose();
        Minimize.Dispose();
        ToggleMaximize.Dispose();
        CloseWindow.Dispose();
    }

    /// <summary>Runs a command when it can run.</summary>
    /// <param name="command">The command.</param>
    internal static void Run(ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    /// <summary>Runs one of the digital-signature commands of the open document, creating the signatures part only on use.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="pick">Selects the command on <see cref="HyperPdf.ViewModels.Signing.SignaturesViewModel"/>.</param>
    private static void RunSignature(MainViewModel main, Func<HyperPdf.ViewModels.Signing.SignaturesViewModel, ICommand> pick)
    {
        if (main.SelectedTab is { IsLoaded: true } tab)
        {
            Run(pick(tab.Signatures));
        }
    }

    /// <summary>Runs one of the bookmark-editing commands of the open document, creating the bookmark editor only on use.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="pick">Selects the command on <see cref="OutlineEditViewModel"/>.</param>
    private static void RunBookmark(MainViewModel main, Func<OutlineEditViewModel, ICommand> pick)
    {
        if (main.SelectedTab is { IsLoaded: true } tab)
        {
            Run(pick(tab.OutlineEditing));
        }
    }

    /// <summary>Finds where a menu opens: under the button of the command that opens it.</summary>
    /// <param name="chrome">The window's controls.</param>
    /// <param name="command">The command the button runs, or <see langword="null"/> before it is made.</param>
    /// <returns>The bottom left corner of the button, or the top left corner of the window when the bars do not show it.</returns>
    private static ScreenPoint Under(ViewerChrome chrome, ICommand? command)
    {
        var bars = chrome.Bars;
        for (var i = 0; i < bars.Count; i++)
        {
            if (ReferenceEquals(bars[i].Command, command))
            {
                return new((int)bars[i].Bounds.X, (int)bars[i].Bounds.Bottom);
            }
        }

        return default;
    }

    /// <summary>Builds the menu for the current document when its toolbar button opens.</summary>
    /// <param name="main">The window's view model.</param>
    /// <param name="chrome">The window controls.</param>
    /// <param name="command">The command on the button.</param>
    /// <param name="entries">Builds the entries for the loaded document.</param>
    private static void OpenMenu(MainViewModel main, ViewerChrome chrome, ICommand? command, Func<MainViewModel, DocumentTabViewModel, List<MenuEntry>> entries)
    {
        if (main.SelectedTab is { IsLoaded: true } tab)
        {
            chrome.ShowMenu(entries(main, tab), Under(chrome, command));
        }
    }

    /// <summary>Opens the menu of the extra marking tools and text settings of the tab shown, under its button.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="chrome">The window's controls.</param>
    /// <param name="command">The command the button runs.</param>
    private static void ShowMarksMenu(MainViewModel main, ViewerChrome chrome, ICommand? command)
    {
        if (main.SelectedTab is { } tab)
        {
            chrome.ShowMenu([..ToolMenus.MoreMarks(tab.Annotations)], Under(chrome, command));
        }
    }

    /// <summary>Shows the marking bar of the tab shown, or puts it away.</summary>
    /// <param name="main">The main view model.</param>
    private static void ToggleMarking(MainViewModel main)
    {
        if (main.SelectedTab?.Annotations is { } marks)
        {
            Run(marks.IsAnnotating ? marks.DoneCommand : marks.StartCommand);
        }
    }

    /// <summary>Shows one list of the side panel of the tab shown, from its start.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="chrome">The window's controls, which keep how far the list is scrolled.</param>
    /// <param name="mode">The list.</param>
    private static void ShowList(MainViewModel main, ViewerChrome chrome, SidebarMode mode)
    {
        if (main.SelectedTab is not { } tab)
        {
            return;
        }

        tab.SidebarMode = mode;
        chrome.ResetSidebarScroll();
    }

    /// <summary>Edits the measuring scale for the active document.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="chrome">The window's controls.</param>
    private static void AskForMeasureScale(MainViewModel main, ViewerChrome chrome)
    {
        if (main.SelectedTab?.MeasureIfMade is not { IsOn: true } measure)
        {
            return;
        }

        var hint = UiText.Get("Type how the page relates to real size, such as 1 cm = 2 m or 1:100.");
        _ = chrome.Prompt(new(UiText.Get("Measuring scale"), hint, measure.ScaleText, UiText.Get("Apply"), false)).SubscribeSafe(
            answer =>
            {
                if (answer is not null)
                {
                    measure.ScaleText = answer;
                }
            },
            ErrorTrace.Write);
    }

    /// <summary>Asks which page of the tab shown to go to, and goes there.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="chrome">The window's controls, which ask.</param>
    private static void AskForPage(MainViewModel main, ViewerChrome chrome)
    {
        if (main.SelectedTab is not { } tab)
        {
            return;
        }

        _ = chrome.Prompt(new(UiText.Get("Go to a page"), UiText.Get("Page number"), tab.PageEntry, UiText.Get("Go"), false)).SubscribeSafe(
            answer =>
            {
                if (answer is null)
                {
                    return;
                }

                tab.PageEntry = answer;
                Run(tab.GoToPageEntryCommand);
            },
            ErrorTrace.Write);
    }

    /// <summary>Opens a menu of the menu bar under its name. Left and Right in it open the menu beside it.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="chrome">The window's controls.</param>
    /// <param name="index">The menu's place on the bar.</param>
    private void OpenBarMenu(MainViewModel main, ViewerChrome chrome, int index)
    {
        if (!MenuBar.CanOpen(main, index))
        {
            return;
        }

        chrome.ShowMenu(MenuBar.Build(index, main, this), MenuBar.Under(chrome.Bars, ShowBarMenu, index), step => OpenBarMenu(main, chrome, MenuBar.Beside(main, index, step)));
    }
}
