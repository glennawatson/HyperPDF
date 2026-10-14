// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>
/// The window's dialogs: opening files, confirming, Preferences, About and Licences, Search in Folder, a new window and a
/// document's properties. Each opens over the window this view sits in.
/// </summary>
[DebuggerDisplay("WindowDialogsView: {ViewModel}")]
public sealed partial class WindowDialogsView : ReactiveUI.Avalonia.ReactiveUserControl<MainViewModel>
{
    /// <summary>The PDF file type filter.</summary>
    private static readonly FilePickerFileType PdfFileType = new("PDF documents") { Patterns = ["*.pdf", "*.PDF"], MimeTypes = ["application/pdf"] };

    /// <summary>The Search in Folder window, while it is open.</summary>
    private FolderSearchWindow? _folderSearch;

    /// <summary>Initializes a new instance of the <see cref="WindowDialogsView"/> class.</summary>
    public WindowDialogsView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.OpenFileInteraction, OpenFilesAsync));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ShowPropertiesInteraction, ShowPropertiesAsync));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ConfirmInteraction, ConfirmAsync));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ShowPreferencesInteraction, ShowPreferencesAsync));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ShowLicencesInteraction, ShowLicencesAsync));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ShowFolderSearchInteraction, ShowFolderSearchAsync));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.NewWindowInteraction, ShowNewWindowAsync));
        });
    }

    /// <summary>Gets the window the dialogs open over.</summary>
    private Window Owner => TopLevel.GetTopLevel(this) as Window ?? throw new InvalidOperationException("The dialogs need a window to open over.");

    /// <summary>Shows the open dialog; on KDE this goes through the XDG portal and shows the KDE file dialog.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task OpenFilesAsync(IInteractionContext<RxVoid, IReadOnlyList<string>> context)
    {
        var files = await Owner.StorageProvider.OpenFilePickerAsync(new() { Title = "Open Document", AllowMultiple = true, FileTypeFilter = [PdfFileType, FilePickerFileTypes.All] });
        context.SetOutput(files.LocalPaths);
    }

    /// <summary>Asks the user to confirm a destructive action.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ConfirmAsync(IInteractionContext<ConfirmRequest, bool> context)
    {
        var dialog = new ConfirmWindow { ViewModel = new(context.Input) };
        context.SetOutput(await dialog.ShowDialog<bool>(Owner));
    }

    /// <summary>Shows the preferences.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ShowPreferencesAsync(IInteractionContext<PreferencesViewModel, RxVoid> context)
    {
        await new PreferencesWindow { ViewModel = context.Input }.ShowDialog(Owner);
        context.SetOutput(RxVoid.Default);
    }

    /// <summary>Shows the About and Licences window.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ShowLicencesAsync(IInteractionContext<LicencesViewModel, RxVoid> context)
    {
        await new LicencesWindow { ViewModel = context.Input }.ShowDialog(Owner);
        context.SetOutput(RxVoid.Default);
    }

    /// <summary>Shows the Search in Folder window, or brings it forward when it is already open.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private Task ShowFolderSearchAsync(IInteractionContext<FolderSearchViewModel, RxVoid> context)
    {
        if (_folderSearch is { IsVisible: true } open)
        {
            open.Activate();
        }
        else
        {
            _folderSearch = new FolderSearchWindow { ViewModel = context.Input };
            _folderSearch.Show(Owner);
        }

        context.SetOutput(RxVoid.Default);
        return Task.CompletedTask;
    }

    /// <summary>Shows another window the size of this one, which releases its view model once closed.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private Task ShowNewWindowAsync(IInteractionContext<MainViewModel, RxVoid> context)
    {
        var owner = Owner;
        new MainWindow { DataContext = context.Input, Width = owner.Width, Height = owner.Height }.Show();
        context.SetOutput(RxVoid.Default);
        return Task.CompletedTask;
    }

    /// <summary>Shows the properties dialog.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ShowPropertiesAsync(IInteractionContext<DocumentTabViewModel, RxVoid> context)
    {
        var dialog = new PropertiesWindow { ViewModel = new(context.Input) };
        await dialog.ShowDialog(Owner);
        context.SetOutput(RxVoid.Default);
    }
}
