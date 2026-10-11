# Scanned-image display resolution, issue #121

The fixtures are the checksum-pinned public-domain books in `tests/corpus/corpus.json`: Internet Archive *United States Reports* volume 341 (928 pages, colour JPEG 2000 with JBIG2 stencil masks) and Google Books *United States Reports* (893 pages, mainly JBIG2). The PDF bytes stay in the local corpus cache.

`scripts/MeasureScannedBook.cs` opened each book in a new .NET 11 Release process, then rendered a 512 × 512 BGRA tile from every page at 150%. Its peak is the process's peak working set. Allocated bytes come from `GC.GetTotalAllocatedBytes`, and cold-tile times use `Stopwatch`. The full-resolution baseline used the same source with `ImageReduction.Select` temporarily returning zero; the script restored the source after each baseline run. The order was reduced, baseline, baseline, reduced. The machine was shared and its CPU governor was `powersave`, so small timing differences are observations rather than a precise speed ratio.

| Book and engine | Peak working set, MiB | Median cold tile, ms | P95 cold tile, ms | Managed allocated, MiB |
|---|---:|---:|---:|---:|
| 928 pages, HyperPDF full decode, run 1 | 1,092.7 | 97.713 | 106.093 | 87,293.7 |
| 928 pages, HyperPDF full decode, run 2 | 1,052.6 | 98.948 | 106.908 | 87,294.2 |
| 928 pages, HyperPDF display decode, run 1 | 567.8 | 40.796 | 49.092 | 24,153.3 |
| 928 pages, HyperPDF display decode, run 2 | 570.2 | 40.462 | 49.286 | 24,155.0 |
| 928 pages, PDFium | 183.5 | 38.367 | 40.469 | 11.3 |
| 893 pages, HyperPDF full decode, run 1 | 767.8 | 58.181 | 64.318 | 15,276.7 |
| 893 pages, HyperPDF full decode, run 2 | 767.9 | 55.123 | 62.311 | 15,276.7 |
| 893 pages, HyperPDF display decode, run 1 | 293.5 | 45.799 | 49.813 | 997.2 |
| 893 pages, HyperPDF display decode, run 2 | 293.7 | 47.792 | 51.621 | 996.9 |
| 893 pages, PDFium | 128.3 | 15.149 | 38.061 | 11.0 |

The reduced JPEG 2000 decoder omits fine wavelet levels. JBIG2 still decodes its packed source bitmap, then averages bit groups into gray or stencil coverage at display size. The image and stencil or soft mask use the same reduction. A page picture is recorded for the upper scale of a power-of-two zoom band; image-cache entries include the reduction level. A closer zoom records a sharper picture. Colour-key masks and unsupported mask codecs stay at full resolution.

At 800% zoom, content-rich 512 × 512 tiles from page 100 of each book were byte-for-byte identical with scan reduction enabled and forced off. The JBIG2 tile had 37,904 nonwhite pixels. The JPEG 2000 tile had nonwhite content across the tile. These checks prove parity for those full-size tiles; they do not establish a PDF conformance oracle for the corpus.

## Binary downsampling

`Jbig2Downsampler.Average` counts packed groups of two, four, eight or sixteen bits with `BitOperations.PopCount`, using a scalar edge path. The output was checked against a pixel-by-pixel reference on odd and aligned dimensions. This is a hardware bit operation where available. The JPEG 2000 wavelet and component transforms and the JBIG2 composer already have `Vector128` or `Vector256` paths. JBIG2 arithmetic coding is context dependent, so its next bit cannot be computed independently as a SIMD lane.

Pinned BenchmarkDotNet runs used 5 warmups and 15 measured iterations on .NET 10 and 11. The table shows representative means; the 99.9% confidence intervals in the linked reports do not overlap for any of the eight input combinations. Both methods wrote into a preallocated output buffer.

| Image and reduction | .NET 10 scalar → bit count | .NET 11 scalar → bit count |
|---|---:|---:|
| 1,879 × 3,059, one halving | 7,253.8 → 3,904.6 µs | 7,007.0 → 4,004.0 µs |
| 1,879 × 3,059, four halvings | 3,934.7 → 313.5 µs | 3,941.7 → 299.5 µs |
| 3,406 × 5,270, one halving | 22,531.7 → 12,538.1 µs | 21,776.4 → 12,119.3 µs |
| 3,406 × 5,270, four halvings | 12,506.8 → 956.3 µs | 12,482.8 → 943.7 µs |

Full results: [.NET 10](jbig2-downsample-net10.md), [.NET 11](jbig2-downsample-net11.md). The CSV files beside them contain each case. The governor was `powersave`; the process was pinned to physical cores 0–6, and higher scheduling priority was unavailable.

## Allocation and memory audit

EventPipe covered the first 100 pages of each exact book at 150% in .NET 11 Release. The complete pass's managed allocation totals are in the first table. In the JPEG 2000 trace, 1,102 GC allocation ticks sampled 1,031 MB of `int[]` at `ScratchPool<int>.Allocate`, 675 MB of `int[]` at `JpxDecodedImage`, 635 MB of `byte[]` at `PixelMemory.Allocate`, and 247 MB of `byte[]` at `JpxImageDecoder.Convert`. In the JBIG2 trace, 234 ticks sampled about 75 MB of pixel arrays and 36 MB of other decoder arrays. These sampled object sizes are diagnostic evidence, not a sum of all allocated bytes. `PictureWeight` had no resolved allocation tick in either trace; that does not prove it allocates nothing. Many profiler samples had no managed frame, and EventPipe does not attribute native Skia memory.

A scratch-pool trial increased its per-type slots from 12 to 24 while retaining the 48 MiB byte limit. On two 100-page runs this reduced allocations by about 65 MiB, but a full-book run raised peak working set from 567.8 to 633.7 MiB. The 12-slot setting remains in use. The remaining JPEG 2000 scratch churn and the JBIG2 arithmetic decoder's cold time are measured limits of this change. At 150%, the final HyperPDF working set remains about three times PDFium's on the 928-page book and more than twice PDFium's on the 893-page book; JBIG2 cold tiles remain about three times PDFium's median.
