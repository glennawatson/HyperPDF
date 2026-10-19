// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls.Templates;
using Avalonia.Platform.Storage;
using HyperPdf.App.Controls;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.App.Views;

/// <summary>The Search in Folder window: every PDF in a folder searched, with the results listed as they arrive.</summary>
[DebuggerDisplay("FolderSearchWindow: {Title}")]
public sealed partial class FolderSearchWindow : ReactiveUI.Avalonia.ReactiveWindow<FolderSearchViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="FolderSearchWindow"/> class.</summary>
    public FolderSearchWindow()
    {
        InitializeComponent();
        ResultList.ItemTemplate = new FuncDataTemplate<FolderSearchResultViewModel>(static (result, _) => FolderSearchResultView.Create(result));
        FieldLabels.Link((FolderBox, FolderLabel), (QueryBox, QueryLabel));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(ItemAutomation.NameItems(ResultList));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Folder, static v => v.FolderBox.Text));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Query, static v => v.QueryBox.Text));
            disposables.Add(this.Bind(ViewModel, static vm => vm.MatchCase, static v => v.MatchCaseBox.IsChecked, static on => on, static value => value == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.WholeWord, static v => v.WholeWordBox.IsChecked, static on => on, static value => value == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IgnoreDiacritics, static v => v.IgnoreAccentsBox.IsChecked, static on => on, static value => value == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.FoldCompatibility, static v => v.SimilarCharactersBox.IsChecked, static on => on, static value => value == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IncludeSubfolders, static v => v.SubfoldersBox.IsChecked, static on => on, static value => value == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Status, static v => v.StatusText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Results, static v => v.ResultList.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsSearching, static v => v.StopButton.IsEnabled));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ChooseFolderCommand, static v => v.ChooseFolderButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SearchCommand, static v => v.SearchButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.StopCommand, static v => v.StopButton));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ChooseFolderInteraction, ChooseFolderAsync));

            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CloseCommand, static v => v.CloseButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CloseCommand)
                .SwitchMap(static closed => closed)
                .SubscribeSafe(_ => Close(), ErrorTrace.Write));
            disposables.Add(ResultList.ItemOpens<FolderSearchResultViewModel>().InvokeCommand(ViewModel, static vm => vm.OpenCommand));

            // The window deactivates when it closes by any route (title bar, Alt+F4), which ends a search still running.
            disposables.Add(Scope.Create(this, static window => window.ViewModel?.Stop()));
            _ = QueryBox.Focus();
        });
    }

    /// <summary>Asks for the folder to search.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ChooseFolderAsync(IInteractionContext<RxVoid, string?> context)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new() { Title = "Search in Folder", AllowMultiple = false });
        context.SetOutput(folders.Count > 0 ? folders[0].TryGetLocalPath() : null);
    }
}
