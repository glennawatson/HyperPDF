// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.ViewModels.Output;

/// <summary>One choice of which pages to export.</summary>
/// <param name="Kind">The choice.</param>
/// <param name="NameKey">The name shown.</param>
[DebuggerDisplay("ExportPagesScopeChoice: {Name}")]
public sealed record ExportPagesScopeChoice(ExportPagesScopeKind Kind, string NameKey) : INotifyPropertyChanged, ILocalisationChildren
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the translated choice.</summary>
    public string Name => UiText.Get(NameKey);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
}
