// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The status line under the window: a message such as a download or its failure, until it is dismissed.</summary>
[DebuggerDisplay("StatusBarView: {ViewModel}")]
public sealed partial class StatusBarView : ReactiveUI.Avalonia.ReactiveUserControl<ViewModels.MainViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="StatusBarView"/> class.</summary>
    public StatusBarView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.StatusMessage, static v => v.StatusText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.StatusMessage, static v => v.StatusBar.IsVisible, static message => message is not null));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.DismissStatusCommand, static v => v.DismissStatusButton));
        });
    }
}
