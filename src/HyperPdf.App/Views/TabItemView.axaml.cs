// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using HyperPdf.App.Controls;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>One tab in the tab strip, with its hover preview.</summary>
[DebuggerDisplay("TabItemView: {ViewModel}")]
public sealed partial class TabItemView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="TabItemView"/> class.</summary>
    public TabItemView()
    {
        InitializeComponent();
        Preview.Width = DocumentTabViewModel.PreviewWidth;
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.NameText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasUnsavedChanges, static v => v.UnsavedDot.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Title, static v => v.PreviewTitle.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PreviewCaption, static v => v.PreviewCaption.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FilePath, static v => v.PreviewPath.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PreviewPageIndex, static v => v.PreviewPage.PageIndex));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PreviewHeight, static v => v.PreviewPage.Height));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PreviewHeight, static v => v.PreviewPage.IsVisible, static height => height > 0));
            disposables.Add(this.WhenChanged(static v => v.ViewModel).BindTo(this, static v => v.PreviewPage.Tab));

            // Closing a tab belongs to the window's view model, not the tab's, so the command comes from that source.
            if (TopLevel.GetTopLevel(this) is MainWindow { ViewModel: { } main })
            {
                disposables.Add(CloseButton.Clicks.MarkHandled().InjectLatest(this.WhenChanged(static v => v.ViewModel)).InvokeCommand(main, static window => window.CloseTabCommand));
            }

            // The unsaved dot is only seen, so the tab's spoken name says it too. The name is an attached property
            // on the tab strip's item, which a binding expression cannot reach.
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.FileName, static v => v.ViewModel!.HasUnsavedChanges, ItemDescriptions.DescribeTab)
                .SubscribeSafe(name => ItemAutomation.NameContainer(this, name), ErrorTrace.Write));
        });
    }
}
