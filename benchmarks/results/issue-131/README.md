# One-pass text-space compaction, issue #131

`TextLineAssembly.CollapseSpaces` already compacts the character and text buffers with read/write indexes and removes each unused tail once. To compare it with the issue's old repeated-shift behavior, two isolated checkouts were made from commit `b57d5ef`. One kept the current algorithm; the other used `RemoveAt` on both buffers for each repeated space. That older algorithm was reconstructed from the issue description because repository history already contains the one-pass form. Both variants passed `TextSpaceCompactionTests` on .NET 10 and 11 before measurement.

BenchmarkDotNet ran 5 warmups and 15 measured iterations pinned to physical cores 0–6. Its times are means in µs; full reports contain 99.9% confidence intervals. The CPU governor was `powersave` and elevated scheduling priority was unavailable.

| Workload | .NET 10 RemoveAt → one pass | .NET 11 RemoveAt → one pass |
|---|---:|---:|
| Zero repeated spaces | 2.157 → 2.185 µs | 2.052 → 2.079 µs |
| 32 repeated spaces | 7.613 → 6.863 µs | 7.614 → 7.062 µs |
| 256 repeated spaces | 63.135 → 39.889 µs | 63.342 → 40.745 µs |
| 1,024 repeated spaces | 635.467 → 181.434 µs | 595.276 → 142.133 µs |
| Ordinary body text | about 18–19 µs in both | about 17–18 µs in both |

The long-run case is materially faster on both runtimes. At zero repeated spaces, the one-pass form costs roughly 0.03 µs more in these isolated measurements. Normal body-text rows stayed in the same broad range and showed no consistent slowdown across runtimes and parameter rows. EventPipe reported 3,756 B/op for the resolved 32- and 256-space windows and 9,426 B/op for the resolved normal-text window, unchanged between algorithms on both runtimes. It did not resolve the zero- and 1,024-space windows, so those windows have no allocation comparison from this trace. The one-pass method writes into the existing buffers and does not create a new collection.

`TextSpaceCompactionTests` cover leading, trailing, mixed glyph and `/ActualText` runs, character kinds and geometry. `TextSpaceCleanupTests` cover cancellation, a font failure and reusable-state cleanup before retry. Both test classes passed on .NET 10 and 11 in the full solution run. The .NET 11 Native AOT application publish passed. SIMD was not added: the current pass must copy matching entries in two buffers in order, and the issue's measured long-run cost came from repeated `RemoveAt` shifts.

Full reports and CSV data are in this folder: [legacy .NET 10](legacy-net10-text-spaces.md), [one pass .NET 10](current-net10-text-spaces.md), [legacy .NET 11](legacy-net11-text-spaces.md), and [one pass .NET 11](current-net11-text-spaces.md).
