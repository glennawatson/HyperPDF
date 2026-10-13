// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Functions;

/// <summary>
/// The operand stack of a PostScript calculator program, over caller-provided storage. ISO 32000-2 7.10.5 makes popping an
/// empty stack (stackunderflow), pushing onto a full stack (stackoverflow) and an operand count or index out of range
/// (rangecheck) errors. Here the first error sets <see cref="Failed"/>; the operation then does nothing, and the program
/// stops without throwing, so the caller decides what a failed program produces.
/// </summary>
internal ref struct PostScriptStack
{
    /// <summary>The storage.</summary>
    private readonly Span<PostScriptValue> _items;

    /// <summary>Initializes a new instance of the <see cref="PostScriptStack"/> struct.</summary>
    /// <param name="items">The storage; its length is the stack limit.</param>
    internal PostScriptStack(Span<PostScriptValue> items) => _items = items;

    /// <summary>Gets the number of operands.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets a value indicating whether the program raised a PostScript error.</summary>
    internal bool Failed { get; private set; }

    /// <summary>Converts a number to an integer, saturating at the integer limits; NaN becomes zero.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Saturate(double value)
    {
        if (!(value > int.MinValue))
        {
            return value <= int.MinValue ? int.MinValue : 0;
        }

        return value >= int.MaxValue ? int.MaxValue : (int)value;
    }

    /// <summary>Records a PostScript error: stackunderflow, stackoverflow, rangecheck or undefinedresult.</summary>
    internal void Fail() => Failed = true;

    /// <summary>Pushes an operand; pushing onto a full stack is a stackoverflow error.</summary>
    /// <param name="value">The operand.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Push(PostScriptValue value)
    {
        if (Count == _items.Length)
        {
            Failed = true;
            return;
        }

        _items[Count] = value;
        Count++;
    }

    /// <summary>Pops an operand.</summary>
    /// <returns>The operand, or zero after a stackunderflow error.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PostScriptValue Pop()
    {
        if (Count == 0)
        {
            Failed = true;
            return default;
        }

        Count--;
        return _items[Count];
    }

    /// <summary>Pops an operand as an integer; reals truncate and out-of-range values saturate.</summary>
    /// <returns>The integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int PopInteger() => Saturate(Pop().Number);

    /// <summary>Gets an operand counted from the top.</summary>
    /// <param name="depth">Zero for the top operand.</param>
    /// <returns>The operand, or zero when the stack is not that deep.</returns>
    internal readonly PostScriptValue Peek(int depth) => (uint)depth < (uint)Count ? _items[Count - 1 - depth] : default;

    /// <summary>Duplicates the top operands; a count that is negative or deeper than the stack is a rangecheck or stackunderflow error, and one that overflows is a stackoverflow error.</summary>
    /// <param name="count">The number of operands.</param>
    internal void Copy(int count)
    {
        if (count < 0 || count > Count || Count + count > _items.Length)
        {
            Failed = true;
            return;
        }

        _items.Slice(Count - count, count).CopyTo(_items[Count..]);
        Count += count;
    }

    /// <summary>Rolls the top operands; a negative count or one deeper than the stack is a rangecheck or stackunderflow error. A count of zero rolls nothing.</summary>
    /// <param name="count">The number of operands rolled.</param>
    /// <param name="shift">The positions moved towards the top; negative moves towards the bottom.</param>
    internal void Roll(int count, int shift)
    {
        if (count < 0 || count > Count)
        {
            Failed = true;
            return;
        }

        if (count == 0)
        {
            return;
        }

        var amount = ((shift % count) + count) % count;
        if (amount == 0)
        {
            return;
        }

        var window = _items.Slice(Count - count, count);
        window.Reverse();
        window[..amount].Reverse();
        window[amount..].Reverse();
    }

    /// <summary>Gets the operands from the bottom up.</summary>
    /// <returns>The operands.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly ReadOnlySpan<PostScriptValue> AsSpan() => _items[..Count];
}
