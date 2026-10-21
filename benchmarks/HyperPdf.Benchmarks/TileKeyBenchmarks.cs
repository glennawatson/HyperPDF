// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures hashing and comparing tile keys, which every cache lookup does.</summary>
public class TileKeyBenchmarks
{
    /// <summary>The number of keys.</summary>
    private const int Count = 512;

    /// <summary>Tiles per page.</summary>
    private const int TilesPerPage = 16;

    /// <summary>Tile columns per page.</summary>
    private const int Columns = 4;

    /// <summary>The scale key used for every tile.</summary>
    private const int ScaleKey = 2048;

    /// <summary>The keys.</summary>
    private readonly TileKey[] _keys = new TileKey[Count];

    /// <summary>A copy of the keys, equal one by one.</summary>
    private readonly TileKey[] _copies = new TileKey[Count];

    /// <summary>Builds the keys.</summary>
    [GlobalSetup]
    public void Setup()
    {
        for (var i = 0; i < Count; i++)
        {
            _keys[i] = Key(i);
            _copies[i] = Key(i);
        }
    }

    /// <summary>Hashes every key.</summary>
    /// <returns>The sum of the hashes.</returns>
    [Benchmark]
    public int GetHashCodeOf512Keys()
    {
        var sum = 0;
        foreach (var key in _keys)
        {
            sum += key.GetHashCode();
        }

        return sum;
    }

    /// <summary>Compares every key with an equal copy.</summary>
    /// <returns>The number equal.</returns>
    [Benchmark]
    public int EqualsOf512EqualKeys()
    {
        var equal = 0;
        for (var i = 0; i < Count; i++)
        {
            if (_keys[i].Equals(_copies[i]))
            {
                equal++;
            }
        }

        return equal;
    }

    /// <summary>Creates a key.</summary>
    /// <param name="index">The tile index.</param>
    /// <returns>The key.</returns>
    private static TileKey Key(int index) => new(1, index / TilesPerPage, ScaleKey, PageRotation.None, 0, (short)(index % Columns), (short)(index % TilesPerPage / Columns));
}
