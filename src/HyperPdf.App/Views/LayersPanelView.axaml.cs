// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The layers of a document as a tree of ticks, with the configurations it defines and a way back to its own settings.</summary>
[DebuggerDisplay("LayersPanelView: {ViewModel}")]
public sealed partial class LayersPanelView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="LayersPanelView"/> class.</summary>
    public LayersPanelView()
    {
        InitializeComponent();
        LayerTree.ItemTemplate = new FuncTreeDataTemplate<LayerItemViewModel>(static (_, _) => new LayerItemView(), static item => item.Children);
        _ = this.WhenActivated(disposables =>
        {
            // Items are named as their containers are prepared, so this comes before the tree is filled.
            disposables.Add(ItemAutomation.NameItems(LayerTree));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsLayersMode, static v => v.LayersPanel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Layers.Items, static v => v.LayerTree.ItemsSource));

            // The tree item holds the focus, not its check box, so Space switches the focused layer.
            disposables.Add(LayerTree.KeyDowns.Space
                .Pick(press => LayerTree.SelectedItem is LayerItemViewModel { CanToggle: true } layer
                    ? new Injected<KeyEventArgs, LayerItemViewModel>(press, layer)
                    : default(Injected<KeyEventArgs, LayerItemViewModel>?))
                .Do(static toggle => toggle.Value.Handled = true)
                .SubscribeSafe(static toggle => toggle.State.IsVisible = !toggle.State.IsVisible, static error => Trace.TraceError(error.ToString())));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Layers.ConfigurationNames, static v => v.ConfigurationBox.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Layers.HasConfigurations, static v => v.ConfigurationBox.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Layers.SelectedConfigurationIndex, static v => v.ConfigurationBox.SelectedIndex));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Layers.ResetCommand, static v => v.ResetLayersButton));
        });
    }
}
