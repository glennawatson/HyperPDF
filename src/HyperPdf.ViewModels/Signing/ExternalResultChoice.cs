// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.ViewModels.Signing;

/// <summary>A kind of answer the external signer can give, offered in the external signing window.</summary>
/// <param name="Kind">The kind of answer.</param>
/// <param name="NameKey">What the signer gives, in plain words.</param>
[DebuggerDisplay("ExternalResultChoice: {Name}")]
public sealed record ExternalResultChoice(ExternalResultKind Kind, string NameKey) : INotifyPropertyChanged, ILocalisationChildren
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the localized name.</summary>
    public string Name => UiText.Get(NameKey);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ExternalResultChoice? other) => other is not null && Kind == other.Kind && NameKey == other.NameKey;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(Kind, NameKey);

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
}
