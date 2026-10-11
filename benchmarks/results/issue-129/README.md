# Page-tree cursor, issue #129

The repository already uses an ancestor cursor in `PageTreeReader`. To test the issue's older width-growing traversal, two isolated checkouts were made from commit `b57d5ef`. One kept the cursor; the other changed only the traversal to push every sibling as a `PendingNode` with inherited attributes. The older algorithm was reconstructed from the issue description because it was not present in repository history. Both versions built cleanly and passed the nine `PageTreeCursorTests` on .NET 10 and 11 before benchmarking.

BenchmarkDotNet ran 5 warmups and 15 measured iterations, pinned to physical cores 0–6. The CPU governor was `powersave` and elevated scheduling priority was unavailable. Times are means in µs except Tiny, which is in ns. Each report includes 99.9% confidence intervals.

| Runtime and workload | Sibling stack | Cursor | Allocation before → after |
|---|---:|---:|---:|
| .NET 10 Tiny | 776.6 ns | 700.3 ns | 792 → 448 B/op |
| .NET 10 Wide, 50 pages | 12.362 µs | 10.920 µs | 18,600 → 8,904 B/op |
| .NET 10 Deep, 32 nodes | 3.349 µs | 2.837 µs | 1,912 → 1,568 B/op |
| .NET 10 PageEditUndo | 39.71 µs | 39.54 µs | 49,811 → 30,404 B/op |
| .NET 11 Tiny | 684.5 ns | 593.9 ns | 792 → 448 B/op |
| .NET 11 Wide, 50 pages | 10.511 µs | 10.762 µs | 18,600 → 8,904 B/op |
| .NET 11 Deep, 32 nodes | 3.078 µs | 2.890 µs | 1,912 → 1,568 B/op |
| .NET 11 PageEditUndo | 37.02 µs | 36.04 µs | 49,801 → 30,409 B/op |

The .NET 11 wide case is about 2.4% slower with the cursor in this run, while .NET 10's wide case and ordinary tiny documents are faster. The page-edit/undo time difference on .NET 10 is unresolved because its confidence intervals overlap. The storage gain is larger than the timing differences: in .NET 11 PageEditUndo, the sibling stack's `PendingNode[]` took about 20,080 B/op across ten backing arrays; the cursor's `AncestorCursor[]` took 688 B/op across two. Returned page objects remain a separate allocation.

EventPipe's allocation audit reported the bytes above for its resolved benchmark windows. It exited cleanly after the edit transaction's touched-object set and before-state list were explained in `benchmarks/allocations-explained.json`. The code keeps one cursor per active ancestor, preserves child order and inherited values, and retains the depth, cycle and cancellation checks. The tests cover wide, deep, mixed, malformed and edit/undo paths. The .NET 11 Native AOT application publish also passed during the rendering work. SIMD does not fit this pointer-driven tree traversal.

Reports and CSV data are in this folder, named `legacy` or `current` plus `net10` or `net11`: [legacy .NET 10 tree](legacy-net10-page-tree.md), [cursor .NET 10 tree](current-net10-page-tree.md), [legacy .NET 11 tree](legacy-net11-page-tree.md), [cursor .NET 11 tree](current-net11-page-tree.md), and the corresponding `page-edit-undo` reports.
