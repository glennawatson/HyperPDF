// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Printing;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The print settings that name the job: where the print goes, which pages, and the printer's copies, colour and two-sided printing.</summary>
[DebuggerDisplay("PrintPrinterSettingsView: {ViewModel}")]
public sealed partial class PrintPrinterSettingsView : ReactiveUI.Avalonia.ReactiveUserControl<PrintPreviewViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="PrintPrinterSettingsView"/> class.</summary>
    public PrintPrinterSettingsView()
    {
        InitializeComponent();
        DestinationBox.ItemTemplate = new FuncDataTemplate<PrintTarget>(static (target, _) => new TextBlock { Text = target?.Label });
        FieldLabels.Link(
            (DestinationBox, DestinationLabel),
            (PagesBox, PagesLabel),
            (CustomPagesBox, PagesLabel),
            (CopiesBox, CopiesLabel),
            (ColourBox, ColourLabel),
            (BindingBox, BindingLabel));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(ItemAutomation.NameItems(DestinationBox));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Targets, static v => v.DestinationBox.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedTarget, static v => v.DestinationBox.SelectedItem, static target => target, static item => item as PrintTarget));
            disposables.Add(this.Bind(ViewModel, static vm => vm.PageChoice, static v => v.PagesBox.SelectedIndex, static c => (int)c, static i => (PrintPageChoice)Math.Max(0, i)));
            disposables.Add(this.Bind(ViewModel, static vm => vm.CustomPages, static v => v.CustomPagesBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PageChoice, static v => v.CustomPagesRow.IsVisible, static c => c == PrintPageChoice.Custom));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Copies, static v => v.CopiesBox.Value, static copies => (decimal?)copies, static value => (int)(value ?? 1)));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Colour, static v => v.ColourBox.SelectedIndex, static colour => colour ? 0 : 1, static index => index != 1));
            disposables.Add(this.Bind(ViewModel, static vm => vm.TwoSided, static v => v.TwoSidedBox.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Binding, static v => v.BindingBox.SelectedIndex, static b => (int)b, static i => (DuplexBinding)Math.Clamp(i, 0, 1)));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.TwoSided, static v => v.BindingBox.IsEnabled));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.CopiesRow.IsVisible, static d => d == PrintDestination.Printer));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.ColourRow.IsVisible, static d => d == PrintDestination.Printer));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.SidesRow.IsVisible, static d => d == PrintDestination.Printer));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.BindingRow.IsVisible, static d => d == PrintDestination.Printer));
        });
    }
}
