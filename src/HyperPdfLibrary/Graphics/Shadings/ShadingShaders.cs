// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Functions;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Shadings;

/// <summary>Builds backend shaders for function, axial and radial shadings.</summary>
internal static class ShadingShaders
{
    /// <summary>The colours sampled along a gradient.</summary>
    internal const int GradientSamples = 256;

    /// <summary>The width and height of the bitmap a function-based shading is rasterised to.</summary>
    private const int FunctionRaster = 256;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset from a pixel's corner to its centre.</summary>
    private const float PixelCentre = 0.5F;

    /// <summary>The numbers in an axial shading's /Coords.</summary>
    private const int AxialCoords = 4;

    /// <summary>The numbers in a radial shading's /Coords.</summary>
    private const int RadialCoords = 6;

    /// <summary>The index of the second radius in a radial shading's /Coords.</summary>
    private const int SecondRadius = 5;

    /// <summary>The numbers in a function-based shading's /Domain.</summary>
    private const int FunctionDomain = 4;

    /// <summary>The numbers in a /Matrix.</summary>
    private const int MatrixNumbers = 6;

    /// <summary>The index of the blue byte in a BGRA pixel.</summary>
    private const int BlueIndex = 2;

    /// <summary>The inputs of a function-based shading's function.</summary>
    private const int FunctionInputs = 2;

    /// <summary>Creates the shader for a shading.</summary>
    /// <param name="shading">The shading.</param>
    /// <returns>The shader, or null when the shading is damaged.</returns>
    internal static IPdfRenderShader? Create(PdfShading shading) => shading.Kind switch
    {
        PdfShadingKind.Axial => CreateAxial(shading),
        PdfShadingKind.Radial => CreateRadial(shading),
        PdfShadingKind.FunctionBased => CreateFunctionBased(shading),
        _ => null,
    };

    /// <summary>Samples a one-input function into evenly spaced colours.</summary>
    /// <param name="shading">The shading.</param>
    /// <param name="start">The first input.</param>
    /// <param name="end">The last input.</param>
    /// <param name="colors">Receives one colour per sample.</param>
    internal static void SampleColors(PdfShading shading, float start, float end, Span<PdfColor> colors)
    {
        var function = shading.Function;
        Span<float> output = stackalloc float[PdfFunction.MaxComponents];
        Span<float> components = stackalloc float[PdfColorSpace.MaxComponents];
        Span<float> input = stackalloc float[1];
        for (var i = 0; i < colors.Length; i++)
        {
            input[0] = start + ((end - start) * i / (colors.Length - 1));
            components.Clear();
            function!.Evaluate(input, output);
            output[..Math.Min(function.OutputCount, components.Length)].CopyTo(components);
            colors[i] = PdfShading.ToColor(shading.ColorSpace, components);
        }
    }

    /// <summary>Creates a linear gradient for an axial shading.</summary>
    /// <param name="shading">The shading.</param>
    /// <returns>The shader.</returns>
    private static IPdfRenderShader? CreateAxial(PdfShading shading)
    {
        if (shading.Function is null || !TryReadGradient(shading, AxialCoords, out var coords, out var domain))
        {
            return null;
        }

        var start = new PdfPoint(coords[0], coords[1]);
        var end = new PdfPoint(coords[2], coords[3]);
        if (start == end)
        {
            return null;
        }

        var stops = CreateStops(shading, domain);
        return PdfDrawingServices.For(shading.Dictionary).Backend.CreateLinearGradient(start, end, stops.Colors, stops.Positions, PdfShaderTileMode.Clamp);
    }

    /// <summary>Creates a two-point conical gradient for a radial shading.</summary>
    /// <param name="shading">The shading.</param>
    /// <returns>The shader.</returns>
    private static IPdfRenderShader? CreateRadial(PdfShading shading)
    {
        if (shading.Function is null || !TryReadGradient(shading, RadialCoords, out var coords, out var domain))
        {
            return null;
        }

        var stops = CreateStops(shading, domain);
        var start = new PdfPoint(coords[0], coords[1]);
        var end = new PdfPoint(coords[AxialCoords - 1], coords[AxialCoords]);
        return PdfDrawingServices.For(shading.Dictionary).Backend.CreateTwoPointConicalGradient(start, coords[2], end, coords[SecondRadius], stops.Colors, stops.Positions, PdfShaderTileMode.Clamp);
    }

    /// <summary>Reads the coordinates and domain of a gradient.</summary>
    /// <param name="shading">The shading.</param>
    /// <param name="count">The coordinates needed.</param>
    /// <param name="coords">Receives the coordinates.</param>
    /// <param name="domain">Receives the start and end of the parameter.</param>
    /// <returns><see langword="true"/> when the dictionary is usable.</returns>
    private static bool TryReadGradient(PdfShading shading, int count, out float[] coords, out Vector2 domain)
    {
        var dictionary = shading.Dictionary;
        coords = new float[count];
        domain = new(0, 1);
        var array = dictionary.GetArray(KnownName.Coords);
        if (array is null || array.Count < count)
        {
            return false;
        }

        _ = array.ReadNumbers(coords);
        if (dictionary.GetArray(KnownName.Domain) is { Count: >= FunctionInputs } range)
        {
            domain = new(range.GetSingle(0), range.GetSingle(1));
        }

        return true;
    }

    /// <summary>
    /// Samples a gradient's colour stops. The backend contract uses one tile mode for both ends, so each end that
    /// /Extend does not extend gets a transparent hard stop: beyond it the gradient paints nothing, as PDF requires.
    /// </summary>
    /// <param name="shading">The shading.</param>
    /// <param name="domain">The start and end of the parameter.</param>
    /// <returns>The colours and their positions.</returns>
    private static GradientStops CreateStops(PdfShading shading, Vector2 domain)
    {
        var extend = shading.Dictionary.GetArray(KnownName.Extend);
        var before = extend is not null && extend.Get(0).AsBoolean() ? 0 : 1;
        var after = extend is not null && extend.Get(1).AsBoolean() ? 0 : 1;
        var colors = new PdfColor[GradientSamples + before + after];
        var positions = new float[colors.Length];
        SampleColors(shading, domain.X, domain.Y, colors.AsSpan(before, GradientSamples));
        for (var i = 0; i < GradientSamples; i++)
        {
            positions[before + i] = (float)i / (GradientSamples - 1);
        }

        // The arrays start zeroed, so a missing extension already has a transparent stop at position 0.
        if (after != 0)
        {
            positions[^1] = 1;
        }

        return new(colors, positions);
    }

    /// <summary>Rasterises a function-based shading to a bitmap shader.</summary>
    /// <param name="shading">The shading.</param>
    /// <returns>The shader.</returns>
    private static IPdfRenderShader? CreateFunctionBased(PdfShading shading)
    {
        var function = shading.Function;
        if (function is null || function.InputCount < FunctionInputs)
        {
            return null;
        }

        Span<float> domain = [0, 1, 0, 1];
        if (shading.Dictionary.GetArray(KnownName.Domain)is { Count: >= FunctionDomain } array)
        {
            _ = array.ReadNumbers(domain);
        }

        var pixels = new byte[FunctionRaster * FunctionRaster * BytesPerPixel];
        Fill(shading, function, domain, pixels);
        var backend = PdfDrawingServices.For(shading.Dictionary).Backend;
        using var image = backend.CreateImage(new(FunctionRaster, FunctionRaster, PdfImagePixelFormat.Bgra8888), pixels, FunctionRaster * BytesPerPixel);
        var cell = Matrix3x2.CreateScale(
            (domain[1] - domain[0]) / FunctionRaster,
            (domain[FunctionInputs + 1] - domain[FunctionInputs]) / FunctionRaster) * Matrix3x2.CreateTranslation(domain[0], domain[FunctionInputs]);
        var local = cell * ReadMatrix(shading.Dictionary);
        return backend.CreateImageShader(image, PdfShaderTileMode.Decal, PdfShaderTileMode.Decal, true, local);
    }

    /// <summary>Evaluates a two-input function for every pixel of the raster.</summary>
    /// <param name="shading">The shading.</param>
    /// <param name="function">The function.</param>
    /// <param name="domain">The x and y intervals.</param>
    /// <param name="pixels">Receives premultiplied BGRA pixels.</param>
    private static void Fill(PdfShading shading, PdfFunction function, ReadOnlySpan<float> domain, byte[] pixels)
    {
        Span<float> output = stackalloc float[PdfFunction.MaxComponents];
        Span<float> components = stackalloc float[PdfColorSpace.MaxComponents];
        Span<float> input = stackalloc float[FunctionInputs];
        for (var y = 0; y < FunctionRaster; y++)
        {
            input[1] = domain[FunctionInputs] + ((domain[FunctionInputs + 1] - domain[FunctionInputs]) * (y + PixelCentre) / FunctionRaster);
            for (var x = 0; x < FunctionRaster; x++)
            {
                input[0] = domain[0] + ((domain[1] - domain[0]) * (x + PixelCentre) / FunctionRaster);
                components.Clear();
                function.Evaluate(input, output);
                output[..Math.Min(function.OutputCount, components.Length)].CopyTo(components);
                var colour = PdfShading.ToColor(shading.ColorSpace, components);
                var offset = ((y * FunctionRaster) + x) * BytesPerPixel;
                pixels[offset] = colour.Blue;
                pixels[offset + 1] = colour.Green;
                pixels[offset + BlueIndex] = colour.Red;
                pixels[offset + BlueIndex + 1] = byte.MaxValue;
            }
        }
    }

    /// <summary>Reads a shading's /Matrix.</summary>
    /// <param name="dictionary">The shading dictionary.</param>
    /// <returns>The matrix; identity when missing.</returns>
    private static Matrix3x2 ReadMatrix(PdfDictionary dictionary)
    {
        Span<float> numbers = stackalloc float[MatrixNumbers];
        return dictionary.GetArray(KnownName.Matrix) is { Count: >= MatrixNumbers } array && array.ReadNumbers(numbers) >= MatrixNumbers
            ? new(numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5])
            : Matrix3x2.Identity;
    }
}
