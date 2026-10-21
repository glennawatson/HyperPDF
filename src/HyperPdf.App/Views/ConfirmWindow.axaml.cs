// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>Asks the user to confirm a destructive action; closes with <see langword="true"/> to go ahead.</summary>
[DebuggerDisplay("ConfirmWindow: {Title}")]
public sealed partial class ConfirmWindow : ReactiveUI.Avalonia.ReactiveWindow<ConfirmViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="ConfirmWindow"/> class.</summary>
    public ConfirmWindow()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Title, static v => v.Title));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Title, static v => v.TitleText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Message, static v => v.MessageText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ConfirmText, static v => v.ConfirmButton.Content));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ConfirmCommand, static v => v.ConfirmButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CancelCommand, static v => v.CancelButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Answered)
                .SwitchMap(static answered => answered)
                .SubscribeSafe(answer => Close(answer), ErrorTrace.Write));

            _ = CancelButton.Focus();
        });
    }
}
