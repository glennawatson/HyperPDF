// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Flattening;
using ReactiveUI;

namespace HyperPdf.ViewModels.Output;

/// <summary>Which look to keep, as offered in the flattened copy window.</summary>
/// <param name="look">The look.</param>
/// <param name="nameKey">The short name.</param>
/// <param name="descriptionKey">What the choice means, in plain words.</param>
[DebuggerDisplay("FlattenLookChoice: {Name}")]
public sealed class FlattenLookChoice(FlattenCopyLook look, string nameKey, string descriptionKey) : ReactiveObject, IEquatable<FlattenLookChoice>, ILocalisationChildren
{
    /// <summary>Gets the look.</summary>
    public FlattenCopyLook Look { get; } = look;

    /// <summary>Gets the short name.</summary>
    public string NameKey { get; } = nameKey;

    /// <summary>Gets what the choice means, in plain words.</summary>
    public string DescriptionKey { get; } = descriptionKey;

    /// <summary>Gets the localized name.</summary>
    public string Name => UiText.Get(NameKey);

    /// <summary>Gets the localized description.</summary>
    public string Description => UiText.Get(DescriptionKey);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(FlattenLookChoice? other) => other is not null && Look == other.Look && NameKey == other.NameKey && DescriptionKey == other.DescriptionKey;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => Equals(obj as FlattenLookChoice);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(Look, NameKey, DescriptionKey);

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(Name));
        this.RaisePropertyChanged(nameof(Description));
    }
}
