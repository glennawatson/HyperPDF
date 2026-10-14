// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The measuring tools: the kind of measurement, the scale, and keeping or clearing the result.</summary>
[DebuggerDisplay("MeasureBarView: {ViewModel}")]
public sealed partial class MeasureBarView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="MeasureBarView"/> class.</summary>
    public MeasureBarView()
    {
        InitializeComponent();
        FieldLabels.Link((ScaleBox, ScaleLabel));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Measure.IsOn, static v => v.MeasureBar.IsVisible));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Measure.KeepCommand, static v => v.MeasureKeepButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Measure.CanKeep, static v => v.MeasureKeepButton.IsEnabled));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Measure.ClearCommand, static v => v.MeasureClearButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Measure.DoneCommand, static v => v.MeasureDoneButton));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Measure.IsDistance, static v => v.DistanceTool.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Measure.IsPerimeter, static v => v.PerimeterTool.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Measure.IsArea, static v => v.AreaTool.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Measure.ScaleText, static v => v.ScaleBox.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Measure.Result, static v => v.MeasureResult.Text));
        });
    }
}
