// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace HyperPdf.Ocr.Lstm;

/// <summary>The values passing between layers: a grid of steps, row after row, each step holding <see cref="Depth"/> features next to each other.</summary>
[DebuggerDisplay("FeatureMap: {Height} rows of {Width} steps of {Depth} features")]
public sealed class FeatureMap
{
    /// <summary>Initializes a new instance of the <see cref="FeatureMap"/> class with its own array of zeros.</summary>
    /// <param name="height">The number of rows.</param>
    /// <param name="width">The number of steps in a row.</param>
    /// <param name="depth">The number of features in a step.</param>
    public FeatureMap(int height, int width, int depth)
        : this(height, width, depth, new float[checked(height * width * depth)])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FeatureMap"/> class over an array.</summary>
    /// <param name="height">The number of rows.</param>
    /// <param name="width">The number of steps in a row.</param>
    /// <param name="depth">The number of features in a step.</param>
    /// <param name="values">The array, at least <c>height * width * depth</c> long.</param>
    internal FeatureMap(int height, int width, int depth, float[] values) => Reset(height, width, depth, values);

    /// <summary>Gets the number of rows.</summary>
    public int Height { get; private set; }

    /// <summary>Gets the number of steps in a row.</summary>
    public int Width { get; private set; }

    /// <summary>Gets the number of features in a step.</summary>
    public int Depth { get; private set; }

    /// <summary>Gets the array holding the values. It may be longer than <see cref="Length"/>.</summary>
    public float[] Values { get; private set; }

    /// <summary>Gets the number of values in the grid.</summary>
    public int Length => Height * Width * Depth;

    /// <summary>Gets the values of the grid.</summary>
    public Span<float> Span => Values.AsSpan(0, Length);

    /// <summary>Gets the number of steps.</summary>
    public int Steps => Height * Width;

    /// <summary>Gets the features of a step.</summary>
    /// <param name="step">The step, counted row after row.</param>
    /// <returns>The features.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> Step(int step) => Values.AsSpan(step * Depth, Depth);

    /// <summary>Gets the features of the step at a place.</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The step in the row.</param>
    /// <returns>The features.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> At(int row, int column) => Step((row * Width) + column);

    /// <summary>Makes the grid a different shape over a different array.</summary>
    /// <param name="height">The number of rows.</param>
    /// <param name="width">The number of steps in a row.</param>
    /// <param name="depth">The number of features in a step.</param>
    /// <param name="values">The array, at least <c>height * width * depth</c> long.</param>
    [MemberNotNull(nameof(Values))]
    internal void Reset(int height, int width, int depth, float[] values)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        ArgumentOutOfRangeException.ThrowIfLessThan(values.Length, checked(height * width * depth));
        Height = height;
        Width = width;
        Depth = depth;
        Values = values;
    }
}
