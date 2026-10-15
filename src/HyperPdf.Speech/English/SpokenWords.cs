// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Speech.English;

/// <summary>Writes words into spoken text with one space between them and none before the first.</summary>
/// <param name="Text">The text the words go into.</param>
/// <param name="Start">The length of the text when the first word is to be written.</param>
internal readonly record struct SpokenWords(SpokenText Text, int Start) : IWordSink
{
    /// <inheritdoc/>
    public void Add(string word)
    {
        if (Text.Length > Start)
        {
            Text.Append(' ');
        }

        Text.Append(word);
    }
}
