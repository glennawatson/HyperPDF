# Async document pipeline, issue #120

The viewer opens selected tabs, prepares pages, searches and saves through cancellable async APIs. A tab switch cancels the old tab's token, and the render scheduler drops or stops its queued work. Single-instance forwarding and claiming await after the dispatcher starts; window creation then runs on the UI thread. A warm document-pool acquire returns a completed `ValueTask` without managed allocations.

## Fresh open and first content stream

`HyperPdfSourceBenchmarks.OpenFirstPage` and `OpenFirstPageAsync` open the same checksum-pinned, public-domain 928-page book and decode the same first page content stream. Each operation creates a new document. The file is in the operating system's page cache during repeated measurements, so these are fresh document costs, not cold disk latency or a viewer frame time. The first version of the async case also prefetched page resources; that unequal-work run was discarded before these reports.

BenchmarkDotNet used 5 warmups and 15 measured iterations with EventPipe allocation tracing on Linux, pinned to cores 0–6. The CPU governor was `powersave`, and higher scheduling priority was unavailable. The table lists means in milliseconds. The full reports give 99.9% confidence intervals: [.NET 10](cold-open-net10.md), [.NET 11](cold-open-net11.md). The CSV files beside them contain each case.

| Source | .NET 10 sync / async | .NET 11 sync / async |
| --- | ---: | ---: |
| Automatic | 4.992 / 5.060 | 4.733 / 4.695 |
| Mapped | 4.931 / 4.965 | 4.712 / 4.686 |
| Memory | 10.916 / 11.477 | 10.667 / 10.548 |
| Stream, 4 MiB page cache | 6.729 / 11.599 | 6.623 / 11.153 |

The automatic and mapped timing intervals overlap on both frameworks. The .NET 10 memory-source async mean is about 5% higher, with nonoverlapping intervals in this run. The cancellable stream path is about 68–72% slower than synchronous stream opening on this book, with nonoverlapping intervals on both frameworks. It remains the viewer's cold-open path because it can await file reads and stop them when the tab changes. These numbers do not establish performance on uncached storage or another PDF.

A [counted seekable-stream probe](../../../scripts/MeasureAsyncOpenReads.cs) explains the stream gap. With the 4 MiB page cache, the synchronous open made 532 reads returning 34,838,691 bytes. The async open made 523 async reads returning 34,773,155 bytes and 527 subsequent synchronous reads returning 34,537,472 bytes. The 35 MiB book's page-tree walk exceeds the cache, so preloaded pages can be evicted before the synchronous parse uses them. This probe uses a wrapped `Stream`, while the benchmark and viewer use a file handle; its read counts diagnose repeated work but its timings are not a substitute for the file-handle benchmark.

EventPipe sampled about 1.855 MiB per synchronous stream open on both frameworks, versus 2.467 MiB for async on .NET 10 and 2.228 MiB on .NET 11. Cold async prefetch adds offset sorting, visited sets, frontier lists and byte-range arrays. The exact sampled type and frame pairs are explained in `benchmarks/allocations-explained.json`; these cold allocations do not occur in a warm tile replay.

## Warm acquire

`AsyncPipelineBenchmarks` reacquires one already-open HyperPDF document through `DocumentPool`. Both methods returned the same document, and EventPipe measured 0.0 managed bytes per operation on both frameworks. The sync and async means were 10.55 and 15.22 ns on .NET 10, and 10.11 and 11.19 ns on .NET 11. The completed async path has a small CPU cost while preserving the no-allocation contract. Full reports: [.NET 10](warm-acquire-net10.md), [.NET 11](warm-acquire-net11.md).

The app suite includes a tab-switch test that cancels a pending open and returns its rented buffer within a five-second bound. HyperPDF tests cancel I/O, page-tree traversal, text extraction and rendering; form and page-edit tests check a token cancelled before mutation. A failed destination replacement leaves a saved snapshot unpublished and keeps the source dirty and pinned until a successful retry. Tests run on both engines where the app uses both.
