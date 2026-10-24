// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Saves claimed PDF/A files that break their level through every writer and the optimiser, and checks with veraPDF that
/// each repaired file meets the level it claims.
/// </summary>
[NotInParallel(nameof(VeraPdfValidator))]
public sealed class PdfAClaimRepairValidationTests
{
    /// <summary>The PDF/A part based on PDF 1.4.</summary>
    private const int Part1 = 1;

    /// <summary>The PDF/A part based on PDF 1.7.</summary>
    private const int Part2 = 2;

    /// <summary>Every repaired save passes veraPDF at the level the file claims.</summary>
    /// <param name="cancellationToken">Cancels the saves and validation.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepairedSavesPassVeraPdf(CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("repaired");
        var files = new List<string>();
        foreach (var (name, save) in Saves())
        {
            var path = Path.Combine(folder.Path, $"{name}.pdf");
            await using (var stream = File.Create(path))
            {
                var check = await save(stream, cancellationToken);
                await Assert.That(check?.Repaired).IsTrue().Because($"{name} was not repaired");
            }

            files.Add(path);
        }

        var results = await VeraPdfValidator.ValidateAsync(home, VeraPdfValidator.DetectFlavour, files, cancellationToken);
        foreach (var file in files)
        {
            var job = results[Path.GetFullPath(file)];
            await Assert.That(job.IsCompliant).IsTrue().Because($"{Path.GetFileName(file)} fails {job.FailureSummary}");
        }
    }

    /// <summary>Gets each save to check, by output name.</summary>
    /// <returns>The saves; each writes the file and returns the claim check.</returns>
    private static IEnumerable<KeyValuePair<string, Func<Stream, CancellationToken, Task<PdfAClaimCheck?>>>> Saves() =>
    [
        new("compact-need-appearances-2b", static (stream, token) => CompactAsync(PdfAClaimGuardTests.RepairablePdfA2B(), stream, token)),
        new("compact-helvetica-2b", static (stream, token) => CompactAsync(ConversionTestPdf.ClaimedHelveticaText(Part2, "B"), stream, token)),
        new("incremental-helvetica-1b", static (stream, token) => IncrementalAsync(ConversionTestPdf.ClaimedHelveticaText(Part1, "B"), stream, token)),
        new("linearized-helvetica-2b", static (stream, token) => LinearizedAsync(ConversionTestPdf.ClaimedHelveticaText(Part2, "B"), stream, token)),
        new("optimised-need-appearances-2b", static (stream, token) => OptimisedAsync(PdfAClaimGuardTests.RepairablePdfA2B(), stream, token)),
    ];

    /// <summary>Saves a file compactly.</summary>
    /// <param name="source">The file.</param>
    /// <param name="stream">Receives the saved file.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The claim check.</returns>
    private static async Task<PdfAClaimCheck?> CompactAsync(byte[] source, Stream stream, CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(source, null);
        var report = new PdfSaveReport();
        await PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, stream, report, cancellationToken);
        return report.Claim;
    }

    /// <summary>Edits a file's title and saves it incrementally.</summary>
    /// <param name="source">The file.</param>
    /// <param name="stream">Receives the saved file.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The claim check.</returns>
    private static async Task<PdfAClaimCheck?> IncrementalAsync(byte[] source, Stream stream, CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(source, null);
        PdfDocumentMetadataEditing.SetMetadata(document, new PdfMetadataEdit { Title = "Repaired on save" });
        var report = new PdfSaveReport();
        await PdfIncrementalWriter.SaveAsync(document.Objects, stream, report, cancellationToken);
        return report.Claim;
    }

    /// <summary>Saves a file linearized.</summary>
    /// <param name="source">The file.</param>
    /// <param name="stream">Receives the saved file.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The claim check.</returns>
    private static async Task<PdfAClaimCheck?> LinearizedAsync(byte[] source, Stream stream, CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(source, null);
        var report = new PdfSaveReport();
        await PdfLinearizedWriter.SaveAsync(document, stream, report, cancellationToken);
        return report.Claim;
    }

    /// <summary>Saves an optimised copy of a file.</summary>
    /// <param name="source">The file.</param>
    /// <param name="stream">Receives the saved file.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The claim check.</returns>
    private static async Task<PdfAClaimCheck?> OptimisedAsync(byte[] source, Stream stream, CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(source, null);
        var report = await PdfOptimizer.OptimizeAsync(document, stream, PdfOptimizeOptions.KeepQuality, null, cancellationToken);
        return report.PdfAClaim;
    }
}
