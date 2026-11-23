// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>The window that asks what text to find on the pages and what to put in its place.</summary>
[DebuggerDisplay("FindReplaceViewModel: {Find} -> {Replacement}")]
public sealed partial class FindReplaceViewModel : ReactiveObject
{
    /// <summary>Initializes a new instance of the <see cref="FindReplaceViewModel"/> class.</summary>
    /// <param name="find">The words to start with, such as the words being searched for.</param>
    public FindReplaceViewModel(string find)
    {
        Find = find;
        Replacement = string.Empty;
        Answered = Signal.Merge(AcceptCommand, CancelCommand);
    }

    /// <summary>Gets the answer: whether to go ahead.</summary>
    public IObservable<bool> Answered { get; }

    /// <summary>Gets or sets the words to find.</summary>
    [Reactive]
    public partial string Find { get; set; }

    /// <summary>Gets or sets the words to put in their place.</summary>
    [Reactive]
    public partial string Replacement { get; set; }

    /// <summary>Gets or sets a value indicating whether capital and small letters must match.</summary>
    [Reactive]
    public partial bool MatchCase { get; set; }

    /// <summary>Closes the window without changing anything.</summary>
    /// <returns><see langword="false"/>.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Cancel() => false;

    /// <summary>Closes the window and replaces the words.</summary>
    /// <returns><see langword="true"/>.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Accept() => true;
}
