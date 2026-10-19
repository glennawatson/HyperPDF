// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The buttons that change where the chosen tag sits in the reading order and in the tree, and remove it or undo the last edit.</summary>
[DebuggerDisplay("TagOrderView: {ViewModel}")]
public sealed partial class TagOrderView : ReactiveUI.Avalonia.ReactiveUserControl<TagsViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="TagOrderView"/> class.</summary>
    public TagOrderView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.MoveUpCommand, static v => v.MoveUpButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.MoveDownCommand, static v => v.MoveDownButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.NestCommand, static v => v.NestButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.UnnestCommand, static v => v.UnnestButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.RemoveCommand, static v => v.RemoveButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.UndoCommand, static v => v.UndoButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.MoveTargets, static v => v.MoveIntoBox.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.MoveTargetIndex, static v => v.MoveIntoBox.SelectedIndex));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.MoveIntoCommand, static v => v.MoveIntoButton));
        });
    }
}
