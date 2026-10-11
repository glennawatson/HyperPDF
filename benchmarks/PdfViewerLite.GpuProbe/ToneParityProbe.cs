// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.App.Rendering;
using PdfViewerLite.Core.Rendering;
using PdfViewerLite.Core.Theming;
using SkiaSharp;

namespace PdfViewerLite.GpuProbe;

/// <summary>Measures GPU page tone and explicitly reads back a small colour grid for parity.</summary>
internal static class ToneParityProbe
{
    /// <summary>The side of the parity grid.</summary>
    private const int PaletteSide = 256;

    /// <summary>The bytes in one BGRA or RGBA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The number of completed tone-pass samples.</summary>
    private const int Repetitions = 40;

    /// <summary>Milliseconds in one second.</summary>
    private const int MillisecondsPerSecond = 1000;

    /// <summary>The largest eight-bit colour value.</summary>
    private const int ChannelMax = 255;

    /// <summary>The source blue pattern's horizontal multiplier.</summary>
    private const int BlueX = 37;

    /// <summary>The source blue pattern's vertical multiplier.</summary>
    private const int BlueY = 71;

    /// <summary>The green channel byte offset.</summary>
    private const int GreenOffset = 1;

    /// <summary>The red channel byte offset.</summary>
    private const int RedOffset = 2;

    /// <summary>The alpha channel byte offset.</summary>
    private const int AlphaOffset = 3;

    /// <summary>Measures a tone pass and checks the GPU's bytes against Core's mapping.</summary>
    /// <param name="gpu">The current compositor graphics context.</param>
    /// <param name="pageImage">A rendered GPU page tile.</param>
    /// <param name="colorType">The supported tile channel order.</param>
    /// <param name="colorSpace">The shared sRGB colour space.</param>
    /// <param name="output">The measurement destination.</param>
    /// <exception cref="InvalidOperationException">GPU tone output differs from Core or a GPU surface fails.</exception>
    internal static void Run(GRContext gpu, SKImage pageImage, SKColorType colorType, SKColorSpace colorSpace, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(gpu);
        ArgumentNullException.ThrowIfNull(pageImage);
        ArgumentNullException.ThrowIfNull(colorSpace);
        ArgumentNullException.ThrowIfNull(output);

        var softPaper = ColorSchemes.SoftPaperTone;
        var info = new SKImageInfo(pageImage.Width, pageImage.Height, colorType, SKAlphaType.Premul, colorSpace);
        GpuProbeEventScope.Start("ToneCompile", 1);
        var compileStart = Stopwatch.GetTimestamp();
        if (!GpuPageTone.CanApply(softPaper))
        {
            throw new InvalidOperationException("The GPU page tone effect could not be created.");
        }

        var compilation = Stopwatch.GetElapsedTime(compileStart);
        GpuProbeEventScope.Stop("ToneCompile");
        gpu.GetResourceCacheUsage(out var resourcesBefore, out var bytesBefore);
        GpuProbeEventScope.Start("FirstTone", 1);
        var firstStart = Stopwatch.GetTimestamp();
        using var first = Apply(pageImage, info, softPaper, gpu);
        if (!first.IsTextureBacked)
        {
            throw new InvalidOperationException("The tone pass did not produce a GPU-backed image.");
        }

        gpu.Flush(true, true);
        var firstCompleted = Stopwatch.GetElapsedTime(firstStart);
        GpuProbeEventScope.Stop("FirstTone");
        gpu.GetResourceCacheUsage(out var resourcesAfter, out var bytesAfter);
        var warm = MeasureWarm(pageImage, info, softPaper, gpu);
        output.WriteLine($"tone_filter_compile_ms={compilation.TotalMilliseconds:F3} first_tone_gpu_completed_ms={firstCompleted.TotalMilliseconds:F3}");
        output.WriteLine($"warm_tone_gpu_completed_mean_ms={warm.Milliseconds:F3} warm_tone_managed_bytes_per_op={warm.ManagedBytesPerOperation:F1}");
        output.WriteLine($"tone_gpu_resources_before={resourcesBefore} tone_gpu_cache_before_bytes={bytesBefore} tone_gpu_resources_after={resourcesAfter} tone_gpu_cache_after_bytes={bytesAfter}");

        var allExact = true;
        PageTone[] tones = [softPaper, ColorSchemes.CalmNightTone, ColorSchemes.DarkTone, ColorSchemes.HighContrastTone];
        foreach (var tone in tones)
        {
            var result = ComparePalette(gpu, colorType, colorSpace, tone);
            output.WriteLine($"tone_parity_id={tone.Id} differing_bytes={result.DifferingBytes} max_channel_delta={result.MaxChannelDelta}");
            output.WriteLine($"tone_parity_first_byte={result.FirstByte} tone_parity_readback_bytes={result.ReadbackBytes}");
            allExact &= result.DifferingBytes == 0;
        }

        if (!allExact)
        {
            throw new InvalidOperationException("GPU page tone pixels differ from Core PageTone.Apply.");
        }
    }

    /// <summary>Applies a tone and requires a GPU-backed output image.</summary>
    /// <param name="source">The source image.</param>
    /// <param name="info">The output layout.</param>
    /// <param name="tone">The requested tone.</param>
    /// <param name="gpu">The shared graphics context.</param>
    /// <returns>The owned toned image.</returns>
    /// <exception cref="InvalidOperationException">The effect or GPU target is unavailable.</exception>
    private static SKImage Apply(SKImage source, SKImageInfo info, PageTone tone, GRContext gpu) =>
        GpuPageTone.TryApply(source, info, tone, gpu) ?? throw new InvalidOperationException("GPU page tone target is unavailable.");

    /// <summary>Measures one completed offscreen GPU tone pass per iteration.</summary>
    /// <param name="source">The already uploaded page image.</param>
    /// <param name="info">The output layout.</param>
    /// <param name="tone">The tone.</param>
    /// <param name="gpu">The shared graphics context.</param>
    /// <returns>Mean completion time and managed allocations.</returns>
    private static ToneTiming MeasureWarm(SKImage source, SKImageInfo info, PageTone tone, GRContext gpu)
    {
        long ticks = 0;
        GpuProbeEventScope.Start("WarmTone", Repetitions);
        var allocationStart = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < Repetitions; index++)
        {
            var start = Stopwatch.GetTimestamp();
            using var image = Apply(source, info, tone, gpu);
            gpu.Flush(true, true);
            ticks += Stopwatch.GetTimestamp() - start;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        GpuProbeEventScope.Stop("WarmTone");
        return new((double)ticks * MillisecondsPerSecond / Stopwatch.Frequency / Repetitions, (double)allocated / Repetitions);
    }

    /// <summary>Compares one explicit GPU readback with the CPU tone result.</summary>
    /// <param name="gpu">The shared context.</param>
    /// <param name="colorType">The supported channel order.</param>
    /// <param name="colorSpace">The sRGB colour space.</param>
    /// <param name="tone">The tone.</param>
    /// <returns>The mismatch summary.</returns>
    /// <exception cref="InvalidOperationException">The GPU tone target or readback fails.</exception>
    private static PaletteResult ComparePalette(GRContext gpu, SKColorType colorType, SKColorSpace colorSpace, PageTone tone)
    {
        var reference = CreatePalette();
        var input = (byte[])reference.Clone();
        tone.Apply(reference);
        if (colorType == SKColorType.Rgba8888)
        {
            SwapRedAndBlue(input);
        }

        var info = new SKImageInfo(PaletteSide, PaletteSide, colorType, SKAlphaType.Premul, colorSpace);
        using var source = SKImage.FromPixelCopy(info, input, PaletteSide * BytesPerPixel);
        using var toned = Apply(source, info, tone, gpu);
        if (!toned.IsTextureBacked)
        {
            throw new InvalidOperationException("The tone parity image is not GPU-backed.");
        }

        gpu.Flush(true, true);
        var actual = new byte[input.Length];
        if (!ReadPixels(toned, info, actual))
        {
            throw new InvalidOperationException("The explicit GPU tone readback failed.");
        }

        if (colorType == SKColorType.Rgba8888)
        {
            SwapRedAndBlue(actual);
        }

        return Compare(reference, actual);
    }

    /// <summary>Builds opaque BGRA pixels with varied colours and every red and green byte.</summary>
    /// <returns>The source grid.</returns>
    private static byte[] CreatePalette()
    {
        var pixels = new byte[PaletteSide * PaletteSide * BytesPerPixel];
        for (var y = 0; y < PaletteSide; y++)
        {
            for (var x = 0; x < PaletteSide; x++)
            {
                var offset = ((y * PaletteSide) + x) * BytesPerPixel;
                pixels[offset] = (byte)(((x * BlueX) + (y * BlueY)) & ChannelMax);
                pixels[offset + GreenOffset] = (byte)y;
                pixels[offset + RedOffset] = (byte)x;
                pixels[offset + AlphaOffset] = ChannelMax;
            }
        }

        return pixels;
    }

    /// <summary>Converts between packed BGRA and RGBA channel order in place.</summary>
    /// <param name="pixels">The pixels.</param>
    private static void SwapRedAndBlue(byte[] pixels)
    {
        for (var offset = 0; offset < pixels.Length; offset += BytesPerPixel)
        {
            var red = pixels[offset + RedOffset];
            pixels[offset + RedOffset] = pixels[offset];
            pixels[offset] = red;
        }
    }

    /// <summary>Reads a completed GPU image back for the explicit parity check.</summary>
    /// <param name="image">The GPU result.</param>
    /// <param name="info">The output layout.</param>
    /// <param name="pixels">The destination bytes.</param>
    /// <returns>Whether readback succeeded.</returns>
    private static unsafe bool ReadPixels(SKImage image, SKImageInfo info, byte[] pixels)
    {
        fixed (byte* pointer = pixels)
        {
            return image.ReadPixels(info, (nint)pointer, PaletteSide * BytesPerPixel);
        }
    }

    /// <summary>Counts byte differences and their largest magnitude.</summary>
    /// <param name="expected">The CPU pixels.</param>
    /// <param name="actual">The GPU pixels.</param>
    /// <returns>The mismatch summary.</returns>
    private static PaletteResult Compare(byte[] expected, byte[] actual)
    {
        var different = 0;
        var maximum = 0;
        var first = -1;
        for (var index = 0; index < expected.Length; index++)
        {
            var delta = Math.Abs(expected[index] - actual[index]);
            if (delta == 0)
            {
                continue;
            }

            different++;
            first = first < 0 ? index : first;
            maximum = Math.Max(maximum, delta);
        }

        return new(different, maximum, first, actual.Length);
    }

    /// <summary>Completed tone timing and allocation totals.</summary>
    /// <param name="Milliseconds">Mean completed GPU time per tile.</param>
    /// <param name="ManagedBytesPerOperation">Managed bytes per tile.</param>
    private readonly record struct ToneTiming(double Milliseconds, double ManagedBytesPerOperation);

    /// <summary>The byte-level output comparison.</summary>
    /// <param name="DifferingBytes">The number of mismatched channel bytes.</param>
    /// <param name="MaxChannelDelta">The largest absolute channel difference.</param>
    /// <param name="FirstByte">The first mismatched byte index, or -1.</param>
    /// <param name="ReadbackBytes">The bytes read once for this tone.</param>
    private readonly record struct PaletteResult(int DifferingBytes, int MaxChannelDelta, int FirstByte, int ReadbackBytes);
}
