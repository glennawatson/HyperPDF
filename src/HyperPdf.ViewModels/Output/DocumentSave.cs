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
            Finish(source, temporary, serialized, published);
        }
    }

    /// <summary>Writes with cancellable output I/O and publishes an edited document.</summary>
    /// <param name="editor">The document writer.</param>
    /// <param name="source">The source whose unsaved state must survive a failed replacement.</param>
    /// <param name="path">The destination file.</param>
    /// <param name="cancellationToken">Cancels preparation and writing.</param>
    /// <returns>The save outcome.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Task<DocumentSaveOutcome> SaveAsync(IAnnotationEditor editor, DocumentSource source, string path, CancellationToken cancellationToken) =>
        SaveAsync(editor, source, path, null, cancellationToken);

    /// <summary>Writes with cancellable output I/O and publishes an edited document, as a PDF 2.0 file when an engine is given and can convert it.</summary>
    /// <param name="editor">The document writer.</param>
    /// <param name="source">The source whose unsaved state must survive a failed replacement.</param>
    /// <param name="path">The destination file.</param>
    /// <param name="upgrade">How to rewrite the saved file as PDF 2.0, or <see langword="null"/> to keep the file's own version.</param>
    /// <param name="cancellationToken">Cancels preparation and writing.</param>
    /// <returns>The save outcome.</returns>
    internal static async Task<DocumentSaveOutcome> SaveAsync(IAnnotationEditor editor, DocumentSource source, string path, Pdf20Upgrade? upgrade, CancellationToken cancellationToken)
    {
        var temporary = $"{path}.saving";
        var serialized = false;
        var published = false;
        var report = new DocumentSaveReport();
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous };
            await using (var stream = new FileStream(temporary, options))
            {
                // Serializing the whole file runs before the library's first await, so it starts on the pool to keep the window alive.
                serialized = await Task.Run(() => editor.SaveAsync(stream, report, cancellationToken).AsTask(), cancellationToken).ConfigureAwait(true);
            }

            if (!serialized)
            {
                return new(false, report.Failure);
            }

            var upgraded = upgrade is null ? null : await Pdf20Resave.TryAsync(upgrade, temporary, cancellationToken).ConfigureAwait(true);
            FileReplacement.Replace(temporary, path);
            published = true;
            source.MarkSavePublished();
            return Saved(report) with { Pdf20Written = upgraded?.Written == true, Pdf20Refusal = upgraded is { Written: false } ? upgraded.Reason : null };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(false, null);
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
            Finish(source, temporary, serialized, published);
        }
    }

    /// <summary>Describes a save that replaced the destination, with what the save found that the reader should know.</summary>
    /// <param name="report">The save's report.</param>
    /// <returns>The outcome.</returns>
    private static DocumentSaveOutcome Saved(DocumentSaveReport report) => new(true, null, report.ClaimRemoved)
    {
        ClaimRepaired = report.ClaimRepaired,
        TrapNetworkWarnings = report.TrapNetworkWarnings,
        RequirementWarnings = report.RequirementWarnings,
        RemovedRequirements = report.RemovedRequirements,
    };

    /// <summary>Deletes unpublished output and keeps serialized edits dirty when replacement failed.</summary>
    /// <param name="source">The edited document source.</param>
    /// <param name="temporary">The temporary output file.</param>
    /// <param name="serialized">Whether the editor wrote a complete snapshot.</param>
    /// <param name="published">Whether the destination was replaced.</param>
    private static void Finish(DocumentSource source, string temporary, bool serialized, bool published)
    {
        if (published)
        {
            return;
        }

        if (serialized)
        {
            source.MarkSaveUnpublished();
        }

        try
        {
            File.Delete(temporary);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
