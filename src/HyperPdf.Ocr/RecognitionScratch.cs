// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdf.Ocr.Layout;
using HyperPdf.Ocr.Lstm;

namespace HyperPdf.Ocr;

/// <summary>
/// The working memory of one text recogniser: arrays and grids of values handed out and taken back, so a page is read
/// without allocating once the recogniser has warmed up. It belongs to the recogniser that made it, which is used by one
/// thread at a time.
/// </summary>
[DebuggerDisplay("RecognitionScratch: {_floatFree.Count} float arrays, {_byteFree.Count} byte arrays free")]
public sealed class RecognitionScratch
{
    /// <summary>The seed the noise generator starts from before a model is run.</summary>
    private const int DefaultSeed = 0;

    /// <summary>The arrays of floats not in use, in no particular order.</summary>
    private readonly List<float[]> _floatFree = [];

    /// <summary>The arrays of bytes not in use, in no particular order.</summary>
    private readonly List<byte[]> _byteFree = [];

    /// <summary>The grids not in use.</summary>
    private readonly List<FeatureMap> _mapFree = [];

    /// <summary>The character a decoder is part way through, made when first needed.</summary>
    private PendingCharacter? _pending;

    /// <summary>Gets the generator that fills the area beyond a picture's edge. A model reseeds it before each run.</summary>
    public LehmerRandom Random { get; } = new(DefaultSeed);

    /// <summary>Gets the characters of the reading being tried, reused for each line.</summary>
    internal List<RecognizedCharacter> CandidateCharacters { get; } = [];

    /// <summary>Gets the characters of the best reading so far, reused for each line.</summary>
    internal List<RecognizedCharacter> BestCharacters { get; } = [];

    /// <summary>Gets the mask of the page being read.</summary>
    internal InkMask Ink { get; } = new();

    /// <summary>Gets the runs of ink on the page being read.</summary>
    internal RunLists Runs { get; } = new();

    /// <summary>Gets the blobs of ink on the page being read.</summary>
    internal List<ComponentBox> Components { get; } = [];

    /// <summary>Gets the lines of text on the page being read.</summary>
    internal List<TextLine> Lines { get; } = [];

    /// <summary>Gets the blobs of the page that could be letters.</summary>
    internal List<ComponentBox> Letters { get; } = [];

    /// <summary>Gets the heights of the blobs of the page.</summary>
    internal List<int> Heights { get; } = [];

    /// <summary>Gets the lines being built from the blobs.</summary>
    internal List<TextLineFinder.LineBuilder> LineBuilders { get; } = [];

    /// <summary>Gets the word being read off a line.</summary>
    internal WordInProgress Word { get; } = new();

    /// <summary>Gets a grid of values with undefined contents.</summary>
    /// <param name="height">The number of rows.</param>
    /// <param name="width">The number of steps in a row.</param>
    /// <param name="depth">The number of features in a step.</param>
    /// <returns>The grid. Give it back with <see cref="Return(FeatureMap)"/>.</returns>
    public FeatureMap RentMap(int height, int width, int depth)
    {
        var values = RentFloats(checked(height * width * depth));
        if (_mapFree.Count == 0)
        {
            return new(height, width, depth, values);
        }

        var map = _mapFree[^1];
        _mapFree.RemoveAt(_mapFree.Count - 1);
        map.Reset(height, width, depth, values);
        return map;
    }

    /// <summary>Gets an array of floats with undefined contents.</summary>
    /// <param name="length">The least length needed.</param>
    /// <returns>The array, at least as long as needed. Give it back with <see cref="Return(float[])"/>.</returns>
    public float[] RentFloats(int length)
    {
        var size = RoundUp(length);
        for (var i = _floatFree.Count - 1; i >= 0; i--)
        {
            if (_floatFree[i].Length != size)
            {
                continue;
            }

            var found = _floatFree[i];
            _floatFree.RemoveAt(i);
            return found;
        }

        return new float[size];
    }

    /// <summary>Gets an array of floats with every value zero.</summary>
    /// <param name="length">The least length needed.</param>
    /// <returns>The array, at least as long as needed. Give it back with <see cref="Return(float[])"/>.</returns>
    public float[] RentZeroedFloats(int length)
    {
        var values = RentFloats(length);
        values.AsSpan(0, length).Clear();
        return values;
    }

    /// <summary>Gets an array of bytes with undefined contents.</summary>
    /// <param name="length">The least length needed.</param>
    /// <returns>The array, at least as long as needed. Give it back with <see cref="Return(byte[])"/>.</returns>
    public byte[] RentBytes(int length)
    {
        var size = RoundUp(length);
        for (var i = _byteFree.Count - 1; i >= 0; i--)
        {
            if (_byteFree[i].Length != size)
            {
                continue;
            }

            var found = _byteFree[i];
            _byteFree.RemoveAt(i);
            return found;
        }

        return new byte[size];
    }

    /// <summary>Gives a grid back.</summary>
    /// <param name="map">The grid; it must not be used again.</param>
    public void Return(FeatureMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        Return(map.Values);
        _mapFree.Add(map);
    }

    /// <summary>Gives an array of floats back.</summary>
    /// <param name="values">The array; it must not be used again.</param>
    public void Return(float[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _floatFree.Add(values);
    }

    /// <summary>Gives an array of bytes back.</summary>
    /// <param name="values">The array; it must not be used again.</param>
    public void Return(byte[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _byteFree.Add(values);
    }

    /// <summary>Gets the character a decoder is part way through, ready to start at the start of a coder's tree.</summary>
    /// <param name="start">The start of the tree of code runs.</param>
    /// <returns>The character, forgotten and at the start.</returns>
    internal PendingCharacter StartCharacter(CharacterCoder.CodeNode start)
    {
        if (_pending is null)
        {
            _pending = new(start);
            return _pending;
        }

        _pending.Reset(start);
        return _pending;
    }

    /// <summary>Rounds a length up to a power of two, so arrays of nearly the same length are interchangeable.</summary>
    /// <param name="length">The length.</param>
    /// <returns>The rounded length, at least 1.</returns>
    private static int RoundUp(int length) => length <= 1 ? 1 : (int)BitOperations.RoundUpToPowerOf2((uint)length);
}
