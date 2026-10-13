// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Field text encoded for a font: one unit per character, each a one-byte or two-byte code with its width. Layout works
/// in units, so one-byte fonts and two-byte (Type0) fonts share the same line breaking and measuring.
/// </summary>
[DebuggerDisplay("FormCodes: {Length} units of {_unitBytes} bytes")]
internal sealed class FormCodes
{
    /// <summary>The kind of an ordinary character.</summary>
    internal const byte Ordinary = 0;

    /// <summary>The kind of a space, where lines may break.</summary>
    internal const byte Space = 1;

    /// <summary>The kind of a line feed, which ends a line.</summary>
    internal const byte LineFeed = 2;

    /// <summary>The encoded bytes, <c>_unitBytes</c> per unit.</summary>
    private readonly byte[] _bytes;

    /// <summary>The bytes in one unit's code.</summary>
    private readonly int _unitBytes;

    /// <summary>Where each unit starts, for codes of different lengths; <see langword="null"/> when every unit takes <c>_unitBytes</c>.</summary>
    private readonly int[]? _offsets;

    /// <summary>The width of each unit in thousandths of an em.</summary>
    private readonly float[] _widths;

    /// <summary>The kind of each unit.</summary>
    private readonly byte[] _kinds;

    /// <summary>Initializes a new instance of the <see cref="FormCodes"/> class.</summary>
    /// <param name="bytes">The encoded bytes.</param>
    /// <param name="unitBytes">The bytes in one unit's code.</param>
    /// <param name="widths">The width of each unit in thousandths of an em.</param>
    /// <param name="kinds">The kind of each unit.</param>
    internal FormCodes(byte[] bytes, int unitBytes, float[] widths, byte[] kinds)
    {
        _bytes = bytes;
        _unitBytes = unitBytes;
        _widths = widths;
        _kinds = kinds;
    }

    /// <summary>Initializes a new instance of the <see cref="FormCodes"/> class for codes of different lengths.</summary>
    /// <param name="bytes">The encoded bytes.</param>
    /// <param name="offsets">Where each unit starts in <paramref name="bytes"/>, with the end of the last unit after them.</param>
    /// <param name="widths">The width of each unit in thousandths of an em.</param>
    /// <param name="kinds">The kind of each unit.</param>
    internal FormCodes(byte[] bytes, int[] offsets, float[] widths, byte[] kinds)
    {
        _bytes = bytes;
        _offsets = offsets;
        _widths = widths;
        _kinds = kinds;
    }

    /// <summary>Gets the number of units.</summary>
    internal int Length => _widths.Length;

    /// <summary>Gets the width of a unit.</summary>
    /// <param name="index">The unit.</param>
    /// <returns>The width in thousandths of an em.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal float GetWidth(int index) => _widths[index];

    /// <summary>Gets the kind of a unit.</summary>
    /// <param name="index">The unit.</param>
    /// <returns><see cref="Ordinary"/>, <see cref="Space"/> or <see cref="LineFeed"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte GetKind(int index) => _kinds[index];

    /// <summary>Gets the encoded bytes of a run of units.</summary>
    /// <param name="start">The first unit.</param>
    /// <param name="length">The number of units.</param>
    /// <returns>The bytes to show.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<byte> Slice(int start, int length) => _offsets is null
        ? _bytes.AsSpan(start * _unitBytes, length * _unitBytes)
        : _bytes.AsSpan(_offsets[start], _offsets[start + length] - _offsets[start]);

    /// <summary>Measures a run of units.</summary>
    /// <param name="start">The first unit.</param>
    /// <param name="length">The number of units.</param>
    /// <returns>The width in thousandths of an em.</returns>
    internal float Measure(int start, int length)
    {
        var total = 0F;
        for (var i = start; i < start + length; i++)
        {
            total += _widths[i];
        }

        return total;
    }

    /// <summary>Finds the next line feed.</summary>
    /// <param name="start">The unit to start looking at.</param>
    /// <returns>The unit, or -1.</returns>
    internal int IndexOfLineFeed(int start)
    {
        for (var i = start; i < _kinds.Length; i++)
        {
            if (_kinds[i] == LineFeed)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Counts the units of a run without its trailing spaces.</summary>
    /// <param name="start">The first unit.</param>
    /// <param name="length">The number of units.</param>
    /// <returns>The number of units up to the last non-space.</returns>
    internal int TrimmedLength(int start, int length)
    {
        var end = start + length;
        while (end > start && _kinds[end - 1] == Space)
        {
            end--;
        }

        return end - start;
    }
}
