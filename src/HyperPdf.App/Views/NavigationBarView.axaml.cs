// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using HyperPdf.App.Controls;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Views;

/// <summary>The first tool bar row: the sidebar, Back and Forward, the page box, and zooming.</summary>
[DebuggerDisplay("NavigationBarView: {ViewModel}")]
public sealed partial class NavigationBarView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="NavigationBarView"/> class.</summary>
    public NavigationBarView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.Bind(ViewModel, static vm => vm.SidebarVisible, static v => v.SidebarToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.GoBackCommand, static v => v.BackButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.GoForwardCommand, static v => v.ForwardButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.PreviousPageCommand, static v => v.PreviousPageButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.NextPageCommand, static v => v.NextPageButton));
            disposables.Add(this.Bind(ViewModel, static vm => vm.PageEntry, static v => v.PageBox.Text, static entry => entry, static text => text ?? string.Empty));
            disposables.Add(PageBox.KeyDowns.Enter.AsVoid.InvokeCommand(this, static v => v.ViewModel!.GoToPageEntryCommand));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PageCount, static v => v.PageCountText.Text, static count => string.Create(CultureInfo.CurrentCulture, $"of {count}")));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ZoomOutCommand, static v => v.ZoomOutButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ZoomInCommand, static v => v.ZoomInButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ZoomText, static v => v.ZoomButton.Content));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.FitWidthCommand, static v => v.FitWidthItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.FitPageCommand, static v => v.FitPageItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom50Item, "50".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom75Item, "75".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom100Item, "100".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom125Item, "125".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom150Item, "150".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom200Item, "200".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom400Item, "400".AsCommandParameter));

            // Ctrl+L anywhere in the window puts the keyboard in the page box with its text selected, unless an earlier
            // step used the press.
            disposables.Add((TopLevel.GetTopLevel(this)?.KeyDowns ?? Signal.Never<KeyEventArgs>())
                .Pressing(Key.L)
                .Holding(KeyModifiers.Control)
                .Unhandled()
                .MarkHandled()
                .SelectWith(PageBox, static (box, _) => box)
                .FocusSelectingAll());
        });
    }
}
