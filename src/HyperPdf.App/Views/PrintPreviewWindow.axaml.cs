// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Printing;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The print window: a preview of every sheet and the print settings. Closes with <see langword="true"/> to print.</summary>
[DebuggerDisplay("PrintPreviewWindow: {Title}")]
public sealed partial class PrintPreviewWindow : ReactiveUI.Avalonia.ReactiveWindow<PrintPreviewViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="PrintPreviewWindow"/> class.</summary>
    public PrintPreviewWindow()
    {
        InitializeComponent();
        SheetList.ItemTemplate = new FuncDataTemplate<PrintPreviewPage>(static (_, _) => new PrintPreviewPageView());
        DestinationBox.ItemTemplate = new FuncDataTemplate<PrintTarget>(static (target, _) => new TextBlock { Text = target?.Label });
        foreach (var choice in SheetGrid.Choices)
        {
            _ = PagesPerSheetBox.Items.Add(choice.ToString(CultureInfo.CurrentCulture));
        }

        foreach (var choice in PrintPreviewViewModel.LayoutChoices)
        {
            _ = LayoutBox.Items.Add(choice);
        }

        LinkLabels();

        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(ItemAutomation.NameItems(SheetList));
            disposables.Add(ItemAutomation.NameItems(DestinationBox));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.Title, static name => $"Print {name}"));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Summary, static v => v.SummaryText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Sheets, static v => v.SheetList.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedSheetIndex, static v => v.SheetList.SelectedIndex));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Targets, static v => v.DestinationBox.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedTarget, static v => v.DestinationBox.SelectedItem, static target => target, static item => item as PrintTarget));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Copies, static v => v.CopiesBox.Value, static copies => (decimal?)copies, static value => (int)(value ?? 1)));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Colour, static v => v.ColourBox.SelectedIndex, static colour => colour ? 0 : 1, static index => index != 1));
            disposables.Add(this.Bind(ViewModel, static vm => vm.TwoSided, static v => v.TwoSidedBox.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Binding, static v => v.BindingBox.SelectedIndex, static b => (int)b, static i => (DuplexBinding)Math.Clamp(i, 0, 1)));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.TwoSided, static v => v.BindingBox.IsEnabled));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.CopiesRow.IsVisible, static d => d == PrintDestination.Printer));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.ColourRow.IsVisible, static d => d == PrintDestination.Printer));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.SidesRow.IsVisible, static d => d == PrintDestination.Printer));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.BindingRow.IsVisible, static d => d == PrintDestination.Printer));
            disposables.Add(this.Bind(ViewModel, static vm => vm.PageChoice, static v => v.PagesBox.SelectedIndex, static c => (int)c, static i => (PrintPageChoice)Math.Max(0, i)));
            disposables.Add(this.Bind(ViewModel, static vm => vm.CustomPages, static v => v.CustomPagesBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PageChoice, static v => v.CustomPagesRow.IsVisible, static c => c == PrintPageChoice.Custom));
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
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.PrinterHint.IsVisible, static d => d == PrintDestination.SystemDialog));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.PrintButton.Content, static destination => destination.ButtonText));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ConfirmCommand, static v => v.PrintButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SystemDialogCommand, static v => v.SystemDialogButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CancelCommand, static v => v.CancelButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Answered)
                .SwitchMap(static answered => answered)
                .SubscribeSafe(answer => Close(answer), OnError));

            // Ctrl+Shift+P opens the system print dialog.
            disposables.Add(this.KeyDowns.Chord(new(Key.P, KeyModifiers.Control | KeyModifiers.Shift)).MarkHandled().AsVoid
                .InvokeCommand(this, static v => v.ViewModel!.SystemDialogCommand));

            // Start on the first setting; the sheets are a preview, so the keyboard begins where choices are made.
            _ = DestinationBox.Focus();
        });
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Ties each setting to the label beside it for screen readers.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LinkLabels() =>
        FieldLabels.Link(
            (DestinationBox, DestinationLabel),
            (PagesBox, PagesLabel),
            (CustomPagesBox, PagesLabel),
            (CopiesBox, CopiesLabel),
            (ColourBox, ColourLabel),
            (BindingBox, BindingLabel),
            (LayoutBox, LayoutLabel),
            (PagesPerSheetBox, PagesPerSheetLabel),
            (PaperBox, PaperLabel),
            (ScalingBox, ScalingLabel),
            (ScalePercentBox, ScalePercentLabel));
}
