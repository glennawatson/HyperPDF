// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Lays headings, paragraphs, horizontal rules, images, tables and code blocks out on pages. Text wraps to the page width
/// and runs onto new pages; blocks with <see cref="PdfFlowBlock.KeepWithNext"/> stay with the start of the following block;
/// oversized images follow their <see cref="PdfFlowImage.Fit"/> policy; in a tagged document headings become <c>H1</c> to
/// <c>H6</c>, paragraphs <c>P</c>, tables <c>Table</c> and images <c>Figure</c> with their alternative text, in reading
/// order, while rules, decorative images and page numbers are artifacts.
/// </summary>
public static class PdfTextFlow
{
    /// <summary>Lays blocks out as new pages of a document.</summary>
    /// <param name="creator">The document.</param>
    /// <param name="blocks">The blocks, in reading order.</param>
    /// <param name="options">The page size, margins and fonts.</param>
    /// <param name="cancellationToken">Cancels the layout between blocks and pages.</param>
    /// <returns>The number of pages added.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size in the options is not positive, or the margins leave no room for text.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static int Add(PdfDocumentCreator creator, IReadOnlyList<PdfFlowBlock> blocks, PdfFlowOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(options);
        var run = new FlowRun(creator, options, cancellationToken);
        try
        {
            return run.Run(blocks);
        }
        finally
        {
            run.Abandon();
        }
    }
}
