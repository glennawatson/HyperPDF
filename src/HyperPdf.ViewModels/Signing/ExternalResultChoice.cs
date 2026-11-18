// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using ReactiveUI;

namespace HyperPdf.ViewModels.Signing;

/// <summary>A kind of answer the external signer can give, offered in the external signing window.</summary>
/// <param name="kind">The kind of answer.</param>
/// <param name="nameKey">What the signer gives, in plain words.</param>
[DebuggerDisplay("ExternalResultChoice: {Name}")]
public sealed class ExternalResultChoice(ExternalResultKind kind, string nameKey) : ReactiveObject, IEquatable<ExternalResultChoice>, ILocalisationChildren
{
    /// <summary>Gets the kind of answer.</summary>
    public ExternalResultKind Kind { get; } = kind;

    /// <summary>Gets what the signer gives, in plain words.</summary>
    public string NameKey { get; } = nameKey;

    /// <summary>Gets the localized name.</summary>
    public string Name => UiText.Get(NameKey);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ExternalResultChoice? other) => other is not null && Kind == other.Kind && NameKey == other.NameKey;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => Equals(obj as ExternalResultChoice);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(Kind, NameKey);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited) => this.RaisePropertyChanged(nameof(Name));
}
