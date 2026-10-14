// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input.Platform;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>
/// Shows this application's licence first, then every component it includes or downloads grouped by licence, with a
/// search box, the full licence text and a button that copies it.
/// </summary>
[DebuggerDisplay("LicencesWindow: {Title}")]
public sealed partial class LicencesWindow : ReactiveUI.Avalonia.ReactiveWindow<LicencesViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="LicencesWindow"/> class.</summary>
    public LicencesWindow()
    {
        InitializeComponent();
        LicenceTree.ItemTemplate = new FuncTreeDataTemplate<LicenceNode>(static (node, _) => new TextBlock { Text = node?.Title }, static node => node.Children);
        FieldLabels.Link((SearchBox, SearchLabel));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.Bind(ViewModel, static vm => vm.Search, static v => v.SearchBox.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Nodes, static v => v.LicenceTree.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedNode, static v => v.LicenceTree.SelectedItem, static node => node, static item => item as LicenceNode));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Summary, static v => v.SummaryText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.DetailTitle, static v => v.DetailTitleText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Details, static v => v.DetailsText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.LicenceText, static v => v.LicenceTextBox.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Status, static v => v.StatusText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SelectedNode, static v => v.CopyButton.IsEnabled, static node => node?.Entry is not null));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CopyCommand, static v => v.CopyButton));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.CopyInteraction, CopyAsync));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CloseCommand, static v => v.CloseButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CloseCommand)
                .SwitchMap(static closed => closed)
                .SubscribeSafe(_ => Close(), ErrorTrace.Write));
            _ = SearchBox.Focus();
        });
    }

    /// <summary>Puts the licence text on the clipboard.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task CopyAsync(IInteractionContext<string, RxVoid> context)
    {
        if (Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(context.Input).ConfigureAwait(true);
        }

        context.SetOutput(RxVoid.Default);
    }
}
