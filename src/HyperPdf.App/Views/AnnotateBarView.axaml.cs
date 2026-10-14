// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The Annotate tools: highlighting, drawing, notes, text, shapes and stamps, with undo and redo.</summary>
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
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsNoteTool, static v => v.NoteTool.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsTextTool, static v => v.TextTool.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.ShapeLabel, static v => v.ShapeText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.StampButtonLabel, static v => v.StampText.Text));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.RectangleItem, AnnotationTool.Rectangle.AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.EllipseItem, AnnotationTool.Ellipse.AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.ArrowItem, AnnotationTool.Arrow.AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.LineItem, AnnotationTool.Line.AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.CalloutItem, AnnotationTool.Callout.AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.PolygonItem, AnnotationTool.Polygon.AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.CloudItem, AnnotationTool.Cloud.AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.PolyLineItem, AnnotationTool.PolyLine.AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.RedactAreaItem, AnnotationTool.Redact.AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.RedactTextItem, AnnotationTool.RedactText.AsCommandParameter));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Copies.Redact.CanRun, static v => v.RedactAreaItem.IsEnabled));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Copies.Redact.CanRun, static v => v.RedactTextItem.IsEnabled));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetStampCommand, static v => v.ApprovedItem, "APPROVED".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetStampCommand, static v => v.ReviewedItem, "REVIEWED".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetStampCommand, static v => v.DraftItem, "DRAFT".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetStampCommand, static v => v.ConfidentialItem, "CONFIDENTIAL".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetStampCommand, static v => v.FinalItem, "FINAL".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetStampCommand, static v => v.NotApprovedItem, "NOT APPROVED".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.CustomStampCommand, static v => v.CustomStampItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.PictureStampCommand, static v => v.PictureStampItem));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Annotations.ChoosePictureInteraction).HandleWith(ChoosePictureAsync));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Annotations.PromptInteraction).HandleWith(PromptAsync));
        });
    }

    /// <summary>Asks the user for text.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task PromptAsync(IInteractionContext<TextPrompt, string?> context)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            context.SetOutput(null);
            return;
        }

        context.SetOutput(await new PromptWindow { ViewModel = new(context.Input) }.ShowDialog<string?>(owner));
    }

    /// <summary>Asks for a picture to use as a stamp, through the desktop's open dialog.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ChoosePictureAsync(IInteractionContext<RxVoid, string?> context)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            context.SetOutput(null);
            return;
        }

        var files = await storage.OpenFilePickerAsync(new()
        {
            Title = "Choose a Picture for the Stamp",
            AllowMultiple = false,
            FileTypeFilter = [new("Pictures (.png, .jpg, .bmp, .webp)") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"] }],
        });
        context.SetOutput(files.Count > 0 ? files[0].TryGetLocalPath() : null);
    }
}
