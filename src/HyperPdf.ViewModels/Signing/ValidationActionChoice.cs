// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using ReactiveUI;

namespace HyperPdf.ViewModels.Signing;

/// <summary>A choice offered in the validation data and timestamp window.</summary>
/// <param name="action">What the choice adds.</param>
/// <param name="nameKey">The short name.</param>
/// <param name="descriptionKey">What the choice does, in plain words.</param>
[DebuggerDisplay("ValidationActionChoice: {Name}")]
public sealed class ValidationActionChoice(ValidationAction action, string nameKey, string descriptionKey) : ReactiveObject, IEquatable<ValidationActionChoice>, ILocalisationChildren
{
    /// <summary>Gets what the choice adds.</summary>
    public ValidationAction Action { get; } = action;

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
    public bool Equals(ValidationActionChoice? other) => other is not null && Action == other.Action && NameKey == other.NameKey && DescriptionKey == other.DescriptionKey;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => Equals(obj as ValidationActionChoice);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(Action, NameKey, DescriptionKey);

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(Name));
        this.RaisePropertyChanged(nameof(Description));
    }
}
