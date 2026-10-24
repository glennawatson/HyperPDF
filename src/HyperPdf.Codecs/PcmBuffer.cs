// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs;

/// <summary>
/// Decoded sound: interleaved 32 bit float samples between -1 and 1. A decoder appends to the buffer, the reader of the sound takes
/// what is there and clears it, and the same array is used again so decoding allocates only until the buffer is as big as the biggest frame.
/// </summary>
[DebuggerDisplay("PcmBuffer: {Count} samples")]
public sealed class PcmBuffer
{
    /// <summary>How many times bigger the array becomes when it is full.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The samples, which can be longer than the sound held.</summary>
    private float[] _samples = [];

    /// <summary>Gets the number of samples held, counting every channel.</summary>
    public int Count { get; private set; }

    /// <summary>Gets the samples held.</summary>
    public ReadOnlySpan<float> Samples => _samples.AsSpan(0, Count);

    /// <summary>Makes room for more samples at the end and counts them as held. The caller fills them.</summary>
    /// <param name="count">The number of samples to add, counting every channel.</param>
    /// <returns>The place for the new samples.</returns>
    public Span<float> Append(int count)
    {
        var needed = Count + count;
        if (_samples.Length < needed)
        {
            Array.Resize(ref _samples, Math.Max(needed, _samples.Length * GrowthFactor));
        }

        var added = _samples.AsSpan(Count, count);
        Count = needed;
        return added;
    }

    /// <summary>Takes back samples that were counted but not filled, such as the end of a frame that turned out shorter.</summary>
    /// <param name="count">The number of samples to drop from the end.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Trim(int count) => Count = Math.Max(0, Count - count);

    /// <summary>Empties the buffer so the sound can be appended again.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => Count = 0;
}
