// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.App.Controls;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The Shape button of the Annotate tools: a menu of shapes, and of marking an area or text to redact.</summary>
[DebuggerDisplay("AnnotateShapeButtonView: {ViewModel}")]
public sealed partial class AnnotateShapeButtonView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="AnnotateShapeButtonView"/> class.</summary>
    public AnnotateShapeButtonView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.ShapeLabel, static v => v.ShapeText.Text));
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
        });
    }
}
