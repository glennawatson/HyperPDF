// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Signatures.Signing;
using ReactiveUI;

namespace HyperPdf.ViewModels.Signing;

/// <summary>A kind of signature an external signer's key makes, offered in the external signing window.</summary>
/// <param name="kind">The kind of signature.</param>
/// <param name="nameKey">The kind, in plain words.</param>
[DebuggerDisplay("SigningKeyChoice: {Name}")]
public sealed class SigningKeyChoice(SigningKeyKind kind, string nameKey) : ReactiveObject, IEquatable<SigningKeyChoice>, ILocalisationChildren
{
    /// <summary>Gets the kind of signature.</summary>
    public SigningKeyKind Kind { get; } = kind;

    /// <summary>Gets the kind, in plain words.</summary>
    public string NameKey { get; } = nameKey;

    /// <summary>Gets the localized name.</summary>
    public string Name => UiText.Get(NameKey);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(SigningKeyChoice? other) => other is not null && Kind == other.Kind && NameKey == other.NameKey;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => Equals(obj as SigningKeyChoice);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(Kind, NameKey);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited) => this.RaisePropertyChanged(nameof(Name));
}
