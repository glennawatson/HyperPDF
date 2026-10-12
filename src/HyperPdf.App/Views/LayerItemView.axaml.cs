// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>One layer in the layers panel: a check box with the layer's name.</summary>
[DebuggerDisplay("LayerItemView: {ViewModel}")]
public sealed partial class LayerItemView : ReactiveUI.Avalonia.ReactiveUserControl<LayerItemViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="LayerItemView"/> class.</summary>
    public LayerItemView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Name, static v => v.LayerBox.Content));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsVisible, static v => v.LayerBox.IsChecked, static on => on, IsOn));
        });
    }

    /// <summary>Converts a nullable toggle state to a plain flag.</summary>
    /// <param name="value">The toggle state.</param>
    /// <returns><see langword="true"/> only when checked.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOn(bool? value) => value == true;
}
