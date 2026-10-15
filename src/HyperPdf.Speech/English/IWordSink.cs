// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Speech.English;

/// <summary>Receives the words of a number said aloud, one at a time, so no string holds the whole phrase.</summary>
internal interface IWordSink
{
    /// <summary>Takes the next word.</summary>
    /// <param name="word">The word, which is a fixed string.</param>
    void Add(string word);
}
