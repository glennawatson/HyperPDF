// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.App.Controls;
using HyperPdf.App.Services;
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

            // The pages take the keyboard once the view has opened, and whenever a bar or the sidebar hands it back.
            disposables.Add(Signal.Merge(Signal.Return(RxVoid.Default), FindPane.KeyboardToPages, SidebarPane.KeyboardToPages, FillSign.KeyboardToPages)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .InjectLatest(tabs)
                .SelectWith(PageArea, static (area, _) => area.Canvas)
                .TakeFocus());

            // The Document menu's Select All selects the text on the page shown, as Ctrl+A on the pages does.
            disposables.Add(Actions.SelectAllClicks
                .SelectWith(PageArea, static (area, _) => area.Canvas)
                .SubscribeSafe(static pages => pages.SelectAllText(), ErrorTrace.Write));
        });
    }
}
