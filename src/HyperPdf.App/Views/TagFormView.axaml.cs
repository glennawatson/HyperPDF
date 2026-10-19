// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The fields of the chosen tag: its type, table and list attributes, placement and texts, and the button that saves them.</summary>
[DebuggerDisplay("TagFormView: {ViewModel}")]
public sealed partial class TagFormView : ReactiveUI.Avalonia.ReactiveUserControl<TagsViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="TagFormView"/> class.</summary>
    public TagFormView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.TypeChoices, static v => v.TypeBox.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedType, static v => v.TypeBox.SelectedItem, static type => type, static item => item as string));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ScopeChoices, static v => v.ScopeBox.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedScope, static v => v.ScopeBox.SelectedItem, static scope => scope, static item => item as string));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsHeaderCell, static v => v.ScopeBox.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsHeaderCell, static v => v.ScopeLabel.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.RowSpanText, static v => v.RowSpanBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.ColumnSpanText, static v => v.ColumnSpanBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsCell, static v => v.RowSpanBox.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsCell, static v => v.RowSpanLabel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsCell, static v => v.ColumnSpanBox.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsCell, static v => v.ColumnSpanLabel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ListNumberingChoices, static v => v.NumberingBox.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedListNumbering, static v => v.NumberingBox.SelectedItem, static numbering => numbering, static item => item as string));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsList, static v => v.NumberingBox.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsList, static v => v.NumberingLabel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PlacementChoices, static v => v.PlacementBox.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedPlacement, static v => v.PlacementBox.SelectedItem, static placement => placement, static item => item as string));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.CanChangePlacement, static v => v.PlacementBox.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.CanChangePlacement, static v => v.PlacementLabel.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.AlternateText, static v => v.AlternateBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.ActualText, static v => v.ActualBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Language, static v => v.LanguageBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Title, static v => v.TitleBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ApplyCommand, static v => v.ApplyButton));
        });
    }
}
