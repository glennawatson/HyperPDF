// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Markup;
using HyperPdf.ViewModels.Output;
using HyperPdf.ViewModels.Preferences;
using HyperPdf.ViewModels.Reading;
using HyperPdf.ViewModels.Services;
using HyperPdf.ViewModels.Sidebar;
using HyperPdf.ViewModels.Signing;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Viewer.Chrome.Forms;
using HyperPdf.Viewer.Services;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer;

/// <summary>
/// Answers what a tab and the window ask of the desktop: which file to read or write, whether to open a file a document points at,
/// and where a link leads. Each question is an interaction of a view model, and each answer is a stream from one of the window's dialogs.
/// </summary>
internal static class TabRequests
{
    /// <summary>
    /// Answers the questions of a tab for as long as it is shown. A web or mail address goes to the desktop, and a file a document
    /// points at is opened only after the user agrees.
    /// </summary>
    /// <param name="chrome">The window's controls, which show the dialogs.</param>
    /// <param name="tab">The tab shown.</param>
    /// <returns>What ends the answering.</returns>
    internal static MultipleDisposable Follow(ViewerChrome chrome, DocumentTabViewModel tab) =>
    [
        tab.OpenLinkInteraction.RegisterHandler(static context =>
        {
            _ = ShellLauncher.OpenLink(context.Input);
            context.SetOutput(RxVoid.Default);
        }),

        // Files to read.
        tab.CertificateBrowseInteraction.RegisterHandler(context => chrome.PickFile(FolderOf(tab), null).Do(context.SetOutput)),

        // Files to write, each offered under the name the view model suggests.
        tab.Attachments.SaveInteraction.RegisterHandler(context => chrome.PickFile(FolderOf(tab), Path.GetFileName(context.Input)).Do(context.SetOutput)),
        tab.PageTools.SaveImageInteraction.RegisterHandler(context => chrome.PickFile(FolderOf(tab), Path.GetFileName(context.Input)).Do(context.SetOutput)),

        // A part of the tab that is made on first use is followed from when it is made, so showing a tab does not make it.
        FollowParts(chrome, tab),
    ];

    /// <summary>Answers the questions of the window itself.</summary>
    /// <param name="chrome">The window's controls, which show the dialogs.</param>
    /// <param name="main">The main view model.</param>
    /// <returns>What ends the answering.</returns>
    internal static MultipleDisposable Follow(ViewerChrome chrome, MainViewModel main) =>
    [
        main.ConvertPicturesInteraction.RegisterHandler(context => chrome.PickFiles(FolderOf(main.SelectedTab)).Do(context.SetOutput)),
        main.ConvertDestinationInteraction.RegisterHandler(context => chrome.PickFile(FolderOf(main.SelectedTab), Path.GetFileName(context.Input)).Do(context.SetOutput)),
        main.ShowFolderSearchInteraction.RegisterHandler(context =>
        {
            var choosing = context.Input.ChooseFolderInteraction.RegisterHandler(where => chrome.PickFolder(FolderOf(main.SelectedTab)).Do(where.SetOutput));
            return chrome.ShowForm(ToolForms.FolderSearch(context.Input)).AsSignal().Do(context.SetOutput).Finally(choosing.Dispose);
        }),
        main.NewWindowInteraction.RegisterHandler(static context =>
        {
            // A new window is a new viewer process. The view model made for it only carries which documents it opens.
            _ = ShellLauncher.OpenWindow(PathsOf(context.Input));
            context.Input.Dispose();
            context.SetOutput(RxVoid.Default);
        }),
    ];

    /// <summary>Picks the folder a file dialog starts in: the folder of the tab's document, or the user's own folder.</summary>
    /// <param name="tab">The tab shown, or <see langword="null"/> when none is open.</param>
    /// <returns>The folder.</returns>
    internal static string FolderOf(DocumentTabViewModel? tab) =>
        tab is { FilePath: { Length: > 0 } path } && Path.GetDirectoryName(Path.GetFullPath(path)) is { Length: > 0 } folder
            ? folder
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>Makes the question a dialog asks from the question a view model asks.</summary>
    /// <param name="asked">What the view model asks for.</param>
    /// <returns>The question.</returns>
    internal static PromptRequest PromptOf(TextPrompt asked) => new(asked.Title, asked.Label, asked.Text, asked.AcceptText, false);

    /// <summary>Follows a part of the tab from when it is made until the tab is left.</summary>
    /// <typeparam name="TPart">The part's type.</typeparam>
    /// <param name="tab">The tab shown.</param>
    /// <param name="follow">Answers the part's questions, and gives what ends the answering.</param>
    /// <returns>What ends the waiting and the answering.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IDisposable FollowWhenMade<TPart>(DocumentTabViewModel tab, Func<TPart, IDisposable> follow)
        where TPart : class =>
        tab.MadeParts
            .OfType<TPart>()
            .Take(1)
            .SwitchMap(part => Signal.Using(() => follow(part), static _ => Signal.Never<RxVoid>()))
            .SubscribeSafe(static _ => { }, ErrorTrace.Write);

    /// <summary>
    /// Follows every part of the tab that is made on first use, from when it is made until the tab is left. One subscription to
    /// the parts the tab makes serves them all, so showing a tab costs the same however many kinds of part there are.
    /// </summary>
    /// <param name="chrome">The window's controls, which show the dialogs.</param>
    /// <param name="tab">The tab shown.</param>
    /// <returns>What ends the waiting and the answering.</returns>
    private static MultipleDisposable FollowParts(ViewerChrome chrome, DocumentTabViewModel tab)
    {
        var followed = new MultipleDisposable();
        followed.Add(tab.MadeParts.SubscribeSafe(
            part =>
            {
                if ((FollowTool(chrome, tab, part) ?? FollowContent(chrome, tab, part)) is { } answering)
                {
                    followed.Add(answering);
                }
            },
            ErrorTrace.Write));
        return followed;
    }

    /// <summary>Answers the questions of a tool of the tab, when the part is one.</summary>
    /// <param name="chrome">The window's controls, which show the dialogs.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="part">The part just made.</param>
    /// <returns>What ends the answering, or <see langword="null"/> when the part is not a tool with questions.</returns>
    private static IDisposable? FollowTool(ViewerChrome chrome, DocumentTabViewModel tab, object part) => part switch
    {
        TabCopyActionSet copies => FollowCopies(chrome, tab, copies),
        PageManagementViewModel pages => FollowPages(chrome, tab, pages),
        AnnotationsViewModel marks => FollowMarks(chrome, tab, marks),
        TextRecognitionViewModel text => text.ManageLanguagesInteraction.RegisterHandler(context => chrome.ShowForm(OcrLanguageForms.For(context.Input)).Do(context.SetOutput)),
        FocusModeViewModel focus => focus.ShowSettingsInteraction.RegisterHandler(
            context => chrome.ShowForm(ToolForms.FocusSettings(context.Input)).Select(static _ => RxVoid.Default).Do(context.SetOutput)),
        PageDecorationViewModel decorations => decorations.ShowInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.PageDecoration(context.Input)).Do(context.SetOutput)),
        _ => null,
    };

    /// <summary>Answers the questions of a part that lists what the document holds, when the part is one.</summary>
    /// <param name="chrome">The window's controls, which show the dialogs.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="part">The part just made.</param>
    /// <returns>What ends the answering, or <see langword="null"/> when the part has no questions.</returns>
    private static IDisposable? FollowContent(ViewerChrome chrome, DocumentTabViewModel tab, object part) => part switch
    {
        TabFileLinks links => FollowLinks(chrome, links),
        PageContentEditViewModel edit => FollowEditing(chrome, edit),
        FillAndSignViewModel filling => filling.MarkInteraction.RegisterHandler(context =>
        {
            var browsing = context.Input.BrowseImageInteraction.RegisterHandler(where => chrome.PickFile(FolderOf(tab), null).Do(where.SetOutput));
            return chrome.ShowSignatureMark(context.Input).Do(context.SetOutput).Finally(browsing.Dispose);
        }),
        DocumentMediaViewModel media => FollowMedia(chrome, tab, media),
        SignaturesViewModel signatures => FollowSignatures(chrome, tab, signatures),
        TagsViewModel tags => tags.EditInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.TagEditor(context.Input)).AsSignal().Do(context.SetOutput)),
        OutlineEditViewModel bookmarks => bookmarks.PromptInteraction.RegisterHandler(context => chrome.Prompt(PromptOf(context.Input)).Do(context.SetOutput)),
        _ => null,
    };

    /// <summary>Answers the questions of the tab's links to other files: opening one, and whether to.</summary>
    /// <param name="chrome">The window's controls, which show the dialogs.</param>
    /// <param name="links">The links.</param>
    /// <returns>What ends the answering.</returns>
    private static MultipleDisposable FollowLinks(ViewerChrome chrome, TabFileLinks links) =>
    [
        links.LaunchFileInteraction.RegisterHandler(static context =>
        {
            _ = ShellLauncher.OpenFile(context.Input);
            context.SetOutput(RxVoid.Default);
        }),
        links.ConfirmOpenFileInteraction.RegisterHandler(context => chrome.Confirm(OpenFileRequest(context.Input)).Do(context.SetOutput)),
    ];

    /// <summary>Answers the questions of editing the page's own content: the program a picture is handed to, and the address of a link.</summary>
    /// <param name="chrome">The window's controls, which show the dialogs.</param>
    /// <param name="edit">The content editing.</param>
    /// <returns>What ends the answering.</returns>
    private static MultipleDisposable FollowEditing(ViewerChrome chrome, PageContentEditViewModel edit) =>
    [

        // A picture handed to the program the system uses for it; the picture comes back through the file when it is saved there.
        edit.ExternalEditInteraction.RegisterHandler(static context => context.SetOutput(ShellLauncher.OpenFile(context.Input))),
        edit.LinkPromptInteraction.RegisterHandler(context => chrome.Prompt(PromptOf(context.Input)).Do(context.SetOutput)),
    ];

    /// <summary>Answers the questions of the films and sounds: where to save one, and the player that plays one.</summary>
    /// <param name="chrome">The window's controls, which show the dialogs.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="media">The films and sounds.</param>
    /// <returns>What ends the answering.</returns>
    private static MultipleDisposable FollowMedia(ViewerChrome chrome, DocumentTabViewModel tab, DocumentMediaViewModel media) =>
    [
        media.SaveInteraction.RegisterHandler(context => chrome.PickFile(FolderOf(tab), Path.GetFileName(context.Input)).Do(context.SetOutput)),
        media.ShowPlayerInteraction.RegisterHandler(context => chrome.ShowMedia(context.Input).AsSignal().Do(context.SetOutput)),
    ];

    /// <summary>Answers the questions of the signatures: their window, and the window that signs with a certificate.</summary>
    /// <param name="chrome">The window's controls, which show the dialogs.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="signatures">The signatures.</param>
    /// <returns>What ends the answering.</returns>
    private static MultipleDisposable FollowSignatures(ViewerChrome chrome, DocumentTabViewModel tab, SignaturesViewModel signatures) =>
    [
        signatures.ShowInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.Signatures(context.Input)).AsSignal().Do(context.SetOutput)),
        signatures.CertificateSignInteraction.RegisterHandler(context =>
        {
            var browsing = context.Input.BrowseInteraction.RegisterHandler(where => chrome.PickFile(FolderOf(tab), null).Do(where.SetOutput));
            return chrome.ShowForm(ToolForms.CertificateSign(context.Input)).Do(context.SetOutput).Finally(browsing.Dispose);
        }),
    ];

    /// <summary>Answers the questions of the annotation tools: which picture to stamp, and their text and filter windows.</summary>
    /// <param name="chrome">The window's controls, which show the dialogs.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="marks">The annotation tools.</param>
    /// <returns>What ends the answering.</returns>
    private static MultipleDisposable FollowMarks(ViewerChrome chrome, DocumentTabViewModel tab, AnnotationsViewModel marks) =>
    [
        marks.ChoosePictureInteraction.RegisterHandler(context => chrome.PickFile(FolderOf(tab), null).Do(context.SetOutput)),
        marks.TextPropertiesInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.TextProperties(context.Input)).Do(context.SetOutput)),
        marks.ShowFilterInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.NoteFilter(context.Input)).Select(static _ => RxVoid.Default).Do(context.SetOutput)),
    ];

    /// <summary>Answers the questions of the page tools: which files to put in, where to write pages taken out, and their windows.</summary>
    /// <param name="chrome">The window's controls, which show the dialogs.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="pages">The page tools.</param>
    /// <returns>What ends the answering.</returns>
    private static MultipleDisposable FollowPages(ViewerChrome chrome, DocumentTabViewModel tab, PageManagementViewModel pages) =>
    [
        pages.InsertInteraction.RegisterHandler(context => chrome.PickFiles(FolderOf(tab)).Do(context.SetOutput)),
        pages.MergeFilesInteraction.RegisterHandler(context => chrome.PickFiles(FolderOf(tab)).Do(context.SetOutput)),
        pages.InsertImagesInteraction.RegisterHandler(context => chrome.PickFiles(FolderOf(tab)).Do(context.SetOutput)),
        pages.ExtractInteraction.RegisterHandler(context => chrome.PickFile(FolderOf(tab), Path.GetFileName(context.Input)).Do(context.SetOutput)),
        pages.Compare.ChooseFileInteraction.RegisterHandler(context => chrome.PickFiles(FolderOf(tab)).Select(static files => files.Length > 0 ? files[0] : null).Do(context.SetOutput)),
        pages.Compare.ShowInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.Compare(context.Input)).Select(static _ => RxVoid.Default).Do(context.SetOutput)),
        pages.Sanitize.ShowInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.Sanitize(context.Input)).Do(context.SetOutput)),
        pages.Labels.ShowInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.PageLabels(context.Input)).Do(context.SetOutput)),
    ];

    /// <summary>Answers the questions of the commands that write a changed copy. Each asks where to save through a file dialog that opens over its window.</summary>
    /// <param name="chrome">The window's controls, which show the dialogs.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="copies">The commands.</param>
    /// <returns>What ends the answering.</returns>
    private static MultipleDisposable FollowCopies(ViewerChrome chrome, DocumentTabViewModel tab, TabCopyActionSet copies) =>
    [
        Copy(chrome, tab, copies.SaveCopy, CopyForms.SaveCopy, static m => m.SaveInteraction),
        Copy(chrome, tab, copies.Optimize, CopyForms.Optimize, static m => m.SaveInteraction),
        Copy(chrome, tab, copies.Archive, CopyForms.Archive, static m => m.SaveInteraction),
        Copy(chrome, tab, copies.Pdf20, CopyForms.Pdf20, static m => m.SaveInteraction),
        Copy(chrome, tab, copies.Flatten, CopyForms.Flatten, static m => m.SaveInteraction),
        Copy(chrome, tab, copies.ExportPages, CopyForms.ExportPages, static m => m.SaveInteraction),
        Copy(chrome, tab, copies.Protect, CopyForms.Protect, static m => m.SaveInteraction),
        Copy(chrome, tab, copies.Unprotect, CopyForms.Unprotect, static m => m.SaveInteraction),
        Copy(chrome, tab, copies.Redact, CopyForms.Redact, static m => m.SaveInteraction),
        Copy(chrome, tab, copies.Validation, CopyForms.Validation, static m => m.SaveInteraction),
        copies.ExternalSign.Interaction.RegisterHandler(context => SignOutside(chrome, tab, context)),
        copies.Pdf20SaveInteraction.RegisterHandler(context => chrome.Confirm(context.Input).Do(context.SetOutput)),
    ];

    /// <summary>Shows the window of one kind of copy when its view model asks, and answers where the copy is saved while the window is open.</summary>
    /// <typeparam name="T">The view model of the copy.</typeparam>
    /// <param name="chrome">The window's controls, which show the dialogs.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="action">The copy, which asks for its window.</param>
    /// <param name="form">Makes the window from the view model.</param>
    /// <param name="save">Finds the question that asks where to save.</param>
    /// <returns>What ends the answering.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IDisposable Copy<T>(ViewerChrome chrome, DocumentTabViewModel tab, TabCopyAction<T> action, Func<T, FormRequest> form, Func<T, Interaction<string, string?>> save)
        where T : class, ICopyWindowRequest =>
        action.Interaction.RegisterHandler(context =>
        {
            var model = context.Input;
            var saving = save(model).RegisterHandler(where => chrome.PickFile(FolderOf(tab), Path.GetFileName(where.Input)).Do(where.SetOutput));
            return chrome.ShowForm(form(model)).AsSignal().Do(context.SetOutput).Finally(() =>
            {
                // Closing the window stops work that is still going on, and lets the save question go.
                Signal.Return(RxVoid.Default).InvokeCommand(model.CloseCommand).Dispose();
                saving.Dispose();
            });
        });

    /// <summary>Shows the window that signs with a key held somewhere else, and answers its questions about files while it is open.</summary>
    /// <param name="chrome">The window's controls, which show the dialogs.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="context">The request to show the window.</param>
    /// <returns>One value when the window closes.</returns>
    private static IObservable<RxVoid> SignOutside(ViewerChrome chrome, DocumentTabViewModel tab, IInteractionContext<ExternalSignViewModel, RxVoid> context)
    {
        var model = context.Input;
        MultipleDisposable files =
        [
            model.BrowseInteraction.RegisterHandler(where => chrome.PickFiles(FolderOf(tab)).Select(static paths => paths.Length > 0 ? paths : null).Do(where.SetOutput)),
            model.SaveToSignInteraction.RegisterHandler(where => chrome.PickFile(FolderOf(tab), Path.GetFileName(where.Input)).Do(where.SetOutput)),
            model.OpenSignatureInteraction.RegisterHandler(where => chrome.PickFile(FolderOf(tab), null).Do(where.SetOutput)),
            model.SaveCopyInteraction.RegisterHandler(where => chrome.PickFile(FolderOf(tab), Path.GetFileName(where.Input)).Do(where.SetOutput)),
        ];
        return chrome.ShowForm(ToolForms.ExternalSign(model)).AsSignal().Do(context.SetOutput).Finally(() =>
        {
            Signal.Return(RxVoid.Default).InvokeCommand(model.CloseCommand).Dispose();
            files.Dispose();
        });
    }

    /// <summary>Makes the question asked before a file a document points at is opened in another app.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The question.</returns>
    private static ConfirmRequest OpenFileRequest(string file) =>
        new(UiText.Get("Open File"), UiText.Format("Open {0} in another app? Only open files you trust.", file), UiText.Get("Open File"));

    /// <summary>Lists the documents a window's view model holds.</summary>
    /// <param name="window">The view model.</param>
    /// <returns>The paths of its tabs.</returns>
    private static List<string> PathsOf(MainViewModel window)
    {
        var paths = new List<string>(window.Tabs.Count);
        foreach (var tab in window.Tabs)
        {
            if (tab.FilePath.Length > 0)
            {
                paths.Add(tab.FilePath);
            }
        }

        return paths;
    }
}
