// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.HyperPdf.Tests.RenderingComparison;
using SkiaSharp;

namespace PdfViewerLite.GpuProbe;

/// <summary>Reads GPU pixels once per generated PDF and distinguishes edge coverage from interior differences.</summary>
internal static class StandardsParityProbe
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The channel difference treated as output quantization.</summary>
    private const int QuantizationDelta = 2;

    /// <summary>The neighbour contrast that identifies an antialiased edge.</summary>
    private const int EdgeContrast = 16;

    /// <summary>The green channel byte offset.</summary>
    private const int GreenOffset = 1;

    /// <summary>The red channel byte offset.</summary>
    private const int RedOffset = 2;

    /// <summary>The alpha channel byte offset.</summary>
    private const int AlphaOffset = 3;

    /// <summary>Checks existing standards fixtures and additional graphics families.</summary>
    /// <param name="gpu">The current compositor context.</param>
    /// <param name="colorType">The supported GPU channel order.</param>
    /// <param name="colorSpace">The sRGB output colour space.</param>
    /// <param name="output">The comparison report.</param>
    /// <exception cref="InvalidOperationException">A fixture fails to render or differs materially.</exception>
    internal static void Run(GRContext gpu, SKColorType colorType, SKColorSpace colorSpace, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(gpu);
        ArgumentNullException.ThrowIfNull(colorSpace);
        ArgumentNullException.ThrowIfNull(output);
        var reviewed = 0;
        var material = 0;
        var oracleFailures = 0;
        foreach (var kind in Enum.GetValues<StandardsRenderCase>())
        {
            var generated = StandardsRenderFixtures.Create(kind);
            var fixture = new StandardsParityFixture(kind.ToString(), generated.Pdf, generated.Oracle.Width, generated.Oracle.Height, PdfRenderFlags.None, false, generated.Oracle);
            var score = CompareFixture(gpu, colorType, colorSpace, fixture);
            Report(output, fixture.Name, score);
            reviewed++;
            material += score.MaterialPixels;
            oracleFailures += score.OracleFailures;
        }

        foreach (var fixture in GpuStandardsExtraFixtures.Create())
        {
            var score = CompareFixture(gpu, colorType, colorSpace, fixture);
            Report(output, fixture.Name, score);
            reviewed++;
            material += score.MaterialPixels;
            oracleFailures += score.OracleFailures;
        }

        output.WriteLine($"standards_gpu_cases={reviewed} material_interior_pixels={material} oracle_failures={oracleFailures}");
        if (material != 0 || oracleFailures != 0)
        {
            throw new InvalidOperationException("GPU and CPU renderings differ outside classified antialias or quantization regions.");
        }
    }

    /// <summary>Renders one valid PDF into independent CPU and GPU targets.</summary>
    /// <param name="gpu">The compositor context.</param>
    /// <param name="colorType">The preferred GPU layout.</param>
    /// <param name="colorSpace">The output colour space.</param>
    /// <param name="fixture">The generated PDF and flags.</param>
    /// <returns>The pixel comparison.</returns>
    /// <exception cref="InvalidOperationException">A page, target or readback fails.</exception>
    private static StandardsParityScore CompareFixture(GRContext gpu, SKColorType colorType, SKColorSpace colorSpace, in StandardsParityFixture fixture)
    {
        using var document = PdfDocumentReader.Open(fixture.Pdf, null);
        var options = PdfRenderOptions.Default with { SimulateOverprint = fixture.SimulateOverprint };
        using var renderer = new PdfPageRenderer(document, options);
        var request = new PdfTileRequest(0, 1, 0, 0, 0, fixture.Flags);
        if (!renderer.Prepare(request, CancellationToken.None))
        {
            throw new InvalidOperationException($"Could not record standards fixture {fixture.Name}.");
        }

        var rowBytes = checked(fixture.Width * BytesPerPixel);
        var cpu = new byte[checked(rowBytes * fixture.Height)];
        if (!renderer.Render(request, new(cpu, fixture.Width, fixture.Height, rowBytes)))
        {
            throw new InvalidOperationException($"Could not render CPU fixture {fixture.Name}.");
        }

        var order = colorType;
        using var target = CreateTarget(gpu, colorSpace, fixture.Width, fixture.Height, ref order);
        if (!renderer.RenderToTarget(request, target))
        {
            throw new InvalidOperationException($"Could not render GPU fixture {fixture.Name}.");
        }

        using var image = target.Snapshot();
        if (!image.IsTextureBacked)
        {
            throw new InvalidOperationException($"Standards fixture {fixture.Name} did not use a GPU image.");
        }

        gpu.Flush(true, true);
        var info = new SKImageInfo(fixture.Width, fixture.Height, order, SKAlphaType.Premul, colorSpace);
        var gpuPixels = new byte[cpu.Length];
        if (!ReadPixels(image, info, gpuPixels, rowBytes))
        {
            throw new InvalidOperationException($"GPU readback failed for standards fixture {fixture.Name}.");
        }

        if (order == SKColorType.Rgba8888)
        {
            SwapRedAndBlue(gpuPixels);
        }

        return Score(cpu, gpuPixels, fixture.Width, fixture.Height, fixture.Oracle);
    }

    /// <summary>Creates a GPU target in the compositor's format or its supported alternate.</summary>
    /// <param name="gpu">The current context.</param>
    /// <param name="colorSpace">The output colour space.</param>
    /// <param name="width">The target width.</param>
    /// <param name="height">The target height.</param>
    /// <param name="colorType">The preferred format, updated on fallback.</param>
    /// <returns>The owned target.</returns>
    /// <exception cref="InvalidOperationException">Neither common layout is supported.</exception>
    private static SkiaSurfaceRenderTarget CreateTarget(GRContext gpu, SKColorSpace colorSpace, int width, int height, ref SKColorType colorType)
    {
        var info = new SKImageInfo(width, height, colorType, SKAlphaType.Premul, colorSpace);
        var surface = TryCreateSurface(gpu, info);
        if (surface is null)
        {
            colorType = colorType == SKColorType.Bgra8888 ? SKColorType.Rgba8888 : SKColorType.Bgra8888;
            info = new(width, height, colorType, SKAlphaType.Premul, colorSpace);
            surface = TryCreateSurface(gpu, info) ?? throw new InvalidOperationException("The GPU cannot create a standards parity target.");
        }

        return new(surface, info);
    }

    /// <summary>Attempts an offscreen surface without assuming driver support.</summary>
    /// <param name="gpu">The current context.</param>
    /// <param name="info">The target layout.</param>
    /// <returns>The GPU surface or null.</returns>
    private static SKSurface? TryCreateSurface(GRContext gpu, SKImageInfo info)
    {
        try
        {
            return SKSurface.Create(gpu, false, info);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            return null;
        }
    }

    /// <summary>Reads the completed GPU image only for this comparison.</summary>
    /// <param name="image">The GPU image.</param>
    /// <param name="info">The pixel layout.</param>
    /// <param name="pixels">The destination bytes.</param>
    /// <param name="rowBytes">The row stride.</param>
    /// <returns>Whether readback succeeded.</returns>
    private static unsafe bool ReadPixels(SKImage image, SKImageInfo info, byte[] pixels, int rowBytes)
    {
        fixed (byte* pointer = pixels)
        {
            return image.ReadPixels(info, (nint)pointer, rowBytes);
        }
    }

    /// <summary>Converts RGBA readback to the CPU renderer's BGRA order.</summary>
    /// <param name="pixels">The returned image bytes.</param>
    private static void SwapRedAndBlue(byte[] pixels)
    {
        for (var offset = 0; offset < pixels.Length; offset += BytesPerPixel)
        {
            var red = pixels[offset + RedOffset];
            pixels[offset + RedOffset] = pixels[offset];
            pixels[offset] = red;
        }
    }

    /// <summary>Classifies quantization, antialias coverage and interior differences separately.</summary>
    /// <param name="cpu">The CPU raster.</param>
    /// <param name="gpu">The GPU readback.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="oracle">The optional independent interior expectations.</param>
    /// <returns>The comparison score.</returns>
    private static StandardsParityScore Score(byte[] cpu, byte[] gpu, int width, int height, StandardsOracle? oracle)
    {
        var exact = 0;
        var quantized = 0;
        var edge = 0;
        var material = 0;
        var maximum = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var delta = PixelDelta(cpu, gpu, ((y * width) + x) * BytesPerPixel);
                maximum = Math.Max(maximum, delta);
                if (delta == 0)
                {
                    exact++;
                }
                else if (delta <= QuantizationDelta)
                {
                    quantized++;
                }
                else if (IsEdge(cpu, width, height, x, y))
                {
                    edge++;
                }
                else
                {
                    material++;
                }
            }
        }

        return new(exact, quantized, edge, material, maximum, CountOracleFailures(gpu, width, oracle), gpu.Length);
    }

    /// <summary>Finds the largest byte difference in one premultiplied pixel.</summary>
    /// <param name="first">The first raster.</param>
    /// <param name="second">The second raster.</param>
    /// <param name="offset">The pixel's first byte.</param>
    /// <returns>The maximum channel delta.</returns>
    private static int PixelDelta(byte[] first, byte[] second, int offset)
    {
        var delta = 0;
        for (var channel = 0; channel < BytesPerPixel; channel++)
        {
            delta = Math.Max(delta, Math.Abs(first[offset + channel] - second[offset + channel]));
        }

        return delta;
    }

    /// <summary>Detects contrast adjacent to a pixel where coverage differences are expected.</summary>
    /// <param name="pixels">The CPU reference raster.</param>
    /// <param name="width">The raster width.</param>
    /// <param name="height">The raster height.</param>
    /// <param name="x">The pixel column.</param>
    /// <param name="y">The pixel row.</param>
    /// <returns>Whether this pixel borders a sharp colour transition.</returns>
    private static bool IsEdge(byte[] pixels, int width, int height, int x, int y)
    {
        var center = ((y * width) + x) * BytesPerPixel;
        for (var otherY = Math.Max(0, y - 1); otherY <= Math.Min(height - 1, y + 1); otherY++)
        {
            for (var otherX = Math.Max(0, x - 1); otherX <= Math.Min(width - 1, x + 1); otherX++)
            {
                var neighbor = ((otherY * width) + otherX) * BytesPerPixel;
                if (NeighborDelta(pixels, center, neighbor) >= EdgeContrast)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Finds the greatest channel contrast between two pixels in one raster.</summary>
    /// <param name="pixels">The raster.</param>
    /// <param name="first">The first pixel offset.</param>
    /// <param name="second">The second pixel offset.</param>
    /// <returns>The greatest channel contrast.</returns>
    private static int NeighborDelta(byte[] pixels, int first, int second)
    {
        var delta = 0;
        for (var channel = 0; channel < BytesPerPixel; channel++)
        {
            delta = Math.Max(delta, Math.Abs(pixels[first + channel] - pixels[second + channel]));
        }

        return delta;
    }

    /// <summary>Checks reviewed standards interiors, independent of CPU renderer output.</summary>
    /// <param name="gpu">The GPU pixels.</param>
    /// <param name="width">The image width.</param>
    /// <param name="oracle">The independent expected regions.</param>
    /// <returns>The number of pixels outside the oracle's tolerance.</returns>
    private static int CountOracleFailures(byte[] gpu, int width, StandardsOracle? oracle)
    {
        if (oracle is null)
        {
            return 0;
        }

        var failures = 0;
        foreach (var region in oracle.Regions.Span)
        {
            for (var y = region.Y; y < region.Y + region.Height; y++)
            {
                for (var x = region.X; x < region.X + region.Width; x++)
                {
                    if (OracleDelta(gpu, ((y * width) + x) * BytesPerPixel, region.Expected) > region.Tolerance)
                    {
                        failures++;
                    }
                }
            }
        }

        return failures;
    }

    /// <summary>Compares an output pixel to one standards-derived BGRA colour.</summary>
    /// <param name="pixels">The GPU pixels.</param>
    /// <param name="offset">The pixel offset.</param>
    /// <param name="expected">The expected colour.</param>
    /// <returns>The largest channel delta.</returns>
    private static int OracleDelta(byte[] pixels, int offset, Bgra32Color expected) =>
        Math.Max(
            Math.Max(Math.Abs(pixels[offset] - expected.Blue), Math.Abs(pixels[offset + GreenOffset] - expected.Green)),
            Math.Max(Math.Abs(pixels[offset + RedOffset] - expected.Red), Math.Abs(pixels[offset + AlphaOffset] - expected.Alpha)));

    /// <summary>Writes one fixture's output without treating antialias changes as standards failures.</summary>
    /// <param name="output">The report.</param>
    /// <param name="name">The fixture name.</param>
    /// <param name="score">The classification.</param>
    private static void Report(TextWriter output, string name, StandardsParityScore score)
    {
        output.WriteLine($"fixture={name} exact_pixels={score.ExactPixels} quantization_pixels={score.QuantizationPixels} edge_coverage_pixels={score.EdgePixels}");
        output.WriteLine($"fixture={name} material_interior_pixels={score.MaterialPixels} max_channel_delta={score.MaxChannelDelta}");
        output.WriteLine($"fixture={name} oracle_failures={score.OracleFailures} readback_bytes={score.ReadbackBytes}");
    }
}
