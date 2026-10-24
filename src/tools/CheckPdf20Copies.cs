#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0
#:project ../HyperPdfLibrary/HyperPdfLibrary.csproj

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Pdf20;

namespace HyperPdf.Scripts;

/// <summary>
/// Saves a PDF 2.0 copy of every PDF in a folder and checks each copy: its header reads <c>%PDF-2.0</c>, <c>qpdf --check</c>
/// finds no error or warning, and HyperPDF opens it again with the same number of pages. A file the copy refuses, with the
/// reason, is listed and skipped; at least one copy must be made.
/// </summary>
internal static class CheckPdf20Copies
{
    /// <summary>The longest the whole run may take.</summary>
    private const int TimeoutMinutes = 10;

    /// <summary>The arguments: the folder of PDFs and the folder for the copies.</summary>
    private const int ArgumentCount = 2;

    /// <summary>The qpdf program name, found on the path.</summary>
    private const string Qpdf = "qpdf";

    /// <summary>Gets the bytes a PDF 2.0 file starts with.</summary>
    private static ReadOnlySpan<byte> Header => "%PDF-2.0"u8;

    /// <summary>Makes and checks the copies.</summary>
    /// <param name="args">The folder of PDFs and the folder the copies are saved in.</param>
    /// <returns>A task whose result is 0 when every copy passed, and 1 otherwise.</returns>
    /// <exception cref="ArgumentException">The arguments are wrong.</exception>
    internal static async Task<int> Main(string[] args)
    {
        if (args.Length != ArgumentCount)
        {
            throw new ArgumentException("Provide the folder of PDFs and the folder to save the PDF 2.0 copies in.");
        }

        var input = Path.GetFullPath(args[0]);
        var output = Path.GetFullPath(args[1]);
        _ = Directory.CreateDirectory(output);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(TimeoutMinutes));
        var paths = Directory.GetFiles(input, "*.pdf");
        Array.Sort(paths, StringComparer.Ordinal);
        var made = 0;
        var failed = 0;
        foreach (var path in paths)
        {
            var problem = await CheckOneAsync(path, Path.Combine(output, Path.GetFileName(path)), timeout.Token).ConfigureAwait(false);
            made += problem is null ? 1 : 0;
            failed += problem is { Length: > 0 } ? 1 : 0;
        }

        await ReportAsync($"{made} copies passed, {failed} failed.", timeout.Token).ConfigureAwait(false);
        return failed == 0 && made > 0 ? 0 : 1;
    }

    /// <summary>Prints a line.</summary>
    /// <param name="line">The line.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>A task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task ReportAsync(string line, CancellationToken cancellationToken) => Console.Out.WriteLineAsync(line.AsMemory(), cancellationToken);

    /// <summary>Makes and checks one copy.</summary>
    /// <param name="path">The source PDF.</param>
    /// <param name="copyPath">Where the copy is saved.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns><see langword="null"/> when the copy passed, an empty string when the copy was refused, and the problem otherwise.</returns>
    private static async Task<string?> CheckOneAsync(string path, string copyPath, CancellationToken cancellationToken)
    {
        var name = Path.GetFileName(path);
        using var source = await PdfDocumentReader.OpenAsync(path, null, cancellationToken).ConfigureAwait(false);
        Pdf20ConversionResult result;
        await using (var stream = new FileStream(copyPath, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous))
        {
            result = await Pdf20Converter.SaveAsPdf20Async(source, stream, cancellationToken).ConfigureAwait(false);
        }

        if (!result.Converted)
        {
            File.Delete(copyPath);
            var reasons = new List<string>(result.Problems.Length);
            foreach (var problem in result.Problems)
            {
                reasons.Add(problem.Description);
            }

            await ReportAsync($"{name}: refused: {string.Join(' ', reasons)}", cancellationToken).ConfigureAwait(false);
            return string.Empty;
        }

        var found = await FindProblemAsync(copyPath, source.PageCount, cancellationToken).ConfigureAwait(false);
        await ReportAsync($"{name}: {(found is null ? "passed" : $"FAILED: {found}")}", cancellationToken).ConfigureAwait(false);
        return found;
    }

    /// <summary>Checks a saved copy.</summary>
    /// <param name="copyPath">The copy.</param>
    /// <param name="pageCount">The pages the source has.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The first problem found, or <see langword="null"/>.</returns>
    private static async Task<string?> FindProblemAsync(string copyPath, int pageCount, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(copyPath, cancellationToken).ConfigureAwait(false);
        if (!bytes.AsSpan().StartsWith(Header))
        {
            return "the header does not read %PDF-2.0";
        }

        using var reopened = await PdfDocumentReader.OpenAsync(copyPath, null, cancellationToken).ConfigureAwait(false);
        return reopened.PageCount == pageCount
            ? await RunQpdfAsync(copyPath, cancellationToken).ConfigureAwait(false)
            : $"HyperPDF reopened the copy with {reopened.PageCount} pages instead of {pageCount}";
    }

    /// <summary>Runs <c>qpdf --check</c> on a copy.</summary>
    /// <param name="copyPath">The copy.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns><see langword="null"/> when qpdf finds nothing wrong, and its report otherwise.</returns>
    private static async Task<string?> RunQpdfAsync(string copyPath, CancellationToken cancellationToken)
    {
        var result = await Process.RunAndCaptureTextAsync(Qpdf, ["--check", copyPath], cancellationToken).ConfigureAwait(false);

        // A non-zero exit is an error, a warning or a signal; the copy must pass without any of them.
        return result.ExitStatus is { ExitCode: 0, Signal: null }
            ? null
            : $"qpdf did not pass the copy: {result.StandardOutput} {result.StandardError}";
    }
}
