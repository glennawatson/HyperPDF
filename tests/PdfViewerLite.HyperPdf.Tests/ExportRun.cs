// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Printing;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>One exported file reopened for assertions and deleted on dispose.</summary>
internal sealed class ExportRun : IDisposable
{
    /// <summary>The file the export was written to.</summary>
    private readonly string _path;

    /// <summary>Initializes a new instance of the <see cref="ExportRun"/> class.</summary>
    /// <param name="path">The exported file.</param>
    /// <param name="written">Whether the export reported success.</param>
    private ExportRun(string path, bool written)
    {
        _path = path;
        Written = written;
        Output = written ? new HyperPdfEngine().Open(path, null) : null;
    }

    /// <summary>Gets the exported file's path.</summary>
    internal string FilePath => _path;

    /// <summary>Gets a value indicating whether the export reported success.</summary>
    internal bool Written { get; }

    /// <summary>Gets the reopened export, or <see langword="null"/> when nothing was written.</summary>
    internal IDocument? Output { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Output?.Dispose();
        File.Delete(_path);
    }

    /// <summary>Exports pages of a file with one engine.</summary>
    /// <param name="source">The source file.</param>
    /// <param name="pages">The pages, zero-based.</param>
    /// <param name="layout">The layout.</param>
    /// <returns>The export.</returns>
    internal static ExportRun Create(string source, int[] pages, in SheetLayout layout)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-export-{Guid.NewGuid():N}.pdf");
        bool written;
        using (var document = new HyperPdfEngine().Open(source, null))
        {
            using var stream = File.Create(path);
            written = ((IPageExporter)DocumentFeatures.CastFeature(document, typeof(IPageExporter))!).ExportPages(pages, layout, stream);
        }

        return new(path, written);
    }

    /// <summary>Opens a generated file, lets a callback edit it, and saves the result to a new file.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    /// <param name="edit">Edits the document.</param>
    /// <returns>The path of the edited file, which the caller deletes.</returns>
    internal static string CreateEditedSource(byte[] bytes, Action<IDocument> edit)
    {
        var original = Path.Combine(Path.GetTempPath(), $"hyperpdf-source-{Guid.NewGuid():N}.pdf");
        var edited = Path.Combine(Path.GetTempPath(), $"hyperpdf-edited-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(original, bytes);
        try
        {
            using var document = new HyperPdfEngine().Open(original, null);
            edit(document);
            using var stream = File.Create(edited);
            _ = ((IAnnotationEditor)DocumentFeatures.CastFeature(document, typeof(IAnnotationEditor))!).Save(stream);
        }
        finally
        {
            File.Delete(original);
        }

        return edited;
    }

    /// <summary>Joins the text of every page of a document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The text.</returns>
    internal static string GetAllText(IDocument document) =>
        string.Concat(Enumerable.Range(0, document.PageCount).Select(page => document.GetText(page, 0, document.GetCharacterCount(page))));
}
