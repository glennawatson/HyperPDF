// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Conformance;

/// <summary>Repairs a written file that broke its claimed PDF/A level, by converting it at that level.</summary>
internal static class PdfAClaimRepairs
{
    /// <summary>The reason given when the written file cannot be read back to repair.</summary>
    private const string UnreadableReason =
        "The file is protected with an open password, and PDF/A does not allow encryption, so it cannot be repaired.";

    /// <summary>Converts a written file at its claimed level, using only font data already on this computer.</summary>
    /// <param name="output">The written file.</param>
    /// <param name="level">The level it claims.</param>
    /// <param name="cancellationToken">Stops the conversion.</param>
    /// <returns>The repair; its file is <see langword="null"/> when the conversion refused.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static PdfAClaimRepair Repair(byte[] output, PdfALevel level, CancellationToken cancellationToken)
    {
        try
        {
            using var written = PdfDocumentReader.Open(output, null);
            using var repaired = new MemoryStream();
            var result = PdfAConverter.SaveAsPdfA(written, level, repaired, cancellationToken);
            return new(result.Changes, result.Problems, result.Converted ? repaired.ToArray() : null);
        }
        catch (PdfException exception) when (exception.Error == PdfError.Password)
        {
            return Unreadable(level);
        }
    }

    /// <summary>Converts a written file at its claimed level, downloading font data the conversion needs.</summary>
    /// <param name="output">The written file.</param>
    /// <param name="level">The level it claims.</param>
    /// <param name="cancellationToken">Stops the conversion and the downloads.</param>
    /// <returns>The repair; its file is <see langword="null"/> when the conversion refused.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static async ValueTask<PdfAClaimRepair> RepairAsync(byte[] output, PdfALevel level, CancellationToken cancellationToken)
    {
        PdfDocument written;
        try
        {
            written = PdfDocumentReader.Open(output, null);
        }
        catch (PdfException exception) when (exception.Error == PdfError.Password)
        {
            return Unreadable(level);
        }

        using (written)
        {
            var repaired = new MemoryStream();
            await using (repaired.ConfigureAwait(false))
            {
                var result = await PdfAConverter.SaveAsPdfAAsync(written, level, repaired, cancellationToken).ConfigureAwait(false);
                return new(result.Changes, result.Problems, result.Converted ? repaired.ToArray() : null);
            }
        }
    }

    /// <summary>Builds the refusal of a file that cannot be read back without its password.</summary>
    /// <param name="level">The level it claims.</param>
    /// <returns>The repair, with no file.</returns>
    private static PdfAClaimRepair Unreadable(PdfALevel level)
    {
        var clause = string.Empty;
        foreach (var reference in PdfARuleCatalog.For(level))
        {
            if (reference.Rule != PdfARule.Encryption)
            {
                continue;
            }

            clause = reference.Clause;
            break;
        }

        return new([], [new(PdfALevels.Specification(level), clause, UnreadableReason)], null);
    }
}
