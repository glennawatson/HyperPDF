// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Optimizing;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Optimizing;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Checks the adapter that writes an optimised copy through the app's <see cref="IDocumentOptimizer"/>.</summary>
public sealed class OptimizeCopyTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 4;

    /// <summary>The balanced preset's image resolution, as the library defines it.</summary>
    private const int BalancedDpi = 200;

    /// <summary>The smaller preset's image resolution, as the library defines it.</summary>
    private const int SmallerDpi = 150;

    /// <summary>The text of the note that stays.</summary>
    private const string KeptNote = "Keep this note";

    /// <summary>The text of the note that is removed but kept.</summary>
    private const string RemovedNote = "Hidden note";

    /// <summary>The text typed into the form.</summary>
    private const string TypedName = "Glenn Watson";

    /// <summary>Where the first note goes, in points from the left.</summary>
    private const float NoteX = 200;

    /// <summary>Where the first note goes, in points from the top.</summary>
    private const float NoteY = 300;

    /// <summary>How far below the first note the second goes.</summary>
    private const float NoteGap = 60;

    /// <summary>The notes' colour.</summary>
    private const uint NoteColor = 0xFFFF00;

    /// <summary>Gets the presets, for <c>[MethodDataSource]</c>.</summary>
    /// <returns>The presets.</returns>
    public static IEnumerable<OptimizePreset> Presets() => [OptimizePreset.Smaller, OptimizePreset.Balanced, OptimizePreset.KeepQuality];

    /// <summary>An optimised copy reopens with the same pages, the report matches the files and the original is untouched.</summary>
    /// <param name="preset">The preset.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Presets))]
    public async Task CopyReopensWithTheSamePages(OptimizePreset preset)
    {
        using var source = new HyperPdfTestDocument(TestPdf.CreateArticle(Pages));
        var before = await File.ReadAllBytesAsync(source.FilePath);
        var optimizer = PdfViewerLite.Core.Documents.DocumentFeature<IDocumentOptimizer>.Cast(source.Document)!;
        var steps = new List<OptimizeStep>();
        await using var output = new MemoryStream();

        var report = await optimizer.OptimizeAsync(output, new(preset, true, "en-AU", false, true), new StepRecorder(steps), CancellationToken.None);

        var path = Path.Combine(Path.GetTempPath(), $"optimised-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, output.ToArray());
        try
        {
            using var reopened = new HyperPdfEngine().Open(path, null);
            await Assert.That(reopened.PageCount).IsEqualTo(Pages);
        }
        finally
        {
            File.Delete(path);
        }

        await Assert.That(report.BytesBefore).IsEqualTo(before.Length);
        await Assert.That(report.BytesAfter).IsEqualTo(output.Length);
        await Assert.That(steps).Contains(OptimizeStep.Writing);
        await Assert.That(steps[^1]).IsEqualTo(OptimizeStep.Done);
        await Assert.That(await File.ReadAllBytesAsync(source.FilePath)).IsEquivalentTo(before);
    }

    /// <summary>Unsaved edits are in the copy, and an annotation that is removed but kept is left out and can still be restored.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsavedEditsAreIncluded()
    {
        using var source = new HyperPdfTestDocument(TestPdf.CreateForm());
        var editor = PdfViewerLite.Core.Documents.DocumentFeature<IAnnotationEditor>.Cast(source.Document)!;
        var filler = PdfViewerLite.Core.Documents.DocumentFeature<IFormFiller>.Cast(source.Document)!;
        var fields = new List<FormField>();
        filler.GetFields(0, fields);
        var kept = editor.AddNote(0, new(NoteX, NoteY), KeptNote, NoteColor);
        var removed = editor.AddNote(0, new(NoteX, NoteY + NoteGap), RemovedNote, NoteColor);
        var typed = filler.SetText(0, fields[0].Index, TypedName);
        var hidden = editor.SetRemoved(0, removed, true);
        await using var output = new MemoryStream();

        _ = await PdfViewerLite.Core.Documents.DocumentFeature<IDocumentOptimizer>.Cast(source.Document)!.OptimizeAsync(
            output,
            new(OptimizePreset.KeepQuality, false, string.Empty, false, false),
            null,
            CancellationToken.None);

        var path = Path.Combine(Path.GetTempPath(), $"optimised-edits-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, output.ToArray());
        try
        {
            using var reopened = new HyperPdfEngine().Open(path, null);
            var notes = new List<PageAnnotation>();
            PdfViewerLite.Core.Documents.DocumentFeature<IAnnotationEditor>.Cast(reopened)!.GetAnnotations(0, notes);
            var reopenedFields = new List<FormField>();
            PdfViewerLite.Core.Documents.DocumentFeature<IFormFiller>.Cast(reopened)!.GetFields(0, reopenedFields);
            var current = new List<PageAnnotation>();
            editor.GetAnnotations(0, current);
            var restored = editor.SetRemoved(0, removed, false);
            var after = new List<PageAnnotation>();
            editor.GetAnnotations(0, after);

            using (Assert.Multiple())
            {
                await Assert.That(kept >= 0 && typed && hidden).IsTrue();
                await Assert.That(notes.Exists(static n => n.Contents == KeptNote)).IsTrue();
                await Assert.That(notes.Exists(static n => n.Contents == RemovedNote)).IsFalse();
                await Assert.That(reopenedFields[0].Value).IsEqualTo(TypedName);
                await Assert.That(current.Exists(static n => n.Contents == RemovedNote)).IsFalse();
                await Assert.That(restored).IsTrue();
                await Assert.That(after.Exists(static n => n.Contents == RemovedNote)).IsTrue();
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A token that is already cancelled stops the run.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledTokenStopsTheRun()
    {
        using var source = new HyperPdfTestDocument(TestPdf.CreateArticle(Pages));
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        await using var output = new MemoryStream();

        await Assert.That(async () => await PdfViewerLite.Core.Documents.DocumentFeature<IDocumentOptimizer>.Cast(
            source.Document)!.OptimizeAsync(output, OptimizeSettings.Default, null, cancel.Token))
            .Throws<OperationCanceledException>();
    }

    /// <summary>A closed document refuses to optimise.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosedDocumentIsRefused()
    {
        var source = new HyperPdfTestDocument(TestPdf.CreateArticle(Pages));
        var optimizer = PdfViewerLite.Core.Documents.DocumentFeature<IDocumentOptimizer>.Cast(source.Document)!;
        source.Dispose();
        await using var output = new MemoryStream();

        await Assert.That(async () => await optimizer.OptimizeAsync(output, OptimizeSettings.Default, null, CancellationToken.None))
            .Throws<ObjectDisposedException>();
    }

    /// <summary>The settings map onto the library's presets and options.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SettingsMapToLibraryOptions()
    {
        var smaller = OptimizeMapping.ToOptions(new(OptimizePreset.Smaller, false, " en-NZ ", true, true));
        var balanced = OptimizeMapping.ToOptions(OptimizeSettings.Default);
        var keep = OptimizeMapping.ToOptions(new(OptimizePreset.KeepQuality, true, string.Empty, false, false));

        using (Assert.Multiple())
        {
            await Assert.That(smaller.ColorImageDpi).IsEqualTo(SmallerDpi);
            await Assert.That(smaller.FixAccessibility).IsFalse();
            await Assert.That(smaller.Language).IsEqualTo("en-NZ");
            await Assert.That(smaller.AddInferredTags).IsTrue();
            await Assert.That(smaller.Cleanup).IsEqualTo(PdfCleanupItems.All);
            await Assert.That(balanced.ColorImageDpi).IsEqualTo(BalancedDpi);
            await Assert.That(balanced.Cleanup).IsEqualTo(PdfCleanupItems.None);
            await Assert.That(keep.AllowLossyImages).IsFalse();
            await Assert.That(keep.Language).IsNull();
        }
    }

    /// <summary>Records the step of each progress report.</summary>
    /// <param name="steps">Receives the steps.</param>
    private sealed class StepRecorder(List<OptimizeStep> steps) : IProgress<OptimizeProgress>
    {
        /// <inheritdoc/>
        public void Report(OptimizeProgress value) => steps.Add(value.Step);
    }
}
