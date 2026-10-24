// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// Writes a forward-growing HT bit-stream (MagSgn or SigProp): least significant bit first, with only seven bits, the
/// top one a stuffed zero, in the byte after 0xFF.
/// </summary>
internal sealed class JpxTestHtForwardWriter
{
    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The value of a byte that is all ones.</summary>
    private const int AllOnes = 0xFF;

    /// <summary>The bytes written.</summary>
    private readonly List<byte> _bytes = [];

    /// <summary>The bit that pads the last byte.</summary>
    private readonly int _pad;

    /// <summary>The byte being filled.</summary>
    private int _current;

    /// <summary>The bits in the byte being filled.</summary>
    private int _used;

    /// <summary>The bits the byte being filled holds.</summary>
    private int _capacity = ByteBits;

    /// <summary>Initializes a new instance of the <see cref="JpxTestHtForwardWriter"/> class.</summary>
    /// <param name="pad">The bit that pads the last byte: 1 for MagSgn, 0 for SigProp.</param>
    internal JpxTestHtForwardWriter(int pad) => _pad = pad;

    /// <summary>Writes bits, least significant first.</summary>
    /// <param name="value">The bits.</param>
    /// <param name="count">The number of bits.</param>
    internal void Write(uint value, int count)
    {
        for (var i = 0; i < count; i++)
        {
            _current |= (int)((value >> i) & 1) << _used;
            _used++;
            if (_used == _capacity)
            {
                Emit();
            }
        }
    }

    /// <summary>Pads and returns the bytes.</summary>
    /// <returns>The stream.</returns>
    internal byte[] Finish()
    {
        while (_used > 0)
        {
            Write((uint)_pad, 1);
        }

        return [.. _bytes];
    }

    /// <summary>Emits the byte being filled.</summary>
    private void Emit()
    {
        _bytes.Add((byte)_current);
        _capacity = _current == AllOnes ? ByteBits - 1 : ByteBits;
        _current = 0;
        _used = 0;
    }
}
