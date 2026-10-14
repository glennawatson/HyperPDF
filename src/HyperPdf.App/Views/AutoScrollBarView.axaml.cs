// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The bar that shows while the pages scroll by themselves, with the controls to slow, speed up or stop them.</summary>
[DebuggerDisplay("AutoScrollBarView: {ViewModel}")]
public sealed partial class AutoScrollBarView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="AutoScrollBarView"/> class.</summary>
    public AutoScrollBarView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PageTools.IsAutoScrolling, static v => v.AutoScrollBar.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PageTools.AutoScrollText, static v => v.AutoScrollText.Text));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.PageTools.SlowerAutoScrollCommand, static v => v.AutoScrollSlowerButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.PageTools.FasterAutoScrollCommand, static v => v.AutoScrollFasterButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.PageTools.StopAutoScrollCommand, static v => v.AutoScrollStopButton));
        });
    }
}
