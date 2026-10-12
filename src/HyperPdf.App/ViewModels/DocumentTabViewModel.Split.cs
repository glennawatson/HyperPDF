// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <content>Split view: a second, independent view of the same document beside the first.</content>
public sealed partial class DocumentTabViewModel
{
    /// <summary>Gets how the window splits or unsplits the view of this document; set by the window.</summary>
    public Action<DocumentTabViewModel>? ToggleSplitView { get; init; }

    /// <summary>Gets a value indicating whether this is the second view of a split, which leaves file watching and messages to the first.</summary>
    public bool IsSecondaryView { get; init; }

    /// <summary>Gets a value indicating whether the window shows this document split into two views.</summary>
    [Reactive]
    public partial bool IsSplitView { get; internal set; }

    /// <summary>Splits the view in two, or puts the second view away.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SplitView() => ToggleSplitView?.Invoke(this);
}
