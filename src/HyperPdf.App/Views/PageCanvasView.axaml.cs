// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The pages of a document with the editors placed over them, the Focus Mode text, and what opens links and files.</summary>
[DebuggerDisplay("PageCanvasView: {ViewModel}")]
public sealed partial class PageCanvasView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="PageCanvasView"/> class.</summary>
    public PageCanvasView()
    {
        InitializeComponent();
        FieldEditing.Pages = Canvas;
        PageTextEditing.Pages = Canvas;
        var tabs = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(tabs.BindTo(this, static v => v.Canvas.Tab));
            disposables.Add(PageSurface.PointerPresses
                .Inject(tabs)
                .Where(static press => press.State.PageTools.IsAutoScrolling)
                .SubscribeSafe(StopAutoScrollOnPagePress, OnError));

            // The scroll bars handle their own presses, so their scrolls stop auto-scroll too.
            disposables.Add(Scroller.TemplateBarScrolls
                .InjectLatest(tabs)
                .Where(static tab => tab.PageTools.IsAutoScrolling)
                .SubscribeSafe(static tab => tab.PageTools.IsAutoScrolling = false, OnError));
            disposables.Add(this.OneWayBind(
                ViewModel,
                static vm => vm.IsPresenting,
                static v => v.Scroller.VerticalScrollBarVisibility,
                static presenting => presenting ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto));
            disposables.Add(this.OneWayBind(
                ViewModel,
                static vm => vm.IsPresenting,
                static v => v.Scroller.HorizontalScrollBarVisibility,
                static presenting => presenting ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FocusMode, static v => v.FocusPane.ViewModel));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FocusMode.IsOn, static v => v.FocusPane.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FocusMode.IsOn, static v => v.Scroller.IsVisible, static on => !on));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Forms.Focused).SubscribeSafe(_ => Canvas.InvalidateVisual(), OnError));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Annotations.RegionsVersion, static v => v.ViewModel!.Annotations.Tool, static (version, _) => version)
                .SubscribeSafe(_ => Canvas.InvalidateVisual(), OnError));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.PageFocusRequests)
                .SwitchMap(static requests => requests)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .SubscribeSafe(_ => Canvas.Focus(), OnError));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.UriRequests).SwitchTo().SubscribeSafe(OpenUri, OnError));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Links.FileLaunchRequests).SwitchMap(static requests => requests).SubscribeSafe(LaunchFile, OnError));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Links.ConfirmOpenFileInteraction).HandleWith(ConfirmOpenFileAsync));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Forms.ConfirmImportInteraction).HandleWith(ConfirmImportAsync));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Forms.ChooseImportFileInteraction).HandleWith(ChooseImportFileAsync));
        });
    }

    /// <summary>Gets the pages.</summary>
    internal PageCanvas Pages => Canvas;

    /// <summary>Copies the selected text.</summary>
    /// <returns>The selected text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetSelectedText() => Canvas.GetSelectedText();

    /// <summary>Focuses the main content: the Focus Mode text when it is on, otherwise the pages.</summary>
    internal void FocusMain()
    {
        if (ViewModel?.FocusMode.IsOn == true)
        {
            FocusPane.FocusFirstControl();
            return;
        }

        _ = Canvas.Focus();
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Stops auto-scroll when any part of the page area is clicked.</summary>
    /// <param name="press">The pointer press and the tab that was auto-scrolling.</param>
    private void StopAutoScrollOnPagePress(Injected<PointerPressedEventArgs, DocumentTabViewModel> press)
    {
        var (args, tab) = press;
        tab.PageTools.IsAutoScrolling = false;
        _ = Canvas.Focus();
        args.Handled = true;
    }

    /// <summary>Opens a file the reader agreed to open with the desktop's default app.</summary>
    /// <param name="path">The file.</param>
    private void LaunchFile(string path)
    {
        if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchFileInfoAsync(new(path));
        }
    }

    /// <summary>Opens external links with the desktop's default handler.</summary>
    /// <param name="uri">The URI.</param>
    private void OpenUri(Uri uri)
    {
        if (uri.Scheme is "http" or "https" or "mailto" && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(uri);
        }
    }

    /// <summary>Asks before a linked or attached file opens in another app.</summary>
    /// <param name="context">The file's name in; <see langword="true"/> out to open it.</param>
    /// <returns>A task.</returns>
    private async Task ConfirmOpenFileAsync(IInteractionContext<string, bool> context)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            context.SetOutput(false);
            return;
        }

        ConfirmRequest request = new("Open File", $"Open {context.Input} in another app? Only open files you trust.", "Open File");
        context.SetOutput(await new ConfirmWindow { ViewModel = new(request) }.ShowDialog<bool>(owner));
    }

    /// <summary>Asks before a form loads answers from a file, naming the file the form asked for.</summary>
    /// <param name="context">The file's name in; <see langword="true"/> out to go on.</param>
    /// <returns>A task.</returns>
    private async Task ConfirmImportAsync(IInteractionContext<string, bool> context)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            context.SetOutput(false);
            return;
        }

        ConfirmRequest request = new(
            "Load Answers",
            $"This form asks to load answers from {context.Input}. You choose the file next, and Undo takes the change back. Only load files you trust.",
            "Choose File");
        context.SetOutput(await new ConfirmWindow { ViewModel = new(request) }.ShowDialog<bool>(owner));
    }

    /// <summary>Lets the reader pick the FDF or XFDF file to load, through the desktop's open dialog.</summary>
    /// <param name="context">The file name the form asked for in; the chosen path out, or <see langword="null"/>.</param>
    /// <returns>A task.</returns>
    private async Task ChooseImportFileAsync(IInteractionContext<string, string?> context)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            context.SetOutput(null);
            return;
        }

        var files = await storage.OpenFilePickerAsync(new()
        {
            Title = $"Choose the Answers to Load ({context.Input})",
            AllowMultiple = false,
            FileTypeFilter = [new("Form data (.fdf, .xfdf)") { Patterns = ["*.fdf", "*.xfdf"] }],
        });
        context.SetOutput(files.Count > 0 ? files[0].TryGetLocalPath() : null);
    }
}
