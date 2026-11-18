// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Redaction;
using ReactiveUI;

namespace HyperPdf.ViewModels.Security;

/// <summary>One choice for what happens to drawings under a mark.</summary>
/// <param name="choice">The choice.</param>
/// <param name="nameKey">The name shown.</param>
/// <param name="descriptionKey">What it does, in plain words.</param>
[DebuggerDisplay("RedactionLineArtOption: {Name}")]
public sealed class RedactionLineArtOption(RedactionLineArtChoice choice, string nameKey, string descriptionKey) : ReactiveObject, IEquatable<RedactionLineArtOption>, ILocalisationChildren
{
    /// <summary>Gets the choice.</summary>
    public RedactionLineArtChoice Choice { get; } = choice;

    /// <summary>Gets the name shown.</summary>
    public string NameKey { get; } = nameKey;

    /// <summary>Gets what it does, in plain words.</summary>
    public string DescriptionKey { get; } = descriptionKey;

    /// <summary>Gets the localized name.</summary>
    public string Name => UiText.Get(NameKey);

    /// <summary>Gets the localized description.</summary>
    public string Description => UiText.Get(DescriptionKey);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(RedactionLineArtOption? other) => other is not null && Choice == other.Choice && NameKey == other.NameKey && DescriptionKey == other.DescriptionKey;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => Equals(obj as RedactionLineArtOption);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(Choice, NameKey, DescriptionKey);

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(Name));
        this.RaisePropertyChanged(nameof(Description));
    }
}
