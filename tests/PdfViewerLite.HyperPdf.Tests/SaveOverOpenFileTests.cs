// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Platform;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// Checks that a document opened by path, whose file the engine maps rather than loads, can be saved over its own file
/// the way the app saves: write a new file beside it, then move it over the original while the document is still open.
/// </summary>
public sealed class SaveOverOpenFileTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>The highlighted line.</summary>
    private static readonly PageRect Line = new(72, 100, 200, 14);

    /// <summary>The first note included in the save snapshot.</summary>
    private static readonly PagePoint FirstNote = new(72, 100);

    /// <summary>The note added while the snapshot is being written.</summary>
    private static readonly PagePoint SecondNote = new(90, 120);

    /// <summary>The maximum time for a paused test write to start.</summary>
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Saving over the open file replaces it, the open document keeps reading the old file, and the new file holds the edit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavingOverTheOpenFileWorks()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hyperpdf-save-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "document.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.Create(Pages));
        try
        {
            using (var document = new HyperPdfEngine().Open(path, null))
            {
                var editor = (IAnnotationEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(IAnnotationEditor))!;
                _ = editor.AddMarkup(0, AnnotationKind.Highlight, [Line], AnnotationColors.Sage, "Saved over");
                await Assert.That(SaveOver(editor, path)).IsTrue();

                // The open document still reads the file it was opened from.
                await Assert.That(document.PageCount).IsEqualTo(Pages);
                var lastPage = new List<PageAnnotation>();
                editor.GetAnnotations(Pages - 1, lastPage);
                await Assert.That(lastPage).IsEmpty();
            }

            using var reopened = new HyperPdfEngine().Open(path, null);
            var annotations = new List<PageAnnotation>();
            ((IAnnotationEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(reopened, typeof(IAnnotationEditor))!).GetAnnotations(0, annotations);
            await Assert.That(annotations.Count).IsEqualTo(1);
            await Assert.That(Directory.GetFiles(directory).Length).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>An edit made during async output remains unsaved after the earlier snapshot finishes writing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncSaveKeepsLaterEditsUnsaved()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hyperpdf-async-save-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "document.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.Create(1));
        try
        {
            using var document = new HyperPdfEngine().Open(path, null);
            var editor = (IAnnotationEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(IAnnotationEditor))!;
            _ = editor.AddNote(0, FirstNote, "first", AnnotationColors.Sage);
            await using var output = new PausedWriteStream();
            using var timeout = new CancellationTokenSource(WriteTimeout);

            var save = editor.SaveAsync(output, timeout.Token);
            await output.Entered.WaitAsync(timeout.Token);
            _ = editor.AddNote(0, SecondNote, "second", AnnotationColors.Sage);
            output.Release();

            await Assert.That(await save).IsTrue();
            await Assert.That(editor.HasUnsavedChanges).IsTrue();
            await Assert.That(output.Length).IsGreaterThan(0);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Saves the way the app does: to a file beside the original, then moved over it.</summary>
    /// <param name="editor">The document.</param>
    /// <param name="path">The original file.</param>
    /// <returns><see langword="true"/> when saved.</returns>
    private static bool SaveOver(IAnnotationEditor editor, string path)
    {
        var temporary = $"{path}.saving";
        using (var stream = File.Create(temporary))
        {
            if (!editor.Save(stream))
            {
                return false;
            }
        }

        FileReplacement.Replace(temporary, path);
        return true;
    }

    /// <summary>Holds an async write until the test makes a later edit.</summary>
    private sealed class PausedWriteStream : MemoryStream
    {
        /// <summary>Signals when output reaches the stream.</summary>
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Lets the test resume output.</summary>
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets a task that completes when the writer reaches output I/O.</summary>
        internal Task Entered => _entered.Task;

        /// <inheritdoc/>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _ = _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            await base.WriteAsync(buffer, cancellationToken);
        }

        /// <summary>Allows the pending output write to finish.</summary>
        internal void Release() => _ = _release.TrySetResult();
    }
}
