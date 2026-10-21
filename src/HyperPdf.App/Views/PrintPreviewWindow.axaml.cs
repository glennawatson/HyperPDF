// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using HyperPdf.App.Controls;
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
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(ItemAutomation.NameItems(SheetList));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.Title, static name => $"Print {name}"));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Summary, static v => v.SummaryText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Sheets, static v => v.SheetList.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedSheetIndex, static v => v.SheetList.SelectedIndex));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.PrinterHint.IsVisible, static d => d == PrintDestination.SystemDialog));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.PrintButton.Content, static destination => destination.ButtonText));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ConfirmCommand, static v => v.PrintButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SystemDialogCommand, static v => v.SystemDialogButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CancelCommand, static v => v.CancelButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Answered)
                .SwitchMap(static answered => answered)
                .SubscribeSafe(answer => Close(answer), ErrorTrace.Write));

            // Ctrl+Shift+P opens the system print dialog.
            disposables.Add(this.KeyDowns.Chord(new(Key.P, KeyModifiers.Control | KeyModifiers.Shift)).MarkHandled().AsVoid
                .InvokeCommand(this, static v => v.ViewModel!.SystemDialogCommand));

            // Start on the first setting; the sheets are a preview, so the keyboard begins where choices are made.
            _ = PrinterSettings.DestinationBox.Focus();
        });
    }
}
