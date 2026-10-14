// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using HyperPdf.App.Controls;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Views;

/// <summary>The outline of a document: its bookmarks as a tree, and the controls that edit them with the pointer or the keyboard.</summary>
[DebuggerDisplay("OutlinePanelView: {ViewModel}")]
public sealed partial class OutlinePanelView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="OutlinePanelView"/> class.</summary>
    public OutlinePanelView()
    {
        InitializeComponent();
        OutlineTree.ItemTemplate = new FuncTreeDataTemplate<OutlineItemViewModel>(static (_, _) => new OutlineItemView(), static item => item.Children);
        const KeyModifiers Arrange = KeyModifiers.Control | KeyModifiers.Shift;
        var keys = OutlineTree.TunnelKeyDowns;
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsOutlineMode, static v => v.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Outline, static v => v.OutlineTree.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedOutlineItem, static v => v.OutlineTree.SelectedItem, static item => item, static item => item as OutlineItemViewModel));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasOutline, static v => v.NoOutlineText.IsVisible, static has => !has));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.OutlineEditing, static v => v.OutlineTools.ViewModel));
            disposables.Add(keys.Chord(new(Key.F2, KeyModifiers.None)).MarkHandled().AsVoid.InvokeCommand(this, static v => v.ViewModel!.OutlineEditing.RenameCommand));
            disposables.Add(keys.Chord(new(Key.Delete, KeyModifiers.None)).MarkHandled().AsVoid.InvokeCommand(this, static v => v.ViewModel!.OutlineEditing.RemoveCommand));
            disposables.Add(keys.Chord(new(Key.Up, Arrange)).MarkHandled().AsVoid.InvokeCommand(this, static v => v.ViewModel!.OutlineEditing.MoveUpCommand));
            disposables.Add(keys.Chord(new(Key.Down, Arrange)).MarkHandled().AsVoid.InvokeCommand(this, static v => v.ViewModel!.OutlineEditing.MoveDownCommand));
            disposables.Add(keys.Chord(new(Key.Right, Arrange)).MarkHandled().AsVoid.InvokeCommand(this, static v => v.ViewModel!.OutlineEditing.IndentCommand));
            disposables.Add(keys.Chord(new(Key.Left, Arrange)).MarkHandled().AsVoid.InvokeCommand(this, static v => v.ViewModel!.OutlineEditing.OutdentCommand));

            // An edit replaces the rows, so a row that held the keyboard focus is gone; the focus goes back to the chosen bookmark.
            disposables.Add(this.WhenChanged(static v => v.ViewModel)
                .SwitchMap(static tab => tab is null ? Signal.Never<int>() : tab.OutlineEditing.WhenChanged(static editing => editing.EditVersion).Skip(1))
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .WhileFocusOutside(OutlineTools)
                .InjectLatest(this.WhenChanged(static v => v.ViewModel))
                .Select(static tab => tab.SelectedOutlineItem)
                .RowsIn(OutlineTree)
                .SubscribeSafe(static row => _ = row.Focus(), ErrorTrace.Write));
        });
    }
}
