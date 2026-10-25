// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Conformance.VeraPdf;

namespace HyperPdfLibrary.Tests.Conformance.Isartor;

/// <summary>
/// Runs HyperPdf's PDF/A-1b validator over the Isartor test suite from the PDF Association and records, for each file,
/// whether the validator reports the violation the file was built to contain. The suite is not part of this repository
/// and is used only to check PDF validation software, as its terms of use allow. Without the files the test skips.
/// </summary>
public sealed class IsartorSuiteTests
{
    /// <summary>The variable naming the folder of extracted Isartor test files.</summary>
    internal const string FilesVariable = "HYPERPDF_ISARTOR_FILES";

    /// <summary>Validates every Isartor file and writes the per-file results list.</summary>
    /// <param name="cancellationToken">Cancels the validation.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidatorResultsAreRecorded(CancellationToken cancellationToken)
    {
        var root = RequireFiles();
        var results = new List<IsartorResult>();
        var files = Directory.GetFiles(root, "isartor-*.pdf", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        foreach (var file in files)
        {
            results.Add(await ValidateAsync(root, file, cancellationToken));
        }

        using var output = new PdfAOutputFolder("isartor");
        var list = Path.Combine(output.Path, IsartorResultsFile.FileName);
        await IsartorResultsFile.WriteAsync(list, results, cancellationToken);
        TestContext.Current?.Output.WriteLine($"Isartor:{results.Count} files, {Count(results, IsartorOutcome.DetectedInClause)} flagged in the expected clause, "
            + $"{Count(results, IsartorOutcome.DetectedElsewhere)} flagged in another clause, {Count(results, IsartorOutcome.Missed)} missed, "
            + $"{Count(results, IsartorOutcome.NotOpened)} not opened. Results: {list}");

        await Assert.That(results).IsNotEmpty();
        await Assert.That(File.Exists(list)).IsTrue();

        // Every file is built to break a requirement, so a file with no reported failure is a check the validator lost.
        var missed = results.Where(static result => result.Outcome == IsartorOutcome.Missed).Select(static result => result.Path).ToArray();
        await Assert.That(missed).IsEmpty();
    }

    /// <summary>Validates one file against PDF/A-1b.</summary>
    /// <param name="root">The suite folder.</param>
    /// <param name="file">The file.</param>
    /// <param name="cancellationToken">Cancels the validation.</param>
    /// <returns>The result.</returns>
    private static async Task<IsartorResult> ValidateAsync(string root, string file, CancellationToken cancellationToken)
    {
        var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
        var clause = IsartorResultsFile.ClauseOf(Path.GetFileName(file));
        try
        {
            using var document = await PdfDocumentReader.OpenAsync(file, null, cancellationToken);
            var report = await PdfAValidator.ValidateAsync(document, PdfALevel.PdfA1B, cancellationToken);
            var failed = Array.ConvertAll(report.Failed, static rule => $"{rule.Clause}:{rule.Test}");
            return new(relative, clause, IsartorResultsFile.Classify(report, clause), failed);
        }
        catch (PdfException exception)
        {
            return new(relative, clause, IsartorOutcome.NotOpened, [exception.Message]);
        }
    }

    /// <summary>Counts the results with an outcome.</summary>
    /// <param name="results">The results.</param>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The count.</returns>
    private static int Count(List<IsartorResult> results, IsartorOutcome outcome)
    {
        var count = 0;
        foreach (var result in results)
        {
            if (result.Outcome == outcome)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Gets the folder of Isartor test files, or skips the test when there is none.</summary>
    /// <returns>The folder.</returns>
    private static string RequireFiles()
    {
        var folder = Environment.GetEnvironmentVariable(FilesVariable);
        if (folder is { Length: > 0 } && Directory.Exists(folder))
        {
            return folder;
        }

        Skip.Test($"No Isartor test files. Extract the suite with 'dotnet run --file src/tools/ExtractIsartorSuite.cs -- <zip> <dir>' and set {FilesVariable} to that directory.");
        return string.Empty;
    }
}
