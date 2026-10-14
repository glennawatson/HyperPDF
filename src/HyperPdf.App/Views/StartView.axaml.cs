// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls.Templates;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Platform;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The start page: open a document, or pick a recent one.</summary>
[DebuggerDisplay("StartView")]
public sealed partial class StartView : ReactiveUI.Avalonia.ReactiveUserControl<MainViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="StartView"/> class.</summary>
    public StartView()
    {
        InitializeComponent();
        RecentList.ItemTemplate = new FuncDataTemplate<RecentDocument>((_, _) => new RecentDocumentView { RemoveRequested = recent => ViewModel?.RemoveRecent(recent) });
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(ItemAutomation.NameItems(RecentList));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.OpenCommand, static v => v.OpenButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ClearRecentCommand, static v => v.ClearRecentButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RecentDocuments, static v => v.RecentList.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RecentDocuments.Count, static v => v.RecentPanel.IsVisible, static count => count > 0));

            // A click or Enter opens the document; the remove button on a row does not.
            disposables.Add(RecentList.ItemPicks<RecentDocument>().InvokeCommand(ViewModel, static vm => vm.OpenRecentCommand));
        });
    }
}
