// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using ReactiveUI;

namespace HyperPdf.ViewModels.Output;

/// <summary>One choice of which pages to export.</summary>
/// <param name="kind">The choice.</param>
/// <param name="nameKey">The name shown.</param>
[DebuggerDisplay("ExportPagesScopeChoice: {Name}")]
public sealed class ExportPagesScopeChoice(ExportPagesScopeKind kind, string nameKey) : ReactiveObject, IEquatable<ExportPagesScopeChoice>, ILocalisationChildren
{
    /// <summary>Gets the choice.</summary>
    public ExportPagesScopeKind Kind { get; } = kind;

    /// <summary>Gets the name shown.</summary>
    public string NameKey { get; } = nameKey;

    /// <summary>Gets the translated choice.</summary>
    public string Name => UiText.Get(NameKey);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ExportPagesScopeChoice? other) => other is not null && Kind == other.Kind && NameKey == other.NameKey;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => Equals(obj as ExportPagesScopeChoice);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(Kind, NameKey);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited) => this.RaisePropertyChanged(nameof(Name));
}
