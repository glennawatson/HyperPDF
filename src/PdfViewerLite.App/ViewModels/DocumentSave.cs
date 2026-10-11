// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Platform;

namespace PdfViewerLite.App.ViewModels;

/// <summary>Writes a document beside its destination and publishes it only after the write succeeds.</summary>
internal static class DocumentSave
{
    /// <summary>Writes and publishes an edited document.</summary>
    /// <param name="editor">The document writer.</param>
    /// <param name="source">The source whose unsaved state must survive a failed replacement.</param>
    /// <param name="path">The destination file.</param>
    /// <returns>The save outcome.</returns>
    internal static SaveOutcome Save(IAnnotationEditor editor, DocumentSource source, string path)
    {
        var temporary = $"{path}.saving";
        var serialized = false;
        var published = false;
        try
        {
            using (var stream = File.Create(temporary))
            {
                serialized = editor.Save(stream);
            }

            if (!serialized)
            {
                return new(false, null);
            }

            FileReplacement.Replace(temporary, path);
            published = true;
            source.MarkSavePublished();
            return new(true, null);
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
    internal static async Task<SaveOutcome> SaveAsync(IAnnotationEditor editor, DocumentSource source, string path, CancellationToken cancellationToken)
    {
        var temporary = $"{path}.saving";
        var serialized = false;
        var published = false;
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous };
            await using (var stream = new FileStream(temporary, options))
            {
                serialized = await editor.SaveAsync(stream, cancellationToken).ConfigureAwait(true);
            }

            if (!serialized)
            {
                return new(false, null);
            }

            FileReplacement.Replace(temporary, path);
            published = true;
            source.MarkSavePublished();
            return new(true, null);
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

    /// <summary>The result of writing and publishing a document.</summary>
    /// <param name="Saved">Whether the destination was replaced.</param>
    /// <param name="Error">A file-system error to show to the reader, or <see langword="null"/>.</param>
    internal readonly record struct SaveOutcome(bool Saved, string? Error);
}
