// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// Writes the MEL stream of an HT cleanup segment: the adaptive run-length code of the significance events of
/// zero-context quads. Bits are most significant first, seven in the byte after 0xFF.
/// </summary>
internal sealed class JpxTestHtMelWriter
{
    /// <summary>The highest adaptation state.</summary>
    private const int MaxState = 12;

    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The value of a byte that is all ones.</summary>
    private const int AllOnes = 0xFF;

    /// <summary>The run exponent of each adaptation state.</summary>
    private static readonly int[] Exponents = [0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 4, 5];

    /// <summary>The bytes written.</summary>
    private readonly List<byte> _bytes = [];

    /// <summary>The byte being filled.</summary>
    private int _current;

    /// <summary>The free bits of the byte being filled.</summary>
    private int _free = ByteBits;

    /// <summary>The bits the byte being filled holds.</summary>
    private int _capacity = ByteBits;

    /// <summary>The adaptation state.</summary>
    private int _state;

    /// <summary>The zero events not yet coded.</summary>
    private int _run;

    /// <summary>Codes one event.</summary>
    /// <param name="significant">The event: whether the quad is significant.</param>
    internal void Encode(bool significant)
    {
        var exponent = Exponents[_state];
        if (!significant)
        {
            _run++;
            if (_run == 1 << exponent)
            {
                WriteBit(1);
                _run = 0;
                _state = Math.Min(_state + 1, MaxState);
            }

            return;
        }

        WriteBit(0);
        for (var i = exponent - 1; i >= 0; i--)
        {
            WriteBit((_run >> i) & 1);
        }

        _run = 0;
        _state = Math.Max(_state - 1, 0);
    }

    /// <summary>Codes any open run as a full one, pads the last byte and returns the bytes.</summary>
    /// <returns>The stream; never ending in 0xFF.</returns>
    internal byte[] Finish()
    {
        if (_run > 0)
        {
            WriteBit(1);
        }

        while (_free != _capacity)
        {
            WriteBit(0);
        }

        if (_bytes.Count > 0 && _bytes[^1] == AllOnes)
        {
            // A stuffed byte keeps a final 0xFF from running into the VLC bytes.
            _bytes.Add(0);
        }

        return [.. _bytes];
    }

    /// <summary>Writes one bit.</summary>
    /// <param name="bit">The bit.</param>
    private void WriteBit(int bit)
    {
        _free--;
        _current |= (bit & 1) << _free;
        if (_free != 0)
        {
            return;
        }

        _bytes.Add((byte)_current);
        _capacity = _current == AllOnes ? ByteBits - 1 : ByteBits;
        _free = _capacity;
        _current = 0;
    }
}
