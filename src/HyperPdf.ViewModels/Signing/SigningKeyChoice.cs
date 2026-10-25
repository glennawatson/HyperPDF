// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Signatures.Signing;

namespace HyperPdf.ViewModels.Signing;

/// <summary>A kind of signature an external signer's key makes, offered in the external signing window.</summary>
/// <param name="Kind">The kind of signature.</param>
/// <param name="NameKey">The kind, in plain words.</param>
[DebuggerDisplay("SigningKeyChoice: {Name}")]
public sealed record SigningKeyChoice(SigningKeyKind Kind, string NameKey) : INotifyPropertyChanged, ILocalisationChildren
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the localized name.</summary>
    public string Name => UiText.Get(NameKey);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(SigningKeyChoice? other) => other is not null && Kind == other.Kind && NameKey == other.NameKey;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(Kind, NameKey);

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
}
