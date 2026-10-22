// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Decodes the MEL stream of an HT cleanup segment (T.814): an adaptive run-length code of the
/// significance events of quads with zero context. Bits come most significant first; the byte after 0xFF holds only seven
/// bits. The stream shares its last byte with the VLC stream, whose low nibble reads as ones; past the end it reads ones.
/// </summary>
internal ref struct JpxHtMelDecoder
{
    /// <summary>The highest adaptation state.</summary>
    private const int MaxState = 12;

    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The value of a byte that is all ones.</summary>
    private const int AllOnes = 0xFF;

    /// <summary>The low nibble, which the last MEL byte shares with the VLC stream.</summary>
    private const int LowNibble = 0x0F;

    /// <summary>The bytes of the stream.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The position of the next byte.</summary>
    private int _position;

    /// <summary>The current byte.</summary>
    private int _byte;

    /// <summary>The bits left in the current byte.</summary>
    private int _bitsLeft;

    /// <summary>Whether the current byte is 0xFF, so the next one is stuffed.</summary>
    private bool _stuffNext;

    /// <summary>The adaptation state k.</summary>
    private int _state;

    /// <summary>The zero events left in the current run.</summary>
    private int _zeros;

    /// <summary>Whether a one event follows the current run.</summary>
    private bool _oneFollows;

    /// <summary>Initializes a new instance of the <see cref="JpxHtMelDecoder"/> struct.</summary>
    /// <param name="segment">The cleanup segment.</param>
    /// <param name="suffixLength">The bytes of MEL and VLC data, Scup.</param>
    internal JpxHtMelDecoder(ReadOnlySpan<byte> segment, int suffixLength) =>
        _data = segment.Slice(segment.Length - suffixLength, suffixLength - 1);

    /// <summary>Gets the run exponents of each adaptation state.</summary>
    private static ReadOnlySpan<byte> Exponents => [0x00, 0x00, 0x00, 0x01, 0x01, 0x01, 0x02, 0x02, 0x02, 0x03, 0x03, 0x04, 0x05];

    /// <summary>Decodes the next event.</summary>
    /// <returns>1 for a significant quad, 0 otherwise.</returns>
    internal int Next()
    {
        while (_zeros <= 0 && !_oneFollows)
        {
            ReadRun();
        }

        if (_zeros > 0)
        {
            _zeros--;
            return 0;
        }

        _oneFollows = false;
        return 1;
    }

    /// <summary>Reads one MEL codeword: a one for a full run of zeros, or a zero and the length of a run ended by a one.</summary>
    private void ReadRun()
    {
        var exponent = Exponents[_state];
        if (ReadBit() == 1)
        {
            _zeros = 1 << exponent;
            _oneFollows = false;
            _state = Math.Min(_state + 1, MaxState);
            return;
        }

        var run = 0;
        for (var i = 0; i < exponent; i++)
        {
            run = (run << 1) | ReadBit();
        }

        _zeros = run;
        _oneFollows = true;
        _state = Math.Max(_state - 1, 0);
    }

    /// <summary>Reads one bit.</summary>
    /// <returns>The bit.</returns>
    private int ReadBit()
    {
        if (_bitsLeft == 0)
        {
            LoadByte();
        }

        _bitsLeft--;
        return (_byte >> _bitsLeft) & 1;
    }

    /// <summary>Loads the next byte, skipping its top bit after 0xFF.</summary>
    private void LoadByte()
    {
        var value = AllOnes;
        if (_position < _data.Length)
        {
            value = _data[_position];
            if (_position == _data.Length - 1)
            {
                value |= LowNibble;
            }

            _position++;
        }

        _bitsLeft = _stuffNext ? ByteBits - 1 : ByteBits;
        _byte = value;
        _stuffNext = value == AllOnes;
    }
}
