# Repository tools

Run tools from the checkout with `dotnet run --file src/tools/<name>.cs -- <arguments>`.
Each entry point targets .NET 11. Files named `*Builder.cs`, `ScriptProcess.cs` and `SourceLines.cs` are shared source, not entry points.

| Task | Tool |
| --- | --- |
| Measure and explain allocations for all supplied benchmark filters in one process | `AuditAllocations.cs '*Benchmark*' '*Another*'` (`--root <checkout>` audits another worktree) |
| Compare a baseline ref with the current code using the same benchmark definitions | `CompareBenchmarks.cs <ref> '*Benchmark*' [more filters] [--leeway percent]` |
| Read existing allocation or CPU traces | `AllocationAudit.cs`, `CpuAudit.cs` |
| Name the source line behind each allocation or CPU sample | Set `HYPERPDF_SYMBOLS` to the folder of the built benchmark assemblies, which embed their PDB files, before `AuditAllocations.cs` or `AllocationAudit.cs` to see `File.cs:line` after each site. For CPU, run the benchmark project with `--cpu-lines --artifacts <folder>` (it traces samples from start-up with the JIT line map instead of BenchmarkDotNet's profiler), then `CpuAudit.cs <folder>` lists the busiest lines. The sampler stops threads at GC safepoints, so a line is where the thread was stopped and can sit a little after the code that cost the time; a profile with `Thread.PollGCWorker` high in it needs reading with that in mind. |
| Publish Native AOT and separate debug symbols | `Publish.cs <runtime identifier> [publish arguments]` |
| Fetch the FFmpeg libraries for a runtime (`Publish.cs` runs this; the build is pinned in `src/ffmpeg.json`) | `FetchFfmpeg.cs <runtime identifier> <folder>` |
| Install a published Linux build and desktop integration | `InstallLinux.cs [--prefix directory] [--from directory]` |
| Check screen reader access on a private Linux display and D-Bus session | `CheckScreenReader.cs [--wayland] <app path> [minimum controls]` |
| Write the accessibility check PDF or walk an existing AT-SPI tree | `AccessibilityCheck.cs` |
| Fetch reference PDFs and check PDF 2.0 copies | `FetchReferencePdfs.cs`, `CheckPdf20Copies.cs` |
| Install veraPDF and record PDF/A validation results | `InstallVeraPdf.cs`, `FetchVeraPdfTestFiles.cs`, `FetchVeraPdfProfiles.cs`, `RecordVeraPdfResults.cs` |
| Extract the Isartor PDF/A test files | `ExtractIsartorSuite.cs` |
| Refresh generated lookup tables | `KnownNameTableGenerator.cs`, `UnicodeTextTableGenerator.cs`, `JpxHtTableGenerator.cs` |
| Refresh the PDF/UA, tagged PDF, Well-Tagged PDF and WCAG rule table from veraPDF's profile files | `StandardsRuleTableGenerator.cs <folder of PDF_UA profile XML files>` |
| Refresh dependency notices for every project (`.github/workflows/refresh-notices.yml` runs it with `--push` on Renovate pull requests) | `RefreshThirdPartyNotices.cs [--push]` |
| Write one notices file from chosen assets files | `GenerateThirdPartyNotices.cs` |

The tests that compare the PDF/UA, parent-child and WCAG rule reports with veraPDF's own profiles read the profile XML files (CC BY 4.0, from `veraPDF-validation-profiles`, folder `PDF_UA`) from the folder named by `HYPERPDF_VERAPDF_PROFILES`, and veraPDF's PDF/UA test files from `HYPERPDF_VERAPDF_TEST_FILES`. Without them the tests skip.

Allocation and comparison evidence stays under `~/.cache/hyperpdf`. Set `BENCHMARK_FRAMEWORK=net10.0` to measure the other supported runtime. The default is `net11.0`.

The screen reader check needs D-Bus, AT-SPI, `gdbus`, `busctl` and either Xvfb or Weston on PATH. It creates a private display, runtime directory and settings directories, and stops its children when the check finishes. It never connects to the desktop display.
