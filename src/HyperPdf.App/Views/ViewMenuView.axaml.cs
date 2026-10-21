// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.App.Controls;
using HyperPdf.Core.Layout;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The View menu: rotating, the page layout, reading and presenting, moving around, and what dragging on the pages does.</summary>
[DebuggerDisplay("ViewMenuView: {ViewModel}")]
public sealed partial class ViewMenuView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="ViewMenuView"/> class.</summary>
    public ViewMenuView()
    {
        InitializeComponent();
        SingleLayoutItem.CommandParameter = "Single";
        DualLayoutItem.CommandParameter = "Dual";
        CoverLayoutItem.CommandParameter = "DualCover";
        VerticalScrollItem.CommandParameter = "Vertical";
        HorizontalScrollItem.CommandParameter = "Horizontal";
        WrappedScrollItem.CommandParameter = "Wrapped";
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SetScrollModeCommand, static v => v.VerticalScrollItem, "Vertical".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SetScrollModeCommand, static v => v.HorizontalScrollItem, "Horizontal".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SetScrollModeCommand, static v => v.WrappedScrollItem, "Wrapped".AsCommandParameter));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ScrollMode, static v => v.VerticalScrollItem.IsChecked, static mode => mode == ScrollMode.Vertical));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ScrollMode, static v => v.HorizontalScrollItem.IsChecked, static mode => mode == ScrollMode.Horizontal));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ScrollMode, static v => v.WrappedScrollItem.IsChecked, static mode => mode == ScrollMode.Wrapped));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.RotateLeftCommand, static v => v.RotateLeftItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.RotateRightCommand, static v => v.RotateRightItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SetLayoutCommand, static v => v.SingleLayoutItem, "Single".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SetLayoutCommand, static v => v.DualLayoutItem, "Dual".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SetLayoutCommand, static v => v.CoverLayoutItem, "DualCover".AsCommandParameter));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.LayoutMode, static v => v.SingleLayoutItem.IsChecked, static mode => mode == PageLayoutMode.Single));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.LayoutMode, static v => v.DualLayoutItem.IsChecked, static mode => mode == PageLayoutMode.Dual));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.LayoutMode, static v => v.CoverLayoutItem.IsChecked, static mode => mode == PageLayoutMode.DualCover));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ReadModeCommand, static v => v.ReadModeItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.PresentCommand, static v => v.PresentItem));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsCaretMode, static v => v.CaretModeItem.IsChecked, static on => on, static on => on));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsPageByPage, static v => v.PageByPageItem.IsChecked, static on => on, static on => on));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsSplitView, static v => v.SplitViewItem.IsChecked));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SplitViewCommand, static v => v.SplitViewItem));
            disposables.Add(this.Bind(ViewModel, static vm => vm.PageTools.IsAutoScrolling, static v => v.AutoScrollItem.IsChecked, static on => on, static on => on));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.FirstPageCommand, static v => v.FirstPageItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.LastPageCommand, static v => v.LastPageItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.PageTools.SetPageToolCommand, static v => v.SelectTextToolItem, PageTool.SelectText.AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.PageTools.SetPageToolCommand, static v => v.HandToolItem, PageTool.Hand.AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.PageTools.SetPageToolCommand, static v => v.ZoomAreaToolItem, PageTool.ZoomArea.AsCommandParameter));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PageTools.IsSelectTextTool, static v => v.SelectTextToolItem.IsChecked));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PageTools.IsHandTool, static v => v.HandToolItem.IsChecked));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PageTools.IsZoomAreaTool, static v => v.ZoomAreaToolItem.IsChecked));
        });
    }
}
