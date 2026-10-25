// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.ViewModels.Signing;

/// <summary>A choice offered in the validation data and timestamp window.</summary>
/// <param name="Action">What the choice adds.</param>
/// <param name="NameKey">The short name.</param>
/// <param name="DescriptionKey">What the choice does, in plain words.</param>
[DebuggerDisplay("ValidationActionChoice: {Name}")]
public sealed record ValidationActionChoice(ValidationAction Action, string NameKey, string DescriptionKey) : INotifyPropertyChanged, ILocalisationChildren
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the localized name.</summary>
    public string Name => UiText.Get(NameKey);

    /// <summary>Gets the localized description.</summary>
    public string Description => UiText.Get(DescriptionKey);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ValidationActionChoice? other) => other is not null && Action == other.Action && NameKey == other.NameKey && DescriptionKey == other.DescriptionKey;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(Action, NameKey, DescriptionKey);

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
    }
}
