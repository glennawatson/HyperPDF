// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>Rewrites a document with first-page and hint sections for linearized PDF readers.</summary>
public static class PdfLinearizedWriter
{
    /// <summary>The maximum layout passes before refusing an unstable file.</summary>
    private const int MaxPasses = 8;

    /// <summary>Writes the document into a new linearized file array.</summary>
    /// <param name="document">The document to save.</param>
    /// <returns>The file bytes.</returns>
    /// <exception cref="PdfException">The document cannot be written as a conforming linearized file.</exception>
    public static byte[] Save(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var plan = PdfLinearizationPlan.Create(document, CancellationToken.None);
        return Build(document.Objects, plan, CancellationToken.None);
    }

    /// <summary>Writes a linearized document to a stream asynchronously.</summary>
    /// <param name="document">The document to save.</param>
    /// <param name="destination">The writable stream.</param>
    /// <param name="cancellationToken">Cancels the save and stream write.</param>
    /// <returns>A task that completes after the output is written.</returns>
    /// <exception cref="PdfException">The document cannot be written as a conforming linearized file.</exception>
    public static async Task SaveAsync(PdfDocument document, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        var plan = PdfLinearizationPlan.Create(document, cancellationToken);
        var bytes = Build(document.Objects, plan, cancellationToken);
        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Repeats the write until all cross-reference and hint offsets are stable.</summary>
    /// <param name="store">The original file.</param>
    /// <param name="plan">The object ordering.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A complete linearized file.</returns>
    /// <exception cref="PdfException">The file layout does not stabilize.</exception>
    private static byte[] Build(PdfObjectStore store, PdfLinearizationPlan plan, CancellationToken cancellationToken)
    {
        var previous = new PdfLinearizationLayout(plan.HintNumber, plan.PageGroups.Length);
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var writer = new PdfObjectWriter(store.Names, store.Security);
            try
            {
                var current = PdfLinearizationOutput.Write(ref writer, store, plan, previous, cancellationToken);
                if (current.Matches(previous))
                {
                    return writer.ToArray();
                }

                previous = current;
            }
            finally
            {
                writer.Dispose();
            }
        }

        throw new PdfException(PdfError.Format, "The linearized file positions did not settle before saving.");
    }
}
