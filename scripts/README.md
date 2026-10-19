# Repository tools

Run tools from the checkout with `dotnet run --file scripts/<name>.cs -- <arguments>`.
Each entry point targets .NET 11. Files named `*Builder.cs` and `ScriptProcess.cs` are shared source, not entry points.

| Task | Tool |
| --- | --- |
| Measure and explain allocations for all supplied benchmark filters in one process | `AuditAllocations.cs '*Benchmark*' '*Another*'` (`--root <checkout>` audits another worktree) |
| Compare a baseline ref with the current code using the same benchmark definitions | `CompareBenchmarks.cs <ref> '*Benchmark*' [more filters] [--leeway percent]` |
| Read existing allocation or CPU traces | `AllocationAudit.cs`, `CpuAudit.cs` |
| Publish Native AOT and separate debug symbols | `Publish.cs <runtime identifier> [publish arguments]` |
| Install a published Linux build and desktop integration | `InstallLinux.cs [--prefix directory] [--from directory]` |
| Check screen reader access on a private Linux display and D-Bus session | `CheckScreenReader.cs [--wayland] <app path> [minimum controls]` |
| Write the accessibility check PDF or walk an existing AT-SPI tree | `AccessibilityCheck.cs` |
| Fetch reference PDFs and check PDF 2.0 copies | `FetchReferencePdfs.cs`, `CheckPdf20Copies.cs` |
| Install comparison tools and record PDF/A validation results | `InstallComparisonFirefox.cs`, `InstallVeraPdf.cs`, `FetchVeraPdfTestFiles.cs`, `FetchVeraPdfProfiles.cs`, `RecordVeraPdfResults.cs` |
| Extract the Isartor PDF/A test files | `ExtractIsartorSuite.cs` |
| Refresh generated lookup tables | `KnownNameTableGenerator.cs`, `UnicodeTextTableGenerator.cs`, `JpxHtTableGenerator.cs` |
| Refresh the PDF/UA, tagged PDF, Well-Tagged PDF and WCAG rule table from veraPDF's profile files | `StandardsRuleTableGenerator.cs <folder of PDF_UA profile XML files>` |
| Refresh dependency notices | `GenerateThirdPartyNotices.cs` |

The tests that compare the PDF/UA, parent-child and WCAG rule reports with veraPDF's own profiles read the profile XML files (CC BY 4.0, from `veraPDF-validation-profiles`, folder `PDF_UA`) from the folder named by `HYPERPDF_VERAPDF_PROFILES`, and veraPDF's PDF/UA test files from `HYPERPDF_VERAPDF_TEST_FILES`. Without them the tests skip.

Allocation and comparison evidence stays under `~/.cache/hyperpdf`. Set `BENCHMARK_FRAMEWORK=net10.0` to measure the other supported runtime. The default is `net11.0`.

The screen reader check needs D-Bus, AT-SPI, `gdbus`, `busctl` and either Xvfb or Weston on PATH. It creates a private display, runtime directory and settings directories, and stops its children when the check finishes. It never connects to the desktop display.
