// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Archiving;
using ReactiveUI;

namespace HyperPdf.ViewModels.Output;

/// <summary>A PDF/A level offered in the PDF/A copy window.</summary>
/// <param name="level">The level.</param>
/// <param name="nameKey">The short name, such as PDF/A-2b.</param>
/// <param name="descriptionKey">What the level means, in plain words.</param>
[DebuggerDisplay("ArchiveLevelChoice: {Name}")]
public sealed class ArchiveLevelChoice(ArchiveLevel level, string nameKey, string descriptionKey) : ReactiveObject, IEquatable<ArchiveLevelChoice>, ILocalisationChildren
{
    /// <summary>Gets the level.</summary>
    public ArchiveLevel Level { get; } = level;

    /// <summary>Gets the short name, such as PDF/A-2b.</summary>
    public string NameKey { get; } = nameKey;

    /// <summary>Gets what the level means, in plain words.</summary>
    public string DescriptionKey { get; } = descriptionKey;

    /// <summary>Gets the localized name.</summary>
    public string Name => UiText.Get(NameKey);

    /// <summary>Gets the localized description.</summary>
    public string Description => UiText.Get(DescriptionKey);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ArchiveLevelChoice? other) => other is not null && Level == other.Level && NameKey == other.NameKey && DescriptionKey == other.DescriptionKey;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => Equals(obj as ArchiveLevelChoice);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(Level, NameKey, DescriptionKey);

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(Name));
        this.RaisePropertyChanged(nameof(Description));
    }
}
