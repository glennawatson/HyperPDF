// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Decodes a stream's filters with <c>qpdf --filtered-stream-data</c>, when qpdf is installed, to check encoder output against another reader.</summary>
internal static class QpdfStreamDecoder
{
    /// <summary>The object number of the stream in the file built here.</summary>
    private const int StreamObject = 3;

    /// <summary>Wraps encoded data in a one-stream PDF and has qpdf decode it.</summary>
    /// <param name="filterEntries">The dictionary entries naming the filters, for example <c>/Filter /LZWDecode</c>.</param>
    /// <param name="encoded">The encoded stream data.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The decoded bytes and what qpdf reported, or <see langword="null"/> when qpdf is not installed.</returns>
    internal static async Task<QpdfDecodeResult?> DecodeAsync(string filterEntries, byte[] encoded, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"filters-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "stream.pdf");
            var file = MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [] /Count 0 >>",
                MiniPdf.Stream(filterEntries, Encoding.Latin1.GetString(encoded)));
            await File.WriteAllBytesAsync(path, file, cancellationToken).ConfigureAwait(false);
            return await RunAsync(path, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Runs qpdf on the file.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The result, or <see langword="null"/> when qpdf cannot be started.</returns>
    private static async Task<QpdfDecodeResult?> RunAsync(string path, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("qpdf") { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(string.Create(CultureInfo.InvariantCulture, $"--show-object={StreamObject}"));
        start.ArgumentList.Add("--filtered-stream-data");
        start.ArgumentList.Add(path);
        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            // Standard error is read at the same time as standard output so neither pipe fills and stalls qpdf.
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            await using var data = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(data, cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return new(data.ToArray(), await errors.ConfigureAwait(false));
        }
        catch (Win32Exception)
        {
            return null;
        }
    }
}
