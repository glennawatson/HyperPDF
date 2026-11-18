// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Platform;

namespace HyperPdf.ViewModels.Output;

/// <summary>Writes a document beside its destination and publishes it only after the write succeeds.</summary>
internal static class DocumentSave
{
    /// <summary>Writes and publishes an edited document.</summary>
    /// <param name="editor">The document writer.</param>
    /// <param name="source">The source whose unsaved state must survive a failed replacement.</param>
    /// <param name="path">The destination file.</param>
    /// <returns>The save outcome.</returns>
    internal static DocumentSaveOutcome Save(IAnnotationEditor editor, DocumentSource source, string path)
    {
        var temporary = $"{path}.saving";
        var serialized = false;
        var published = false;
        var report = new DocumentSaveReport();
        try
        {
            using (var stream = File.Create(temporary))
            {
                serialized = editor.Save(stream, report);
            }

            if (!serialized)
            {
                return new(false, report.Failure);
            }

            FileReplacement.Replace(temporary, path);
            published = true;
            source.MarkSavePublished();
            return Saved(report);
        }
        catch (IOException ex)
        {
            return new(false, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new(false, ex.Message);
        }
        finally
        {
            if (!published)
            {
                Finish(source, new DocumentSaveState { Serialized = serialized });
                TryDelete(temporary);
            }
        }
    }

    /// <summary>Writes with cancellable output I/O and publishes an edited document, as a PDF 2.0 file when an engine is given and can convert it.</summary>
    /// <param name="editor">The document writer.</param>
    /// <param name="source">The source whose unsaved state must survive a failed replacement.</param>
    /// <param name="path">The destination file.</param>
    /// <param name="upgrade">How to rewrite the saved file as PDF 2.0, or <see langword="null"/> to keep the file's own version.</param>
    /// <returns>A cold signal giving the save outcome; unsubscribing cancels preparation and writing and deletes the temporary file.</returns>
    internal static IObservable<DocumentSaveOutcome> Write(IAnnotationEditor editor, DocumentSource source, string path, Pdf20Upgrade? upgrade)
    {
        var temporary = $"{path}.saving";
        var report = new DocumentSaveReport();
        var state = new DocumentSaveState();
        return Signal.Using(
                () => new TemporaryCopy(temporary),
                copy => Serialize(editor, copy, report, state)
                    .SwitchMap(serialized => serialized
                        ? Upgrade(upgrade, temporary).Select(upgraded => Publish(copy, source, path, report, state, upgraded))
                        : Signal.Return<DocumentSaveOutcome>(new(false, report.Failure))))
            .Catch<DocumentSaveOutcome, Exception>(error => Failed(source, state, error))
            .Finally(() => Finish(source, state));
    }

    /// <summary>Serializes the document to the temporary file on the pool, then closes it.</summary>
    /// <param name="editor">The document writer.</param>
    /// <param name="copy">The temporary file.</param>
    /// <param name="report">What the save found.</param>
    /// <param name="state">Whether the document was serialized or published.</param>
    /// <returns>A cold signal giving whether the editor wrote a complete snapshot.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<bool> Serialize(IAnnotationEditor editor, TemporaryCopy copy, DocumentSaveReport report, DocumentSaveState state) =>
        // Serializing the whole file runs before the library's first await, so it starts on the pool to keep the window alive.
        editor.SaveToStream(copy.Stream, report)
            .SubscribeOn(TaskPoolSequencer.Default)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Do(serialized => Serialized(copy, state, serialized));

    /// <summary>Records that the document was serialized and closes the temporary file so it can be replaced.</summary>
    /// <param name="copy">The temporary file.</param>
    /// <param name="state">Whether the document was serialized or published.</param>
    /// <param name="serialized">Whether the editor wrote a complete snapshot.</param>
    private static void Serialized(TemporaryCopy copy, DocumentSaveState state, bool serialized)
    {
        state.Serialized = serialized;
        copy.Stream.Dispose();
    }

    /// <summary>Rewrites the saved temporary file as PDF 2.0 when an upgrade was asked for.</summary>
    /// <param name="upgrade">How to rewrite the file, or <see langword="null"/> to keep its own version.</param>
    /// <param name="temporary">The temporary file.</param>
    /// <returns>A cold signal giving what the rewrite did, or <see langword="null"/> when none was asked for.</returns>
    private static IObservable<Pdf20ResaveResult?> Upgrade(Pdf20Upgrade? upgrade, string temporary) =>
        upgrade is null
            ? Signal.Return<Pdf20ResaveResult?>(null)
            : Pdf20Resave.Try(upgrade, temporary).Select(static result => (Pdf20ResaveResult?)result);

    /// <summary>Moves the temporary file over the destination and says the document was saved.</summary>
    /// <param name="copy">The temporary file.</param>
    /// <param name="source">The source that publishes the save.</param>
    /// <param name="path">The destination file.</param>
    /// <param name="report">What the save found.</param>
    /// <param name="state">Whether the document was serialized or published.</param>
    /// <param name="upgraded">What the PDF 2.0 rewrite did, or <see langword="null"/>.</param>
    /// <returns>The save outcome.</returns>
    private static DocumentSaveOutcome Publish(TemporaryCopy copy, DocumentSource source, string path, DocumentSaveReport report, DocumentSaveState state, Pdf20ResaveResult? upgraded)
    {
        copy.MoveTo(path);
        state.Published = true;
        source.MarkSavePublished();
        return Saved(report) with { Pdf20Written = upgraded?.Written == true, Pdf20Refusal = upgraded is { Written: false } ? upgraded.Reason : null };
    }

    /// <summary>Turns an expected failure into an outcome that says why nothing was saved, and passes on any other.</summary>
    /// <param name="source">The source whose unsaved state must survive.</param>
    /// <param name="state">Whether the document was serialized or published.</param>
    /// <param name="error">The failure.</param>
    /// <returns>A signal giving the outcome, or failing with the same error.</returns>
    private static IObservable<DocumentSaveOutcome> Failed(DocumentSource source, DocumentSaveState state, Exception error)
    {
        Finish(source, state);
        return error switch
        {
            OperationCanceledException => Signal.Return<DocumentSaveOutcome>(new(false, null)),
            IOException or UnauthorizedAccessException => Signal.Return<DocumentSaveOutcome>(new(false, error.Message)),
            _ => Signal.Throw<DocumentSaveOutcome>(error),
        };
    }

    /// <summary>Describes a save that replaced the destination, with what the save found that the reader should know.</summary>
    /// <param name="report">The save's report.</param>
    /// <returns>The outcome.</returns>
    private static DocumentSaveOutcome Saved(DocumentSaveReport report) => new(true, null, report.ClaimRemoved)
    {
        ClaimRepaired = report.ClaimRepaired,
        TrapNetworkWarnings = [..report.TrapNetworkWarnings],
        RequirementWarnings = [..report.RequirementWarnings],
        RemovedRequirements = [..report.RemovedRequirements],
    };

    /// <summary>Deletes a temporary file, ignoring a failure because the file is only a leftover.</summary>
    /// <param name="path">The file.</param>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A leftover temporary file does no harm and a later save replaces it.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }

    /// <summary>Keeps serialized edits dirty when replacement failed; the temporary file deletes itself.</summary>
    /// <param name="source">The edited document source.</param>
    /// <param name="state">Whether the document was serialized or published.</param>
    private static void Finish(DocumentSource source, DocumentSaveState state)
    {
        if (state.Serialized && !state.Published)
        {
            source.MarkSaveUnpublished();
        }
    }
}
