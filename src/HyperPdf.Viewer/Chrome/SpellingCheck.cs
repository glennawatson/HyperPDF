// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Reading;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What spell checking found about the word at the end of a text.</summary>
/// <param name="Text">The text that was checked.</param>
/// <param name="Word">The word at the end of the text when it is spelled wrong, or the default when it is not.</param>
/// <param name="Suggestions">The words that could replace it.</param>
/// <param name="Labels">The words that could replace it, then the choice to keep it.</param>
/// <param name="Misspelled">Every word of the text that is spelled wrong.</param>
[DebuggerDisplay("SpellingCheck: {Labels.Length} corrections for '{Text}'")]
internal sealed record SpellingCheck(string Text, TextRange Word, string[] Suggestions, string[] Labels, TextRange[] Misspelled)
{
    /// <summary>Gets the check of a text with nothing wrong in it.</summary>
    internal static SpellingCheck None { get; } = new(string.Empty, default, [], [], []);
}
