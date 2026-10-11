# Issue 137 rendering measurements

`RenderWorkloadBenchmarks` measures 512 × 512 CPU tiles after opening, first recording, cached replay, a zoom step and a scrolled tile. Its generated text, scan and transparency PDFs are committed benchmark inputs. Set `PDFVIEWERLITE_BENCHMARK_CHART` to a local chart path for local testing. The file is read during setup and is never copied into the repository.

Set `PDFVIEWERLITE_BENCHMARK_USGS_RASTER` and `PDFVIEWERLITE_BENCHMARK_USGS_LAYERED` to the checksum-verified files fetched by `scripts/FetchComparisonCorpus.cs`. Set `PDFVIEWERLITE_BENCHMARK_EXTERNAL_ONLY=1` to measure only configured external PDFs. The map identities, licences and checksums are in `tests/render-corpus/corpus.json`.

The initial CPU baseline is commit `b57d5ef`. The current benchmark source was copied into a detached checkout of that commit so baseline and change use the same workload. The Linux run used Fedora 45, Ryzen 7 5800X, .NET SDK 11.0.100-rc.1, cores 0–3 via `taskset`, 5 warmups and 15 measured iterations. The CPU governor was `powersave`; high-priority scheduling was unavailable (`ulimit -e` was 0). Other builds and desktop work used the machine during the run. Both framework runs enabled EventPipe allocation tracing, which adds timing overhead. These times are CPU tile times, not end-to-end frame or completed GPU times.

| Runtime | Baseline report | CSV |
| --- | --- | --- |
| .NET 10 | [Report](baseline-net10.md) | [CSV](baseline-net10.csv) |
| .NET 11 | [Report](baseline-net11.md) | [CSV](baseline-net11.csv) |

`OwnedRenderTargetBenchmarks` compares the new target API against the caller-buffer API on the same software Skia device. This isolates the API shape from GPU driver behavior. On .NET 10, the caller buffer was 6.843 ± 0.074 ms and the owned raster target was 6.834 ± 0.125 ms. On .NET 11, they were 6.800 ± 0.071 ms and 6.940 ± 0.157 ms. The 99.9% intervals overlap on both runtimes. EventPipe found 0.0 managed B/op for both paths over 1,920 measured operations each. Full reports: [.NET 10](owned-target-net10.md), [.NET 11](owned-target-net11.md).

The .NET 11 EventPipe audit measured 0.0 managed bytes per operation in all nine warm replay, zoom and scroll cases. Each case included 480–3,840 measured operations. Cold scan opening and rendering measured about 1.07 MB per operation, largely decoded image pixels. Every observed cold type/frame pair from both frameworks has an explanation in `benchmarks/allocations-explained.json`. JIT inlining groups some constructors under benchmark methods, so those frames do not identify the precise nested constructor. The .NET 10 timing intervals for text cold opening and some scroll or recording cases were wide during concurrent compilation. No speed comparison follows from these baseline runs alone.

For the .NET 11 CPU comparison, the same benchmark source ran baseline (`b57d5ef`), change, change, baseline in one session with EventPipe CPU sampling. Every cold and warm text, scan and transparency pair had overlapping BenchmarkDotNet 99.9% timing intervals. `CpuAudit.cs` found no case with more than 5% growth in both pairs. It attributed almost all warm tile CPU time to `SkiaDrawingSession.DrawPageContent`; cold text also spent about 28% in glyph content processing. Several intervals widened when other compilation and document builds used the host. The evidence does not resolve a CPU timing improvement or regression from this change.

| Pass | Baseline report | Change report |
| --- | --- | --- |
| First pair | [Baseline A](ab-net11-baseline-a.md) | [Change A](ab-net11-change-a.md) |
| Confirmation pair | [Baseline B](ab-net11-baseline-b.md) | [Change B](ab-net11-change-b.md) |

The method-level and run-by-run EventPipe output is in [cpu-audit-net11.txt](cpu-audit-net11.txt).

## Completed GPU work on Linux

`PdfViewerLite.GpuProbe` leases the active Avalonia compositor context and renders into a GPU-backed `SKSurface` on that context's thread. Every GPU sample calls `GRContext.Flush(true, true)`, so its elapsed time includes command completion on the device. Page parsing and recording finish before replay is measured. The cached-image draw samples use the compositor canvas; they do not include the later window swap or display presentation. The table below uses the identity page tone.

The probe ran on a private Weston headless GL compositor using the AMD Radeon RX 7900 XTX. Each warm value is the mean of 100 operations in one run. The .NET 11 workloads were also run three times from the initial probe: Morrison warm GPU means were 0.179, 0.181 and 0.185 ms; Lost City means were 8.112, 8.093 and 8.089 ms; WAC centre means were 0.093, 0.101 and 0.102 ms. The maintained project was then run on both frameworks:

| PDF and 512 px tile | Runtime | First GPU replay + completion | Warm GPU replay + completion | Warm CPU replay | Cached image draw + completion | Skia GPU cache after |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| USGS Morrison, origin | .NET 10 | 39.793 ms | 0.200 ms | 17.821 ms | 0.075 ms | 90,590,544 B |
| USGS Morrison, origin | .NET 11 | 45.886 ms | 0.214 ms | 17.679 ms | 0.089 ms | 90,590,544 B |
| USGS Lost City, origin | .NET 10 | 40.771 ms | 8.552 ms | 12.606 ms | 0.075 ms | 7,995,114 B |
| USGS Lost City, origin | .NET 11 | 38.199 ms | 9.193 ms | 13.005 ms | 0.083 ms | 7,995,114 B |
| Local WAC Sydney, offset (3000, 2000) | .NET 10 | 18.662 ms | 0.106 ms | 9.257 ms | 0.076 ms | 14,865,760 B |
| Local WAC Sydney, offset (3000, 2000) | .NET 11 | 17.333 ms | 0.101 ms | 8.850 ms | 0.082 ms | 14,865,760 B |

The WAC origin tile was nearly empty, so the centre tile was measured. WAC bytes stay on the local machine. Each run's initial Skia GPU cache was 1,048,576 B. The cache figure includes context resources and is not a complete device-memory accounting. The code performs no GPU readback in these timed paths. Skia does not expose exact image-upload byte counts here; first replay includes upload plus drawing and completion. The managed allocation counter measured 0 B/op in each warmed CPU replay, GPU replay and cached draw. EventPipe markers separately delimit GPU replay for allocation auditing. These point estimates show this device's behavior; they do not establish an end-to-end frame latency or a cross-platform performance ranking.

The generated workloads exposed a different shape on the same Radeon. These are completed warm 512 px tile replays at the page origin, each compared with the same page's CPU replay. The probe also ran a 128 px scroll offset and 125% zoom on both runtimes. Cached GPU image draw completion was about 0.07–0.09 ms across these cases.

| Fixture | .NET 10 GPU / CPU | .NET 11 GPU / CPU |
| --- | ---: | ---: |
| Dense text | 11.983 / 4.061 ms | 7.768 / 3.584 ms |
| Scan | 0.088 / 17.067 ms | 0.086 / 15.764 ms |
| Transparency | 0.594 / 6.992 ms | 0.458 / 7.072 ms |

Uncached text replay is slower on this GPU. First synthetic GPU replays ranged from about 13 to 109 ms across fixture and zoom combinations. Retaining completed tile images makes later composition cheap, but a first visible text tile can still miss a frame deadline. These runs support no blanket GPU speed claim.

The text fixture contains roughly 2,900 glyph outlines in 56 lines. Skia's [Ganesh atlas path cache](https://skia.googlesource.com/skia/+/a2d00a28c563/src/gpu/ganesh/ops/AtlasPathRenderer.h) includes a path generation ID and matrix in its key, and the [Flutter project has reported](https://github.com/flutter/flutter/issues/33849) high cost from many separate path draws. These sources suggest that rebuilding an `SKPath` for each repeated glyph can lose useful path identity. They do not identify the whole cost of this PDF workload: its warm GPU submission alone took 3.333 ms before the change, and completed replay took 8.009 ms, while CPU replay took 3.567 ms.

The renderer now reuses up to 512 glyph paths per page recording, keyed by font and character code. On the same Radeon and 512 px text fixture, two subsequent .NET 11 runs measured 2.708/2.798 ms for warm GPU submission and 6.783/7.040 ms for completed warm replay. First completed replay was 19.270/19.276 ms, versus 44.219 ms in the earlier run; recording was 74.555/75.803 ms, versus 81.120 ms. Skia's reported GPU resource count after replay was 65, versus 1,597. This was a before/after probe across separate runs, not a same-session randomized benchmark, so timing differences are indicative. The resource count reduction is clear. The standards probe still passed all 16 GPU parity cases, including text, with zero material interior differences and zero oracle failures.

The path cache alone did not batch draws, and [Skia's atlas key includes each glyph's translation](https://skia.googlesource.com/skia/+/a2d00a28c563/src/gpu/ganesh/ops/AtlasPathRenderer.cpp#170). A bounded vector-path group now combines up to four separate opaque glyphs of one solid colour before recording one draw. In paired A/B/B/A runs on both runtimes, warm completed GPU replay fell from 6.60–6.81 ms with the path cache alone to 0.59–0.64 ms with grouping. CPU replay fell from 3.43–3.52 ms to 3.17–3.23 ms. The first GPU replay did not show a resolved change. [The paired measurements and pixel comparison](glyph-batch-abba.md) include the cache-memory tradeoff and the minor antialiased-edge differences. `SKCanvas.DrawAtlas` in the local SkiaSharp checkout draws bitmap sprites; the selected implementation retains vector outlines and uses SkiaSharp's existing native path-builder binding.

Explicit GPU readback checks compared the four page tones against `PageTone.Apply` at 256 × 256 pixels: SoftPaper, CalmNight, Dark and HighContrast each had zero differing bytes. Sixteen standards cases compared GPU output with CPU output; eight have independent interior-colour oracles. All had zero material interior differences and zero oracle failures. Fourteen were byte exact. The axial gradient differed at 512 pixels and the transparency group at 2,304 pixels, each by at most one channel unit from GPU quantization, with no edge-coverage differences. Readback occurred only in these parity checks. The tone filter took 6.388 ms to compile once, 2.612 ms for its first completed pass and 0.097 ms per warm completed pass in 40 samples; a new toned image allocated 2,064 managed bytes per pass. Production tones a tile once and retains its image.

EventPipe found 0.0 managed B/op over 100 warm GPU replays and 100 cached-image draws. The first GPU replay sampled 1,424 B, including one 24 B drawing session and first-use Skia handle bookkeeping. Repeating the new-tile surface, replay and snapshot 100 times measured 2,008 managed B/tile by the thread allocation counter; EventPipe sampled 1,056.7 B/tile, mostly Skia wrappers and handle tables. The sampled count underestimates the counter and some nested constructors remain unresolved; the exact type/frame observations and limits are in `benchmarks/allocations-explained.json`.

## Prewarm experiment and fallback

### Vulkan and X11 fallback

On the same Radeon through the local DRI3-capable XWayland display, Avalonia's default Vulkan 1.1 instance let the platform create a device but the pinned SkiaSharp backend could not create a `GRContext`. Requesting Vulkan 1.3 left two device procedures unavailable; requesting 1.4 made the context and swapchain work. The viewer now requests Vulkan 1.4 before trying EGL, GLX and software on X11. Windows requests the same Vulkan version before its ANGLE, WGL and software options. A forced unavailable Vulkan instance extension in the probe selected OpenGL and completed a frame, verifying the ordered X11 fallback when platform initialization fails.

The maintained GPU probe ran its 16 standards cases on Vulkan on both .NET 10 and 11. Each run reported zero material interior differences and zero independent-oracle failures against CPU output. Four page tones were byte exact after explicit Vulkan readback on both frameworks. A live viewer run then opened the public-domain Morrison map through its single-instance activation on X11; [its EventPipe summary](vulkan-live.txt) recorded two GPU tile snapshots, one GPU tile presentation, no software tile and 13 Vulkan compositor sessions. The count includes only events after the tracing session attached. These checks prove the local shared-device path and its output; Windows and macOS runtime behavior still needs hardware validation.

The optional compositor prewarm candidate was removed. In a paired private Weston run with a 20-page generated PDF at 100% zoom, enabling it added three GPU prefetches and five compositor sessions, versus two sessions with it disabled. One prefetch callback took 865 µs. The [enabled trace](prewarm-on-100.txt) and [disabled trace](prewarm-off-100.txt) time submission through the platform session, not physical display presentation; that run did not measure a later scroll benefit.

The public-domain USGS Morrison map at 100% zoom made the private compositor stop answering a screenshot request within five seconds when prewarm was enabled; the viewer then remained idle and the compositor did not recover after the viewer exited. On a fresh private Weston compositor, the same map and zoom rendered with prewarm disabled. After removing the candidate, the default viewer rendered the map and remained responsive; [its trace](map-final-100.txt) observed five GPU snapshots and presentations, zero software tiles, and three compositor sessions. A [frame image](map-final-100.png) records the visible result. The observations identify a failure in this local prewarm scenario; they do not isolate the driver-level cause. The retained-image GPU path remains in use.

## Input and cache layout

Synchronous engine opens use automatic source selection: a read-only memory map on Linux and macOS when mapping succeeds, or `StreamPdfByteSource` on Windows and mapping failure. The viewer's new cancellable cold-open path currently chooses the stream source so it can prefetch with real asynchronous I/O. That source has a 4 MiB default page cache with pooled 64 KiB pages; reads spanning at least 256 KiB bypass the cache. The document's decoded-image cache defaults to 64 MiB, and recorded page pictures to 128 MiB. The viewer uses 512 × 512 pixel tiles; a full premultiplied BGRA or RGBA tile is 1,048,576 logical bytes. GPU snapshots are charged by that logical byte size to the existing `TileCache`, and the viewer also caps Skia's GPU resource cache at 128 MiB. The Skia cache counter above includes shared context resources, so it does not isolate one tile's memory.

The GPU probe currently reads its PDF into a byte array before opening it. Its `open_ms` therefore does not measure the application's memory-mapped or paged-stream input path. Those input-cache sizes are source configuration, not measured performance wins. The following tile sweep compares layouts on one GPU and one map; it cannot establish a universal optimum.

A local Radeon/OpenGL layout sweep then held Skia to the viewer's 128 MiB GPU resource-cache limit and rendered the USGS Morrison map at its page origin. Each entry below is the range of two separate 100-replay runs. The colour formats are premultiplied sRGB; the compositor reported BGRA. The probe completes GPU work with `Flush(true, true)` after each operation.

| Tile layout | Logical tile bytes | First completed replay | Warm completed replay | Cached draw completion | Skia cache after |
| --- | ---: | ---: | ---: | ---: | ---: |
| 256 px BGRA | 262,144 | 21.1–21.8 ms | 0.111–0.113 ms | 0.068–0.069 ms | 22,695,252 B |
| 512 px BGRA | 1,048,576 | 34.7–39.1 ms | 0.183–0.203 ms | 0.077–0.079 ms | 90,590,544 B |
| 1024 px BGRA | 4,194,304 | 84.5–87.7 ms | 43.08–43.17 ms | 0.085–0.092 ms | 132,883,107 B |
| 512 px RGBA | 1,048,576 | 35.8–36.5 ms | 0.203–0.206 ms | 0.081–0.083 ms | 90,590,544 B |

The 1024 px case approached the 128 MiB cache limit and its warm replay was much slower on this map. Under Skia's unconstrained default cache, the same case held about 267 MiB and warmed in about 19–22 ms, which shows how strongly this result depends on the memory cap. Four 256 px tiles cover the area of one 512 px tile, so their per-tile draw times cannot be compared as equal frames. Matching the compositor's BGRA layout avoids a channel-order conversion; this small sweep does not resolve a timing difference between 512 px BGRA and RGBA. The measurements support keeping 512 px as the current Linux/Radeon choice while other documents, devices and presentation paths are checked. They do not prove a universal optimum.
