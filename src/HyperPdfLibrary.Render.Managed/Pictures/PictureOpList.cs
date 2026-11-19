// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Render.Managed.Pictures;

/// <summary>
/// The steps of a picture while it is recorded. A page of text has thousands of steps, so the room for them comes from the pool
/// and goes back to it: growing the list leaves nothing behind, and only the finished picture's own array is made.
/// </summary>
[DebuggerDisplay("PictureOpList: {Count} steps")]
internal sealed class PictureOpList : IDisposable
{
    /// <summary>The steps the list has room for when it is first used.</summary>
    private const int FirstRoom = 256;

    /// <summary>The steps, in an array from the pool. Empty until the first step is added.</summary>
    private PictureOp[] _ops = [];

    /// <summary>Gets the number of steps recorded.</summary>
    internal int Count { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Release(_ops);
        _ops = [];
        Count = 0;
    }

    /// <summary>Adds a step.</summary>
    /// <param name="op">The step.</param>
    internal void Add(in PictureOp op)
    {
        if (Count == _ops.Length)
        {
            Grow();
        }

        _ops[Count] = op;
        Count++;
    }

    /// <summary>Copies the steps into an array of their own, which the finished picture keeps.</summary>
    /// <returns>The steps.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PictureOp[] ToArray() => _ops.AsSpan(0, Count).ToArray();

    /// <summary>Gives an array back to the pool with the steps it held forgotten, so the pool keeps no path or image alive.</summary>
    /// <param name="ops">The array.</param>
    private static void Release(PictureOp[] ops)
    {
        if (ops.Length == 0)
        {
            return;
        }

        ArrayPool<PictureOp>.Shared.Return(ops, clearArray: true);
    }

    /// <summary>Moves the steps into an array from the pool with twice the room.</summary>
    private void Grow()
    {
        var larger = ArrayPool<PictureOp>.Shared.Rent(Math.Max(FirstRoom, _ops.Length + _ops.Length));
        _ops.AsSpan(0, Count).CopyTo(larger);
        Release(_ops);
        _ops = larger;
    }
}
