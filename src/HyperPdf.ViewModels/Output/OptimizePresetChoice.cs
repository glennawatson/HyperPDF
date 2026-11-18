// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Optimizing;
using ReactiveUI;

namespace HyperPdf.ViewModels.Output;

/// <summary>A size choice offered in the optimised copy dialog.</summary>
/// <param name="preset">The preset.</param>
/// <param name="nameKey">The short name.</param>
/// <param name="descriptionKey">What the choice does, in plain words.</param>
[DebuggerDisplay("OptimizePresetChoice: {Name}")]
public sealed class OptimizePresetChoice(OptimizePreset preset, string nameKey, string descriptionKey) : ReactiveObject, IEquatable<OptimizePresetChoice>, ILocalisationChildren
{
    /// <summary>Gets the preset.</summary>
    public OptimizePreset Preset { get; } = preset;

    /// <summary>Gets the short name.</summary>
    public string NameKey { get; } = nameKey;

    /// <summary>Gets what the choice does, in plain words.</summary>
    public string DescriptionKey { get; } = descriptionKey;

    /// <summary>Gets the localized name.</summary>
    public string Name => UiText.Get(NameKey);

    /// <summary>Gets the localized description.</summary>
    public string Description => UiText.Get(DescriptionKey);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(OptimizePresetChoice? other) => other is not null && Preset == other.Preset && NameKey == other.NameKey && DescriptionKey == other.DescriptionKey;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => Equals(obj as OptimizePresetChoice);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(Preset, NameKey, DescriptionKey);

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(Name));
        this.RaisePropertyChanged(nameof(Description));
    }
}
