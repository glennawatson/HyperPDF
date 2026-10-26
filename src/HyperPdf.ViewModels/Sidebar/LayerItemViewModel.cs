// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Sidebar;

/// <summary>
/// An entry of the layers tree: a layer with a check box showing or hiding it, or a label that groups layers. A locked
/// layer is shown but cannot be changed.
/// </summary>
[DebuggerDisplay("LayerItemViewModel: {Name} visible={IsVisible}")]
public sealed partial class LayerItemViewModel : ReactiveObject, ILocalisationChildren
{
    /// <summary>Initializes a new instance of the <see cref="LayerItemViewModel"/> class for an unlocked layer without sublayers.</summary>
    /// <param name="id">The layer's id.</param>
    /// <param name="name">The layer's name.</param>
    /// <param name="visible">Whether it is shown.</param>
    public LayerItemViewModel(int id, string name, bool visible)
        : this(id, name, visible, false, string.Empty, [])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LayerItemViewModel"/> class.</summary>
    /// <param name="id">The layer's id; 0 for a label.</param>
    /// <param name="name">The layer's name, or the label's text.</param>
    /// <param name="visible">Whether it is shown.</param>
    /// <param name="locked">Whether the file locks it.</param>
    /// <param name="note">A visible note on the layer, such as that it is locked; empty for none.</param>
    /// <param name="children">The entries listed under this one.</param>
    public LayerItemViewModel(int id, string name, bool visible, bool locked, string note, IReadOnlyList<LayerItemViewModel> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        Id = id;
        Name = name;
        IsVisible = visible;
        IsLocked = locked;
        Note = note;
        Children = children;
        IsExpanded = true;
        SpokenName = LayerText.Speak(name, id == 0, note, children.Count);
    }

    /// <summary>Gets the layer's id; 0 for a label.</summary>
    public int Id { get; }

    /// <summary>Gets the layer's name, or the label's text.</summary>
    public string Name { get; }

    /// <summary>Gets a value indicating whether this entry is a label, which has no check box.</summary>
    public bool IsLabel => Id == 0;

    /// <summary>Gets a value indicating whether the file locks the layer.</summary>
    public bool IsLocked { get; }

    /// <summary>Gets a value indicating whether the check box can be changed.</summary>
    public bool CanToggle => !IsLabel && !IsLocked;

    /// <summary>Gets a visible note on the layer, such as that it is locked; empty for none.</summary>
    public string Note { get; }

    /// <summary>Gets a value indicating whether the note has text.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>Gets how a screen reader says the entry.</summary>
    public string SpokenName { get; }

    /// <summary>Gets the entries listed under this one.</summary>
    public IReadOnlyList<LayerItemViewModel> Children { get; }

    /// <summary>Gets or sets a value indicating whether the layer is shown.</summary>
    [Reactive]
    public partial bool IsVisible { get; set; }

    /// <summary>Gets or sets a value indicating whether the entry is expanded.</summary>
    [Reactive]
    public partial bool IsExpanded { get; set; }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited) => LocalisationRefresh.RefreshCore(Children, visited);
}
