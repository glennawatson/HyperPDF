// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;

namespace HyperPdf.Engine.Tests;

/// <summary>Runs <c>qpdf --check</c> on a written file when qpdf is installed.</summary>
internal static class QpdfCheck
{
    /// <summary>The sentence qpdf prints when it finds no syntax or stream encoding errors.</summary>
    private const string CleanSentence = "No syntax or stream encoding errors found";

    /// <summary>Checks a file with qpdf.</summary>
    /// <param name="file">The file bytes.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>What qpdf printed when it found a problem, an empty string when the file is clean, or <see langword="null"/> when qpdf is not installed.</returns>
    internal static async Task<string?> RunAsync(byte[] file, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-qpdf-{Guid.NewGuid():N}.pdf");
        try
        {
            await File.WriteAllBytesAsync(path, file, cancellationToken).ConfigureAwait(false);
            var start = new ProcessStartInfo("qpdf") { RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("--check");
            start.ArgumentList.Add(path);
            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var text = await output.ConfigureAwait(false) + await errors.ConfigureAwait(false);
            var clean = text.Contains(CleanSentence, StringComparison.Ordinal)
                && !text.Contains("WARNING", StringComparison.Ordinal)
                && !text.Contains("ERROR", StringComparison.Ordinal);
            return clean ? string.Empty : text;
        }
        catch (Win32Exception)
        {
            return null;
        }
        finally
        {
            File.Delete(path);
        }
    }
}
