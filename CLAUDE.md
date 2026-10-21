# Project rules

- **Frozen collections are banned throughout this project.** Use `Dictionary` or `HashSet`, never `System.Collections.Frozen`, `FrozenDictionary`, `FrozenSet`, `ToFrozenDictionary`, or `ToFrozenSet`. The reusable library may run in a short-lived host. The global long-running-application exception does not apply here.

HyperPDF is a local PDF viewer for Linux, Windows and macOS. Primary users are people with ADHD or autism. Improve low-vision access alongside those needs. Follow the [comfort rules](../docs/PdViewerLite/COMFORT.md). Use the [research](docs/research/README.md) to guide choices. Keep controls clear, stable and adjustable.

## Performance

- Low allocations and low execution time are core requirements. Back every code change with relevant BenchmarkDotNet measurements. Compare baseline and change under the same conditions. Prefer the lowest measured cost that preserves correctness and access.
- Do not judge performance from source code or subjective impressions. Overlapping timing error bars are inconclusive. Every feature needs a benchmark.
- Measure allocations with EventPipe and `scripts/AllocationAudit.cs`. Never use `MemoryDiagnoser`. Remove every measured allocation. Explain one in `benchmarks/allocations-explained.json` only after removing it has been exhausted or removing it shows a dramatic time penalty. The render hot path must not allocate. The UI and ReactiveUI work is the exception: this app is a ReactiveUI technology demo, so the small allocations that ReactiveUI.Primitives observables, operators and the generated `Events()` streams make when wiring and raising UI events are allowed. Do not remove them by dropping back to `+=`, Avalonia observable helpers or hand-written handlers. Explain them in `benchmarks/allocations-explained.json` as ReactiveUI event wiring, name the chain, and still remove any allocation in that code that is not part of the Primitives or `Events()` machinery itself (for example boxing, closures that a `static` lambda avoids, or per-event collections).
- Look for fast paths that avoid the computation in the first place (early exits, caches, skipping unchanged work) before optimising the computation itself. Then consider SIMD and the GPU.
- Run benchmarks and allocation audits as one set. Pass every filter to one invocation (`dotnet run --file scripts/AuditAllocations.cs -- '*A*' '*B*'`, or BenchmarkDotNet's `--filter '*A*' '*B*'`) so the build and the run happen once; never loop over families with a separate `dotnet run` each.

## Implementation

- Put behaviour in focused static classes with helper methods that operate on records holding the state. Never grow one type and split it across partial files; `partial` is only for source generators. Callers compose the focused helpers directly; change the downstream code instead of adding a facade.
- **Static fields are allowed if, and only if, their state never changes after initialisation.** Keep fixed rule data and immutable lookup tables static rather than rebuilding them for every operation. A `readonly` reference does not make a mutable cache, pool or counter immutable. Mutable reusable state belongs to the caller's lifetime owner and is passed explicitly to helpers.
- Use sealed positional records for data snapshots and positional readonly record structs for small values. Refactor data-only classes and verbose property-only records into these shapes when touching them. Do not replace them with raw-field data containers. Use classes for genuinely mutable state, services and owned lifetimes, with encapsulated state and proper methods. Raw fields require a concrete need, such as native layout. Record collection equality follows the collection's own equality.
- Pass only the state each focused helper needs. Keep reusable buffers and mutable working state in the caller's owner; do not hide them in global storage or ambient state.
- Never use value tuple types: not as return types, fields, parameters, collection elements, or `(a, b) = (b, a)` swaps. Name the shape with a positional record (`record struct` for small values, `record` for reference data). Prefer positional syntax. On hot paths, use readonly record structs that stay on the stack or in pooled arrays, so they allocate nothing.
- Keep deconstruction. Positional records deconstruct, so write `var (key, value) = entry;` and `foreach (var (key, value) in entries)` wherever it reads better than member access.
- Every project targets `net10.0;net11.0`: libraries, the app, tests and benchmarks. Native AOT publishes from `net11.0`. Use `global.json`. Package versions belong in `Directory.Packages.props`.
- Shipped code must be trim and Native AOT safe. Use generated JSON, bindings and HTTP clients. Avoid reflection and activation APIs that require unreferenced code.
- XAML is layout and styling only. Views implement `IViewFor<T>`. Use theme resources and visible action labels.
- Views bind in the constructor inside `this.WhenActivated(disposables => ...)`. Use `OneWayBind` and `Bind` for view model to view, `BindCommand` for commands and `BindInteraction` or `RegisterHandler` for interactions. Use `WhenChanged` only to react to a change, not to copy a value into a control. Bind through the view's `ViewModel` path, never a view model captured at activation. Rx operators use `static` lambdas that read from the value they receive, not captured state. A capturing lambda is a last resort when no static form exists; comment why at the lambda. Exception: a terminal `SubscribeSafe` callback may capture `this` to call an instance member (`SubscribeSafe(_ => Close(), OnError)`), because Primitives has no state-passing `SubscribeSafe` yet; do not use `DoWith` plus an empty subscriber to avoid it.
- View models derive from `ReactiveObject`. Use ReactiveUI.SourceGenerators: `[Reactive]` partial properties, `[ReactiveCommand]` methods, and ReactiveUI.Binding's `[ObservableAsProperty]` with `ToProperty` for derived values. Put side effects in `WhenChanged` subscriptions, not setters.
- Notifications use ReactiveUI.Primitives observables and `SubscribeSafe`. Own subscriptions with `MultipleDisposable`. Observe events with the ReactiveUI.Primitives.ObservableEvents source generator's `Events()`; never subscribe with `+=`. Do not block on async.
- Build event and binding chains from ReactiveUI.Primitives operators (see its README.md and Skill.md). Do not use Avalonia's own observable helpers (`GetObservable`, routed-event observers such as `ObserveRouted`). A chain shape that repeats goes into one extension block on `IObservable<T>` that holds the shared operators; do not add static or local helper methods on views.
- This is a ReactiveUI-first UI. Attach from the view's constructor with `this.WhenActivated`, owning subscriptions in a `MultipleDisposable`. Inject the state a chain needs at the source, where you call the event or selection observable on the control (`Select(_ => strip)` inside the extension block), not by reading a field or property on the control inside a handler, and not with an outer operator or extra parameters. Use `Where` and `Select` instead of `OnXyz` methods that only wrap an `if`.
- UI rules that follow from that, for every view, control, code-behind file in `src/HyperPdf.App` and every view model in `src/HyperPdf.ViewModels`:
  - Subscriptions are made in the constructor inside `this.WhenActivated` and owned by a `MultipleDisposable`. Controls that need attach and detach logic implement `IActivatableView` and use `WhenActivated`; do not write `Attach` and `Detach` methods or handlers on `AttachedToVisualTree` and `DetachedFromVisualTree`.
  - Events come from the generated `Events()` streams. Where `Events()` cannot see a member (for example controls the XAML name generator creates), read it once from a typed receiver in an extension block; do not fall back to `AddHandler`, `+=`, `GetObservable`, `AddClassHandler` or `Raised`. If a case truly has no documented ReactiveUI way, keep the workaround in one place, comment why at the statement, and list it in the pull request.
  - Marshal to the UI thread with `ObserveOn` and the ReactiveUI schedulers, not `Dispatcher.UIThread`.
  - Shared chain shapes (key chords, press and release, focus, drag and drop, injecting state) live in the extension blocks in `src/HyperPdf.App/Controls`. Read them first and add to them instead of writing a new chain in a view.
  - State travels as the value in the chain, in a small record, not as a captured field. Name each shape with a positional record, never a tuple.
  - Keep behaviour, keyboard access, screen-reader names and the comfort rules unchanged by a rework, and cover new chains with headless App tests, including drag, reorder, wheel and middle-click paths.
  - When ReactiveUI.Primitives or the `Events()` generator lacks something a view needs, say so in the pull request and the ReactiveUI findings notes instead of working around it silently.
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
- Keep native resources owned and disposed.

## HyperPdfLibrary

- `src/HyperPdfLibrary` is the managed PDF engine; `src/HyperPdf.Engine` adapts it to the app's interfaces. The app always opens documents with HyperPDF.
- Codebase layout:
  - `src/HyperPdfLibrary/Syntax/` (`PdfLexer`, `PdfParser`, `PdfStringDecoder`) — Tokenizers, lexing, string decoding.
  - `src/HyperPdfLibrary/Objects/` (`PdfObjectStore`, `PdfDictionary`, `PdfStream`, `PdfValue`) — Core PDF object model, revisions, repairs.
  - `src/HyperPdfLibrary/Structure/` (`XrefReader`, `XrefRepair`, `ObjectStreamIndex`) — Cross-reference tables/streams and object streams.
  - `src/HyperPdfLibrary/Structure/Tagged/` (`PdfStructureTree`, `PdfPageRecogniser`, `LayoutRuledTables`, `LayoutAlignedTables`, `LayoutTableBuilder`) — Tagged PDF structure & table/layout inference.
  - `src/HyperPdfLibrary/Content/` & `Graphics/` — Content stream parser, operators, graphics state, color spaces.
  - `src/HyperPdfLibrary/Filters/` — Stream codecs (`Flate`, `Lzw`, `RunLength`, `Jbig2`, `Jpx`, `Ccitt`, `Dct`).
  - `src/HyperPdfLibrary/Fonts/` — Simple, composite (CID), and Type 3 fonts, CMap/ToUnicode parsing, glyph mapping.
  - `src/HyperPdfLibrary/Text/` & `TextLayer/` (`PdfTextPage`, `TextDevice`, `TextSearch`, `PdfTextLayer`) — Text extraction, BiDi, hit-testing, search.
  - `src/HyperPdfLibrary/Rendering/` (`PdfPageRenderer`, `PageRecorder`, `AnnotationAppearances`) — Display-list recording and appearance streams.
  - `src/HyperPdfLibrary/Creation/` (`PdfDocumentBuilder`, `PdfFlowDocument`, `PdfTableFlow`, `PdfRichTableFlow`, `TableLayout`) — Document & flow-layout builder (tables, text wrapping, rules, images).
  - `src/HyperPdfLibrary/Writing/`, `Editing/`, `Optimizing/`, `Redaction/`, `Signing/`, `Signatures/`, `Security/`, `Conformance/` (`PdfAValidator`), `PdfUa/`, `Xfa/`, `Forms/`, `Annotations/`.
  - `src/HyperPdfLibrary.Render.Skia` — SkiaSharp rasterizer (`SkiaPageRasterizer`, `SkiaContentDevice`).
  - `src/HyperPdf.Core` — Viewer domain logic, layout, navigation, search, reading, settings.
  - `src/HyperPdf.ViewModels` — Toolkit-free view models, services, help text and the render hub, grouped by feature folder (`Markup/`, `Finding/`, `Sidebar/`, `Reading/`, `Output/`, `Security/`, `Signing/`, `FormFilling/`, `Viewing/`, `Preferences/`, `Dialogs/`, `Services/`, `Rendering/`, `Help/`). It must not reference Avalonia or any UI toolkit. Anything a toolkit supplies comes through `IRenderHead` (tiles, frame requests, picture decoding, notices assembly), so another render head (WebAssembly or other) can reuse all of it. Make what a head needs `public`; there is no `InternalsVisibleTo` from it to the head.
  - `src/HyperPdf.Presentation` — Toolkit-neutral UI mechanics between the view models and a head: the input vocabulary (`ViewerKey`, `ViewerModifiers`, `ViewerButtons`, `ViewerCursor`), key and pointer routing, and the page canvas state (layout, scrolling, page by page, wheel and pinch zoom, panning, caret, annotation moving and drawing, position reports, and the frame plan that decides which tiles are drawn and asked for) that works through `ICanvasHost` and `ITilePainter`. It must not reference Avalonia or any other toolkit and holds no PDF viewing logic of its own; UI state such as scroll offsets and zoom easing lives here, never in a view model. A head (Avalonia today, ReactivePresentationFoundation or MAUI later) implements `ICanvasHost` and `ITilePainter`, converts its own key, pointer and cursor types, and paints what the plan says. Text selection is `TextSelection` in `HyperPdf.ViewModels`.
  - `src/HyperPdf.Rendering.Skia` — Skia tile surfaces with no Avalonia dependency: the GPU-prepared tile, the page tone shader, GPU availability and image retirement, and `SkiaBitmapRenderSurface`, the software tile the GPU tile falls back to and draws itself. The head passes the factory through a delegate and reaches a Skia canvas to draw the tiles.
  - `src/HyperPdf.App` — Avalonia + ReactiveUI render head (`Views/`, `Controls/`, `Rendering/`): XAML, code-behind events and Avalonia drawing only. Put viewing logic that does not need Avalonia in `HyperPdf.ViewModels`, and UI mechanics that do not need Avalonia in `HyperPdf.Presentation`.
  - `tests/HyperPdf.ViewModels.Tests` — View model tests with a fake render head and no Avalonia. `tests/HyperPdf.Presentation.Tests` tests key and pointer routing and the canvas state through a fake `ICanvasHost`. `tests/HyperPdf.App.Tests` keeps the headless UI tests and links the shared fakes from them.
- Read leniently, write strictly. Open and recover malformed files where possible, but every file the library writes must conform to ISO 32000 (and to any PDF/A or PDF/UA level the file claims). Saving fixes what can be fixed in the parts it writes, such as wrong `/Length`, broken cross-reference data, missing required keys and invalid boxes. The viewer tells the user in plain words when a file was damaged and repaired on open, and what saving will fix. Never add a feature that needs non-conforming output.
- Never copy from a PDF engine.
- Tests, committed scripts and docs never contain paths or assumptions specific to one machine (home folders, installed fonts, tools or profiles, local clones). Optional inputs come from an environment variable or PATH and skip with a reason when absent.
- The engine parses UTF-8 bytes directly and its types are thread-safe: documents are used from many render threads at once.
- The public API is async-first: every public operation has an async form taking a `CancellationToken` (`ValueTask` where results are often ready, `IAsyncEnumerable` for long walks). Async methods prefetch the bytes they need with real async I/O, then run the synchronous core inline. Pass the token explicitly through parsing, decoding, rendering and writing, including JPX, JBIG2, JPEG, Flate and font paths; spans and `ref struct`s never cross an `await`, and the library never uses `Task.Run`. Long loops check the token cheaply (per chunk, segment or operator batch, never per byte, never allocating). The sync API stays the fast path for warm calls. Callers that may wait on I/O or need to cancel (open, first page, search, save, tab switches) use the async forms. The NuGet package builds without runtime async so Mono hosts can load it; the app builds with it.
- Text is UTF-8 by default: byte constants use `u8` literals, and parsing, XML and exports work in UTF-8. Use UTF-16 only where the PDF spec requires it (UTF-16BE text strings before PDF 2.0, ToUnicode CMap values, font name and cmap tables). Write new PDF text strings as PDFDocEncoding when they fit; otherwise UTF-8 with its prefix in PDF 2.0 files, UTF-16BE in earlier versions. Saving preserves existing encodings: unchanged strings and packets keep their bytes, and an edited value keeps the encoding of the value it replaces (UTF-16BE stays UTF-16BE).
- XML (XMP, XFDF, XFA packets) is processed by streaming only: forward-only `XmlReader`/`XmlWriter` over the bytes, DTDs prohibited, no resolver, bounded size. Never `XDocument`, `XmlDocument` or LINQ to XML.
- Follow the analyzers and existing C# style. Document members, name constants and use explicit enum values. Fix warnings; do not add suppressions. The existing tools-only SST1449 exception is authorised.
- Use .NET tools only. Use Roslyn for structural C# edits. Run single-file tools with `dotnet run --file`. Keep docs short. Keep research sources and limits. Add no attribution trailers or generated-by footers.

## Verification

Native AOT publishing needs a native compiler and zlib development files. Run the app with `dotnet run -f net11.0 --project src/HyperPdf.App -- some.pdf`; install a Linux build with `dotnet run --file scripts/InstallLinux.cs --`. Keep `README.md` for end users only; build, test and contributor notes belong here.

Always pass `-v q --nologo` (and `--no-restore` after the first build) to `dotnet build` and `dotnet test` to avoid console log bloat. Always filter tests to the single class or method being worked on (`--treenode-filter`) rather than running the full test suite until final verification. Never read raw `.pdf` fixtures, `.ttf.z` fonts, `verapdf-results.tsv`, `PdfARules.tsv`, `Fonts/Data/*Data.cs`, `licenses/notices/*`, or full `benchmarks/allocations-explained.json` into context—query specific lines with `git grep` or `rg`.

```console
dotnet build HyperPdf.slnx -v q --nologo
dotnet test --solution HyperPdf.slnx -v q --nologo
# one test project or filtered test, from the repo root; runner flags go after --; never --no-build
dotnet test --project "$PWD/tests/HyperPdfLibrary.Tests/HyperPdfLibrary.Tests.csproj" -f net11.0 -v q --nologo --no-restore -- --treenode-filter '/*/*/ClassName/*'
dotnet test --project "$PWD/tests/HyperPdfLibrary.Tests/HyperPdfLibrary.Tests.csproj" -f net11.0 -v q --nologo --no-restore -- --treenode-filter '/*/*/ClassName/MethodName'
dotnet run -c Release -f net11.0 --project benchmarks/HyperPdf.Benchmarks -- --filter '*RelevantBenchmark*'
dotnet run --file scripts/AuditAllocations.cs -- '*RelevantBenchmark*'
dotnet run --file scripts/Publish.cs -- linux-x64
```

### PDF/A output

Every change to a writer, editor or the optimiser must keep PDF/A output valid. The `HyperPdfLibrary.Tests.Conformance.VeraPdf` tests check generated files and veraPDF's sample PDFs with veraPDF, which runs as a separate Java (11+) process and is never linked or shipped. They skip without veraPDF; CI's `PDF/A output validation (veraPDF)` job requires it.

```console
dotnet run --file scripts/InstallVeraPdf.cs -- ~/.cache/hyperpdf/verapdf
dotnet run --file scripts/FetchReferencePdfs.cs -- tests/reference-pdfs/manifest.json ~/.cache/hyperpdf/pdfa-samples <the verapdf-pdfa* ids in the manifest>
dotnet run --file scripts/FetchVeraPdfTestFiles.cs -- ~/.cache/hyperpdf/verapdf-test-files
HYPERPDF_VERAPDF=~/.cache/hyperpdf/verapdf HYPERPDF_PDFA_SAMPLES=~/.cache/hyperpdf/pdfa-samples \
  HYPERPDF_VERAPDF_TEST_FILES=~/.cache/hyperpdf/verapdf-test-files \
  dotnet test --project "$PWD/../tests/HyperPdfLibrary.Tests/HyperPdfLibrary.Tests.csproj" -f net11.0 -- --treenode-filter '/*/HyperPdfLibrary.Tests.Conformance.VeraPdf/*/*'
```

The installer reads the release from `verapdf.json`, which Renovate updates; the same file pins the commit of veraPDF's PDF/A test files. Set `HYPERPDF_PDFA_OUTPUT` to keep the files the tests write. CI lists the sample ids in `.github/workflows/ci.yml`.

`PdfAValidator` must give veraPDF's pass or fail for every rule it checks on every one of veraPDF's PDF/A test files; `PdfAValidatorAgreementTests` compares it with `tests/HyperPdfLibrary.Tests/Conformance/VeraPdf/Data/verapdf-results.tsv`. After a veraPDF update or a new test file commit, record that file again with `dotnet run --file scripts/RecordVeraPdfResults.cs -- <test files folder> <verapdf home> <that file>`. A rule joins a checker's `Rules` list only when it agrees on all of veraPDF's PDF/A test files.

Warnings are failures. Verify all affected frameworks, saved output and user-visible behaviour. TUnit uses Microsoft.Testing.Platform; runner arguments follow `--`. With SDK 11, use absolute paths for `--project`. UI tests and benchmarks run headless. Set `HYPERPDF_SCREENSHOTS` to save UI frames.

Continue authorised goal work through verification. Treat feedback as work to fix. Report concrete blockers and missing dependencies. Follow the user's current commit and publishing instructions.
