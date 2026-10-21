// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls.Templates;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The document's parts as a tree of page ranges, and its article threads to read place by place.</summary>
[DebuggerDisplay("PartsPanelView: {ViewModel}")]
public sealed partial class PartsPanelView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="PartsPanelView"/> class.</summary>
    public PartsPanelView()
    {
        InitializeComponent();
        PartTree.ItemTemplate = new FuncTreeDataTemplate<PartItemViewModel>(static (_, _) => new PartItemView(), static item => item.Children);
        ThreadList.ItemTemplate = new FuncDataTemplate<ThreadItemViewModel>(static (_, _) => new ThreadItemView());
        _ = this.WhenActivated(disposables =>
        {
            // Items are named as their containers are prepared, so this comes before the lists are filled.
            disposables.Add(ItemAutomation.NameItems(PartTree));
            disposables.Add(ItemAutomation.NameItems(ThreadList));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsPartsMode, static v => v.PartsPanel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Parts.Parts, static v => v.PartTree.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Parts.HasParts, static v => v.PartSection.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Parts.SelectedPart, static v => v.PartTree.SelectedItem, static item => item, static item => item as PartItemViewModel));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Parts.Threads, static v => v.ThreadList.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Parts.HasThreads, static v => v.ThreadSection.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Parts.SelectedThread, static v => v.ThreadList.SelectedItem, static item => item, static item => item as ThreadItemViewModel));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Parts.PlaceText, static v => v.PlaceText.Text));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Parts.PreviousPlaceCommand, static v => v.PreviousPlaceButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Parts.NextPlaceCommand, static v => v.NextPlaceButton));
        });
    }
}
