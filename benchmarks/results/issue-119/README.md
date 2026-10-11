# Text hit testing, issue #119

The issue records an older 15.3 µs HyperPDF hit query against PDFium's 9.2 µs and proposes struct-of-arrays storage, a line index and SIMD. The current renderer instead keeps conservative bounds for contiguous 32-character blocks on pages with at least 128 characters. It scans only blocks whose bounds can contain the query and keeps the original character order and nearest-hit tie rule. This index was already present in `TextHitTesting` before this work.

`HyperPdfTextBenchmarks` used the same generated 48-line text page for both engines. The adapter case includes HyperPDF's page lookup and viewer-to-user coordinate conversion, just as the PDFium case includes its adapter call. BenchmarkDotNet ran 5 warmups and 15 measured iterations pinned to physical cores 0–6. The CPU governor was `powersave`, and elevated scheduling priority was unavailable. Each row's 99.9% confidence interval is in the linked report.

| Runtime | PDFium adapter | HyperPDF adapter | HyperPDF cached text page |
|---|---:|---:|---:|
| .NET 10 | 9.979 µs | 0.584 µs | 0.539 µs |
| .NET 11 | 9.924 µs | 0.593 µs | 0.541 µs |

Full reports: [.NET 10](hit-testing-net10.md), [.NET 11](hit-testing-net11.md). The CSV files beside them contain the same measurements. `TextHitTestingBlockTests.CorpusPagesMatchScalarSearch` checked cached corpus pages against an independent scalar reference. `TextHitTestingBlockTests.WarmQueriesDoNotAllocate` and `EngineReadingSuiteTests.WarmHyperPdfHitTestingDoesNotAllocate` measured zero managed bytes over repeated warm queries on both runtimes. The latter checks the adapter call, not just the internal text page.

The proposed SoA and SIMD representation would retain four additional float arrays alongside the public character records. The current block index already exceeds the issue's PDFium throughput target on the measured page and keeps a smaller retained index. No SoA/SIMD implementation was added for hit testing. These numbers establish the stated performance outcome for the benchmark workload; they do not prove every possible hit-query pattern is optimal.
