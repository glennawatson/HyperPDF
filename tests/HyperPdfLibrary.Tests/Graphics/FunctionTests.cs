// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Graphics.Functions;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Tests for the PDF function types.</summary>
public sealed class FunctionTests
{
    /// <summary>The tolerance for float results.</summary>
    private const float Tolerance = 1e-4F;

    /// <summary>A quarter of the domain.</summary>
    private const float Quarter = 0.25F;

    /// <summary>Half of the domain.</summary>
    private const float Half = 0.5F;

    /// <summary>Three quarters of the domain.</summary>
    private const float ThreeQuarters = 0.75F;

    /// <summary>A point past the stitching bound.</summary>
    private const float PastBound = 0.6F;

    /// <summary>A quadratic exponent.</summary>
    private const float Square = 2;

    /// <summary>The second output of the exponential test at x = 1.</summary>
    private const float SecondEnd = 0.5F;

    /// <summary>The middle sample of the sampled test, 128 / 255.</summary>
    private const float MiddleSample = 128F / 255F;

    /// <summary>The bits per sample of the sampled tests.</summary>
    private const int SampleBits = 8;

    /// <summary>The grid points of the one-input sampled test.</summary>
    private const int ThreePoints = 3;

    /// <summary>The grid points per input of the two-input sampled test.</summary>
    private const int TwoPoints = 2;

    /// <summary>The upper range of the PostScript tests.</summary>
    private const float RangeHigh = 4;

    /// <summary>The lower range of the PostScript tests.</summary>
    private const float RangeLow = -1;

    /// <summary>The expected result of the ifelse program for 0.75.</summary>
    private const float IfElseHigh = -0.25F;

    /// <summary>The input for the root program.</summary>
    private const float RootInput = 0.6F;

    /// <summary>The expected result of the root program.</summary>
    private const float RootResult = 0.8F;

    /// <summary>The input for the integer program.</summary>
    private const float IntegerInput = 0.95F;

    /// <summary>The expected result of the shift program.</summary>
    private const float ShiftResult = 2;

    /// <summary>The input for the conditional program that keeps its value.</summary>
    private const float KeptInput = 0.7F;

    /// <summary>The input for the conditional program that replaces its value.</summary>
    private const float ReplacedInput = 0.3F;

    /// <summary>An exponential function gives C0 + x^N (C1 - C0) per output.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExponentialInterpolates()
    {
        var function = PdfFunction.Parse(PdfValue.FromDictionary(Exponential([0, 0], [1, SecondEnd], Square)))!;
        var output = Run(function, Half);

        await Assert.That(function.OutputCount).IsEqualTo(TwoPoints);
        await Assert.That(output[0]).IsEqualTo(Quarter).Within(Tolerance);
        await Assert.That(output[1]).IsEqualTo(Quarter * SecondEnd).Within(Tolerance);
    }

    /// <summary>A one-input sampled function interpolates between neighbouring samples.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SampledInterpolatesOneInput()
    {
        var function = PdfFunction.Parse(Sampled([ThreePoints], [0, 1], [0, 1], [0x00, 0x80, 0xFF]))!;

        await Assert.That(Run(function, Quarter)[0]).IsEqualTo(MiddleSample / TwoPoints).Within(Tolerance);
        await Assert.That(Run(function, 1)[0]).IsEqualTo(1).Within(Tolerance);
    }

    /// <summary>A two-input sampled function interpolates bilinearly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SampledInterpolatesTwoInputs()
    {
        var function = PdfFunction.Parse(Sampled([TwoPoints, TwoPoints], [0, 1, 0, 1], [0, 1], [0x00, 0xFF, 0xFF, 0x00]))!;

        await Assert.That(Run(function, Half, Half)[0]).IsEqualTo(Half).Within(Tolerance);
        await Assert.That(Run(function, 1, 0)[0]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(Run(function, 1, 1)[0]).IsEqualTo(0).Within(Tolerance);
    }

    /// <summary>A stitching function picks the subdomain and encodes into it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StitchingSelectsSubdomain()
    {
        var functions = new PdfArray(null);
        functions.Add(PdfValue.FromDictionary(Exponential([0], [1], 1)));
        functions.Add(PdfValue.FromDictionary(Exponential([1], [0], 1)));
        var dictionary = FunctionDictionary(FunctionReader.StitchingType, [0, 1]);
        dictionary.Add(KnownName.Functions, PdfValue.FromArray(functions));
        dictionary.Add(KnownName.Bounds, Numbers(Half));
        dictionary.Add(KnownName.Encode, Numbers(0, 1, 0, 1));
        var function = PdfFunction.Parse(PdfValue.FromDictionary(dictionary))!;

        await Assert.That(Run(function, Quarter)[0]).IsEqualTo(Half).Within(Tolerance);
        await Assert.That(Run(function, ThreeQuarters)[0]).IsEqualTo(Half).Within(Tolerance);
        await Assert.That(Run(function, PastBound)[0]).IsEqualTo(1 - ((PastBound - Half) * TwoPoints)).Within(Tolerance);
    }

    /// <summary>An array of one-output functions acts as one function with an output per member.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ArrayCombinesFunctions()
    {
        var array = new PdfArray(null);
        array.Add(PdfValue.FromDictionary(Exponential([0], [1], 1)));
        array.Add(PdfValue.FromDictionary(Exponential([1], [0], 1)));
        var function = PdfFunction.Parse(PdfValue.FromArray(array))!;
        var output = Run(function, Quarter);

        await Assert.That(function.OutputCount).IsEqualTo(TwoPoints);
        await Assert.That(output[0]).IsEqualTo(Quarter).Within(Tolerance);
        await Assert.That(output[1]).IsEqualTo(ThreeQuarters).Within(Tolerance);
    }

    /// <summary>PostScript ifelse runs one branch or the other.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PostScriptIfElse()
    {
        var function = PostScript("{ dup 0.5 gt { 1 sub } { 2 mul } ifelse }", 1);

        await Assert.That(Run(function, Quarter)[0]).IsEqualTo(Half).Within(Tolerance);
        await Assert.That(Run(function, ThreeQuarters)[0]).IsEqualTo(IfElseHigh).Within(Tolerance);
    }

    /// <summary>PostScript if runs its procedure only when the condition holds.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PostScriptIf()
    {
        var function = PostScript("{ dup 0.5 lt { pop 0 } if }", 1);

        await Assert.That(Run(function, ReplacedInput)[0]).IsEqualTo(0).Within(Tolerance);
        await Assert.That(Run(function, KeptInput)[0]).IsEqualTo(KeptInput).Within(Tolerance);
    }

    /// <summary>PostScript arithmetic, integer, boolean, bitwise and stack operators give the expected results.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PostScriptOperators()
    {
        await Assert.That(Run(PostScript("{ 360 mul sin }", 1), Quarter)[0]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(Run(PostScript("{ 2 exp 1 exch sub abs sqrt }", 1), RootInput)[0]).IsEqualTo(RootResult).Within(Tolerance);
        await Assert.That(Run(PostScript("{ 10 mul cvi 3 idiv 2 mod }", 1), IntegerInput)[0]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(Run(PostScript("{ pop 1 1 bitshift }", 1), 0)[0]).IsEqualTo(ShiftResult).Within(Tolerance);
        await Assert.That(Run(PostScript("{ pop true false xor { 1 } { 0 } ifelse }", 1), 0)[0]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(Run(PostScript("{ pop 3 4 2 copy gt exch pop exch pop { 1 } { 0 } ifelse }", 1), 0)[0]).IsEqualTo(0).Within(Tolerance);
        await Assert.That(Run(PostScript("{ 1 2 3 3 1 roll pop pop add }", 1), Half)[0]).IsEqualTo(ThreePoints + Half).Within(Tolerance);
        await Assert.That(Run(PostScript("{ 0 index add 4 ne not { 1 } { 0 } ifelse }", 1), ShiftResult)[0]).IsEqualTo(1).Within(Tolerance);
    }

    /// <summary>A program with two outputs takes the top two values of the stack.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PostScriptTwoOutputs()
    {
        var output = Run(PostScript("{ dup 2 mul }", TwoPoints), Quarter);

        await Assert.That(output[0]).IsEqualTo(Quarter).Within(Tolerance);
        await Assert.That(output[1]).IsEqualTo(Half).Within(Tolerance);
    }

    /// <summary>A failing program gives the low end of the range; an invalid one does not parse.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PostScriptFailuresAreSafe()
    {
        await Assert.That(Run(PostScript("{ pop pop }", 1), Half)[0]).IsEqualTo(RangeLow).Within(Tolerance);

        // A division by zero is an undefinedresult error (ISO 32000-2 7.10.5), so the function fails.
        await Assert.That(Run(PostScript("{ 0 div }", 1), Half)[0]).IsEqualTo(RangeLow).Within(Tolerance);
        await Assert.That(PdfFunction.Parse(PostScriptValue("{ frobnicate }", 1))).IsNull();
        await Assert.That(PdfFunction.Parse(PostScriptValue("{ { 1 } }", 1))).IsNull();
    }

    /// <summary>Invalid function dictionaries parse to null instead of throwing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidFunctionsParseToNull()
    {
        await Assert.That(PdfFunction.Parse(PdfValue.Null)).IsNull();
        await Assert.That(PdfFunction.Parse(PdfValue.FromDictionary(new(null)))).IsNull();
        await Assert.That(PdfFunction.Parse(PdfValue.FromDictionary(FunctionDictionary(FunctionReader.SampledType, [0, 1])))).IsNull();
        await Assert.That(PdfFunction.Parse(PdfValue.FromDictionary(FunctionDictionary(FunctionReader.StitchingType, [1, 0])))).IsNull();
    }

    /// <summary>Inputs outside the domain are clamped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InputsAreClamped()
    {
        var function = PdfFunction.Parse(PdfValue.FromDictionary(Exponential([0], [1], 1)))!;

        await Assert.That(Run(function, RangeHigh)[0]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(Run(function, RangeLow)[0]).IsEqualTo(0).Within(Tolerance);
    }

    /// <summary>Evaluates a function.</summary>
    /// <param name="function">The function.</param>
    /// <param name="input">The inputs.</param>
    /// <returns>The outputs.</returns>
    private static float[] Run(PdfFunction function, params float[] input)
    {
        var output = new float[function.OutputCount];
        function.Evaluate(input, output);
        return output;
    }

    /// <summary>Creates a number array value.</summary>
    /// <param name="values">The numbers.</param>
    /// <returns>The value.</returns>
    private static PdfValue Numbers(params float[] values) => PdfValue.FromArray(PdfArray.FromNumbers(null, values));

    /// <summary>Creates a function dictionary with a type and domain.</summary>
    /// <param name="type">The /FunctionType.</param>
    /// <param name="domain">The /Domain.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary FunctionDictionary(int type, float[] domain)
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.FunctionType, PdfValue.FromInteger(type));
        dictionary.Add(KnownName.Domain, Numbers(domain));
        return dictionary;
    }

    /// <summary>Creates an exponential function dictionary over [0 1].</summary>
    /// <param name="c0">The outputs at 0.</param>
    /// <param name="c1">The outputs at 1.</param>
    /// <param name="exponent">The exponent.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Exponential(float[] c0, float[] c1, float exponent)
    {
        var dictionary = FunctionDictionary(FunctionReader.ExponentialType, [0, 1]);
        dictionary.Add(KnownName.C0, Numbers(c0));
        dictionary.Add(KnownName.C1, Numbers(c1));
        dictionary.Add(KnownName.N, PdfValue.FromReal(exponent));
        return dictionary;
    }

    /// <summary>Creates an 8-bit sampled function stream.</summary>
    /// <param name="size">The /Size.</param>
    /// <param name="domain">The /Domain.</param>
    /// <param name="range">The /Range.</param>
    /// <param name="samples">The samples.</param>
    /// <returns>The stream value.</returns>
    private static PdfValue Sampled(float[] size, float[] domain, float[] range, byte[] samples)
    {
        var dictionary = FunctionDictionary(FunctionReader.SampledType, domain);
        dictionary.Add(KnownName.Range, Numbers(range));
        dictionary.Add(KnownName.Size, Numbers(size));
        dictionary.Add(KnownName.BitsPerSample, PdfValue.FromInteger(SampleBits));
        return PdfValue.FromStream(new(dictionary, samples));
    }

    /// <summary>Creates a PostScript calculator function value over [0 1] with range [-1 4] per output.</summary>
    /// <param name="program">The program.</param>
    /// <param name="outputs">The number of outputs.</param>
    /// <returns>The stream value.</returns>
    private static PdfValue PostScriptValue(string program, int outputs)
    {
        var dictionary = FunctionDictionary(FunctionReader.PostScriptType, [RangeLow, RangeHigh]);
        var range = new float[TwoPoints * outputs];
        for (var i = 0; i < range.Length; i += TwoPoints)
        {
            range[i] = RangeLow;
            range[i + 1] = RangeHigh;
        }

        dictionary.Add(KnownName.Range, Numbers(range));
        return PdfValue.FromStream(new(dictionary, Encoding.ASCII.GetBytes(program)));
    }

    /// <summary>Parses a PostScript calculator function.</summary>
    /// <param name="program">The program.</param>
    /// <param name="outputs">The number of outputs.</param>
    /// <returns>The function.</returns>
    private static PdfFunction PostScript(string program, int outputs) => PdfFunction.Parse(PostScriptValue(program, outputs))!;
}
