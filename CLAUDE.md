# Project rules

HyperPDF is a local PDF viewer for Linux, Windows and macOS. Primary users are people with ADHD or autism. Improve low-vision access alongside those needs. Follow the [comfort rules](../docs/PdViewerLite/COMFORT.md). Use the [research](docs/research/README.md) to guide choices. Keep controls clear, stable and adjustable.

## Performance

- Low allocations and low execution time are core requirements. Back every code change with relevant BenchmarkDotNet measurements. Compare baseline and change under the same conditions. Prefer the lowest measured cost that preserves correctness and access.
- Do not judge performance from source code or subjective impressions. Overlapping timing error bars are inconclusive. Every feature needs a benchmark.
- Measure allocations with EventPipe and `scripts/AllocationAudit.cs`. Never use `MemoryDiagnoser`. Explain every measured allocation in `benchmarks/allocations-explained.json`. The render hot path must not allocate.

## Implementation

- Put behaviour in focused static classes with helper methods that operate on records holding the state. Never grow one type and split it across partial files; `partial` is only for source generators. Callers compose the focused helpers directly; change the downstream code instead of adding a facade.
- Prefer sealed records for data snapshots. Prefer readonly record structs for small values. Use classes for mutable state, services and owned lifetimes. Record collection equality follows the collection's own equality.
- Never use value tuple types: not as return types, fields, parameters, collection elements, or `(a, b) = (b, a)` swaps. Name the shape with a positional record (`record struct` for small values, `record` for reference data). Prefer positional syntax. On hot paths, use readonly record structs that stay on the stack or in pooled arrays, so they allocate nothing.
- Keep deconstruction. Positional records deconstruct, so write `var (key, value) = entry;` and `foreach (var (key, value) in entries)` wherever it reads better than member access.
- Every project targets `net10.0;net11.0`: libraries, the app, tests and benchmarks. Native AOT publishes from `net11.0`. Use `global.json`. Package versions belong in `Directory.Packages.props`.
- Shipped code must be trim and Native AOT safe. Use generated JSON, bindings and HTTP clients. Avoid reflection and activation APIs that require unreferenced code.
- XAML is layout and styling only. Views implement `IViewFor<T>`. Use theme resources and visible action labels.
- Views bind in the constructor inside `this.WhenActivated(disposables => ...)`. Use `OneWayBind` and `Bind` for view model to view, `BindCommand` for commands and `BindInteraction` or `RegisterHandler` for interactions. Use `WhenChanged` only to react to a change, not to copy a value into a control. Bind through the view's `ViewModel` path, never a view model captured at activation. Rx operators use `static` lambdas that read from the value they receive, not captured state. A capturing lambda is a last resort when no static form exists; comment why at the lambda. Exception: a terminal `SubscribeSafe` callback may capture `this` to call an instance member (`SubscribeSafe(_ => Close(), OnError)`), because Primitives has no state-passing `SubscribeSafe` yet; do not use `DoWith` plus an empty subscriber to avoid it.
- View models derive from `ReactiveObject`. Use ReactiveUI.SourceGenerators: `[Reactive]` partial properties, `[ReactiveCommand]` methods, and ReactiveUI.Binding's `[ObservableAsProperty]` with `ToProperty` for derived values. Put side effects in `WhenChanged` subscriptions, not setters.
- Notifications use ReactiveUI.Primitives observables and `SubscribeSafe`. Own subscriptions with `MultipleDisposable`. Observe events with ReactiveUI.Primitives.ObservableEvents `Events()`; never subscribe with `+=`. Do not block on async.
- HTTP goes through Refit clients with generated JSON contexts.
- Write byte-first and span-based code on hot paths: UTF-8 bytes, pooled buffers, SIMD where measured to help, and early exits. Shared caches are thread-safe; publish them with `Volatile` or `Interlocked`, or guard them with a `Lock`.
- Use the newest .NET 11 APIs, such as span `ZLibDecoder`/`ZLibEncoder` and `ReadOnlyMemoryStream`. Keep every `#if NET11_0_OR_GREATER` branch in a small static helper in the project's `Compat/` folder, with the .NET 10 fallback behind the same signature. Feature code calls the helper.
- C# 15 is on: unions, `[with(...)]` collection arguments, `field`, `allows ref struct`. Unions compile on `net10.0` through a `UnionAttribute`/`IUnion` polyfill in `Compat/`. Use them only for reference-type cases, because value cases box.
- Analyzer habits:
  - Only 0, 1 and -1 inline; name other numbers as constants. Hex data tables are fine.
  - Keep methods small: complexity 10, cognitive complexity 15. Use table-driven dispatch.
  - No `x++` inside an expression.
  - Members of internal types are internal.
  - Use overloads, not optional parameters.
  - Braced switch sections.
  - Run `dotnet format analyzers --diagnostics <ids>` for mechanical fixes.
- Hold `PdfiumLibrary.EnterScope()` for every PDFium call. Keep native resources owned and disposed.

## HyperPdfLibrary

- `src/HyperPdfLibrary` is the managed PDF engine; `src/HyperPdf.Engine` adapts it to the app's interfaces. Neither references PDFium; `EngineIndependenceTests` checks this. The app always opens documents with HyperPDF.
- Match PDFium's behaviour where useful, and use pdf.js where PDFium is clearly wrong. Add parity tests comparing both engines through `EnginePair`.
- Read leniently, write strictly. Open and recover malformed files where possible, but every file the library writes must conform to ISO 32000 (and to any PDF/A or PDF/UA level the file claims). Saving fixes what can be fixed in the parts it writes, such as wrong `/Length`, broken cross-reference data, missing required keys and invalid boxes. The viewer tells the user in plain words when a file was damaged and repaired on open, and what saving will fix. Never add a feature that needs non-conforming output.
- Where PDFium blocks a standards feature (PDF 2.0, PDF/A, PDF/UA, PDF/R, XFDF, XMP), follow the standard. Keep a way to get PDFium-compatible output so parity tests still compare. PDFium remains a test and parity reference, not an app engine.
- Read references in `~/source/pdf` (pdfium, pdf.js, pdfbox, qpdf, pdfpig) for behaviour only. MuPDF, Poppler and FreeType are copyleft: read them, never copy them. PdfPig is not a model for .NET code.
- The engine parses UTF-8 bytes directly and its types are thread-safe: documents are used from many render threads at once.
- The public API is async-first: every public operation has an async form taking a `CancellationToken` (`ValueTask` where results are often ready, `IAsyncEnumerable` for long walks). Async methods prefetch the bytes they need with real async I/O, then run the synchronous core inline. Pass the token explicitly through parsing, decoding, rendering and writing, including JPX, JBIG2, JPEG, Flate and font paths; spans and `ref struct`s never cross an `await`, and the library never uses `Task.Run`. Long loops check the token cheaply (per chunk, segment or operator batch, never per byte, never allocating). The sync API stays the fast path for warm calls. Callers that may wait on I/O or need to cancel (open, first page, search, save, tab switches) use the async forms. The NuGet package builds without runtime async so Mono hosts can load it; the app builds with it.
- Text is UTF-8 by default: byte constants use `u8` literals, and parsing, XML and exports work in UTF-8. Use UTF-16 only where the PDF spec requires it (UTF-16BE text strings before PDF 2.0, ToUnicode CMap values, font name and cmap tables). Write new PDF text strings as PDFDocEncoding when they fit; otherwise UTF-8 with its prefix in PDF 2.0 files, UTF-16BE in earlier versions. Saving preserves existing encodings: unchanged strings and packets keep their bytes, and an edited value keeps the encoding of the value it replaces (UTF-16BE stays UTF-16BE).
- XML (XMP, XFDF, XFA packets) is processed by streaming only: forward-only `XmlReader`/`XmlWriter` over the bytes, DTDs prohibited, no resolver, bounded size. Never `XDocument`, `XmlDocument` or LINQ to XML.
- Follow the analyzers and existing C# style. Document members, name constants and use explicit enum values. Fix warnings; do not add suppressions. The existing tools-only SST1449 exception is authorised.
- Use .NET tools only. Use Roslyn for structural C# edits. Run single-file tools with `dotnet run --file`. Keep docs short. Keep research sources and limits. Add no attribution trailers or generated-by footers.

## Verification

Native AOT publishing needs a native compiler and zlib development files. Run the app with `dotnet run -f net11.0 --project src/PdfViewerLite.App -- some.pdf`; install a Linux build with `packaging/linux/install.sh`. Keep `README.md` for end users only; build, test and contributor notes belong here.

```bash
dotnet build HyperPdf.slnx
dotnet test --solution HyperPdf.slnx
dotnet run -c Release -f net11.0 --project benchmarks/HyperPdf.Benchmarks -- --filter '*RelevantBenchmark*'
scripts/audit-allocations.sh '*RelevantBenchmark*'
scripts/publish-linux.sh linux-x64
```

### PDF/A output

Every change to a writer, editor or the optimiser must keep PDF/A output valid. The `HyperPdfLibrary.Tests.Conformance.VeraPdf` tests check generated and corpus files with veraPDF, which runs as a separate Java (11+) process and is never linked or shipped. They skip without veraPDF; CI's `PDF/A output validation (veraPDF)` job requires it.

```bash
dotnet run --file scripts/InstallVeraPdf.cs -- ~/.cache/pdfviewerlite/verapdf
dotnet run --file scripts/FetchReferencePdfs.cs -- tests/reference-pdfs/manifest.json ~/.cache/pdfviewerlite/pdfa-corpus <the verapdf-pdfa* ids in the manifest>
PDFVIEWERLITE_VERAPDF=~/.cache/pdfviewerlite/verapdf PDFVIEWERLITE_PDFA_CORPUS_DIR=~/.cache/pdfviewerlite/pdfa-corpus \
  dotnet run --project "$PWD/tests/HyperPdfLibrary.Tests/HyperPdfLibrary.Tests.csproj" -f net11.0 -- --treenode-filter '/*/HyperPdfLibrary.Tests.Conformance.VeraPdf/*/*'
```

The installer reads the release from `verapdf.json`, which Renovate updates. Set `PDFVIEWERLITE_PDFA_OUTPUT` to keep the files the tests write. CI lists the corpus ids in `.github/workflows/ci.yml`.

Warnings are failures. Verify all affected frameworks, saved output and user-visible behaviour. TUnit uses Microsoft.Testing.Platform; runner arguments follow `--`. With SDK 11, use absolute paths for `--project`. UI tests and benchmarks run headless. Set `HYPERPDF_SCREENSHOTS` to save UI frames.

Continue authorised goal work through verification. Treat feedback as work to fix. Report concrete blockers and missing dependencies. Follow the user's current commit and publishing instructions.
