// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using HyperPdfLibrary.Tests.Writing;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>
/// Runs the <c>qpdf</c> command line on a file, as an independent reader of what the encryption writer made. With
/// <see cref="QpdfCheck.OutputVariable"/> set, the files are kept in that folder.
/// </summary>
internal static class QpdfCommand
{
    /// <summary>The argument that stands for the input file's path.</summary>
    internal const string InputToken = "{file}";

    /// <summary>The argument that stands for the path of a file qpdf writes.</summary>
    internal const string OutputToken = "{out}";

    /// <summary>Writes a file and runs qpdf with arguments in which <see cref="InputToken"/> and <see cref="OutputToken"/> are replaced by paths.</summary>
    /// <param name="name">The file name without extension.</param>
    /// <param name="file">The file bytes.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The outcome, or <see langword="null"/> when qpdf is not installed.</returns>
    internal static async Task<QpdfCommandResult?> RunAsync(string name, byte[] file, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var kept = Environment.GetEnvironmentVariable(QpdfCheck.OutputVariable);
        var folder = string.IsNullOrWhiteSpace(kept) ? Path.Combine(Path.GetTempPath(), $"encryption-{Guid.NewGuid():N}") : kept;
        _ = Directory.CreateDirectory(folder);
        try
        {
            var input = Path.Combine(folder, $"{name}.pdf");
            var output = Path.Combine(folder, $"{name}.out.pdf");
            await File.WriteAllBytesAsync(input, file, cancellationToken).ConfigureAwait(false);
            var result = await RunPathsAsync(input, output, arguments, cancellationToken).ConfigureAwait(false);
            return result is { } run && File.Exists(output) ? run with { OutputFile = await File.ReadAllBytesAsync(output, cancellationToken).ConfigureAwait(false) } : result;
        }
        finally
        {
            if (string.IsNullOrWhiteSpace(kept))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    /// <summary>Replaces an input or output token with its path.</summary>
    /// <param name="argument">The argument.</param>
    /// <param name="input">The input path.</param>
    /// <param name="output">The output path.</param>
    /// <returns>The argument to pass.</returns>
    private static string Resolve(string argument, string input, string output) => argument switch
    {
        InputToken => input,
        OutputToken => output,
        _ => argument,
    };

    /// <summary>Runs qpdf with arguments.</summary>
    /// <param name="input">The input path.</param>
    /// <param name="output">The path for a file qpdf writes.</param>
    /// <param name="arguments">The arguments, with the tokens still in them.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The outcome, or <see langword="null"/> when qpdf cannot be started.</returns>
    private static async Task<QpdfCommandResult?> RunPathsAsync(string input, string output, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("qpdf") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(Resolve(argument, input, output));
        }

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            var text = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return new(await text.ConfigureAwait(false) + await errors.ConfigureAwait(false), null);
        }
        catch (Win32Exception)
        {
            return null;
        }
    }
}
