// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// The characters a font's codes have been searched for, ascending, with the code found for each. A code of length 0
/// records that the font has no such character, so the search is not repeated. An instance never changes; a map adds
/// characters by making a new one.
/// </summary>
[DebuggerDisplay("FormCodeEntries: {_characters.Length} characters")]
internal sealed class FormCodeEntries
{
    /// <summary>The searched characters, ascending.</summary>
    private readonly int[] _characters;

    /// <summary>The code found for each character, parallel to <see cref="_characters"/>.</summary>
    private readonly FormCode[] _codes;

    /// <summary>Initializes a new instance of the <see cref="FormCodeEntries"/> class.</summary>
    /// <param name="characters">The searched characters, ascending.</param>
    /// <param name="codes">The code found for each character.</param>
    private FormCodeEntries(int[] characters, FormCode[] codes)
    {
        _characters = characters;
        _codes = codes;
    }

    /// <summary>Gets the entries of a map that has searched for nothing.</summary>
    internal static FormCodeEntries Empty { get; } = new([], []);

    /// <summary>Finds the entry for a character.</summary>
    /// <param name="character">The character's code point.</param>
    /// <param name="code">Receives the code; its length is 0 when the font has no such character.</param>
    /// <returns><see langword="true"/> when the character has been searched for.</returns>
    internal bool TryGet(int character, out FormCode code)
    {
        var index = Array.BinarySearch(_characters, character);
        code = index >= 0 ? _codes[index] : default;
        return index >= 0;
    }

    /// <summary>Makes entries that also hold the result of a search.</summary>
    /// <param name="characters">The characters searched for, ascending and distinct.</param>
    /// <param name="codes">The code found for each, with length 0 for a character the font lacks.</param>
    /// <returns>The new entries; this instance when the search added nothing.</returns>
    internal FormCodeEntries With(ReadOnlySpan<int> characters, ReadOnlySpan<FormCode> codes)
    {
        var added = 0;
        foreach (var character in characters)
        {
            added += Array.BinarySearch(_characters, character) < 0 ? 1 : 0;
        }

        if (added == 0)
        {
            return this;
        }

        var mergedCharacters = new int[_characters.Length + added];
        var mergedCodes = new FormCode[mergedCharacters.Length];
        _characters.CopyTo(mergedCharacters, 0);
        _codes.CopyTo(mergedCodes, 0);
        var next = _characters.Length;
        for (var i = 0; i < characters.Length; i++)
        {
            if (Array.BinarySearch(_characters, characters[i]) >= 0)
            {
                continue;
            }

            mergedCharacters[next] = characters[i];
            mergedCodes[next] = codes[i];
            next++;
        }

        Array.Sort(mergedCharacters, mergedCodes);
        return new(mergedCharacters, mergedCodes);
    }
}
