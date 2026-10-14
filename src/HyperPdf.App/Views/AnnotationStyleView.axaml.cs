// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The colour and line width of the annotations being made, each shown by name as well as by look.</summary>
[DebuggerDisplay("AnnotationStyleView: {ViewModel}")]
public sealed partial class AnnotationStyleView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="AnnotationStyleView"/> class.</summary>
    public AnnotationStyleView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.ColorName, static v => v.ColourText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.Color, static v => v.ColourSwatch.Background, ColourBrushes.Opaque));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.YellowItem, "Yellow".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.GreenItem, "Green".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.BlueItem, "Blue".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.RedItem, "Red".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.PurpleItem, "Purple".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.OrangeItem, "Orange".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.GreyItem, "Grey".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.DarkBlueItem, "Dark blue".AsCommandParameter));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.LineWidthName, static v => v.LineWidthText.Text));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetLineWidthCommand, static v => v.ThinLineItem, "Thin".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetLineWidthCommand, static v => v.MediumLineItem, "Medium".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetLineWidthCommand, static v => v.ThickLineItem, "Thick".AsCommandParameter));
        });
    }
}
