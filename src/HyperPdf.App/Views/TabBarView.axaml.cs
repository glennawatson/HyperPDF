// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using HyperPdf.App.Controls;
using HyperPdf.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The tab strip with its Open button; the tab finder and the Tabs and settings menu are views of their own.</summary>
[DebuggerDisplay("TabBarView: {ViewModel}")]
public sealed partial class TabBarView : ReactiveUI.Avalonia.ReactiveUserControl<MainViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="TabBarView"/> class.</summary>
    public TabBarView()
    {
        InitializeComponent();
        TabStrip.ItemTemplate = new FuncDataTemplate<DocumentTabViewModel>(static (_, _) => new TabItemView());
        var viewModels = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(viewModels.BindTo(this, static v => v.TabFinder.ViewModel));
            disposables.Add(viewModels.BindTo(this, static v => v.TabsMenu.ViewModel));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Tabs, static v => v.TabStrip.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedTab, static v => v.TabStrip.SelectedItem, static tab => tab, static item => item as DocumentTabViewModel));
            disposables.Add(TabStrip.TabMiddleClicks.InvokeCommand(this, static v => v.ViewModel!.CloseTabCommand));
            disposables.Add(TabStrip.TabReorders
                .Inject(viewModels)
                .SubscribeSafe(static reorder => reorder.State.MoveTab(reorder.State.Tabs.IndexOf(reorder.Value.Drag.Tab), reorder.State.Tabs.IndexOf(reorder.Value.Target)), ErrorTrace.Write));
            disposables.Add(TabStrip.TabScrolls.SubscribeSafe(static scroll => scroll.Scroller.Offset = scroll.Offset, ErrorTrace.Write));

            // Hovering another tab replaces the preview being prepared, which stops.
            disposables.Add(TabStrip.TabPreviewOpenings
                .SwitchAsync(static async (tab, token) =>
                {
                    await tab.PreparePreviewAsync(token).ConfigureAwait(true);
                    return RxVoid.Default;
                })
                .SubscribeSafeErrors(ErrorTrace.Write));
            disposables.Add(TabStrip.ItemContextRequests<DocumentTabViewModel>()
                .Inject(viewModels)
                .SubscribeSafe(static request => ShowTabMenu(request.Value, request.State), ErrorTrace.Write));

            // A tab picked from the finder or opened anywhere else is brought into view in the strip.
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.SelectedTab)
                .KeepNotNull()
                .SubscribeSafe(TabStrip.ScrollIntoView, ErrorTrace.Write));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.OpenCommand, static v => v.OpenButton));
        });
    }

    /// <summary>Opens the menu of the tab under the pointer: close it, close the others, close all, reload.</summary>
    /// <param name="menu">The request for the tab's menu.</param>
    /// <param name="viewModel">The window's view model.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ShowTabMenu(ItemContextRequest<DocumentTabViewModel> menu, MainViewModel viewModel) =>
        new ContextMenu
        {
            ItemsSource = new Control[]
            {
                new MenuItem { Header = UiText.Get("_Close"), Command = viewModel.CloseTabCommand, CommandParameter = menu.Item, InputGesture = new(Key.W, KeyModifiers.Control) },
                new MenuItem { Header = UiText.Get("Close _Other Tabs"), Command = viewModel.CloseOtherTabsCommand, CommandParameter = menu.Item },
                new MenuItem { Header = UiText.Get("Close _All Tabs"), Command = viewModel.CloseAllTabsCommand },
                new Separator(),
                new MenuItem { Header = UiText.Get("_Save"), Command = menu.Item.SaveCommand, IsEnabled = menu.Item.HasUnsavedChanges },
                new MenuItem { Header = UiText.Get("Re_load"), Command = menu.Item.ReloadCommand },
            },
        }.Open(menu.Source);
}
