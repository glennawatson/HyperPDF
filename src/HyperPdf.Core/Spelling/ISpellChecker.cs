// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Spelling;

/// <summary>Checks the spelling of words typed into form fields, using the desktop's own dictionaries.</summary>
public interface ISpellChecker
{
    /// <summary>Gets a value indicating whether a dictionary was found, so words can be checked.</summary>
    bool IsAvailable { get; }

    /// <summary>Determines whether a word is spelled correctly.</summary>
    /// <param name="word">The word.</param>
    /// <returns><see langword="true"/> when it is, or when it cannot be checked.</returns>
    bool IsCorrect(ReadOnlySpan<char> word);

    /// <summary>Adds corrections for a misspelled word to a list, the likeliest first.</summary>
    /// <param name="word">The word.</param>
    /// <param name="suggestions">The list the corrections are added to; nothing is added when there are none.</param>
    void Suggest(string word, List<string> suggestions);
}
