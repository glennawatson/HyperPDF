// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Markup;
using HyperPdf.ViewModels.Output;
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
        tab.Links.LaunchFileInteraction.RegisterHandler(static context =>
        {
            _ = ShellLauncher.OpenFile(context.Input);
            context.SetOutput(RxVoid.Default);
        }),
        tab.Links.ConfirmOpenFileInteraction.RegisterHandler(context => chrome.Confirm(OpenFileRequest(context.Input)).Do(context.SetOutput)),

        // Files to read.
        tab.CertificateBrowseInteraction.RegisterHandler(context => chrome.PickFile(FolderOf(tab), null).Do(context.SetOutput)),
        tab.Annotations.ChoosePictureInteraction.RegisterHandler(context => chrome.PickFile(FolderOf(tab), null).Do(context.SetOutput)),
        tab.Pages.InsertInteraction.RegisterHandler(context => chrome.PickFiles(FolderOf(tab)).Select(ToArray).Do(context.SetOutput)),
        tab.Pages.MergeFilesInteraction.RegisterHandler(context => chrome.PickFiles(FolderOf(tab)).Select(ToArray).Do(context.SetOutput)),
        tab.Pages.InsertImagesInteraction.RegisterHandler(context => chrome.PickFiles(FolderOf(tab)).Select(ToArray).Do(context.SetOutput)),

        // Files to write, each offered under the name the view model suggests.
        tab.Pages.ExtractInteraction.RegisterHandler(context => chrome.PickFile(FolderOf(tab), Path.GetFileName(context.Input)).Do(context.SetOutput)),
        tab.Attachments.SaveInteraction.RegisterHandler(context => chrome.PickFile(FolderOf(tab), Path.GetFileName(context.Input)).Do(context.SetOutput)),
        tab.Media.SaveInteraction.RegisterHandler(context => chrome.PickFile(FolderOf(tab), Path.GetFileName(context.Input)).Do(context.SetOutput)),
        tab.PageTools.SaveImageInteraction.RegisterHandler(context => chrome.PickFile(FolderOf(tab), Path.GetFileName(context.Input)).Do(context.SetOutput)),

        // A picture handed to the program the system uses for it; the picture comes back through the file when it is saved there.
        tab.ContentEdit.ExternalEditInteraction.RegisterHandler(static context => context.SetOutput(ShellLauncher.OpenFile(context.Input))),

        // The windows that write a changed copy. Each asks where to save through a file dialog that opens over it.
        Copy(chrome, tab, tab.Copies.SaveCopy, CopyForms.SaveCopy, static m => m.SaveInteraction),
        Copy(chrome, tab, tab.Copies.Optimize, CopyForms.Optimize, static m => m.SaveInteraction),
        Copy(chrome, tab, tab.Copies.Archive, CopyForms.Archive, static m => m.SaveInteraction),
        Copy(chrome, tab, tab.Copies.Pdf20, CopyForms.Pdf20, static m => m.SaveInteraction),
        Copy(chrome, tab, tab.Copies.Flatten, CopyForms.Flatten, static m => m.SaveInteraction),
        Copy(chrome, tab, tab.Copies.ExportPages, CopyForms.ExportPages, static m => m.SaveInteraction),
        Copy(chrome, tab, tab.Copies.Protect, CopyForms.Protect, static m => m.SaveInteraction),
        Copy(chrome, tab, tab.Copies.Unprotect, CopyForms.Unprotect, static m => m.SaveInteraction),
        Copy(chrome, tab, tab.Copies.Redact, CopyForms.Redact, static m => m.SaveInteraction),
        Copy(chrome, tab, tab.Copies.Validation, CopyForms.Validation, static m => m.SaveInteraction),

        // Signing, the mark of a hand signature and the look of typed text. Each window answers for itself when its own command is done.
        tab.Media.ShowPlayerInteraction.RegisterHandler(context => chrome.ShowMedia(context.Input).AsSignal().Do(context.SetOutput)),
        tab.Tags.EditInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.TagEditor(context.Input)).AsSignal().Do(context.SetOutput)),
        tab.Signatures.ShowInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.Signatures(context.Input)).AsSignal().Do(context.SetOutput)),
        tab.Signatures.CertificateSignInteraction.RegisterHandler(context =>
        {
            var browsing = context.Input.BrowseInteraction.RegisterHandler(where => chrome.PickFile(FolderOf(tab), null).Do(where.SetOutput));
            return chrome.ShowForm(ToolForms.CertificateSign(context.Input)).Do(context.SetOutput).Finally(browsing.Dispose);
        }),
        tab.FillAndSign.MarkInteraction.RegisterHandler(context =>
        {
            var browsing = context.Input.BrowseImageInteraction.RegisterHandler(where => chrome.PickFile(FolderOf(tab), null).Do(where.SetOutput));
            return chrome.ShowForm(ToolForms.SignatureMark(context.Input)).Do(context.SetOutput).Finally(browsing.Dispose);
        }),
        tab.Annotations.TextPropertiesInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.TextProperties(context.Input)).Do(context.SetOutput)),
        tab.Annotations.ShowFilterInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.NoteFilter(context.Input)).Select(static _ => RxVoid.Default).Do(context.SetOutput)),
        tab.FocusMode.ShowSettingsInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.FocusSettings(context.Input)).Select(static _ => RxVoid.Default).Do(context.SetOutput)),
        tab.Decorations.ShowInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.PageDecoration(context.Input)).Do(context.SetOutput)),
        tab.TextRecognition.ManageLanguagesInteraction.RegisterHandler(context => chrome.ShowForm(OcrLanguageForms.For(context.Input)).Do(context.SetOutput)),
        tab.Pages.Compare.ChooseFileInteraction.RegisterHandler(context => chrome.PickFiles(FolderOf(tab)).Select(static files => files.Count > 0 ? files[0] : null).Do(context.SetOutput)),
        tab.Pages.Compare.ShowInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.Compare(context.Input)).Select(static _ => RxVoid.Default).Do(context.SetOutput)),
        tab.Pages.Sanitize.ShowInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.Sanitize(context.Input)).Do(context.SetOutput)),
        tab.Pages.Labels.ShowInteraction.RegisterHandler(context => chrome.ShowForm(ToolForms.PageLabels(context.Input)).Do(context.SetOutput)),
        tab.Copies.ExternalSign.Interaction.RegisterHandler(context => SignOutside(chrome, tab, context)),

        // Questions.
        tab.Copies.Pdf20SaveInteraction.RegisterHandler(context => chrome.Confirm(context.Input).Do(context.SetOutput)),
        tab.OutlineEditing.PromptInteraction.RegisterHandler(context => chrome.Prompt(PromptOf(context.Input)).Do(context.SetOutput)),
        tab.ContentEdit.LinkPromptInteraction.RegisterHandler(context => chrome.Prompt(PromptOf(context.Input)).Do(context.SetOutput)),
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
            model.BrowseInteraction.RegisterHandler(where => chrome.PickFiles(FolderOf(tab)).Select(static paths => paths.Count > 0 ? ToArray(paths) : null).Do(where.SetOutput)),
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

    /// <summary>Copies chosen paths into an array, which is what the page tools take.</summary>
    /// <param name="paths">The paths.</param>
    /// <returns>The array.</returns>
    private static string[] ToArray(IReadOnlyList<string> paths) => [.. paths];

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
