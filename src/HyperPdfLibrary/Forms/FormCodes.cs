// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Field text encoded for a font: one unit per character, each a one-byte or two-byte code with its width. Layout works
/// in units, so one-byte fonts and two-byte (Type0) fonts share the same line breaking and measuring. An instance is
/// rented, filled, used and disposed, and its arrays are reused by the next text.
/// </summary>
[DebuggerDisplay("FormCodes: {Length} units")]
internal sealed class FormCodes : IDisposable
{
    /// <summary>The kind of an ordinary character.</summary>
    internal const byte Ordinary = 0;

    /// <summary>The kind of a space, where lines may break.</summary>
    internal const byte Space = 1;

    /// <summary>The kind of a line feed, which ends a line.</summary>
    internal const byte LineFeed = 2;

    /// <summary>The most units an idle instance keeps room for; a larger one is left to the collector.</summary>
    private const int MaxKeptUnits = 16_384;

    /// <summary>The most bytes one unit's code takes.</summary>
    private const int MaxUnitBytes = 2;

    /// <summary>The bits in a byte.</summary>
    private const int Bits = 8;

    /// <summary>The factor by which a full array grows.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The document-owned pool this instance returns to.</summary>
    private FormCodes?[]? _pool;

    /// <summary>The encoded bytes.</summary>
    private byte[] _bytes = [];

    /// <summary>The bytes in one unit's code; 0 when the codes differ in length.</summary>
    private int _unitBytes;

    /// <summary>Where each unit starts, with the end of the last unit after them; used when the codes differ in length.</summary>
    private int[] _offsets = [];

    /// <summary>The width of each unit in thousandths of an em.</summary>
    private float[] _widths = [];

    /// <summary>The kind of each unit.</summary>
    private byte[] _kinds = [];

    /// <summary>The number of units.</summary>
    private int _count;

    /// <summary>Whether the instance is held by a caller, so a second dispose is ignored.</summary>
    private bool _rented;

    /// <summary>Gets the number of units.</summary>
    internal int Length => _count;

    /// <summary>Gives the instance back for the next text.</summary>
    public void Dispose()
    {
        if (!_rented)
        {
            return;
        }

        _rented = false;
        if (_widths.Length > MaxKeptUnits || _pool is not { } pool)
        {
            return;
        }

        for (var i = 0; i < pool.Length; i++)
        {
            if (Interlocked.CompareExchange(ref pool[i], this, null) is null)
            {
                return;
            }
        }
    }

    /// <summary>Gets an instance to fill.</summary>
    /// <param name="pool">The owning document's idle instances, or null for an uncached operation.</param>
    /// <returns>The instance; dispose it to give its arrays back.</returns>
    internal static FormCodes Rent(FormCodes?[]? pool)
    {
        FormCodes? codes = null;
        for (var i = 0; pool is not null && i < pool.Length && codes is null; i++)
        {
            codes = Interlocked.Exchange(ref pool[i], null);
        }

        codes ??= new();
        codes._pool = pool;
        codes._rented = true;
        return codes;
    }

    /// <summary>Sets up room for a number of units.</summary>
    /// <param name="count">The number of units.</param>
    /// <param name="unitBytes">The bytes in each unit's code; 0 when the codes are one or two bytes long.</param>
    internal void Begin(int count, int unitBytes)
    {
        _count = count;
        _unitBytes = unitBytes;
        Ensure(ref _widths, count);
        Ensure(ref _kinds, count);
        if (unitBytes == 0)
        {
            Ensure(ref _offsets, count + 1);
            _offsets[0] = 0;
            Ensure(ref _bytes, count * MaxUnitBytes);
            return;
        }

        Ensure(ref _bytes, count * unitBytes);
    }

    /// <summary>Sets one unit.</summary>
    /// <param name="index">The unit.</param>
    /// <param name="code">The unit's code.</param>
    /// <param name="width">The unit's width in thousandths of an em.</param>
    /// <param name="kind">The unit's kind.</param>
    internal void Set(int index, FormCode code, float width, byte kind)
    {
        _widths[index] = width;
        _kinds[index] = kind;
        var offset = _unitBytes == 0 ? _offsets[index] : index * _unitBytes;
        if (_unitBytes == 0)
        {
            _offsets[index + 1] = offset + code.Length;
        }

        for (var i = 0; i < code.Length; i++)
        {
            _bytes[offset + code.Length - 1 - i] = (byte)(code.Value >> (i * Bits));
        }
    }

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
    internal ReadOnlySpan<byte> Slice(int start, int length) => _unitBytes != 0
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
        var offset = _kinds.AsSpan(start, _count - start).IndexOf(LineFeed);
        return offset < 0 ? -1 : start + offset;
    }

    /// <summary>Counts the units of a run without its trailing spaces.</summary>
    /// <param name="start">The first unit.</param>
    /// <param name="length">The number of units.</param>
    /// <returns>The number of units up to the last non-space.</returns>
    internal int TrimmedLength(int start, int length) =>
        _kinds.AsSpan(start, length).LastIndexOfAnyExcept(Space) + 1;

    /// <summary>Makes an array at least a size, keeping nothing of the old contents.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The array.</param>
    /// <param name="size">The elements needed.</param>
    private static void Ensure<T>(ref T[] array, int size)
    {
        if (array.Length < size)
        {
            array = new T[Math.Max(size, array.Length * GrowthFactor)];
        }
    }
}
