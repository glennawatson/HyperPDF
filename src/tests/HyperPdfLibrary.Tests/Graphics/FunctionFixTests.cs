// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Graphics.Functions;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Tests for PostScript function errors and for stitching, sampled and exponential function validation.</summary>
public sealed class FunctionFixTests
{
    /// <summary>The tolerance for float results.</summary>
    private const float Tolerance = 1e-4F;

    /// <summary>A result of three.</summary>
    private const float Three = 3F;

    /// <summary>The input of the PostScript tests.</summary>
    private const float Input = 0.5F;

    /// <summary>The upper end of the PostScript output range.</summary>
    private const float RangeHigh = 4F;

    /// <summary>The lower end of the PostScript output range.</summary>
    private const float RangeLow = -1F;

    /// <summary>The number of outputs of the padded exponential function.</summary>
    private const int PaddedOutputs = 3;

    /// <summary>The sample size that the short sampled data cannot fill.</summary>
    private const int LargeSize = 100;

    /// <summary>The bits per sample.</summary>
    private const int EightBits = 8;

    /// <summary>The stitched boundary.</summary>
    private const float Boundary = 0.5F;

    /// <summary>A second boundary, past the first.</summary>
    private const float SecondBoundary = 0.75F;

    /// <summary>The number of ones pushed by the overflow program: more than the stack holds.</summary>
    private const int PostScriptLimitPlus = 150;

    /// <summary>A PostScript program that raises an error of ISO 32000-2 7.10.5 fails, and a failed function gives the low end of its range.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PostScriptErrorsFailTheFunction()
    {
        // Popping an empty stack is a stackunderflow error.
        await Assert.That(Evaluate("{ pop pop pop 3 add }")).IsEqualTo(RangeLow).Within(Tolerance);

        // Division by zero is an undefinedresult error, for div, idiv and mod.
        await Assert.That(Evaluate("{ pop 5 0 div }")).IsEqualTo(RangeLow).Within(Tolerance);
        await Assert.That(Evaluate("{ pop 5 0 idiv }")).IsEqualTo(RangeLow).Within(Tolerance);
        await Assert.That(Evaluate("{ pop 5 0 mod }")).IsEqualTo(RangeLow).Within(Tolerance);

        // So are a logarithm of zero, a negative square root and atan of two zeros.
        await Assert.That(Evaluate("{ pop 0 ln }")).IsEqualTo(RangeLow).Within(Tolerance);
        await Assert.That(Evaluate("{ pop -4 sqrt }")).IsEqualTo(RangeLow).Within(Tolerance);
        await Assert.That(Evaluate("{ pop 0 0 atan }")).IsEqualTo(RangeLow).Within(Tolerance);

        // idiv and mod accept reals, truncating them.
        await Assert.That(Evaluate("{ pop 7.9 2.2 idiv }")).IsEqualTo(Three).Within(Tolerance);
        await Assert.That(Evaluate("{ pop 7.9 2.2 mod }")).IsEqualTo(1).Within(Tolerance);

        // A copy count deeper than the stack and an index outside it are range errors.
        await Assert.That(Evaluate("{ 99 copy 1 add }")).IsEqualTo(RangeLow).Within(Tolerance);
        await Assert.That(Evaluate("{ 5 index }")).IsEqualTo(RangeLow).Within(Tolerance);
    }

    /// <summary>Pushing past the 100-operand limit is a stackoverflow error, so the function fails.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PostScriptFailsPastTheLimit()
    {
        var builder = new StringBuilder("{ pop ");
        for (var i = 0; i < PostScriptLimitPlus; i++)
        {
            _ = builder.Append("1 ");
        }

        _ = builder.Append('}');

        await Assert.That(Evaluate(builder.ToString())).IsEqualTo(RangeLow).Within(Tolerance);
    }

    /// <summary>A stitching function with more bounds than functions minus one is rejected, not left to crash.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StitchingRequiresMatchingBounds()
    {
        var tooMany = Stitching([Boundary, SecondBoundary]);
        var exact = Stitching([Boundary]);

        await Assert.That(PdfFunction.Parse(tooMany)).IsNull();

        var function = PdfFunction.Parse(exact)!;
        var output = new float[1];
        function.Evaluate([0F], output);
        var low = output[0];
        function.Evaluate([1F], output);

        await Assert.That(low).IsEqualTo(0).Within(Tolerance);
        await Assert.That(output[0]).IsEqualTo(1).Within(Tolerance);
    }

    /// <summary>A sampled function whose data is shorter than the declared samples is rejected.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SampledFunctionRejectsShortData()
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.FunctionType, PdfValue.FromInteger(0));
        dictionary.Add(KnownName.Domain, FixTestHelpers.Numbers(0, 1));
        dictionary.Add(KnownName.Range, FixTestHelpers.Numbers(0, 1));
        dictionary.Add(KnownName.Size, FixTestHelpers.Numbers(LargeSize));
        dictionary.Add(KnownName.BitsPerSample, PdfValue.FromInteger(EightBits));

        await Assert.That(PdfFunction.Parse(PdfValue.FromStream(new(dictionary, [0x00, 0xFF])))).IsNull();
    }

    /// <summary>An exponential function pads /C1 and /C0 that are shorter than the range instead of failing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExponentialToleratesLengthMismatch()
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.FunctionType, PdfValue.FromInteger(FunctionReader.ExponentialType));
        dictionary.Add(KnownName.Domain, FixTestHelpers.Numbers(0, 1));
        dictionary.Add(KnownName.Range, FixTestHelpers.Numbers(0, 1, 0, 1, 0, 1));
        dictionary.Add(KnownName.C0, FixTestHelpers.Numbers(0, 0));
        dictionary.Add(KnownName.C1, FixTestHelpers.Numbers(1));
        dictionary.Add(KnownName.N, PdfValue.FromInteger(1));
        var function = PdfFunction.Parse(PdfValue.FromDictionary(dictionary))!;
        var output = new float[PaddedOutputs];
        function.Evaluate([1F], output);

        await Assert.That(function.OutputCount).IsEqualTo(PaddedOutputs);
        await Assert.That(output[0]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(output[1]).IsEqualTo(0).Within(Tolerance);
        await Assert.That(output[PaddedOutputs - 1]).IsEqualTo(0).Within(Tolerance);
    }

    /// <summary>Evaluates a one-output PostScript program at the test input.</summary>
    /// <param name="program">The program.</param>
    /// <returns>The first output.</returns>
    private static float Evaluate(string program)
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.FunctionType, PdfValue.FromInteger(FunctionReader.PostScriptType));
        dictionary.Add(KnownName.Domain, FixTestHelpers.Numbers(0, 1));
        dictionary.Add(KnownName.Range, FixTestHelpers.Numbers(RangeLow, RangeHigh));
        var function = PdfFunction.Parse(PdfValue.FromStream(new(dictionary, Encoding.ASCII.GetBytes(program))))!;
        var output = new float[1];
        function.Evaluate([Input], output);
        return output[0];
    }

    /// <summary>Creates a stitching function of two linear pieces with the given bounds.</summary>
    /// <param name="bounds">The /Bounds numbers.</param>
    /// <returns>The function value.</returns>
    private static PdfValue Stitching(float[] bounds)
    {
        var first = Linear(0, Boundary);
        var second = Linear(Boundary, 1);
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.FunctionType, PdfValue.FromInteger(FunctionReader.StitchingType));
        dictionary.Add(KnownName.Domain, FixTestHelpers.Numbers(0, 1));
        dictionary.Add(KnownName.Functions, FixTestHelpers.Array(first, second));
        dictionary.Add(KnownName.Bounds, FixTestHelpers.Numbers(bounds));
        dictionary.Add(KnownName.Encode, FixTestHelpers.Numbers(0, 1, 0, 1));
        return PdfValue.FromDictionary(dictionary);
    }

    /// <summary>Creates an exponential function that goes from one value to another.</summary>
    /// <param name="from">The output at 0.</param>
    /// <param name="to">The output at 1.</param>
    /// <returns>The function value.</returns>
    private static PdfValue Linear(float from, float to)
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.FunctionType, PdfValue.FromInteger(FunctionReader.ExponentialType));
        dictionary.Add(KnownName.Domain, FixTestHelpers.Numbers(0, 1));
        dictionary.Add(KnownName.C0, FixTestHelpers.Numbers(from));
        dictionary.Add(KnownName.C1, FixTestHelpers.Numbers(to));
        dictionary.Add(KnownName.N, PdfValue.FromInteger(1));
        return PdfValue.FromDictionary(dictionary);
    }
}
