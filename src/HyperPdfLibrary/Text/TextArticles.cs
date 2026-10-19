// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Features;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Reads a page in article order (ISO 32000-2 12.4.3): the document's article threads in the order the catalog lists them,
/// each thread's beads in thread order, and the text inside each bead that is on the page. It follows the order an author
/// gave a reader through columns and sidebars, which neither the drawing order nor the layout order knows.
/// </summary>
internal static class TextArticles
{
    /// <summary>The line break the page text uses between lines.</summary>
    private const string LineBreak = "\r\n";

    /// <summary>Reads the text of the beads on a page.</summary>
    /// <param name="threads">The document's article threads.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="page">The page's text.</param>
    /// <returns>The text of each bead, one after the other; <see langword="null"/> when no bead with a rectangle is on the page.</returns>
    internal static string? Read(PdfThread[] threads, int pageIndex, PdfTextPage page)
    {
        StringBuilder? text = null;
        foreach (var thread in threads)
        {
            foreach (var bead in thread.Beads)
            {
                if (bead.PageIndex != pageIndex || bead.Bounds is not { } bounds)
                {
                    continue;
                }

                text ??= new();
                var piece = page.GetTextInRectangle(bounds);
                _ = piece.Length > 0 && text.Length > 0 ? text.Append(LineBreak) : text;
                _ = text.Append(piece);
            }
        }

        return text?.ToString();
    }
}
