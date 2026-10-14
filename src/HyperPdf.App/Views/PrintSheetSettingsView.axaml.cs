// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Printing;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The print settings that shape the sheets: how pages are laid out, the paper, the scale and the options.</summary>
[DebuggerDisplay("PrintSheetSettingsView: {ViewModel}")]
public sealed partial class PrintSheetSettingsView : ReactiveUI.Avalonia.ReactiveUserControl<PrintPreviewViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="PrintSheetSettingsView"/> class.</summary>
    public PrintSheetSettingsView()
    {
        InitializeComponent();
        foreach (var choice in SheetGrid.Choices)
        {
            _ = PagesPerSheetBox.Items.Add(choice.ToString(CultureInfo.CurrentCulture));
        }

        foreach (var choice in PrintPreviewViewModel.LayoutChoices)
        {
            _ = LayoutBox.Items.Add(choice);
        }

        FieldLabels.Link(
            (LayoutBox, LayoutLabel),
            (PagesPerSheetBox, PagesPerSheetLabel),
            (PaperBox, PaperLabel),
            (ScalingBox, ScalingLabel),
            (ScalePercentBox, ScalePercentLabel));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.Bind(ViewModel, static vm => vm.LayoutIndex, static v => v.LayoutBox.SelectedIndex, static i => i, static i => Math.Max(0, i)));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.LayoutIndex, static v => v.PagesPerSheetRow.IsVisible, static i => i == 0));
            disposables.Add(this.Bind(ViewModel, static vm => vm.PagesPerSheetIndex, static v => v.PagesPerSheetBox.SelectedIndex, static i => i, static i => Math.Max(0, i)));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ShowsPaper, static v => v.PaperRow.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Paper, static v => v.PaperBox.SelectedIndex, static p => (int)p, static i => (PaperSize)Math.Max(0, i)));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Scaling, static v => v.ScalingBox.SelectedIndex, static s => (int)s, static i => (PrintScaling)Math.Max(0, i)));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ShowsScaling, static v => v.ScalingRow.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.ScalePercent, static v => v.ScalePercentBox.Value, static p => (decimal?)p, static value => (int)(value ?? PrintScale.TrueSize)));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ShowsScalePercent, static v => v.ScalePercentRow.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IncludeAnnotations, static v => v.AnnotationsBox.IsChecked, static on => on, static on => on == true));
        });
    }
}
