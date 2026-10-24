// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.InteropServices;

namespace HyperPdf.Ocr.Lstm;

/// <summary>
/// The squashing functions of the layers. A whole vector of values goes through one polynomial approximation of the
/// exponential, accurate to about a millionth, where the model was trained with a table of 4096 values read with linear
/// interpolation. A scalar lookup of that table gives the values for the few left over.
/// </summary>
public static class Activations
{
    /// <summary>The number of entries in a table.</summary>
    private const int TableSize = 4096;

    /// <summary>The number of table entries for each whole unit of input.</summary>
    private const float Scale = 256F;

    /// <summary>The largest softmax input below the top, which keeps a probability above zero.</summary>
    private const float MaxSoftmaxActivation = 86F;

    /// <summary>The input beyond which the tangent is 1 to within a float's precision.</summary>
    private const float TanhLimit = 9F;

    /// <summary>The input beyond which the logistic function is 0 or 1 to within a float's precision.</summary>
    private const float LogisticLimit = 20F;

    /// <summary>The largest input the exponential takes before it would overflow a float.</summary>
    private const float ExpLimit = 88F;

    /// <summary>The logarithm of the base of the exponential, base 2.</summary>
    private const float LogTwoE = 1.44269504088896341F;

    /// <summary>The high part of the natural logarithm of 2, with few enough bits that products with it are exact.</summary>
    private const float LogTwoHigh = 0.693359375F;

    /// <summary>The low part of the natural logarithm of 2, to be subtracted after the high part.</summary>
    private const float LogTwoLow = -2.12194440e-4F;

    /// <summary>The coefficient of the fifth power of the polynomial approximating the exponential on a small range.</summary>
    private const float Poly5 = 1.9875691500E-4F;

    /// <summary>The coefficient of the fourth power.</summary>
    private const float Poly4 = 1.3981999507E-3F;

    /// <summary>The coefficient of the third power.</summary>
    private const float Poly3 = 8.3334519073E-3F;

    /// <summary>The coefficient of the second power.</summary>
    private const float Poly2 = 4.1665795894E-2F;

    /// <summary>The coefficient of the first power.</summary>
    private const float Poly1 = 1.6666665459E-1F;

    /// <summary>The coefficient of the zeroth power.</summary>
    private const float Poly0 = 5.0000001201E-1F;

    /// <summary>The bias of a float's exponent.</summary>
    private const int ExponentBias = 127;

    /// <summary>The number of bits below a float's exponent.</summary>
    private const int MantissaBits = 23;

    /// <summary>One half.</summary>
    private const float Half = 0.5F;

    /// <summary>The number two, used to double an input.</summary>
    private const float Two = 2F;

    /// <summary>The hyperbolic tangent at each table entry.</summary>
    private static readonly float[] TanhTable = BuildTable(Math.Tanh);

    /// <summary>The logistic function at each table entry.</summary>
    private static readonly float[] LogisticTable = BuildTable(static x => 1.0 / (1.0 + Math.Exp(-x)));

    /// <summary>Gets the hyperbolic tangent from the table.</summary>
    /// <param name="x">The input.</param>
    /// <returns>The output, from -1 to 1.</returns>
    public static float Tanh(float x) => x < 0F ? -Interpolate(TanhTable, -x) : Interpolate(TanhTable, x);

    /// <summary>Gets the hyperbolic tangent of every value in a vector.</summary>
    /// <param name="x">The inputs.</param>
    /// <returns>The outputs, from -1 to 1.</returns>
    public static Vector<float> Tanh(Vector<float> x)
    {
        var limited = Vector.Min(Vector.Max(x, Vector.Create(-TanhLimit)), Vector.Create(TanhLimit));
        var exponential = Exp(limited * Two);
        return Vector<float>.One - (Vector.Create(Two) / (exponential + Vector<float>.One));
    }

    /// <summary>Applies the hyperbolic tangent in place.</summary>
    /// <param name="values">The values.</param>
    public static void Tanh(Span<float> values)
    {
        var width = Vector<float>.Count;
        var i = 0;
        if (values.Length >= width)
        {
            var vectors = MemoryMarshal.Cast<float, Vector<float>>(values);
            for (var v = 0; v < vectors.Length; v++)
            {
                vectors[v] = Tanh(vectors[v]);
            }

            i = vectors.Length * width;
        }

        for (; i < values.Length; i++)
        {
            values[i] = Tanh(values[i]);
        }
    }

    /// <summary>Gets the logistic function from the table.</summary>
    /// <param name="x">The input.</param>
    /// <returns>The output, from 0 to 1.</returns>
    public static float Logistic(float x) => x < 0F ? 1F - Interpolate(LogisticTable, -x) : Interpolate(LogisticTable, x);

    /// <summary>Gets the logistic function of every value in a vector.</summary>
    /// <param name="x">The inputs.</param>
    /// <returns>The outputs, from 0 to 1.</returns>
    public static Vector<float> Logistic(Vector<float> x)
    {
        var limited = Vector.Min(Vector.Max(x, Vector.Create(-LogisticLimit)), Vector.Create(LogisticLimit));
        return Vector<float>.One / (Vector<float>.One + Exp(-limited));
    }

    /// <summary>Applies the logistic function in place.</summary>
    /// <param name="values">The values.</param>
    public static void Logistic(Span<float> values)
    {
        var width = Vector<float>.Count;
        var i = 0;
        if (values.Length >= width)
        {
            var vectors = MemoryMarshal.Cast<float, Vector<float>>(values);
            for (var v = 0; v < vectors.Length; v++)
            {
                vectors[v] = Logistic(vectors[v]);
            }

            i = vectors.Length * width;
        }

        for (; i < values.Length; i++)
        {
            values[i] = Logistic(values[i]);
        }
    }

    /// <summary>Applies the rectifier in place.</summary>
    /// <param name="values">The values.</param>
    public static void Relu(Span<float> values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = Math.Max(values[i], 0F);
        }
    }

    /// <summary>Clips values to 0 to 1 in place.</summary>
    /// <param name="values">The values.</param>
    public static void ClipUnit(Span<float> values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = Math.Clamp(values[i], 0F, 1F);
        }
    }

    /// <summary>Clips values to -1 to 1 in place.</summary>
    /// <param name="values">The values.</param>
    public static void ClipSigned(Span<float> values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = Math.Clamp(values[i], -1F, 1F);
        }
    }

    /// <summary>Turns scores into probabilities that add to 1, in place.</summary>
    /// <param name="values">The scores.</param>
    public static void Softmax(Span<float> values)
    {
        if (values.IsEmpty)
        {
            return;
        }

        var max = values[0];
        foreach (var value in values[1..])
        {
            max = Math.Max(max, value);
        }

        var total = 0F;
        for (var i = 0; i < values.Length; i++)
        {
            var probability = MathF.Exp(Math.Clamp(values[i] - max, -MaxSoftmaxActivation, 0F));
            total += probability;
            values[i] = probability;
        }

        if (total <= 0F)
        {
            return;
        }

        for (var i = 0; i < values.Length; i++)
        {
            values[i] /= total;
        }
    }

    /// <summary>
    /// Gets <c>e</c> to the power of every value in a vector. The input is split into a whole number of halvings of
    /// <c>ln 2</c> and a small remainder; a polynomial gives the exponential of the remainder and the whole number is
    /// put straight into the exponent of the result.
    /// </summary>
    /// <param name="x">The inputs.</param>
    /// <returns>The outputs.</returns>
    private static Vector<float> Exp(Vector<float> x)
    {
        var limited = Vector.Min(Vector.Max(x, Vector.Create(-ExpLimit)), Vector.Create(ExpLimit));
        var count = Vector.Floor((limited * LogTwoE) + Vector.Create(Half));
        var remainder = limited - (count * LogTwoHigh) - (count * LogTwoLow);
        var square = remainder * remainder;
        var poly = Vector.Create(Poly5);
        poly = Vector.FusedMultiplyAdd(poly, remainder, Vector.Create(Poly4));
        poly = Vector.FusedMultiplyAdd(poly, remainder, Vector.Create(Poly3));
        poly = Vector.FusedMultiplyAdd(poly, remainder, Vector.Create(Poly2));
        poly = Vector.FusedMultiplyAdd(poly, remainder, Vector.Create(Poly1));
        poly = Vector.FusedMultiplyAdd(poly, remainder, Vector.Create(Poly0));
        poly = Vector.FusedMultiplyAdd(poly, square, remainder + Vector<float>.One);
        var exponent = Vector.ShiftLeft(Vector.ConvertToInt32(count) + Vector.Create(ExponentBias), MantissaBits);
        return poly * exponent.As<int, float>();
    }

    /// <summary>Reads a table at a positive input with linear interpolation.</summary>
    /// <param name="table">The table.</param>
    /// <param name="x">The input, zero or more.</param>
    /// <returns>The output.</returns>
    private static float Interpolate(float[] table, float x)
    {
        var scaled = x * Scale;
        var index = (uint)scaled;
        if (index >= TableSize - 1)
        {
            return 1F;
        }

        var low = table[index];
        return low + ((table[index + 1] - low) * (scaled - index));
    }

    /// <summary>Builds a table of a function at each step of the input.</summary>
    /// <param name="function">The function.</param>
    /// <returns>The table.</returns>
    private static float[] BuildTable(Func<double, double> function)
    {
        var table = new float[TableSize];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = (float)function(i / (double)Scale);
        }

        return table;
    }
}
