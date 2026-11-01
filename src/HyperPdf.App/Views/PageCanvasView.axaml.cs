// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using HyperPdf.App.Controls;
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
        PageObjectEditing.Pages = Canvas;
        AnnotationBubble.Pages = Canvas;
        Marks.Pages = Canvas;
        var tabs = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(tabs.BindTo(this, static v => v.Canvas.Tab));
            disposables.Add(tabs.BindTo(this, static v => v.Marks.Tab));

            // Any click on the page area stops auto-scroll, and the pages keep the keyboard.
            disposables.Add(PageSurface.PointerPresses
                .Inject(tabs)
                .Where(static press => press.State.PageTools.IsAutoScrolling)
                .Do(static press =>
                {
                    press.State.PageTools.IsAutoScrolling = false;
                    press.Value.Handled = true;
                })
                .SelectWith(Canvas, static (pages, _) => pages)
                .TakeFocus());

            // The scroll bars handle their own presses, so their scrolls stop auto-scroll too.
            disposables.Add(Scroller.TemplateBarScrolls
                .InjectLatest(tabs)
                .Where(static tab => tab.PageTools.IsAutoScrolling)
                .SubscribeSafe(static tab => tab.PageTools.IsAutoScrolling = false, ErrorTrace.Write));
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
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.OpenLinkInteraction).HandleWith(OpenLinkAsync));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Links.LaunchFileInteraction).HandleWith(LaunchFileAsync));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Links.ConfirmOpenFileInteraction).HandleWith(ConfirmOpenFileAsync));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Forms.ConfirmImportInteraction).HandleWith(ConfirmImportAsync));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Forms.ChooseImportFileInteraction).HandleWith(ChooseImportFileAsync));
        });
    }

    /// <summary>Gets the main content to focus: the way back to the pages when Focus Mode is on, otherwise the pages.</summary>
    internal InputElement MainContent => ViewModel?.FocusMode.IsOn == true ? FocusPane.Toolbar.BackToPagesButton : Canvas;

    /// <summary>Opens a file the reader agreed to open with the desktop's default app.</summary>
    /// <param name="context">The file's full path in.</param>
    /// <returns>A task that completes once the desktop has the file.</returns>
    private async Task LaunchFileAsync(IInteractionContext<string, RxVoid> context)
    {
        if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = await launcher.LaunchFileInfoAsync(new(context.Input));
        }

        context.SetOutput(RxVoid.Default);
    }

    /// <summary>Opens a web or mail link with the desktop's default handler; other kinds of link are not opened.</summary>
    /// <param name="context">The link in.</param>
    /// <returns>A task that completes once the desktop has the link.</returns>
    private async Task OpenLinkAsync(IInteractionContext<Uri, RxVoid> context)
    {
        if (context.Input.Scheme is "http" or "https" or "mailto" && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = await launcher.LaunchUriAsync(context.Input);
        }

        context.SetOutput(RxVoid.Default);
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

        ConfirmRequest request = new(
            LocalizedMessage.Get(UiText, "Open File"),
            LocalizedMessage.Format(UiText, "Open {0} in another app? Only open files you trust.", context.Input),
            LocalizedMessage.Get(UiText, "Open File"));
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
            LocalizedMessage.Get(UiText, "Load Answers"),
            LocalizedMessage.Format(UiText, "This form asks to load answers from {0}. You choose the file next, and Undo takes the change back. Only load files you trust.", context.Input),
            LocalizedMessage.Get(UiText, "Choose File"));
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
            Title = UiText.Format("Choose the Answers to Load ({0})", context.Input),
            AllowMultiple = false,
            FileTypeFilter = [new(UiText.Get("Form data (.fdf, .xfdf)")) { Patterns = ["*.fdf", "*.xfdf"] }],
        });
        context.SetOutput(files.Count > 0 ? files[0].TryGetLocalPath() : null);
    }
}
