// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Reads and saves valid veraPDF corpus files (CC BY 4.0, fetched by CI from <c>tests/reference-pdfs/manifest.json</c>).
/// HyperPdf must read each file as veraPDF does, and every edited copy it writes must keep a claim that veraPDF accepts.
/// A signed file must be refused by full rewrites and saved incrementally.
/// </summary>
[NotInParallel(nameof(VeraPdfValidator))]
public sealed class PdfACorpusValidationTests
{
    /// <summary>The variable naming the folder of fetched corpus files.</summary>
    internal const string CorpusVariable = "PDFVIEWERLITE_PDFA_CORPUS_DIR";

    /// <summary>Every corpus file reads as veraPDF reads it, and its edited copies pass veraPDF with the same claim.</summary>
    /// <param name="cancellationToken">Cancels the saves and validation.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task CorpusFilesReadAndSaveAsVeraPdfExpects(CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        var files = RequireCorpus();
        var sources = await VeraPdfValidator.ValidateAsync(home, VeraPdfValidator.DetectFlavour, files, cancellationToken);
        var problems = new List<string>();
        var outputs = new Dictionary<string, string?>(StringComparer.Ordinal);
        using var folder = new PdfAOutputFolder("corpus");
        foreach (var file in files)
        {
            var source = sources[Path.GetFullPath(file)];
            var name = Path.GetFileNameWithoutExtension(file);
            if (!source.IsCompliant)
            {
                problems.Add($"{name}: the source fails {source.FailureSummary}");
            }

            problems.AddRange(PdfAReadingComparison.Compare(file, source).Select(difference => $"{name}: {difference}"));
            foreach (var path in (await SaveAsync(file, Path.Combine(folder.Path, name), problems, cancellationToken)).Values)
            {
                outputs[path] = PdfAReadingComparison.ClaimFromProfile(source.ProfileName);
            }
        }

        var saved = await VeraPdfValidator.ValidateAsync(home, VeraPdfValidator.DetectFlavour, [.. outputs.Keys], cancellationToken);
        foreach (var (path, claim) in outputs)
        {
            var job = saved[Path.GetFullPath(path)];
            var label = Path.GetRelativePath(folder.Path, path);
            if (!string.Equals(PdfAReadingComparison.ClaimFromProfile(job.ProfileName), claim, StringComparison.Ordinal))
            {
                problems.Add($"{label}: claim became {job.ProfileName}, was PDF/A-{claim}");
            }

            if (!job.IsCompliant)
            {
                problems.Add($"{label}: fails {job.FailureSummary}");
            }
        }

        await Assert.That(problems).IsEmpty().Because(string.Join(Environment.NewLine, problems));
    }

    /// <summary>Gets the fetched corpus files, or skips the test when there are none.</summary>
    /// <returns>The files.</returns>
    /// <exception cref="DirectoryNotFoundException">The files are required but missing.</exception>
    private static string[] RequireCorpus()
    {
        var folder = Environment.GetEnvironmentVariable(CorpusVariable);
        var files = folder is { Length: > 0 } && Directory.Exists(folder) ? Directory.GetFiles(folder, "verapdf-pdfa*.pdf").Order(StringComparer.Ordinal).ToArray() : [];
        if (files.Length > 0)
        {
            return files;
        }

        const string message = $"No veraPDF corpus files. Fetch the verapdf-pdfa* entries of tests/reference-pdfs/manifest.json with scripts/FetchReferencePdfs.cs and set {CorpusVariable}.";
        if (Environment.GetEnvironmentVariable(VeraPdfValidator.RequireVariable) == "1")
        {
            throw new DirectoryNotFoundException(message);
        }

        Skip.Test(message);
        return [];
    }

    /// <summary>
    /// Edits a corpus file's properties and saves it incrementally and compactly. A signed file must be refused by the
    /// compact writer, as a rewrite would break its signature.
    /// </summary>
    /// <param name="file">The corpus file.</param>
    /// <param name="folder">The folder for this file's outputs.</param>
    /// <param name="problems">Receives what went wrong.</param>
    /// <param name="cancellationToken">Cancels the saves.</param>
    /// <returns>The outputs written, by name.</returns>
    private static async Task<Dictionary<string, string>> SaveAsync(string file, string folder, List<string> problems, CancellationToken cancellationToken)
    {
        _ = Directory.CreateDirectory(folder);
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
        using var document = await PdfDocumentReader.OpenAsync(file, null, cancellationToken);
        PdfDocumentMetadataEditing.SetMetadata(document, PdfAOutputs.Edit);
        outputs["incremental"] = Path.Combine(folder, "incremental.pdf");
        await using (var stream = File.Create(outputs["incremental"]))
        {
            await PdfDocumentSaving.SaveAsync(document, stream, cancellationToken);
        }

        var compact = Path.Combine(folder, "compact.pdf");
        try
        {
            await using (var stream = File.Create(compact))
            {
                await PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, stream, cancellationToken);
            }

            outputs["compact"] = compact;
            if (SignaturePresence.HasSignature(document.Objects))
            {
                problems.Add($"{Path.GetFileName(folder)}: the compact writer rewrote a signed file");
            }
        }
        catch (PdfException) when (SignaturePresence.HasSignature(document.Objects))
        {
            File.Delete(compact);
        }

        return outputs;
    }
}
