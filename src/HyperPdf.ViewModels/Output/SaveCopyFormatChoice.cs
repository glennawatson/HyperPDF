// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using ReactiveUI;

namespace HyperPdf.ViewModels.Output;

/// <summary>One format choice in the "Save a Copy" window, with its plain-English explanation.</summary>
/// <param name="format">The format to write.</param>
/// <param name="nameKey">The short name shown on the choice.</param>
/// <param name="descriptionKey">What this format does and when to choose it.</param>
[DebuggerDisplay("{Name} ({Format})")]
public sealed class SaveCopyFormatChoice(SaveCopyFormat format, string nameKey, string descriptionKey) : ReactiveObject, IEquatable<SaveCopyFormatChoice>, ILocalisationChildren
{
    /// <summary>Gets the format to write.</summary>
    public SaveCopyFormat Format { get; } = format;

    /// <summary>Gets the short name shown on the choice.</summary>
    public string NameKey { get; } = nameKey;

    /// <summary>Gets what this format does and when to choose it.</summary>
    public string DescriptionKey { get; } = descriptionKey;

    /// <summary>Gets the localized name.</summary>
    public string Name => UiText.Get(NameKey);

    /// <summary>Gets the localized description.</summary>
    public string Description => UiText.Get(DescriptionKey);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(SaveCopyFormatChoice? other) => other is not null && Format == other.Format && NameKey == other.NameKey && DescriptionKey == other.DescriptionKey;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => Equals(obj as SaveCopyFormatChoice);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(Format, NameKey, DescriptionKey);

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(Name));
        this.RaisePropertyChanged(nameof(Description));
    }
}
