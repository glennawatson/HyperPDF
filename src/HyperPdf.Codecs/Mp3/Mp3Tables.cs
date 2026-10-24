// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Mp3;

/// <summary>The tables the layer 3 decoder works out once from the formulas of the standard. None of them changes after it is built.</summary>
internal static class Mp3Tables
{
    /// <summary>The samples for each channel in one granule.</summary>
    internal const int GranuleSamples = 576;

    /// <summary>The subbands the polyphase filter splits the sound into.</summary>
    internal const int Subbands = 32;

    /// <summary>The samples in one subband in one granule.</summary>
    internal const int SubbandSamples = 18;

    /// <summary>The length of one row of the long block transform, which is 36 values padded to a whole number of vectors of any width up to 512 bits.</summary>
    internal const int LongRowLength = 48;

    /// <summary>The values a short block transform produces.</summary>
    internal const int ShortOutputs = 12;

    /// <summary>The values a short block transform takes.</summary>
    internal const int ShortInputs = 6;

    /// <summary>The outputs of the polyphase matrixing for each time step.</summary>
    internal const int MatrixOutputs = 64;

    /// <summary>The steps in the table of powers, enough for the largest value a granule can hold: 15 plus 13 extra bits.</summary>
    internal const int PowerSteps = 8_207;

    /// <summary>The number of window shapes of the long transform: normal, start and stop.</summary>
    internal const int LongWindows = 3;

    /// <summary>The number of butterflies at each subband edge in the alias reduction.</summary>
    internal const int AliasButterflies = 8;

    /// <summary>The intensity positions of MPEG 1 that carry a ratio.</summary>
    internal const int IntensityPositions = 7;

    /// <summary>The table of values to the power of four thirds.</summary>
    internal static readonly ReadOnlyMemory<float> Power43 = BuildPower43();

    /// <summary>The fourth roots of 2 raised to 0, 1, 2 and 3, which finish a power of two in quarter steps.</summary>
    internal static readonly ReadOnlyMemory<float> QuarterRoots = BuildQuarterRoots();

    /// <summary>The long block transform for each window shape, one row of <see cref="LongRowLength"/> outputs for each of the 18 inputs.</summary>
    internal static readonly ReadOnlyMemory<float> LongTransform = BuildLongTransform();

    /// <summary>The short block transform, one row of <see cref="ShortOutputs"/> outputs, windowed, for each of the 6 inputs.</summary>
    internal static readonly ReadOnlyMemory<float> ShortTransform = BuildShortTransform();

    /// <summary>The cosine terms of the alias reduction.</summary>
    internal static readonly ReadOnlyMemory<float> AliasCosine = BuildAlias(false);

    /// <summary>The sine terms of the alias reduction.</summary>
    internal static readonly ReadOnlyMemory<float> AliasSine = BuildAlias(true);

    /// <summary>The matrixing of the polyphase filter, one row of 64 outputs for each of the 32 subbands.</summary>
    internal static readonly ReadOnlyMemory<float> SynthesisMatrix = BuildSynthesisMatrix();

    /// <summary>The 512 coefficients of the synthesis window.</summary>
    internal static readonly ReadOnlyMemory<float> SynthesisWindow = BuildWindow();

    /// <summary>The factor for the left channel of each MPEG 1 intensity position.</summary>
    internal static readonly ReadOnlyMemory<float> IntensityLeft = BuildIntensity(true);

    /// <summary>The factor for the right channel of each MPEG 1 intensity position.</summary>
    internal static readonly ReadOnlyMemory<float> IntensityRight = BuildIntensity(false);

    /// <summary>The steps of the window in a whole number: it is stored in steps of 1/65536.</summary>
    private const float WindowStep = 1F / 65_536F;

    /// <summary>The number of fractional steps of a power of two: the exponents of the gain are in quarters.</summary>
    private const int QuarterCount = 4;

    /// <summary>Two, the base of the gain.</summary>
    private const double DoubleBase = 2;

    /// <summary>Half, which puts a sample at the middle of its interval in the transforms.</summary>
    private const double Half = 0.5;

    /// <summary>The number of taps in the long block transform.</summary>
    private const int LongTaps = 36;

    /// <summary>The number of taps in the short block transform.</summary>
    private const int ShortTaps = 12;

    /// <summary>The first sample of the start block window that is one.</summary>
    private const int StartFlatFrom = 18;

    /// <summary>The first sample of the start block window that falls with the short window.</summary>
    private const int StartFallFrom = 24;

    /// <summary>The first sample of the start block window that is zero.</summary>
    private const int StartZeroFrom = 30;

    /// <summary>The last sample of the stop block window that is zero.</summary>
    private const int StopZeroTo = 6;

    /// <summary>The first sample of the stop block window that is one.</summary>
    private const int StopFlatFrom = 12;

    /// <summary>The first sample of the stop block window that follows the long window again.</summary>
    private const int StopLongFrom = 18;

    /// <summary>The first output of the polyphase matrixing.</summary>
    private const int FirstMatrixOutput = 16;

    /// <summary>The angle step of the intensity ratios: a twelfth of a half turn.</summary>
    private const double IntensityStep = Math.PI / 12;

    /// <summary>The scale of the alias reduction coefficients, which are stored as whole numbers of ten thousandths.</summary>
    private const double AliasScale = 10_000;

    /// <summary>Gets the sizes of the reduction coefficients of the alias reduction, in ten thousandths; the coefficients are their negatives.</summary>
    private static ReadOnlySpan<ushort> AliasCoefficients => [0x1770, 0x14E6, 0x0CE4, 0x073A, 0x03B6, 0x019A, 0x008E, 0x0025];

    /// <summary>Builds the table of values to the power of four thirds.</summary>
    /// <returns>The table.</returns>
    private static float[] BuildPower43()
    {
        var table = new float[PowerSteps];
        for (var value = 0; value < table.Length; value++)
        {
            table[value] = (float)(value * Math.Cbrt(value));
        }

        return table;
    }

    /// <summary>Builds the quarter roots of two.</summary>
    /// <returns>The roots.</returns>
    private static float[] BuildQuarterRoots()
    {
        var roots = new float[QuarterCount];
        var quarterRoot = Math.Sqrt(Math.Sqrt(DoubleBase));
        for (var step = 0; step < roots.Length; step++)
        {
            roots[step] = (float)Math.Pow(quarterRoot, step);
        }

        return roots;
    }

    /// <summary>Builds the synthesis window from the integer steps of the standard.</summary>
    /// <returns>The window.</returns>
    private static float[] BuildWindow()
    {
        var window = new float[Mp3TableData.SynthesisWindow.Length];
        for (var index = 0; index < window.Length; index++)
        {
            window[index] = Mp3TableData.SynthesisWindow[index] * WindowStep;
        }

        return window;
    }

    /// <summary>Builds the ratios of MPEG 1 intensity stereo: the left channel gets the tangent of the position over one plus the tangent.</summary>
    /// <param name="left">Whether to build the factors of the left channel rather than the right.</param>
    /// <returns>The factors.</returns>
    private static float[] BuildIntensity(bool left)
    {
        var factors = new float[IntensityPositions];
        for (var position = 0; position < factors.Length; position++)
        {
            var toLeft = 1D;
            if (position != IntensityPositions - 1)
            {
                var ratio = Math.Tan(position * IntensityStep);
                toLeft = ratio / (1 + ratio);
            }

            factors[position] = (float)(left ? toLeft : 1 - toLeft);
        }

        return factors;
    }

    /// <summary>Gets the window of the long transform for a window shape.</summary>
    /// <param name="shape">0 normal, 1 start, 2 stop.</param>
    /// <param name="index">The output.</param>
    /// <returns>The window value.</returns>
    private static double LongWindow(int shape, int index)
    {
        var longWindow = Math.Sin(Math.PI / LongTaps * (index + Half));
        return shape switch
        {
            0 => longWindow,
            1 => StartWindow(index, longWindow),
            _ => StopWindow(index, longWindow),
        };
    }

    /// <summary>Gets the window of the start block: long, then flat, then the fall of a short window, then zero.</summary>
    /// <param name="index">The output.</param>
    /// <param name="longWindow">The long window at this output.</param>
    /// <returns>The window value.</returns>
    private static double StartWindow(int index, double longWindow)
    {
        if (index < StartFlatFrom)
        {
            return longWindow;
        }

        if (index < StartFallFrom)
        {
            return 1;
        }

        return index < StartZeroFrom ? Math.Sin(Math.PI / ShortTaps * (index - StartFlatFrom + Half)) : 0;
    }

    /// <summary>Gets the window of the stop block: zero, then the rise of a short window, then flat, then long.</summary>
    /// <param name="index">The output.</param>
    /// <param name="longWindow">The long window at this output.</param>
    /// <returns>The window value.</returns>
    private static double StopWindow(int index, double longWindow)
    {
        if (index < StopZeroTo)
        {
            return 0;
        }

        if (index < StopFlatFrom)
        {
            return Math.Sin(Math.PI / ShortTaps * (index - StopZeroTo + Half));
        }

        return index < StopLongFrom ? 1 : longWindow;
    }

    /// <summary>Gets the angle of the inverse MDCT: pi over twice the size, times the shifted output and the odd input.</summary>
    /// <param name="size">The number of outputs.</param>
    /// <param name="output">The output index.</param>
    /// <param name="input">The input index.</param>
    /// <returns>The angle.</returns>
    private static double TransformAngle(int size, int output, int input)
    {
        var shifted = ((output << 1) + 1 + (size >> 1)) * ((input << 1) + 1);
        return Math.PI / (size << 1) * shifted;
    }

    /// <summary>Builds the long block transform: each output is the windowed sum of the inputs times a cosine.</summary>
    /// <returns>The transform for each window shape.</returns>
    private static float[] BuildLongTransform()
    {
        var table = new float[LongWindows * SubbandSamples * LongRowLength];
        for (var shape = 0; shape < LongWindows; shape++)
        {
            for (var input = 0; input < SubbandSamples; input++)
            {
                for (var output = 0; output < LongTaps; output++)
                {
                    var cosine = Math.Cos(TransformAngle(LongTaps, output, input));
                    table[(((shape * SubbandSamples) + input) * LongRowLength) + output] = (float)(LongWindow(shape, output) * cosine);
                }
            }
        }

        return table;
    }

    /// <summary>Builds the short block transform.</summary>
    /// <returns>The transform.</returns>
    private static float[] BuildShortTransform()
    {
        var table = new float[ShortInputs * ShortOutputs];
        for (var input = 0; input < ShortInputs; input++)
        {
            for (var output = 0; output < ShortOutputs; output++)
            {
                var window = Math.Sin(Math.PI / ShortTaps * (output + Half));
                table[(input * ShortOutputs) + output] = (float)(window * Math.Cos(TransformAngle(ShortTaps, output, input)));
            }
        }

        return table;
    }

    /// <summary>Builds the alias reduction terms.</summary>
    /// <param name="sine">Whether to build the sine terms rather than the cosine terms.</param>
    /// <returns>The terms.</returns>
    private static float[] BuildAlias(bool sine)
    {
        var terms = new float[AliasButterflies];
        for (var index = 0; index < terms.Length; index++)
        {
            var coefficient = -AliasCoefficients[index] / AliasScale;
            var scale = 1 / Math.Sqrt(1 + (coefficient * coefficient));
            terms[index] = (float)(sine ? coefficient * scale : scale);
        }

        return terms;
    }

    /// <summary>Builds the matrixing of the polyphase filter.</summary>
    /// <returns>The matrix.</returns>
    private static float[] BuildSynthesisMatrix()
    {
        var matrix = new float[Subbands * MatrixOutputs];
        for (var subband = 0; subband < Subbands; subband++)
        {
            for (var output = 0; output < MatrixOutputs; output++)
            {
                var angle = Math.PI / MatrixOutputs * ((FirstMatrixOutput + output) * ((subband << 1) + 1));
                matrix[(subband * MatrixOutputs) + output] = (float)Math.Cos(angle);
            }
        }

        return matrix;
    }
}
