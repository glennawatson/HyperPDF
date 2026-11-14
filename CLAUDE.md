# Project rules

HyperPDF is a local PDF viewer for Linux, Windows and macOS. Primary users are people with ADHD or autism; improve low-vision access alongside. Follow the [comfort rules](../docs/PdViewerLite/COMFORT.md) and the [research](docs/research/README.md). Keep controls clear, stable and adjustable.

- **Frozen collections are banned.** Use `Dictionary` or `HashSet`. The global long-running-application exception does not apply.
- **No UI toolkit and no XAML.** The UI is our own windows and render backends per platform. Never add Avalonia, MAUI, GTK, Qt or a wrapper library.

## Performance

- Low allocations and low execution time are core requirements. Back every change with BenchmarkDotNet, baseline against change under the same conditions. Overlapping error bars are inconclusive. Every feature needs a benchmark.
- Measure allocations with EventPipe and `src/tools/AllocationAudit.cs`, never `MemoryDiagnoser`. Remove every measured allocation; explain one in `src/benchmarks/allocations-explained.json` only when removing it is exhausted or costs a dramatic time penalty. The render and frame hot paths must not allocate. ReactiveUI.Primitives event wiring in view models may allocate; explain it and name the chain.
- Look for fast paths first (early exits, caches, skipping unchanged work), then SIMD and the GPU.
- Run benchmarks and audits as one set: pass every filter to one invocation.

## Implementation

- Behaviour lives in focused static classes with helpers over records that hold the state. `partial` is only for source generators. Callers compose helpers directly; no facades.
- Static fields only for state that never changes after initialisation. Mutable reusable state belongs to the caller's lifetime owner and is passed explicitly.
- Sealed positional records for snapshots, positional readonly record structs for small values, classes for genuinely mutable state and owned lifetimes.
- Never use value tuples, including swaps. Name the shape with a positional record. Deconstruction of records is fine.
- Every project targets `net10.0;net11.0`; Native AOT publishes from `net11.0`. Versions live in `Directory.Packages.props`.
- Shipped code is trim and Native AOT safe: no reflection, generated JSON, Refit clients with generated contexts.
- View models derive from `ReactiveObject` with ReactiveUI.SourceGenerators (`[Reactive]`, `[ReactiveCommand]`, `[ObservableAsProperty]`). Side effects go in `WhenChanged` subscriptions. Use ReactiveUI.Primitives observables, `SubscribeSafe`, `MultipleDisposable` and `static` lambdas; never `+=`; never block on async.
- Hot paths are byte-first and span-based: UTF-8, pooled buffers, measured SIMD, early exits. Shared caches are thread-safe.
- Use the newest .NET 11 APIs. Keep each `#if NET11_0_OR_GREATER` in a small helper in the project's `Compat/` folder with the .NET 10 fallback behind the same signature.
- C# 15 is on. Use unions only for reference-type cases.
- Analyzers: only 0, 1 and -1 inline (hex tables are fine); complexity 10, cognitive 15; no `x++` inside an expression; members of internal types are internal; overloads, not optional parameters; braced switch sections. Fix warnings, never suppress.
- Native code: P/Invokes use `LibraryImport` and live in a `NativeMethods` class per library. The windowing heads and render backends bind the operating system directly (X11, Wayland, Win32, Cocoa, Vulkan, Direct3D, Metal). Ask the owner before adding any other native library. Skia stays only in `HyperPdfLibrary.Render.Skia`. Codec fixes go in the HyperCodecs repository.

## Layout

- `src/HyperPdfLibrary` — managed PDF engine (`Syntax`, `Objects`, `Structure`, `Content`, `Graphics`, `Filters`, `Fonts`, `Text`, `Rendering`, `Creation`, `Writing`, `Editing`, `Signing`, `Security`, `Conformance`, `PdfUa`, `Forms`, `Annotations`). `HyperPdfLibrary.Render.Skia` is its Skia rasteriser, kept for library users.
- `src/HyperPdf.Engine` adapts the library to the app. `src/HyperPdf.Core` is viewer domain logic.
- `src/HyperPdf.ViewModels` — toolkit-free view models and services; what a head supplies comes through `IRenderHead`.
- `src/HyperPdf.Presentation` — toolkit-free input vocabulary, key and pointer routing and page canvas state through `ICanvasHost` and `ITilePainter`. UI state such as scroll offsets lives here, never in a view model.
- `src/HyperPdf.Windowing` — contracts shared by every head: `IPlatformWindow`, `IRenderBackend`, `IFramePass`, `IAccessibilityBridge`.
- `src/HyperPdf.Rendering.Cpu`, `src/HyperPdf.Rendering.Vulkan` — render backends. GPU is the default; CPU is the fallback.
- `src/HyperPdf.Head.Linux` (X11, Wayland, AT-SPI), `src/HyperPdf.Head.Windows` (Win32, UI Automation) — platform windows and accessibility bridges.
- `src/HyperPdf.Viewer` — the viewer shared by the heads: canvas host, frame painter, render head and loop.
- `src/HyperPdf.Platform.*` — operating system services (printing, audio, shell, spelling).
- Tests are in `src/tests`, benchmarks in `src/benchmarks`, single-file tools in `src/tools`.

## HyperPdfLibrary

- Keep the released public API. Additive changes are fine; do not change existing signatures.
- Read leniently, write strictly: every file written conforms to ISO 32000 and to any PDF/A or PDF/UA level it claims. Never add a feature that needs non-conforming output. Never copy from a PDF engine.
- Parses UTF-8 bytes directly; types are thread-safe. Text is UTF-8 by default; UTF-16 only where the PDF spec requires it. Saving preserves existing encodings.
- Async-first public API taking a `CancellationToken`. Async methods prefetch with real async I/O, then run the sync core inline. Pass the token explicitly; long loops check it per chunk. Never `Task.Run`.
- XML is streaming only (`XmlReader`/`XmlWriter`), DTDs prohibited, bounded size.
- Tests, scripts and docs never contain machine-specific paths. Optional inputs come from an environment variable or PATH and skip with a reason when absent.

## Verification

Always pass `-v q --nologo` (and `--no-restore` after the first build). Filter tests to the class or method being worked on until final verification. Warnings are failures. With SDK 11, use absolute paths for `--project`; runner arguments follow `--`. Never read raw `.pdf` fixtures, `.ttf.z` fonts, `verapdf-results.tsv`, `PdfARules.tsv`, `Fonts/Data/*Data.cs`, `src/licenses/notices/*` or the whole `allocations-explained.json` into context; query lines with `git grep`.

```console
dotnet build src/HyperPdf.slnx -v q --nologo
dotnet test --project "$PWD/src/tests/HyperPdfLibrary.Tests/HyperPdfLibrary.Tests.csproj" -f net11.0 -v q --nologo --no-restore -- --treenode-filter '/*/*/ClassName/*'
dotnet run -c Release -f net11.0 --project src/benchmarks/HyperPdf.Benchmarks -- --filter '*Relevant*'
dotnet run --file src/tools/AuditAllocations.cs -- '*Relevant*'
```

Every change to a writer, editor or the optimiser must keep PDF/A output valid. The `HyperPdfLibrary.Tests.Conformance.VeraPdf` tests check this with veraPDF (a separate Java process) and skip without it. Set up with `src/tools/InstallVeraPdf.cs`, `FetchReferencePdfs.cs` and `FetchVeraPdfTestFiles.cs`, then set `HYPERPDF_VERAPDF`, `HYPERPDF_PDFA_SAMPLES` and `HYPERPDF_VERAPDF_TEST_FILES`. `PdfAValidator` must agree with veraPDF on every rule it checks; re-record with `src/tools/RecordVeraPdfResults.cs` after a veraPDF update.

Keep `README.md` for end users. Add no attribution trailers. Follow the user's current commit and publishing instructions.
