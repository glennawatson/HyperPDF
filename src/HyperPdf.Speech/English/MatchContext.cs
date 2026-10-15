// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Speech.English;

/// <summary>One matched piece of text, and where the words that replace it go.</summary>
/// <param name="match">The matched text.</param>
/// <param name="british">Whether to say dates the British way.</param>
/// <param name="output">Receives the words.</param>
internal readonly ref struct MatchContext(ReadOnlySpan<char> match, bool british, SpokenText output)
{
    /// <summary>Gets the matched text.</summary>
    internal ReadOnlySpan<char> Match { get; } = match;

    /// <summary>Gets a value indicating whether to say dates the British way.</summary>
    internal bool British { get; } = british;

    /// <summary>Gets the spoken text the words are written into.</summary>
    internal SpokenText Output { get; } = output;
}
