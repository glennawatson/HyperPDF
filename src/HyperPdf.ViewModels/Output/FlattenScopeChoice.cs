// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Flattening;
using ReactiveUI;

namespace HyperPdf.ViewModels.Output;

/// <summary>What to flatten, as offered in the flattened copy window.</summary>
/// <param name="scope">The scope.</param>
/// <param name="nameKey">The short name.</param>
/// <param name="descriptionKey">What the choice means, in plain words.</param>
[DebuggerDisplay("FlattenScopeChoice: {Name}")]
public sealed class FlattenScopeChoice(FlattenCopyScope scope, string nameKey, string descriptionKey) : ReactiveObject, IEquatable<FlattenScopeChoice>, ILocalisationChildren
{
    /// <summary>Gets the scope.</summary>
    public FlattenCopyScope Scope { get; } = scope;

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
    public bool Equals(FlattenScopeChoice? other) => other is not null && Scope == other.Scope && NameKey == other.NameKey && DescriptionKey == other.DescriptionKey;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => Equals(obj as FlattenScopeChoice);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(Scope, NameKey, DescriptionKey);

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(Name));
        this.RaisePropertyChanged(nameof(Description));
    }
}
