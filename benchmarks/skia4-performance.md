# SkiaSharp 4 performance

Measured on 5 October 2026 with SkiaSharp 4.153.1, BenchmarkDotNet 0.15.8 and .NET 10.0.12 on Linux/Fedora, AMD Ryzen 7 5800X. Runs used CPU 2, five warmups and fifteen measured iterations. The CPU governor was `powersave`; elevated scheduling priority was unavailable. Error values are BenchmarkDotNet's reported confidence interval half-widths.

## Gradients and text

| Workload | Mean | Error |
| --- | ---: | ---: |
| Three-stop gradient, SkiaSharp array factory | 392.6 ns | 3.39 ns |
| Equivalent gradient, native span factory | 177.6 ns | 36.71 ns |
| Shape text using cached HarfBuzz font | 3.544 µs | 0.3614 µs |
| Draw a cached glyph run | 3.291 µs | 0.0266 µs |

The gradient span path used about **55% less time** than the array factory. Their error intervals do not overlap. Both cases create the same three-stop gradient, attach it to a retained paint, then reset the paint. The comparison includes native shader creation and release.

The text cases use DejaVu Sans at 16 pixels and the string `Hyper PDF Viewer: office, AV, search and annotation`. Shaping and drawing are different operations, so their timings are not a speed comparison. Typeface creation and drawing-session setup are outside the measured region.

These comparisons use SkiaSharp 4 in both cases. They measure individual operations, not a whole-app speedup over Avalonia.Skia or a change in FPS. .NET 11 timings, GPU performance, Windows and macOS performance were not measured.

## Rectangle fills

A separate run after build and publish work finished produced the following results on the same pinned core. It replaces the initial rectangle run, whose general-path timings were noisy.

| Workload | Mean | Error |
| --- | ---: | ---: |
| General path, solid 32 × 32 rectangle | 686.3 ns | 4.94 ns |
| Solid rectangle shortcut | 353.1 ns | 2.50 ns |
| Rectangle geometry shortcut | 366.4 ns | 1.09 ns |
| Rounded rectangle, radius 4 | 2,024.9 ns | 7.66 ns |

The solid shortcut used about **49% less time** than drawing the equivalent general path. Rectangle geometry used about **47% less time**. These comparisons have separate error intervals. The rounded rectangle has different pixels and is reported as an absolute cost, not an equivalent-work speed comparison. All cases retain the drawing session and surface.

## Allocations

EventPipe traces and `HyperPdf.AllocationAudit` found no renderer allocation samples in the measured general-path, solid-rectangle, rectangle-geometry or native-gradient cases. The array-factory comparison allocates arrays and shader wrappers; these belong to SkiaSharp and the benchmark harness rather than a renderer-owned allocation site.

Cached text drawing had one sampled 48-byte string with no allocating callee resolved below `DrawGlyphRun`. One tick does not establish a per-draw cost. The trace's 106,048-byte bucket is a sampling weight, not the size of that string or a per-operation allocation total.

The final rounded-fill trace likewise had one 48-byte string with no allocating callee resolved below `DrawRectangle`, weighted at 106,600 bytes. Its source remains unresolved. This is a diagnostic limit rather than evidence of an allocation on every draw.

Separate allocation-counter tests measured zero managed bytes across 1,000 warmed draws of solid rectangles, rounded rectangles, two-stop gradients, rounded gradient fills and cached glyph runs. This excludes setup, native allocations and other threads. Gradients with more than sixteen stops use managed arrays.

Fresh shaping allocates Avalonia's `ShapedBuffer`, pooled-storage wrapper, reference wrapper and reference counter. Glyph storage comes from Avalonia's pool, and retained glyph runs reuse their drawing blob. The trace sampled 348,890,696 B, 96,220,080 B, 91,019,480 B and 87,064,928 B at those respective sites over the trace workload. Those are sampled totals, not per-operation sizes. The explanations are recorded in [allocations-explained.json](allocations-explained.json).

## Correctness and packaging

The solution builds for both library frameworks with zero warnings and errors. The complete test run passed 808 tests and skipped 797, mainly because external PDF and speech assets were unavailable. The Linux Native AOT publish and a dedicated native-gradient drawing probe passed. The published app ran under Xvfb/X11 for twelve seconds with no application exception; the smoke test deliberately ended by timeout.

The restored app graph contains SkiaSharp and its native assets at 4.153.1. Avalonia.Skia and Avalonia.Desktop are absent. Avalonia.X11's unused Skia package dependency is removed through [NuGet package pruning](https://github.com/NuGet/Home/blob/dev/accepted/2024/prune-package-reference.md). Its DLL has no assembly reference to Avalonia.Skia.

HarfBuzzSharp remains for OpenType shaping. The backend has no reflection or private accessors. Unused image brushes are unsupported; direct bitmap drawing remains supported. GPU support uses OpenGL, with software fallback.

Eighteen saved UI frames were compared with an isolated working Avalonia.Skia/SkiaSharp 3 baseline. Most frames had a mean RGB error of 0.00–0.03 on the 0–255 scale. The layers frame captured different PDF-layer state, and the light-theme frame differed more. Incoming icon-style changes also affect the comparison. The comparison does not prove pixel-identical rendering; the semantic rendering tests passed.

## Reproduce

```bash
taskset -c 2 dotnet run -c Release --project "$PWD/benchmarks/HyperPdf.Benchmarks/HyperPdf.Benchmarks.csproj" -- --filter '*GradientBenchmarks*' '*RectangleBenchmarks*' '*GlyphRunBenchmarks*' --warmupCount 5 --iterationCount 15 --artifacts /tmp/pdf-skia-benchmarks
dotnet run --file "$PWD/scripts/AllocationAudit.cs" -- /tmp/pdf-skia-benchmarks "$PWD/benchmarks/allocations-explained.json"
```

Raw reports: [gradients](results/skia4/HyperPdf.Benchmarks.GradientBenchmarks-report-github.md), [rectangles](results/skia4/HyperPdf.Benchmarks.RectangleBenchmarks-report-github.md), [text](results/skia4/HyperPdf.Benchmarks.GlyphRunBenchmarks-report-github.md).
