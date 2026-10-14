// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Views;

/// <summary>A document tab: its tool bars, sidebar and pages, each of which is a view of its own.</summary>
[DebuggerDisplay("DocumentView: {ViewModel}")]
public sealed partial class DocumentView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="DocumentView"/> class.</summary>
    public DocumentView()
    {
        InitializeComponent();
        var tabs = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ShowsChrome, static v => v.Chrome.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsReading, static v => v.ReadModeBar.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.ReadModeTitle.Text));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ReadModeCommand, static v => v.LeaveReadModeButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SidebarVisible, static v => v.SidebarSplitter.IsVisible));

            // Escape cancels placing a signature wherever the keyboard is, for example on the Place button.
            disposables.Add(this.KeyDowns.Escape.AsVoid.InvokeCommand(this, static v => v.ViewModel!.FillAndSign.CancelPlacementCommand));

            // The pages take the keyboard once the view has opened.
            disposables.Add(Signal.Return(RxVoid.Default)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .InjectLatest(tabs)
                .SubscribeSafe(static tab => tab.FocusPages(), static error => Trace.TraceError(error.ToString())));
        });
    }

    /// <summary>Focuses the page box.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FocusPageBox() => Navigation.FocusPageBox();

    /// <summary>Copies the selected text.</summary>
    /// <returns>The selected text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetSelectedText() => PageArea.GetSelectedText();

    /// <summary>Focuses the main content: the Focus Mode text when it is on, otherwise the pages.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void FocusMain() => PageArea.FocusMain();
}
