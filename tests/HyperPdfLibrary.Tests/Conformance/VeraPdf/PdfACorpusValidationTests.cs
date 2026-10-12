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

    /// <summary>
    /// The edit that left these files' linearized layout unsettled, when each pass hashed a fresh /ID of a different
    /// length (#181), and the page reference chain once too deep to walk, now linearize and pass veraPDF.
    /// </summary>
    /// <param name="cancellationToken">Cancels the saves and validation.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task IssueFilesLinearize(CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        var corpus = Path.GetDirectoryName(RequireCorpus()[0])!;
        using var folder = new PdfAOutputFolder("issue-181");
        var outputs = new List<string>();
        foreach (var name in (string[])["verapdf-pdfa1b-linearize-settle.pdf", "verapdf-pdfa1b-deep-references.pdf"])
        {
            var file = Path.Combine(corpus, name);
            using var document = await PdfDocumentReader.OpenAsync(file, null, cancellationToken);
            PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = "HyperPdf standards audit" });
            var path = Path.Combine(folder.Path, Path.GetFileName(file));
            await using (var stream = File.Create(path))
            {
                await PdfLinearizedWriter.SaveAsync(document, stream, cancellationToken);
            }

            outputs.Add(path);
        }

        var results = await VeraPdfValidator.ValidateAsync(home, "1b", outputs, cancellationToken);
        foreach (var path in outputs)
        {
            var job = results[Path.GetFullPath(path)];
            await Assert.That(job.IsCompliant).IsTrue().Because($"{Path.GetFileName(path)} fails {job.FailureSummary}");
        }
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
    /// Edits a corpus file's properties and saves it incrementally, compactly and linearized. A signed file must be
    /// refused by the full rewrites, as a rewrite would break its signature.
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
        await RewriteAsync(outputs, compact, document, problems, stream => PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, stream, cancellationToken));
        var linearized = Path.Combine(folder, "linearized.pdf");
        await RewriteAsync(outputs, linearized, document, problems, stream => PdfLinearizedWriter.SaveAsync(document, stream, cancellationToken));
        return outputs;
    }

    /// <summary>Rewrites the whole file, which must succeed for an unsigned file and be refused for a signed one.</summary>
    /// <param name="outputs">The outputs, added to.</param>
    /// <param name="path">The output path.</param>
    /// <param name="document">The edited document.</param>
    /// <param name="problems">Receives what went wrong.</param>
    /// <param name="save">Writes the file.</param>
    /// <returns>A task.</returns>
    private static async Task RewriteAsync(Dictionary<string, string> outputs, string path, PdfDocument document, List<string> problems, Func<Stream, Task> save)
    {
        var signed = SignaturePresence.HasSignature(document.Objects);
        try
        {
            await using (var stream = File.Create(path))
            {
                await save(stream);
            }

            outputs[Path.GetFileNameWithoutExtension(path)] = path;
            if (signed)
            {
                problems.Add($"{path}: a signed file was rewritten");
            }
        }
        catch (PdfException error)
        {
            File.Delete(path);
            if (!signed)
            {
                problems.Add($"{path}: {error.Message}");
            }
        }
    }
}
