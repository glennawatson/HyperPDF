// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>
/// Runs <c>qpdf --check</c> on a written file when qpdf is installed. With <see cref="OutputVariable"/> set the files are
/// kept in that folder, so they can be looked at or checked again by hand.
/// </summary>
internal static class QpdfCheck
{
    /// <summary>The variable naming a folder that keeps the written files.</summary>
    internal const string OutputVariable = "HYPERPDF_WRITER_OUTPUT";

    /// <summary>The sentence qpdf prints when it finds no syntax or stream encoding errors.</summary>
    private const string CleanSentence = "No syntax or stream encoding errors found";

    /// <summary>Writes a file and checks it with qpdf.</summary>
    /// <param name="name">The file name without extension.</param>
    /// <param name="file">The file bytes.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The outcome, or <see langword="null"/> when qpdf is not installed.</returns>
    internal static async Task<QpdfOutcome?> RunAsync(string name, byte[] file, CancellationToken cancellationToken)
    {
        var kept = Environment.GetEnvironmentVariable(OutputVariable);
        var folder = string.IsNullOrWhiteSpace(kept) ? Path.Combine(Path.GetTempPath(), $"writer-{Guid.NewGuid():N}") : kept;
        _ = Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{name}.pdf");
        try
        {
            await File.WriteAllBytesAsync(path, file, cancellationToken).ConfigureAwait(false);
            return await CheckAsync(path, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (string.IsNullOrWhiteSpace(kept))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    /// <summary>Runs qpdf on a file.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The outcome, or <see langword="null"/> when qpdf cannot be started.</returns>
    private static async Task<QpdfOutcome?> CheckAsync(string path, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("qpdf") { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("--check");
        start.ArgumentList.Add(path);
        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var text = await output.ConfigureAwait(false) + await errors.ConfigureAwait(false);

            // qpdf names every problem WARNING or ERROR and ends a clean check with this sentence.
            var clean = text.Contains(CleanSentence, StringComparison.Ordinal)
                && !text.Contains("WARNING", StringComparison.Ordinal)
                && !text.Contains("ERROR", StringComparison.Ordinal);
            return new(clean, text);
        }
        catch (Win32Exception)
        {
            return null;
        }
    }
}
