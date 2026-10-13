// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Collects the code each character is shown with while a font's codes are read, in pooled arrays. <see cref="Finish"/>
/// keeps the first code added for a character and returns the characters and codes in two exact arrays, ordered by
/// character, so the map keeps no growth garbage.
/// </summary>
internal ref struct FormCodeCollector
{
    /// <summary>The bits that hold the order a code was added in, below the character in a sort key.</summary>
    private const int OrderBits = 32;

    /// <summary>The sort keys: the character above the order the code was added in; a pooled array.</summary>
    private long[]? _keys;

    /// <summary>The codes, in the order they were added; a pooled array.</summary>
    private FormCode[]? _codes;

    /// <summary>The number of codes added.</summary>
    private int _count;

    /// <summary>Adds the code a character is shown with.</summary>
    /// <param name="character">The character's code point.</param>
    /// <param name="code">The code.</param>
    internal void Add(int character, FormCode code)
    {
        PooledArrays.EnsureRoom(ref _keys, _count, clear: false)[_count] = ((long)character << OrderBits) | (uint)_count;
        PooledArrays.EnsureRoom(ref _codes, _count, clear: false)[_count] = code;
        _count++;
    }

    /// <summary>Makes the characters and their first codes, then gives the pooled arrays back.</summary>
    /// <param name="characters">The distinct characters, ascending.</param>
    /// <returns>The first code added for each character, in the same order.</returns>
    internal FormCode[] Finish(out int[] characters)
    {
        if (_keys is null || _codes is null)
        {
            characters = [];
            return [];
        }

        Array.Sort(_keys, _codes, 0, _count);
        var distinct = 0;
        for (var i = 0; i < _count; i++)
        {
            if (i == 0 || (_keys[i] >> OrderBits) != (_keys[i - 1] >> OrderBits))
            {
                distinct++;
            }
        }

        characters = new int[distinct];
        var result = new FormCode[distinct];
        var next = 0;
        for (var i = 0; i < _count; i++)
        {
            var character = (int)(_keys[i] >> OrderBits);
            if (i != 0 && character == characters[next - 1])
            {
                continue;
            }

            characters[next] = character;
            result[next] = _codes[i];
            next++;
        }

        PooledArrays.Return(ref _keys, _count, clear: false);
        PooledArrays.Return(ref _codes, _count, clear: false);
        return result;
    }
}
