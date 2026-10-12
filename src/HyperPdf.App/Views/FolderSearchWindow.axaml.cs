// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

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
                .SubscribeSafe(_ => Close(), OnError));
            disposables.Add(Opens(ResultList).InvokeCommand(ViewModel, static vm => vm.OpenCommand));

            // The window deactivates when it closes by any route (title bar, Alt+F4), which ends a search still running.
            disposables.Add(Scope.Create(this, static window => window.ViewModel?.Stop()));
            _ = QueryBox.Focus();
        });
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Gets the results chosen with a double tap or Enter.</summary>
    /// <param name="results">The results list; Events() needs the typed parameter because it cannot see fields the XAML name generator creates.</param>
    /// <returns>
    /// The chosen results, read from the item the event came from. The declared type matters: the binding generator
    /// cannot see the types Events() generates, so InvokeCommand must start from this method's result.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<FolderSearchResultViewModel> Opens(ListBox results) =>
        Signal.Merge(
                results.Events().DoubleTapped.Select(static args => args.Source),
                results.Events().KeyDown.Where(static args => args.Key == Key.Enter).Select(static args =>
                {
                    args.Handled = true;
                    return args.Source;
                }))
            .Select(static source => (source as StyledElement)?.DataContext)
            .OfType<FolderSearchResultViewModel>();

    /// <summary>Asks for the folder to search.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ChooseFolderAsync(IInteractionContext<RxVoid, string?> context)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new() { Title = "Search in Folder", AllowMultiple = false });
        context.SetOutput(folders.Count > 0 ? folders[0].TryGetLocalPath() : null);
    }
}
