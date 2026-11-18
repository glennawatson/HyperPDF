// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;

namespace HyperPdf.Viewer.Generator;

/// <summary>Reads the commands, numbers and flags of SVG path data, which may run together without spaces.</summary>
/// <param name="text">The path data.</param>
internal sealed class PathScanner(string text)
{
    /// <summary>Where the next character is read.</summary>
    private int _position;

    /// <summary>Reads the letter of the next command, when a command is next.</summary>
    /// <param name="command">Receives the letter.</param>
    /// <returns><see langword="true"/> when a command was read.</returns>
    internal bool TryReadCommand(out char command)
    {
        SkipSeparators();
        if (_position < text.Length && char.IsLetter(text[_position]))
        {
            command = text[_position];
            _position++;
            return true;
        }

        command = default;
        return false;
    }

    /// <summary>Tells whether a number is next, which repeats the command before it.</summary>
    /// <returns><see langword="true"/> when a number is next.</returns>
    internal bool HasNumber()
    {
        SkipSeparators();
        return _position < text.Length && (char.IsDigit(text[_position]) || text[_position] is '.' or '-' or '+');
    }

    /// <summary>Reads one number.</summary>
    /// <returns>The number.</returns>
    /// <exception cref="FormatException">No number is next.</exception>
    internal double ReadNumber()
    {
        SkipSeparators();
        var start = _position;
        SkipSign();
        SkipDigits();
        if (_position < text.Length && text[_position] == '.')
        {
            _position++;
            SkipDigits();
        }

        if (_position < text.Length && text[_position] is 'e' or 'E')
        {
            _position++;
            SkipSign();
            SkipDigits();
        }

        // The compiler's framework has no parse of a span, and the tests compile this file for a newer one that has.
#if NETSTANDARD2_0
        var number = text.Substring(start, _position - start);
#else
        var number = text.AsSpan(start, _position - start);
#endif
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException($"a number was expected at character {start} of the path data");
        }

        return value;
    }

    /// <summary>Reads one flag of an arc, a single 0 or 1 that may touch the number after it.</summary>
    /// <returns><see langword="true"/> for 1.</returns>
    /// <exception cref="FormatException">No flag is next.</exception>
    internal bool ReadFlag()
    {
        SkipSeparators();
        if (_position >= text.Length || text[_position] is not ('0' or '1'))
        {
            throw new FormatException($"an arc flag was expected at character {_position} of the path data");
        }

        var set = text[_position] == '1';
        _position++;
        return set;
    }

    /// <summary>Moves past spaces, line ends and commas.</summary>
    private void SkipSeparators()
    {
        while (_position < text.Length && (char.IsWhiteSpace(text[_position]) || text[_position] == ','))
        {
            _position++;
        }
    }

    /// <summary>Moves past a plus or minus sign.</summary>
    private void SkipSign()
    {
        if (_position < text.Length && text[_position] is '-' or '+')
        {
            _position++;
        }
    }

    /// <summary>Moves past a run of digits.</summary>
    private void SkipDigits()
    {
        while (_position < text.Length && char.IsDigit(text[_position]))
        {
            _position++;
        }
    }
}
