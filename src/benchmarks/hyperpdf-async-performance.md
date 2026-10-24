# HyperPdfLibrary async API: measurements

Measured on 9 October 2026 on an AMD Ryzen 7 5800X. The API itself is described in [docs/hyperpdf/async.md](../docs/hyperpdf/async.md); the benchmarks are the `HyperPdfAsync*` classes in `HyperPdf.Benchmarks`.

Times are means with the half-width of the 99.9% interval. Differences smaller than the two intervals added together
are noise. Each row compares the sync form (S) with the async form (A) in the same run.

### When I/O waits (reads delayed by a throttled stream)

The stream delays every read: blocking for the sync API, awaited for the async API. `Scan` is a 1.5 MB, 44 page
scanned book. The async gain comes from loading the file in a few large requests, not from async alone.

| Case | S .NET 10 | A .NET 10 | S .NET 11 | A .NET 11 |
|---|---:|---:|---:|---:|
| Scan, 1 ms per read, open + 3 pages of text | 31.9 ± 0.4 ms | 7.4 ± 0.2 ms | 31.4 ± 0.4 ms | 6.7 ± 0.1 ms |
| Scan, 5 ms per read | 129.2 ± 0.8 ms | 17.1 ± 0.3 ms | 129.0 ± 1.1 ms | 16.1 ± 0.1 ms |
| Scan, 5 ms per read, 4 tabs at once | 134.1 ± 5.1 ms | 20.5 ± 0.9 ms | 130.9 ± 0.3 ms | 19.3 ± 0.3 ms |
| Report (50 small pages), 5 ms per read | 5.7 ± 0.03 ms | 5.9 ± 0.02 ms | 5.6 ± 0.06 ms | 5.7 ± 0.03 ms |

### Cancellation: the tab swap

The user swaps tabs 60 ms after starting. The sync form cannot stop, so it runs to the end. The async form is
cancelled at 60 ms; its mean minus 60 ms is the cancel latency.

| Work | Sync, runs to end | Async, cancelled at 60 ms | Cancel latency | Bytes allocated after the cancel |
|---|---:|---:|---:|---|
| Open a 150,000-page tree | 300 ms | 61 ms | about 1 ms | 61 MB against 145 MB |
| Render a heavy page (2 pages of 8 MB of drawing) | 2,114 ms | 77 ms | about 17 ms | 5.3 MB against 22.4 MB |
| Extract text of a heavy page | 137 ms | 60.3 ms | under 1 ms | 9.9 MB against 22.4 MB |
| Tab swap: tab A renders, tab B opens and reads text | 2,120 ms | 77 ms | about 17 ms | 6.4 MB against 24.4 MB |

The cancel latencies are the same on .NET 10, .NET 11 with runtime async on, and with it off. The next tab opens at
full speed while the first winds down (tested). Pooled buffers and handles are released: a cancelled render drops its
half-run recording, and every `using` and `finally` runs.

### Warm calls (document open, work done)

All of these allocate **0 bytes** (EventPipe, `src/tools/AllocationAudit.cs`) on mapped, memory and prefetched stream
sources, on .NET 10, .NET 11 with runtime async on, and .NET 11 with it off. A TUnit test
(`AsyncAllocationTests`) checks the same.

| Call | Sync | Async | Source |
|---|---:|---:|---|
| `GetPage` | 2 ns | 7-13 ns | mapped, .NET 11 |
| `GetTextPage` (cached) | 9 ns | 19-27 ns | mapped / stream, .NET 11 |
| `GetLinks` + `GetOutline` | 5 ns | 25-51 ns | mapped / stream, .NET 11 |
| `ScanAnnotations` | 5-23 ns | 10-45 ns | mapped / stream, .NET 11 |
| Render a recorded Report page | 271 ± 1 us | 305 ± 87 us (noisy) | mapped, .NET 11 |
| Render a recorded Report page | 284 ± 55 us | 271 ± 4 us | prefetched stream, .NET 11 |
| Render a recorded Scan page | 18.5 ± 0.1 ms | 20.2 ± 4.0 ms (noisy) | mapped, .NET 11 |
| Render a recorded Scan page | 22.0 ± 2.0 ms | 18.8 ± 0.7 ms | prefetched stream, .NET 11 |

The async wrapper costs 5 to 30 ns and nothing on the heap. A page render is thousands of times longer.

### Cold document, warm file cache (document caches empty)

The operating system's file cache is warm. Means in microseconds, .NET 11 with runtime async on; the other two
configurations differ by less than their intervals except where noted.

| Workload | Source | Sync | Async |
|---|---|---:|---:|
| Open + page count, Form (1 page) | mapped | 30.5 ± 0.5 | 33.6 ± 5.8 |
| Open + page count, Form | file stream | 25.8 ± 4.1 | 31.0 ± 0.6 |
| Open + page count, Report (50 pages) | mapped | 143.6 ± 0.8 | 144.3 ± 1.3 |
| Open + page count, Report | file stream | 138.6 ± 3.7 | 157.9 ± 4.4 |
| Render 10 pages, Report | mapped | 2,807 ± 38 | 3,189 ± 579 (noisy) |
| Render 10 pages, Report | file stream | 2,918 ± 32 | 2,993 ± 50 |
| Extract all text, Report | mapped / stream | 1,191 / 1,267 | 1,232 / 1,262 |
| Extract all text, Scan (44 pages, ms) | mapped / stream | 67.5 ± 13 / 48.4 ± 0.7 | 63.7 ± 13 / 48.4 ± 1.7 |
| Search a word, Report | mapped / stream | 1,209 / 1,266 | 1,220 / 1,262 |
| Outline + links + annotations, Report | mapped | 230 ± 3 | 229 ± 3 |
| Edit + incremental save, Report | mapped / stream | 147 ± 2 / 151 ± 5 | 150 ± 1 / 168 ± 3 |

A separate loop of 300 rounds of "open a file stream, render 10 pages of Report" gave 2,564 us sync and 2,602 us async
once the JIT had settled (1.5% apart). The first pass of the same loop was 4,685 us async and 2,918 us sync, so the
larger gaps in the table above are partly tiered-JIT order and machine noise, not the API. I did not find a
repeatable slowdown beyond the 10 to 25 us the async open adds.

Allocations per operation are the same as the sync forms to within 1.5 KB (for example open: 66,225 B sync and 66,249 B
async; render 10 pages of Report: 199,615 B and 200,995 B).

### Concurrency

Rendering eight recorded pages at once on a mapped Scan document: `Parallel.For` of sync renders 43.5 ± 1.7 ms;
eight async renders each started on the pool by the caller 44.2 ± 6.4 ms; eight async renders started and awaited by
one caller 170.6 ± 6.3 ms. An async form runs its CPU work on the awaiting thread, so it does not create
parallelism. Start each call on the pool (or use `Parallel.ForEachAsync`) to render in parallel.

### .NET 10, .NET 11 runtime async on, and runtime async off

The differences between the three are inside the intervals for the warm calls, the cancel latencies and the slow-stream
cases. The library itself builds with runtime async off when it is packed (`HyperPdfPackageBuild=true`); the
measurements set `DisableRuntimeAsync=true` for the whole build, which also turns it off in the benchmark code.

## Measurement limits

- The machine was shared with other agents. Each run was pinned to three cores and their SMT siblings, but results
  with intervals above about 5% of the mean are noisy. The baseline and the async form always ran in the same
  invocation.
- The operating system's file cache was warm, and cold-disk reads cannot be measured without root. The throttled
  stream tests stand in for slow I/O.
- The `FileStream` rows with 10 iterations are less precise than the longer measurement runs.
- Large images (over 1 MB) and stream bodies over 1 MB are not loaded ahead; they are read when decoded.

