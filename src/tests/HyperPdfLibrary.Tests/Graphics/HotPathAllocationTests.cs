// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Functions;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Checks that colour conversion and function evaluation do not allocate once warmed up.</summary>
public sealed class HotPathAllocationTests
{
    /// <summary>The pixels in the converted row.</summary>
    private const int Pixels = 64;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The components of the widest test space.</summary>
    private const int MaxComponents = 4;

    /// <summary>The calls made before measuring, so tiering and lazy tables settle.</summary>
    private const int Warmup = 50;

    /// <summary>The highest palette index.</summary>
    private const int HighIndex = 255;

    /// <summary>The bytes in an RGB palette entry.</summary>
    private const int RgbBytes = 3;

    /// <summary>The /FunctionType of a PostScript function.</summary>
    private const int PostScriptType = 4;

    /// <summary>The input evaluated.</summary>
    private const float Input = 0.3F;

    /// <summary>Row conversion, RGB conversion and evaluation allocate nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HotPathsDoNotAllocate()
    {
        PdfColorSpace[] spaces =
        [
            PdfColorSpace.DeviceGray,
            PdfColorSpace.DeviceRgb,
            PdfColorSpace.DeviceCmyk,
            PdfColorSpace.Parse(Array(PdfValue.FromName(KnownName.Lab), PdfValue.FromDictionary(new(null))), null),
            PdfColorSpace.Parse(
                Array(PdfValue.FromName(KnownName.Indexed), PdfValue.FromName(KnownName.DeviceRGB), PdfValue.FromInteger(HighIndex), PdfValue.FromString(new byte[(HighIndex + 1) * RgbBytes])),
                null),
        ];

        foreach (var space in spaces)
        {
            await Assert.That(MeasureSpace(space)).IsEqualTo(0L);
        }

        await Assert.That(MeasureFunction(PostScript())).IsEqualTo(0L);
    }

    /// <summary>Measures the bytes allocated by converting a row and a colour.</summary>
    /// <param name="space">The space.</param>
    /// <returns>The bytes allocated.</returns>
    private static long MeasureSpace(PdfColorSpace space)
    {
        var samples = new byte[Pixels * MaxComponents];
        var bgra = new byte[Pixels * BytesPerPixel];
        var components = new float[MaxComponents];
        var rgb = new float[RgbBytes];
        for (var i = 0; i < Warmup; i++)
        {
            space.ConvertRow(samples, bgra, Pixels);
            space.ToRgb(components, rgb);
        }

        return AllocationProbe.Minimum(() =>
        {
            space.ConvertRow(samples, bgra, Pixels);
            space.ToRgb(components, rgb);
        });
    }

    /// <summary>Measures the bytes allocated by evaluating a function.</summary>
    /// <param name="function">The function.</param>
    /// <returns>The bytes allocated.</returns>
    private static long MeasureFunction(PdfFunction function)
    {
        var input = new[] { Input };
        var output = new float[function.OutputCount];
        for (var i = 0; i < Warmup; i++)
        {
            function.Evaluate(input, output);
        }

        return AllocationProbe.Minimum(() => function.Evaluate(input, output));
    }

    /// <summary>Creates an array value.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The value.</returns>
    private static PdfValue Array(params PdfValue[] items) => PdfValue.FromArray(new(null, items));

    /// <summary>Creates a PostScript function with a conditional.</summary>
    /// <returns>The function.</returns>
    private static PdfFunction PostScript()
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.FunctionType, PdfValue.FromInteger(PostScriptType));
        dictionary.Add(KnownName.Domain, PdfValue.FromArray(PdfArray.FromNumbers(null, [0, 1])));
        dictionary.Add(KnownName.Range, PdfValue.FromArray(PdfArray.FromNumbers(null, [0, 1, 0, 1])));
        var program = "{ dup 0.5 gt { 1 exch sub } { 2 mul } ifelse dup mul dup }"u8.ToArray();
        return PdfFunction.Parse(PdfValue.FromStream(new(dictionary, program)))!;
    }
}
