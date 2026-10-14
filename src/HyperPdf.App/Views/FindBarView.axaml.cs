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

/// <summary>The find bar: the text to find, the options, and moving between the results.</summary>
[DebuggerDisplay("FindBarView: {ViewModel}")]
public sealed partial class FindBarView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="FindBarView"/> class.</summary>
    public FindBarView()
    {
        InitializeComponent();
        var tabs = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            // Registered before the find bar's visibility binding, so the focus is checked before the bar hides.
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Search.IsOpen)
                .Where(static open => !open)
                .WhileFocusInside(FindBar)
                .InjectLatest(tabs)
                .SubscribeSafe(static tab => tab.FocusPages(), static error => Trace.TraceError(error.ToString())));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Search.IsOpen, static v => v.FindBar.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Search.Query, static v => v.SearchBox.Text, static query => query, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Search.MatchCase, static v => v.MatchCaseToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Search.WholeWord, static v => v.WholeWordToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Search.PreviousCommand, static v => v.PreviousResultButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Search.NextCommand, static v => v.NextResultButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Search.CloseCommand, static v => v.CloseFindButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Search.Status, static v => v.SearchStatusText.Text));
            disposables.Add(SearchBox.KeyDowns.Enter.AsVoid.InvokeCommand(this, static v => v.ViewModel!.Search.NextCommand));
            disposables.Add(SearchBox.KeyDowns.ShiftEnter.AsVoid.InvokeCommand(this, static v => v.ViewModel!.Search.PreviousCommand));
            disposables.Add(SearchBox.KeyDowns.Escape.AsVoid.InvokeCommand(this, static v => v.ViewModel!.Search.CloseCommand));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Search.IsOpen)
                .Where(static open => open)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .SubscribeSafe(_ => SearchBox.Focus(), static error => Trace.TraceError(error.ToString())));
        });
    }
}
