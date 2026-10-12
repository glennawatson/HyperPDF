// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HyperPdf.App.ViewModels;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.App.Views;

/// <summary>
/// The Annotate tool bar's editing controls (redo, the extra shapes and stamps, colours, line width and text size) and
/// the comment list's filters and sort order.
/// </summary>
public sealed partial class DocumentView
{
    /// <summary>Fills the comment list's fixed choices and links their labels.</summary>
    private void SetUpAnnotationList()
    {
        AnnotationTypeBox.ItemsSource = AnnotationsViewModel.TypeFilterOptions;
        AnnotationColourBox.ItemsSource = AnnotationsViewModel.ColorFilterOptions;
        AnnotationSortBox.ItemsSource = AnnotationsViewModel.SortOptions;
        FieldLabels.Link(
            (AnnotationTypeBox, AnnotationTypeLabel),
            (AnnotationColourBox, AnnotationColourLabel),
            (AnnotationAuthorBox, AnnotationAuthorLabel),
            (AnnotationSortBox, AnnotationSortLabel));
    }

    /// <summary>Binds the tool bar's editing controls and the comment list's filters.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindAnnotationEditing(MultipleDisposable bindings)
    {
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.RedoCommand, static v => v.RedoButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.CanRedo, static v => v.RedoButton.IsEnabled));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.CalloutItem, Parameter(AnnotationTool.Callout)));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.PolygonItem, Parameter(AnnotationTool.Polygon)));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.CloudItem, Parameter(AnnotationTool.Cloud)));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.PolyLineItem, Parameter(AnnotationTool.PolyLine)));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.RedactAreaItem, Parameter(AnnotationTool.Redact)));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.RedactTextItem, Parameter(AnnotationTool.RedactText)));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.CanRedact, static v => v.RedactAreaItem.IsEnabled));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.CanRedact, static v => v.RedactTextItem.IsEnabled));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.CustomStampCommand, static v => v.CustomStampItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.PictureStampCommand, static v => v.PictureStampItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.PurpleItem, Parameter("Purple")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.OrangeItem, Parameter("Orange")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.GreyItem, Parameter("Grey")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.DarkBlueItem, Parameter("Dark blue")));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.LineWidthName, static v => v.LineWidthText.Text));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetLineWidthCommand, static v => v.ThinLineItem, Parameter("Thin")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetLineWidthCommand, static v => v.MediumLineItem, Parameter("Medium")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetLineWidthCommand, static v => v.ThickLineItem, Parameter("Thick")));
        bindings.Add(HandleInteraction(this.WhenChanged(static v => v.ViewModel!.Annotations.ChoosePictureInteraction), ChoosePictureAsync));
        BindAnnotationFilters(bindings);
    }

    /// <summary>Binds the comment list's text filter, filters and sort order.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindAnnotationFilters(MultipleDisposable bindings)
    {
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.NoMatches, static v => v.NoMatchesText.IsVisible));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.FilterText, static v => v.AnnotationFilterBox.Text));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.ShowFilters, static v => v.AnnotationFiltersToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.ShowFilters, static v => v.AnnotationFiltersPanel.IsVisible));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.FilterType, static v => v.AnnotationTypeBox.SelectedIndex));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.FilterColor, static v => v.AnnotationColourBox.SelectedIndex));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.SortOrder, static v => v.AnnotationSortBox.SelectedIndex));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.AuthorOptions, static v => v.AnnotationAuthorBox.ItemsSource));
        bindings.Add(this.Bind(
            ViewModel,
            static vm => vm.Annotations.FilterAuthor,
            static v => v.AnnotationAuthorBox.SelectedItem,
            static author => author,
            static item => item as string ?? AnnotationsViewModel.Everyone));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.ClearFilterCommand, static v => v.ClearAnnotationFilterButton));
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
