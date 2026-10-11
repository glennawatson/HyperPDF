#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0
#:project ../src/PdfViewerLite.HyperPdf/PdfViewerLite.HyperPdf.csproj
#:project ../src/PdfViewerLite.Pdfium/PdfViewerLite.Pdfium.csproj

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using HyperPdfLibrary.Filters;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.HyperPdf;
using PdfViewerLite.Pdfium;

namespace PdfViewerLite.Scripts;

/// <summary>Measures a cold tile from each page of a scanned PDF in a separate process per engine.</summary>
internal static class MeasureScannedBook
{
    /// <summary>The side of one rendered tile.</summary>
    private const int TileSide = 512;

    /// <summary>The bytes in one BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The reporting interval in pages.</summary>
    private const int ReportInterval = 100;

    /// <summary>The required argument count.</summary>
    private const int RequiredArguments = 2;

    /// <summary>The argument count when a page limit is given.</summary>
    private const int LimitedArguments = 3;

    /// <summary>The argument count when a page limit and scale are given.</summary>
    private const int ScaledArguments = 4;

    /// <summary>The default device pixels per point.</summary>
    private const float DefaultScale = 1;

    /// <summary>The divisor used to select the median sample.</summary>
    private const int MedianDivisor = 2;

    /// <summary>Bytes in one MiB.</summary>
    private const double MiB = 1024D * 1024;

    /// <summary>Bytes in one MiB for configuring decoder scratch reuse.</summary>
    private const long BytesPerMiB = 1024L * 1024;

    /// <summary>The diagnostic environment variable for a scratch-pool budget.</summary>
    private const string ScratchBudgetSetting = "PDFVIEWERLITE_MEASURE_SCRATCH_MIB";

    /// <summary>The 95th percentile as a fraction.</summary>
    private const double P95 = 0.95;

    /// <summary>Runs one engine on a whole book, or a stated prefix for a quick probe.</summary>
    /// <param name="args">Engine, local PDF path, optional maximum page count, and optional scale.</param>
    /// <returns>A task for the measurement.</returns>
    /// <exception cref="ArgumentException">The engine or arguments are invalid.</exception>
    /// <exception cref="InvalidOperationException">A page fails to render.</exception>
    internal static async Task Main(string[] args)
    {
        if (args.Length is < RequiredArguments or > ScaledArguments)
        {
            throw new ArgumentException("Usage: MeasureScannedBook hyperpdf|pdfium <PDF path> [max pages] [scale]");
        }

        IDocumentEngine engine = args[0] switch
        {
            "hyperpdf" => new HyperPdfEngine(),
            "pdfium" => new PdfiumEngine(),
            _ => throw new ArgumentException("Choose hyperpdf or pdfium."),
        };
        var path = Path.GetFullPath(args[1]);
        var limit = args.Length >= LimitedArguments && int.TryParse(args[2], out var requested) && requested > 0 ? requested : int.MaxValue;
        var scale = ReadScale(args);
        SetScratchBudget();
        var openStart = Stopwatch.GetTimestamp();
        using var document = await engine.OpenAsync(path, null, CancellationToken.None);
        var sizes = document.GetPageSizes();
        var openTime = Stopwatch.GetElapsedTime(openStart);
        var count = Math.Min(document.PageCount, limit);
        var pixelBuffer = new byte[TileSide * TileSide * PixelBytes];
        var times = new double[count];
        var allocatedBefore = GC.GetTotalAllocatedBytes(true);
        using var process = Process.GetCurrentProcess();
        var peakWorkingSet = process.PeakWorkingSet64;
        for (var page = 0; page < count; page++)
        {
            var start = Stopwatch.GetTimestamp();
            var request = new PageRenderInfo(page, scale, PageRotation.None, 0, 0, RenderFlags.None);
            if (!document.Render(request, new(pixelBuffer, TileSide, TileSide, TileSide * PixelBytes)))
            {
                throw new InvalidOperationException($"Page {page + 1} did not render.");
            }

            times[page] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            process.Refresh();
            peakWorkingSet = Math.Max(peakWorkingSet, process.PeakWorkingSet64);
            if ((page + 1) % ReportInterval == 0)
            {
                Console.WriteLine($"progress_pages={page + 1} peak_working_set_mib={peakWorkingSet / MiB:F1}");
            }
        }

        var totalAllocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
        Array.Sort(times);
        var p95Index = Math.Max(0, (int)Math.Ceiling(count * P95) - 1);
        Console.WriteLine($"engine={engine.Name} file={Path.GetFileName(path)} pages={count}/{document.PageCount} sizes={sizes.Length} scale={scale.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"open_and_sizes_ms={openTime.TotalMilliseconds:F3} cold_tile_median_ms={times[count / MedianDivisor]:F3} cold_tile_p95_ms={times[p95Index]:F3} slowest_ms={times[^1]:F3}");
        Console.WriteLine($"peak_working_set_mib={peakWorkingSet / MiB:F1} managed_allocated_mib={totalAllocated / MiB:F1} managed_heap_after_mib={GC.GetTotalMemory(false) / MiB:F1}");
        Console.WriteLine($"scratch_budget_mib={ScratchPools.Budget / MiB:F1} scratch_retained_mib={ScratchPools.RetainedBytes / MiB:F1}");
    }

    /// <summary>Applies an optional decoder scratch budget for a controlled measurement.</summary>
    /// <exception cref="ArgumentException">The supplied budget is invalid.</exception>
    private static void SetScratchBudget()
    {
        if (Environment.GetEnvironmentVariable(ScratchBudgetSetting) is not { } value)
        {
            return;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var megabytes) || megabytes <= 0)
        {
            throw new ArgumentException($"{ScratchBudgetSetting} must be a positive integer.");
        }

        ScratchPools.Budget = checked(megabytes * BytesPerMiB);
    }

    /// <summary>Reads an optional positive display scale.</summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The scale.</returns>
    /// <exception cref="ArgumentException">The supplied scale is invalid.</exception>
    private static float ReadScale(string[] args)
    {
        if (args.Length < ScaledArguments)
        {
            return DefaultScale;
        }

        if (float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && float.IsFinite(parsed) && parsed > 0)
        {
            return parsed;
        }

        throw new ArgumentException("Scale must be a finite positive number.");
    }
}
