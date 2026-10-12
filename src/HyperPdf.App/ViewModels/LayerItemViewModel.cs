// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>A layer in the layers panel, with a check box showing or hiding it.</summary>
[DebuggerDisplay("LayerItemViewModel: {Name} visible={IsVisible}")]
public sealed partial class LayerItemViewModel : ReactiveObject
{
    /// <summary>Initializes a new instance of the <see cref="LayerItemViewModel"/> class.</summary>
    /// <param name="id">The layer's id.</param>
    /// <param name="name">The layer's name.</param>
    /// <param name="visible">Whether it is shown.</param>
    public LayerItemViewModel(int id, string name, bool visible)
    {
        Id = id;
        Name = name;
        IsVisible = visible;
    }

    /// <summary>Gets the layer's id.</summary>
    public int Id { get; }

    /// <summary>Gets the layer's name.</summary>
    public string Name { get; }

    /// <summary>Gets or sets a value indicating whether the layer is shown.</summary>
    [Reactive]
    public partial bool IsVisible { get; set; }
}
