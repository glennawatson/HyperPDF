// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Optimizing;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Optimizing;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures what the "Save optimised copy" adapter adds on top of <see cref="PdfOptimizer"/>: the same small document is
/// optimised through the library directly (the baseline) and through <see cref="HyperPdfDocument"/>'s
/// <see cref="IDocumentOptimizer"/>, and the settings and report mapping run on their own. The engine's own cost is in
/// <see cref="HyperPdfOptimizerBenchmarks"/>.
/// </summary>
public class HyperPdfOptimizeCopyBenchmarks
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 8;

    /// <summary>Where the notes go, in points.</summary>
    private const float NoteX = 100;

    /// <summary>Where the first note goes from the top, in points.</summary>
    private const float NoteY = 120;

    /// <summary>The notes' colour.</summary>
    private const uint NoteColor = 0xFFFF00;

    /// <summary>The settings a user can choose, with every option on.</summary>
    private static readonly OptimizeSettings Settings = new(OptimizePreset.Balanced, true, "en-AU", true, true);

    /// <summary>The library options the settings map to, so the baseline does the same work as the adapter.</summary>
    private static readonly PdfOptimizeOptions LibraryOptions = OptimizeMapping.ToOptions(Settings);

    /// <summary>The report mapped.</summary>
    private static readonly PdfOptimizeReport Report = new(
        4_194_304,
        2_097_152,
        PdfOptimizeMode.Rewritten,
        [new(PdfOptimizeCategory.Images, 3, 3_000_000, 1_000_000)],
        [],
        ["One figure needs alternative text."],
        [new(PdfOptimizeCategory.Fonts, 7, "The font is not embedded.")]);

    /// <summary>The path of the generated file.</summary>
    private string _path = string.Empty;

    /// <summary>The document, opened through the adapter.</summary>
    private HyperPdfDocument _document = null!;

    /// <summary>The path of the generated file that is edited.</summary>
    private string _editedPath = string.Empty;

    /// <summary>The document with unsaved edits.</summary>
    private HyperPdfDocument _edited = null!;

    /// <summary>Writes and opens the documents, and edits one.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"optimise-copy-bench-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(_path, TestPdf.CreateArticle(Pages));
        _document = (HyperPdfDocument)new HyperPdfEngine().Open(_path, null);
        _editedPath = Path.Combine(Path.GetTempPath(), $"optimise-copy-edited-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(_editedPath, TestPdf.CreateArticle(Pages));
        _edited = (HyperPdfDocument)new HyperPdfEngine().Open(_editedPath, null);
        _ = HyperPdfDocumentAnnotationEditing.AddNote(_edited, 0, new(NoteX, NoteY), "A note", NoteColor);
        var removed = HyperPdfDocumentAnnotationEditing.AddNote(_edited, 0, new(NoteX, NoteY + NoteY), "A hidden note", NoteColor);
        _ = HyperPdfDocumentAnnotationEditing.SetRemoved(_edited, 0, removed, true);
    }

    /// <summary>Closes and deletes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        _edited.Dispose();
        File.Delete(_path);
        File.Delete(_editedPath);
    }

    /// <summary>Optimises through the library directly: the baseline.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark(Baseline = true)]
    public async Task<long> LibraryDirect() =>
        (await PdfOptimizer.OptimizeAsync(_document.Document, Stream.Null, LibraryOptions, null, CancellationToken.None).ConfigureAwait(false)).BytesAfter;

    /// <summary>Optimises through the adapter the app uses.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public async Task<long> ThroughAdapter() =>
        (await HyperPdfDocumentOptimization.OptimizeAsync(_document!, Stream.Null, Settings, null, CancellationToken.None).ConfigureAwait(false)).BytesAfter;

    /// <summary>Optimises a document with unsaved edits and an annotation removed but kept, through the adapter.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public async Task<long> ThroughAdapterWithEdits() =>
        (await HyperPdfDocumentOptimization.OptimizeAsync(_edited, Stream.Null, Settings, null, CancellationToken.None).ConfigureAwait(false)).BytesAfter;

    /// <summary>
    /// Runs the optimisation up to the first write to the output: the snapshot and the plan, which run on the caller's
    /// thread before the first await. The run stops at the first write, so the time is how long a caller's thread is held
    /// (it includes the cost of one thrown exception).
    /// </summary>
    /// <returns>A task.</returns>
    [Benchmark]
    public async Task<bool> TimeBeforeFirstWrite()
    {
        try
        {
            _ = await HyperPdfDocumentOptimization.OptimizeAsync(_edited, new StopAtFirstWriteStream(), Settings, null, CancellationToken.None).ConfigureAwait(false);
            return false;
        }
        catch (StopAtFirstWriteStream.StoppedException)
        {
            return true;
        }
    }

    /// <summary>Maps the app's settings to the library's options.</summary>
    /// <returns>The options.</returns>
    [Benchmark]
    public PdfOptimizeOptions MapSettings() => OptimizeMapping.ToOptions(Settings);

    /// <summary>Maps the library's report to the app's.</summary>
    /// <returns>The report.</returns>
    [Benchmark]
    public OptimizeReport MapReport() => OptimizeMapping.ToReport(Report);

    /// <summary>A stream that stops the run with <see cref="StoppedException"/> at the first write.</summary>
    private sealed class StopAtFirstWriteStream : Stream
    {
        /// <inheritdoc/>
        public override bool CanRead => false;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void Flush()
        {
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => throw new StoppedException();

        /// <inheritdoc/>
        public override void Write(ReadOnlySpan<byte> buffer) => throw new StoppedException();

        /// <inheritdoc/>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new StoppedException();

        /// <summary>Raised at the first write.</summary>
        internal sealed class StoppedException : Exception
        {
            /// <summary>Initializes a new instance of the <see cref="StoppedException"/> class.</summary>
            public StoppedException()
            {
            }

            /// <summary>Initializes a new instance of the <see cref="StoppedException"/> class.</summary>
            /// <param name="message">The message.</param>
            public StoppedException(string message)
                : base(message)
            {
            }

            /// <summary>Initializes a new instance of the <see cref="StoppedException"/> class.</summary>
            /// <param name="message">The message.</param>
            /// <param name="innerException">The cause.</param>
            public StoppedException(string message, Exception innerException)
                : base(message, innerException)
            {
            }
        }
    }
}
