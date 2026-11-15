// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Reading;
using ReactiveUI.Primitives;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What the window can type into where it lies on the page: a form field or a text box.</summary>
/// <param name="Page">The page the text lies on.</param>
/// <param name="Bounds">Where the text lies on the page.</param>
/// <param name="TakesLines">Whether the text can have several lines.</param>
/// <param name="IsOpen">Tells whether the text is still being typed.</param>
/// <param name="Read">Gets the text typed so far.</param>
/// <param name="Write">Replaces the text.</param>
/// <param name="Changes">Signals each time the text changes.</param>
/// <param name="Commit">Writes the text.</param>
/// <param name="Cancel">Gives the text up.</param>
/// <param name="CommitAndMove">Writes the text and moves to the next field, or the one before when the argument is <see langword="true"/>.</param>
/// <param name="Corrections">
/// Gets what can be done about the word at the end of the text when it is spelled wrong: the words that could replace it, then keeping
/// it as it is. Empty when it is spelled right or spelling is not checked.
/// </param>
/// <param name="Correct">Does one of the corrections, given its place in the list.</param>
/// <param name="Misspelled">Gets every word of the text that is spelled wrong, so each can be underlined.</param>
/// <param name="Lifetime">What follows the text; it ends with the typing.</param>
[DebuggerDisplay("TypingTarget: page {Page}")]
internal sealed record TypingTarget(
    int Page,
    PageRect Bounds,
    bool TakesLines,
    Func<bool> IsOpen,
    Func<string> Read,
    Action<string> Write,
    IObservable<RxVoid> Changes,
    Action Commit,
    Action Cancel,
    Action<bool> CommitAndMove,
    Func<string[]> Corrections,
    Action<int> Correct,
    Func<TextRange[]> Misspelled,
    IDisposable Lifetime);
