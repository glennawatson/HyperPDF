// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>Splits the text of an XKB keyboard map into its words, names, strings and punctuation marks.</summary>
/// <param name="text">The map, as the compositor sends it.</param>
internal ref struct XkbTokens(ReadOnlySpan<byte> text)
{
    /// <summary>The map.</summary>
    private readonly ReadOnlySpan<byte> _text = text;

    /// <summary>Where the next word starts.</summary>
    private int _at;

    /// <summary>Reads the next word.</summary>
    /// <param name="word">Receives the word: a word, a key name with its angle brackets, a string with its quotes, or one mark.</param>
    /// <returns><see langword="false"/> at the end of the map.</returns>
    internal bool Next(out ReadOnlySpan<byte> word)
    {
        SkipBlanks();
        if (_at >= _text.Length)
        {
            word = default;
            return false;
        }

        var start = _at;
        var first = _text[_at];
        if (first == (byte)'<')
        {
            _at = EndAfter((byte)'>');
        }
        else if (first == (byte)'"')
        {
            _at++;
            _at = EndAfter((byte)'"');
        }
        else if (IsWord(first))
        {
            while (_at < _text.Length && IsWord(_text[_at]))
            {
                _at++;
            }
        }
        else
        {
            _at++;
        }

        word = _text[start.._at];
        return true;
    }

    /// <summary>Tells whether a byte is part of a word: a name, a number or a key symbol.</summary>
    /// <param name="value">The byte.</param>
    /// <returns><see langword="true"/> for letters, digits and the marks names contain.</returns>
    private static bool IsWord(byte value) => char.IsAsciiLetterOrDigit((char)value) || value is (byte)'_' or (byte)'+' or (byte)'-' or (byte)'.';

    /// <summary>Finds the place after the next occurrence of a byte.</summary>
    /// <param name="end">The byte.</param>
    /// <returns>The place, or the end of the map.</returns>
    private readonly int EndAfter(byte end)
    {
        var found = _text[_at..].IndexOf(end);
        return found < 0 ? _text.Length : _at + found + 1;
    }

    /// <summary>Moves past white space, zero bytes and comments.</summary>
    private void SkipBlanks()
    {
        while (_at < _text.Length)
        {
            var value = _text[_at];
            if (value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0)
            {
                _at++;
                continue;
            }

            if (value != (byte)'#' && !_text[_at..].StartsWith("//"u8))
            {
                return;
            }

            var line = _text[_at..].IndexOf((byte)'\n');
            _at = line < 0 ? _text.Length : _at + line + 1;
        }
    }
}
