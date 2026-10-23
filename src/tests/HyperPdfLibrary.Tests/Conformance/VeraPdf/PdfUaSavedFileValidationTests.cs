// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Redaction;
using HyperPdfLibrary.Tests.PdfUa;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Checks, with veraPDF as the independent PDF/UA-1 validator, that files HyperPdf saves after each kind of edit still pass every PDF/UA-1
/// rule HyperPdf checks: the claim a saved file keeps is a claim veraPDF accepts for those rules. Without veraPDF the test skips.
/// </summary>
[NotInParallel(nameof(VeraPdfValidator))]
public sealed class PdfUaSavedFileValidationTests
{
    /// <summary>The description of an annotation that can be tagged.</summary>
    private const string Described = "Marked for redaction";

    /// <summary>The left edge of the redacted area.</summary>
    private const float AreaLeft = 10F;

    /// <summary>The bottom of the redacted area.</summary>
    private const float AreaBottom = 10F;

    /// <summary>The right edge of the redacted area.</summary>
    private const float AreaRight = 100F;

    /// <summary>The top of the redacted area.</summary>
    private const float AreaTop = 40F;

    /// <summary>The veraPDF flavour of PDF/UA-1.</summary>
    private const string Flavour = "ua1";

    /// <summary>A file saved after a metadata edit, an annotation, a redaction, a page copy or an optimiser run passes the rules HyperPdf checks.</summary>
    /// <param name="cancellationToken">Cancels the saves and the validation.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavedFilesPassTheRulesHyperPdfChecks(CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("pdfua-saved");
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [await Save(folder.Path, "source", PdfUaClaimGuardTests.CreateClaimed(), cancellationToken)] = "source",
            [await Save(folder.Path, "metadata", Metadata(), cancellationToken)] = "metadata",
            [await Save(folder.Path, "annotation", Annotation(), cancellationToken)] = "annotation",
            [await Save(folder.Path, "redaction", Redaction(), cancellationToken)] = "redaction",
            [await Save(folder.Path, "page-copy", PageCopy(), cancellationToken)] = "page-copy",
            [await Save(folder.Path, "optimiser", await Optimise(cancellationToken), cancellationToken)] = "optimiser",
        };

        var jobs = await VeraPdfValidator.ValidateAsync(home, Flavour, [.. files.Keys], cancellationToken);

        var checkedRules = new HashSet<string>(StandardsRuleLinks.All.Where(static link => link.Specification == StandardsRuleLinks.Ua1).SelectMany(static link => link.Rules), StringComparer.Ordinal);
        var failures = new List<string>();
        foreach (var (path, name) in files)
        {
            foreach (var failure in jobs[Path.GetFullPath(path)].Failures)
            {
                if (checkedRules.Contains($"{failure.Clause}:{failure.TestNumber}"))
                {
                    failures.Add($"{name}: {failure}");
                }
            }
        }

        await Assert.That(failures).IsEmpty().Because(string.Join(" | ", failures));
    }

    /// <summary>Writes a file into the folder.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="name">The file's name.</param>
    /// <param name="bytes">The file.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The file's path.</returns>
    private static async Task<string> Save(string folder, string name, byte[] bytes, CancellationToken cancellationToken)
    {
        var path = Path.Combine(folder, $"{name}.pdf");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return path;
    }

    /// <summary>Saves a claimed file after a metadata edit.</summary>
    /// <returns>The saved file.</returns>
    private static byte[] Metadata()
    {
        using var document = PdfDocumentReader.Open(PdfUaClaimGuardTests.CreateClaimed(), null);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = "Edited" });
        using var output = new MemoryStream();
        PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default, output, new());
        return output.ToArray();
    }

    /// <summary>Saves a claimed file after an annotation is added, which the save tags.</summary>
    /// <returns>The saved file.</returns>
    private static byte[] Annotation()
    {
        using var document = PdfDocumentReader.Open(PdfUaClaimGuardTests.CreateClaimed(), null);
        PdfUaClaimGuardTests.AddAnnotation(document, Described);
        using var output = new MemoryStream();
        PdfIncrementalWriter.Save(document.Objects, output, new());
        return output.ToArray();
    }

    /// <summary>Saves a claimed file after a redaction is applied.</summary>
    /// <returns>The saved file.</returns>
    private static byte[] Redaction()
    {
        using var document = PdfDocumentReader.Open(PdfUaClaimGuardTests.CreateClaimed(), null);
        _ = PdfRedactions.Add(document, 0, [new PdfRectangle(AreaLeft, AreaBottom, AreaRight, AreaTop)], PdfRedactionAppearance.Black);
        using var output = new MemoryStream();
        _ = PdfRedactor.ApplyAndSave(document, output, PdfRedactionOptions.Default);
        return output.ToArray();
    }

    /// <summary>Saves a claimed file after a page is copied into it.</summary>
    /// <returns>The saved file.</returns>
    private static byte[] PageCopy()
    {
        using var document = PdfDocumentReader.Open(PdfUaClaimGuardTests.CreateClaimed(), null);
        PdfDocumentPageOperations.InsertPages(document, 1, document, [0]);
        using var output = new MemoryStream();
        PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default, output, new());
        return output.ToArray();
    }

    /// <summary>Optimises a claimed file.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The optimised file.</returns>
    private static async Task<byte[]> Optimise(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(PdfUaClaimGuardTests.CreateClaimed(), null);
        await using var output = new MemoryStream();
        _ = await PdfOptimizer.OptimizeAsync(document, output, PdfOptimizeOptions.KeepQuality, null, cancellationToken);
        return output.ToArray();
    }
}
