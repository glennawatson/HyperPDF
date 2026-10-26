// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HyperPdf.App.Controls;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The Stamp button of the Annotate tools: a menu of stamps, your own words, or a picture.</summary>
[DebuggerDisplay("AnnotateStampButtonView: {ViewModel}")]
public sealed partial class AnnotateStampButtonView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="AnnotateStampButtonView"/> class.</summary>
    public AnnotateStampButtonView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.StampButtonLabel, static v => v.StampText.Text));
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
            Title = UiText.Get("Choose a Picture for the Stamp"),
            AllowMultiple = false,
            FileTypeFilter = [new(UiText.Get("Pictures (.png, .jpg, .bmp, .webp)")) { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"] }],
        });
        context.SetOutput(files.Count > 0 ? files[0].TryGetLocalPath() : null);
    }
}
