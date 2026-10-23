// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>Writes a PDF/A source through every way the library saves a file, after editing its document properties.</summary>
internal static class PdfAOutputs
{
    /// <summary>The title every edited output gets.</summary>
    internal const string EditedTitle = "Edited by HyperPdf: PDF/A output";

    /// <summary>The name of the output saved without edits.</summary>
    internal const string Unedited = "unedited";

    /// <summary>Gets the edit made before each edited save.</summary>
    internal static PdfMetadataEdit Edit => new()
    {
        Title = EditedTitle,
        Author = "HyperPdfLibrary tests",
        Subject = "Library output checked by veraPDF",
        Keywords = "PDF/A; veraPDF",
        Modified = DateTimeOffset.Parse("2026-10-12T10:00:00+11:00", System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>Saves the source without edits, then edits it and saves it incrementally, compactly, classically, linearized and optimised.</summary>
    /// <param name="source">The source file.</param>
    /// <param name="folder">The folder the outputs go to.</param>
    /// <param name="cancellationToken">Cancels the saves.</param>
    /// <returns>Each output's path by its name.</returns>
    internal static async Task<Dictionary<string, string>> WriteAllAsync(string source, string folder, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var unedited = await PdfDocumentReader.OpenAsync(source, null, cancellationToken))
        {
            await WriteAsync(outputs, folder, Unedited, stream => PdfDocumentSaving.SaveAsync(unedited, stream, cancellationToken).AsTask(), cancellationToken);
        }

        using var document = await PdfDocumentReader.OpenAsync(source, null, cancellationToken);
        PdfDocumentMetadataEditing.SetMetadata(document, Edit);
        await WriteAsync(outputs, folder, "incremental", stream => PdfDocumentSaving.SaveAsync(document, stream, cancellationToken).AsTask(), cancellationToken);
        await WriteAsync(outputs, folder, "compact", stream => PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, stream, cancellationToken), cancellationToken);
        await WriteAsync(outputs, folder, "classic", stream => PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Classic, stream, cancellationToken), cancellationToken);
        await WriteAsync(outputs, folder, "linearized", stream => PdfLinearizedWriter.SaveAsync(document, stream, cancellationToken), cancellationToken);

        // Inferred tags add a custom XMP property, which PDF/A parts 1 to 3 must describe in an extension schema.
        var options = new PdfOptimizeOptions { AddInferredTags = true };
        await WriteAsync(outputs, folder, "optimized", stream => PdfOptimizer.OptimizeAsync(document, stream, options, null, cancellationToken), cancellationToken);
        return outputs;
    }

    /// <summary>Writes one output.</summary>
    /// <param name="outputs">The outputs, added to.</param>
    /// <param name="folder">The folder.</param>
    /// <param name="name">The output's name.</param>
    /// <param name="save">Writes the file to a stream.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task.</returns>
    private static async Task WriteAsync(Dictionary<string, string> outputs, string folder, string name, Func<Stream, Task> save, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Path.Combine(folder, $"{name}.pdf");
        await using (var stream = File.Create(path))
        {
            await save(stream);
        }

        outputs[name] = path;
    }
}
