// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Functions;

/// <summary>A type 2 exponential interpolation function: y = C0 + x^N * (C1 - C0).</summary>
internal sealed class ExponentialFunction : PdfFunction
{
    /// <summary>The outputs at x = 0.</summary>
    private readonly float[] _c0;

    /// <summary>The differences C1 - C0.</summary>
    private readonly float[] _delta;

    /// <summary>The exponent.</summary>
    private readonly float _exponent;

    /// <summary>Initializes a new instance of the <see cref="ExponentialFunction"/> class.</summary>
    /// <param name="domain">The input domain.</param>
    /// <param name="range">The output range, or <see langword="null"/>.</param>
    /// <param name="c0">The outputs at x = 0.</param>
    /// <param name="delta">The differences C1 - C0.</param>
    /// <param name="exponent">The exponent.</param>
    private ExponentialFunction(float[] domain, float[]? range, float[] c0, float[] delta, float exponent)
        : base(domain, range, c0.Length)
    {
        _c0 = c0;
        _delta = delta;
        _exponent = exponent;
    }

    /// <summary>Parses an exponential function.</summary>
    /// <param name="dictionary">The function dictionary.</param>
    /// <param name="domain">The input domain.</param>
    /// <param name="range">The output range, or <see langword="null"/>.</param>
    /// <returns>The function, or <see langword="null"/> when invalid.</returns>
    internal static ExponentialFunction? Parse(PdfDictionary dictionary, float[] domain, float[]? range)
    {
        var c0Array = dictionary.GetArray(KnownName.C0);
        var exponent = dictionary.Get(KnownName.N);

        // ISO 32000-2 7.10.3 gives /C0 and /C1 the same length, which sets the output count. A damaged file may disagree, so
        // the count comes from /Range when present, else from /C0, and a shorter /C0 or /C1 is padded with its default (0 or 1).
        var outputs = range is not null ? range.Length / FunctionReader.PairSize : Math.Max(c0Array?.Count ?? 1, 1);
        if (domain.Length != FunctionReader.PairSize || !exponent.IsNumber || outputs > MaxComponents)
        {
            return null;
        }

        var c0 = ReadPadded(c0Array, outputs, 0);
        var c1 = ReadPadded(dictionary.GetArray(KnownName.C1), outputs, 1);
        var delta = new float[c0.Length];
        for (var i = 0; i < delta.Length; i++)
        {
            delta[i] = c1[i] - c0[i];
        }

        return new(domain, range, c0, delta, exponent.AsSingle());
    }

    /// <inheritdoc/>
    private protected override void EvaluateCore(ReadOnlySpan<float> input, Span<float> output)
    {
        var x = input[0];

        // Fast paths for the common linear and constant exponents avoid the power call.
        var scale = _exponent switch
        {
            1 => x,
            0 => 1,
            _ => MathF.Pow(x, _exponent),
        };

        if (!float.IsFinite(scale))
        {
            scale = 0;
        }

        for (var i = 0; i < output.Length; i++)
        {
            output[i] = _c0[i] + (scale * _delta[i]);
        }
    }

    /// <summary>Reads an output array to an exact length; a missing array gives the default and missing items give zero.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <param name="length">The number of outputs.</param>
    /// <param name="missing">The value used for every output when the array is absent.</param>
    /// <returns>The values.</returns>
    private static float[] ReadPadded(PdfArray? array, int length, float missing)
    {
        var values = new float[length];
        if (array is null)
        {
            values.AsSpan().Fill(missing);
            return values;
        }

        for (var i = 0; i < Math.Min(length, array.Count); i++)
        {
            values[i] = array.Get(i).AsSingle();
        }

        return values;
    }
}
