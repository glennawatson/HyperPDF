// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Creation;
using HyperPdf.Core.Documents;
using HyperPdfLibrary;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine;

/// <summary>Creates new PDF documents with the managed HyperPDF library.</summary>
[DebuggerDisplay("HyperPdfDocumentCreator")]
public sealed class HyperPdfDocumentCreator : IDocumentCreator
{
    /// <inheritdoc/>
    public async ValueTask<DocumentCreationResult> CreateAsync(DocumentCreationRequest request, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);
        var creator = new PdfDocumentCreator(DocumentCreationMapping.ToOptions(request));
        var sources = new Dictionary<string, PdfDocument>(StringComparer.Ordinal);
        try
        {
            var font = DocumentCreationMapping.AddFont(creator, request.Font);
            var options = new PdfFlowOptions(font)
            {
                PageWidth = request.PageWidth,
                PageHeight = request.PageHeight,
                Margin = request.Margin,
                BodySize = request.BodySize,
                PageNumbers = request.PageNumbers,
            };
            await AddBlocksAsync(creator, request.Blocks, options, sources, cancellationToken).ConfigureAwait(false);
            var report = await creator.SaveAsync(destination, cancellationToken).ConfigureAwait(false);
            return new(report.PageCount, report.IsTagged, report.ClaimsPdfUa, report.Notes);
        }
        finally
        {
            foreach (var source in sources.Values)
            {
                source.Dispose();
            }
        }
    }

    /// <summary>Adds the blocks to the document, laying out each run of text and images and copying each source page.</summary>
    /// <param name="creator">The document.</param>
    /// <param name="blocks">The blocks.</param>
    /// <param name="options">The page size and fonts of flowed text.</param>
    /// <param name="sources">The source PDFs opened so far, by path.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes when every block is added.</returns>
    private static async ValueTask AddBlocksAsync(
        PdfDocumentCreator creator,
        IReadOnlyList<CreationBlock> blocks,
        PdfFlowOptions options,
        Dictionary<string, PdfDocument> sources,
        CancellationToken cancellationToken)
    {
        var run = new List<PdfFlowBlock>();
        foreach (var block in blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (block is CreationSourcePage page)
            {
                _ = PdfTextFlow.Add(creator, run, options, cancellationToken);
                run.Clear();
                var source = await OpenSourceAsync(sources, page, cancellationToken).ConfigureAwait(false);
                _ = await creator.AddPageAsync(source, page.PageIndex, cancellationToken).ConfigureAwait(false);
                continue;
            }

            run.Add(ToFlowBlock(creator, block, cancellationToken));
        }

        _ = PdfTextFlow.Add(creator, run, options, cancellationToken);
    }

    /// <summary>Maps a block that is laid out as part of the flow.</summary>
    /// <param name="creator">The document.</param>
    /// <param name="block">The block.</param>
    /// <param name="cancellationToken">Cancels image encoding.</param>
    /// <returns>The library block.</returns>
    /// <exception cref="ArgumentException">The block is not one the creator lays out.</exception>
    private static PdfFlowBlock ToFlowBlock(PdfDocumentCreator creator, CreationBlock block, CancellationToken cancellationToken) => block switch
    {
        CreationHeading heading => new PdfFlowHeading(heading.Level, heading.Text),
        CreationParagraph paragraph => new PdfFlowParagraph(paragraph.Text),
        CreationImage image => DocumentCreationMapping.AddImage(creator, image, cancellationToken),
        CreationPageBreak => new PdfFlowPageBreak(),
        _ => throw new ArgumentException("The block is not one the creator can lay out.", nameof(block)),
    };

    /// <summary>Opens a source PDF once, however many pages are copied from it.</summary>
    /// <param name="sources">The source PDFs opened so far, by path.</param>
    /// <param name="page">The block naming the file.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The document.</returns>
    /// <exception cref="DocumentOpenException">The file cannot be opened.</exception>
    private static async ValueTask<PdfDocument> OpenSourceAsync(Dictionary<string, PdfDocument> sources, CreationSourcePage page, CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(page.Path);
        if (sources.TryGetValue(path, out var existing))
        {
            return existing;
        }

        try
        {
            var opened = await PdfDocumentReader.OpenAsync(path, page.Password, cancellationToken).ConfigureAwait(false);
            sources.Add(path, opened);
            return opened;
        }
        catch (PdfException exception)
        {
            throw HyperPdfEngine.CreateOpenException(exception, path);
        }
    }
}
