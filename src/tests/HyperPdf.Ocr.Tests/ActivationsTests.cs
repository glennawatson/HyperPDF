// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Ocr.Lstm;

namespace HyperPdf.Ocr.Tests;

/// <summary>Checks the squashing functions against the exact ones.</summary>
public sealed class ActivationsTests
{
    /// <summary>How far a table lookup may be from the exact value.</summary>
    private const double Tolerance = 1e-4;

    /// <summary>How far a vector result may be from the exact value.</summary>
    private const double VectorTolerance = 2e-6;

    /// <summary>The number of inputs in the sweep, which is not a whole number of vectors.</summary>
    private const int SweepCount = 12_003;

    /// <summary>The first input of the sweep.</summary>
    private const float SweepStart = -30F;

    /// <summary>The distance between inputs of the sweep.</summary>
    private const float SweepStep = 0.005F;

    /// <summary>An input past the end of the tables.</summary>
    private const float Large = 40F;

    /// <summary>The scores softmax is tested with.</summary>
    private static readonly float[] Scores = [1F, 2F, 3F, -1000F];

    /// <summary>The inputs the functions are compared at.</summary>
    private static readonly float[] Points = [-6F, -2.5F, -0.3F, 0F, 0.01F, 0.7F, 3.2F, 7.9F];

    /// <summary>The table lookup of the hyperbolic tangent is close to the exact value everywhere, and saturates.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TanhFollowsTheExactFunction()
    {
        foreach (var x in Points)
        {
            await Assert.That(Math.Abs(Activations.Tanh(x) - Math.Tanh(x))).IsLessThan(Tolerance);
        }

        await Assert.That(Activations.Tanh(Large)).IsEqualTo(1F);
        await Assert.That(Activations.Tanh(-Large)).IsEqualTo(-1F);
    }

    /// <summary>The table lookup of the logistic function is close to the exact value everywhere, and saturates.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LogisticFollowsTheExactFunction()
    {
        foreach (var x in Points)
        {
            await Assert.That(Math.Abs(Activations.Logistic(x) - (1.0 / (1.0 + Math.Exp(-x))))).IsLessThan(Tolerance);
        }

        await Assert.That(Activations.Logistic(Large)).IsEqualTo(1F);
        await Assert.That(Activations.Logistic(-Large)).IsEqualTo(0F);
    }

    /// <summary>The vector versions are close to the exact functions over a dense sweep, including far past where they saturate.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VectorVersionsFollowTheExactFunctions()
    {
        var values = new float[SweepCount];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = SweepStart + (i * SweepStep);
        }

        var tanh = (float[])values.Clone();
        var logistic = (float[])values.Clone();
        Activations.Tanh(tanh);
        Activations.Logistic(logistic);

        for (var i = 0; i < values.Length; i++)
        {
            await Assert.That(Math.Abs(tanh[i] - Math.Tanh(values[i]))).IsLessThan(VectorTolerance);
            await Assert.That(Math.Abs(logistic[i] - (1.0 / (1.0 + Math.Exp(-values[i]))))).IsLessThan(VectorTolerance);
        }
    }

    /// <summary>Softmax gives probabilities that add to one, in the order of the scores, and never a zero for a reachable score.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SoftmaxGivesProbabilities()
    {
        var values = (float[])Scores.Clone();

        Activations.Softmax(values);

        await Assert.That(Math.Abs(values.Sum() - 1F)).IsLessThan((float)Tolerance);
        await Assert.That(values[2]).IsGreaterThan(values[1]);
        await Assert.That(values[1]).IsGreaterThan(values[0]);
        await Assert.That(values[0]).IsGreaterThan(values[3]);
    }
}
