// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Runs <c>qpdf --json-key</c> on a written file when qpdf is installed, so a test can read the file the way qpdf does.</summary>
internal static class QpdfJson
{
    /// <summary>Reads one key of qpdf's JSON description of a file.</summary>
    /// <param name="file">The file bytes.</param>
    /// <param name="key">The key, such as <c>outlines</c>, <c>attachments</c> or <c>qpdf</c>.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The value of the key, or <see langword="null"/> when qpdf is not installed or cannot describe the file.</returns>
    internal static async Task<JsonElement?> ReadAsync(byte[] file, string key, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"qpdf-json-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "file.pdf");
        try
        {
            await File.WriteAllBytesAsync(path, file, cancellationToken).ConfigureAwait(false);
            return await RunAsync(path, key, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Runs qpdf on a file.</summary>
    /// <param name="path">The file.</param>
    /// <param name="key">The JSON key.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    /// <exception cref="InvalidOperationException">qpdf printed no JSON.</exception>
    private static async Task<JsonElement?> RunAsync(string path, string key, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("qpdf") { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("--json");
        start.ArgumentList.Add($"--json-key={key}");
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
            var text = await output.ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(text))
            {
                var message = await errors.ConfigureAwait(false);
                throw new InvalidOperationException($"qpdf printed no JSON: {message}");
            }

            using var document = JsonDocument.Parse(text);
            return document.RootElement.TryGetProperty(key, out var value) ? value.Clone() : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }
}
