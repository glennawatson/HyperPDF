// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The Fill and Sign tools: adding text, making, placing and adjusting a signature or initials, and signing with a certificate.</summary>
[DebuggerDisplay("FillSignBarView: {ViewModel}")]
public sealed partial class FillSignBarView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="FillSignBarView"/> class.</summary>
    public FillSignBarView()
    {
        InitializeComponent();

        // Arrow keys move the mark being placed, so the pages take the keyboard as soon as placing starts, after the
        // signature window has closed and handed focus back.
        KeyboardToPages = this.WhenChanged(static v => v.ViewModel!.FillAndSign.Placement)
            .Select(static placement => placement is not null)
            .DistinctUntilChanged()
            .Where(static placing => placing)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .AsVoid;
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FillAndSign.IsActive, static v => v.FillSignBar.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsTextTool, static v => v.AddTextTool.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.DoneCommand, static v => v.FillSignDoneButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.SignatureCommand, static v => v.SignatureButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.InitialsCommand, static v => v.InitialsButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.PlaceCommand, static v => v.PlaceMarkButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.BiggerCommand, static v => v.BiggerMarkButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.SmallerCommand, static v => v.SmallerMarkButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.CancelPlacementCommand, static v => v.CancelPlacementButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.AdjustCommand, static v => v.AdjustMarkButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.RemoveMarkCommand, static v => v.RemoveMarkButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FillAndSign.Placement, static v => v.PlacingTools.IsVisible, static placement => placement is not null));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FillAndSign.HasPlacedMark, static v => v.PlacedTools.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FillAndSign.Hint, static v => v.FillSignHint.Text));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Signatures.SignWithCertificateCommand, static v => v.CertificateSignButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.FillAndSign.MarkInteraction).AskDialogOver(this, static mark => new SignatureMarkWindow { ViewModel = mark }));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Signatures.CertificateSignInteraction).AskDialogOver(this, static request => new CertificateSignWindow { ViewModel = request }));
        });
    }

    /// <summary>Gets the moments the keyboard should go to the pages: placing a signature or initials starts.</summary>
    internal IObservable<RxVoid> KeyboardToPages { get; }
}
