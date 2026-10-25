// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Optimizing;

namespace HyperPdf.ViewModels.Output;

/// <summary>A size choice offered in the optimised copy dialog.</summary>
/// <param name="Preset">The preset.</param>
/// <param name="NameKey">The short name.</param>
/// <param name="DescriptionKey">What the choice does, in plain words.</param>
[DebuggerDisplay("OptimizePresetChoice: {Name}")]
public sealed record OptimizePresetChoice(OptimizePreset Preset, string NameKey, string DescriptionKey) : INotifyPropertyChanged, ILocalisationChildren
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the localized name.</summary>
    public string Name => UiText.Get(NameKey);

    /// <summary>Gets the localized description.</summary>
    public string Description => UiText.Get(DescriptionKey);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(OptimizePresetChoice? other) => other is not null && Preset == other.Preset && NameKey == other.NameKey && DescriptionKey == other.DescriptionKey;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(Preset, NameKey, DescriptionKey);

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
    }
}
