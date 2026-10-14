// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Interactivity;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The second tool bar row: saving and printing, the Annotate, Fill and Sign, Find, Focus and Read Aloud tools, and the menus.</summary>
[DebuggerDisplay("ActionBarView: {ViewModel}")]
public sealed partial class ActionBarView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="ActionBarView"/> class.</summary>
    public ActionBarView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SaveCommand, static v => v.SaveButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasUnsavedChanges, static v => v.SaveButton.IsVisible));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Printing.PrintCommand, static v => v.PrintItem));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Printing.PrintPreviewInteraction).AskDialogOver(this, static preview => new PrintPreviewWindow { ViewModel = preview }));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsAnnotating, static v => v.AnnotateToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FillAndSign.IsActive, static v => v.FillSignToggle.IsChecked, static on => on));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.ToggleCommand, static v => v.FillSignToggle));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.CanAnnotate, static v => v.AnnotateToggle.IsEnabled));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.CanAnnotate, static v => v.FillSignToggle.IsEnabled));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Search.IsOpen, static v => v.FindToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.FocusMode.IsOn, static v => v.FocusToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.ReadAloud.IsOpen, static v => v.ReadAloudToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Measure.ToggleCommand, static v => v.MeasureItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.PageTools.ToggleSnapshotToolCommand, static v => v.SnapshotToolItem));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PageTools.IsSnapshotTool, static v => v.SnapshotToolItem.IsChecked));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.TextRecognition.RecognizeCommand, static v => v.RecognizeTextItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.TextRecognition.ChooseLanguageCommand, static v => v.RecognizeLanguageItem));
        });
    }

    /// <summary>Gets the clicks of the Document menu's Select All item.</summary>
    internal IObservable<RoutedEventArgs> SelectAllClicks => DocumentMenu.SelectAllClicks;
}
