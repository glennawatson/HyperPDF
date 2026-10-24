// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.Mp4;

/// <summary>
/// Every sample of every track of a file, as parallel arrays so that a million samples cost a few arrays and not a million objects.
/// Samples are added track by track; <see cref="SortByOffset"/> then puts them in the order the file stores them.
/// </summary>
[DebuggerDisplay("Mp4SampleList: {Count} samples")]
internal sealed class Mp4SampleList
{
    /// <summary>The samples the arrays hold at first.</summary>
    private const int InitialCapacity = 1024;

    /// <summary>How many times bigger the arrays become when they are full.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The file offset of each sample.</summary>
    private long[] _offsets = new long[InitialCapacity];

    /// <summary>The size of each sample.</summary>
    private int[] _sizes = new int[InitialCapacity];

    /// <summary>The track of each sample.</summary>
    private int[] _tracks = new int[InitialCapacity];

    /// <summary>Whether decoding can start at each sample.</summary>
    private bool[] _keys = new bool[InitialCapacity];

    /// <summary>When each sample is shown, in seconds.</summary>
    private double[] _times = new double[InitialCapacity];

    /// <summary>When each sample is decoded, in seconds.</summary>
    private double[] _decodeTimes = new double[InitialCapacity];

    /// <summary>How long each sample lasts, in seconds.</summary>
    private double[] _durations = new double[InitialCapacity];

    /// <summary>Gets the number of samples.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets the file offset of a sample.</summary>
    /// <param name="index">The sample.</param>
    /// <returns>The offset.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal long Offset(int index) => _offsets[index];

    /// <summary>Gets the size of a sample.</summary>
    /// <param name="index">The sample.</param>
    /// <returns>The size in bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Size(int index) => _sizes[index];

    /// <summary>Gets the track of a sample.</summary>
    /// <param name="index">The sample.</param>
    /// <returns>The track's number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Track(int index) => _tracks[index];

    /// <summary>Gets whether decoding can start at a sample.</summary>
    /// <param name="index">The sample.</param>
    /// <returns><see langword="true"/> for a key sample.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsKey(int index) => _keys[index];

    /// <summary>Gets when a sample is shown.</summary>
    /// <param name="index">The sample.</param>
    /// <returns>The time in seconds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal double Time(int index) => _times[index];

    /// <summary>Gets when a sample is decoded.</summary>
    /// <param name="index">The sample.</param>
    /// <returns>The time in seconds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal double DecodeTime(int index) => _decodeTimes[index];

    /// <summary>Gets how long a sample lasts.</summary>
    /// <param name="index">The sample.</param>
    /// <returns>The duration in seconds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal double Duration(int index) => _durations[index];

    /// <summary>Adds a sample.</summary>
    /// <param name="offset">The file offset.</param>
    /// <param name="size">The size in bytes.</param>
    /// <param name="track">The track's number.</param>
    /// <param name="isKey">Whether decoding can start here.</param>
    /// <param name="time">When the sample is shown, in seconds.</param>
    /// <param name="decodeTime">When the sample is decoded, in seconds.</param>
    /// <param name="duration">How long it lasts, in seconds.</param>
    internal void Add(long offset, int size, int track, bool isKey, double time, double decodeTime, double duration)
    {
        if (Count == _offsets.Length)
        {
            Grow();
        }

        _offsets[Count] = offset;
        _sizes[Count] = size;
        _tracks[Count] = track;
        _keys[Count] = isKey;
        _times[Count] = time;
        _decodeTimes[Count] = decodeTime;
        _durations[Count] = duration;
        Count++;
    }

    /// <summary>Puts the samples in the order of their offsets in the file, which is the order they are read in.</summary>
    internal void SortByOffset()
    {
        var order = new int[Count];
        for (var index = 0; index < order.Length; index++)
        {
            order[index] = index;
        }

        Array.Sort(_offsets.AsSpan(0, Count).ToArray(), order);
        _offsets = Permute(_offsets, order);
        _sizes = Permute(_sizes, order);
        _tracks = Permute(_tracks, order);
        _keys = Permute(_keys, order);
        _times = Permute(_times, order);
        _decodeTimes = Permute(_decodeTimes, order);
        _durations = Permute(_durations, order);
    }

    /// <summary>Finds the first sample at or after a file offset.</summary>
    /// <param name="offset">The offset.</param>
    /// <returns>The index, which is <see cref="Count"/> when every sample is before the offset.</returns>
    internal int LowerBound(long offset)
    {
        var low = 0;
        var high = Count;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            if (_offsets[middle] < offset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    /// <summary>Makes a copy of an array with its first items put in a new order.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The array.</param>
    /// <param name="order">For each place in the result, the item of the source that goes there.</param>
    /// <returns>The new array.</returns>
    private static T[] Permute<T>(T[] source, int[] order)
    {
        var result = new T[source.Length];
        for (var index = 0; index < order.Length; index++)
        {
            result[index] = source[order[index]];
        }

        return result;
    }

    /// <summary>Makes the arrays bigger.</summary>
    private void Grow()
    {
        var size = _offsets.Length * GrowthFactor;
        Array.Resize(ref _offsets, size);
        Array.Resize(ref _sizes, size);
        Array.Resize(ref _tracks, size);
        Array.Resize(ref _keys, size);
        Array.Resize(ref _times, size);
        Array.Resize(ref _decodeTimes, size);
        Array.Resize(ref _durations, size);
    }
}
