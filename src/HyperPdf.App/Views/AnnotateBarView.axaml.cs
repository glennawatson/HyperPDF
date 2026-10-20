// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The Annotate tools: highlighting, drawing, notes and text, with undo and redo. Shapes and stamps are views of their own.</summary>
[DebuggerDisplay("AnnotateBarView: {ViewModel}")]
public sealed partial class AnnotateBarView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="AnnotateBarView"/> class.</summary>
    public AnnotateBarView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.IsAnnotating, static v => v.AnnotateBar.IsVisible));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.UndoCommand, static v => v.UndoButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.CanUndo, static v => v.UndoButton.IsEnabled));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.RedoCommand, static v => v.RedoButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.CanRedo, static v => v.RedoButton.IsEnabled));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.DoneCommand, static v => v.AnnotateDoneButton));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsSelectTool, static v => v.SelectTool.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsHighlightTool, static v => v.HighlightTool.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsUnderlineTool, static v => v.UnderlineTool.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsStrikeOutTool, static v => v.StrikeOutTool.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsDrawTool, static v => v.DrawTool.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsFreehandHighlightTool, static v => v.FreehandHighlightTool.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsCaretTool, static v => v.CaretTool.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsNoteTool, static v => v.NoteTool.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsTextTool, static v => v.TextTool.IsChecked, static on => on, static on => on == true));
        });
    }
}
