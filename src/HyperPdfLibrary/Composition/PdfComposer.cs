// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>
/// Lays a <see cref="PdfComposition"/> out on pages of a document: sections with their own page setup, headers and footers
/// with page numbers and other fields, paragraphs with full formatting, lists, tab stops with leaders, footnotes, links,
/// bookmarks and an outline built from headings. In a tagged document headings, paragraphs, lists and links get structure
/// elements in reading order, and running headers, footers, borders and rules are artifacts.
/// </summary>
public static class PdfComposer
{
    /// <summary>Lays a composition out as new pages of a document.</summary>
    /// <param name="creator">The document.</param>
    /// <param name="composition">The content and styles.</param>
    /// <param name="options">How to lay it out, or <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">Cancels the layout between blocks and pages.</param>
    /// <returns>What was done.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="creator"/> or <paramref name="composition"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A style is missing, a style has no typeface, or two bookmarks have the same name.</exception>
    /// <exception cref="ArgumentException">A block or an inline is not one the library lays out.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfComposeReport Compose(PdfDocumentCreator creator, PdfComposition composition, PdfComposeOptions? options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(composition);
        var now = (options?.Clock ?? TimeProvider.System).GetUtcNow();
        var env = new LayoutEnv(composition, now, creator.Tags is not null);
        var layout = new DocumentLayout(env, cancellationToken);
        layout.Run();
        var state = new RenderState(creator);
        foreach (var plan in layout.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var running = RunningRenderer.Build(env, plan, layout.Pages.Count, cancellationToken);
            _ = PageWriter.Write(state, plan, running);
        }

        CatalogWriter.Write(state, env, layout.Pages);
        return new(layout.Pages.Count, [.. env.Warnings]);
    }

    /// <summary>Lays a composition out as new pages of a document.</summary>
    /// <param name="creator">The document.</param>
    /// <param name="composition">The content and styles.</param>
    /// <param name="options">How to lay it out, or <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">Cancels the layout between blocks and pages.</param>
    /// <returns>What was done.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="creator"/> or <paramref name="composition"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<PdfComposeReport> ComposeAsync(PdfDocumentCreator creator, PdfComposition composition, PdfComposeOptions? options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Compose(creator, composition, options, cancellationToken));
    }
}
