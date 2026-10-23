// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Renders a one-bit CCITT image with <c>mutool draw</c>, when MuPDF's mutool is installed, to read the encoder's output with another reader.</summary>
internal static class MutoolBilevelRenderer
{
    /// <summary>The header of a binary grey PNM file: the magic number, then width, height and the maximum value.</summary>
    private const string GreyMagic = "P5";

    /// <summary>Renders an image drawn at one pixel per sample.</summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="decodeParameters">The CCITTFaxDecode parameters, as dictionary entries.</param>
    /// <param name="encoded">The encoded image data.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The grey pixels, one byte each, or <see langword="null"/> when mutool is not installed.</returns>
    internal static async Task<byte[]?> RenderAsync(int width, int height, string decodeParameters, byte[] encoded, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"ccitt-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        try
        {
            var source = Path.Combine(folder, "image.pdf");
            var target = Path.Combine(folder, "page.pgm");
            await File.WriteAllBytesAsync(source, BuildFile(width, height, decodeParameters, encoded), cancellationToken).ConfigureAwait(false);
            var ran = await RunAsync(source, target, cancellationToken).ConfigureAwait(false);
            return ran ? ReadPixels(await File.ReadAllBytesAsync(target, cancellationToken).ConfigureAwait(false), width, height) : null;
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Builds a one-page PDF whose page is exactly the image's size and shows only the image.</summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="decodeParameters">The CCITTFaxDecode parameters.</param>
    /// <param name="encoded">The encoded data.</param>
    /// <returns>The file.</returns>
    private static byte[] BuildFile(int width, int height, string decodeParameters, byte[] encoded)
    {
        var content = string.Create(CultureInfo.InvariantCulture, $"q {width} 0 0 {height} 0 0 cm /Im0 Do Q");
        var page = string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Contents 4 0 R /Resources << /XObject << /Im0 5 0 R >> >> >>");
        var imageEntries = string.Create(
            CultureInfo.InvariantCulture,
            $"/Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceGray /BitsPerComponent 1 /Filter /CCITTFaxDecode /DecodeParms << {decodeParameters} >>");
        return MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            page,
            MiniPdf.Stream(string.Empty, content),
            MiniPdf.Stream(imageEntries, Encoding.Latin1.GetString(encoded)));
    }

    /// <summary>Runs <c>mutool draw</c>.</summary>
    /// <param name="source">The PDF file.</param>
    /// <param name="target">The grey PNM file to write.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns><see langword="false"/> when mutool cannot be started.</returns>
    private static async Task<bool> RunAsync(string source, string target, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("mutool") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "draw", "-q", "-c", "gray", "-F", "pnm", "-o", target, source, "1" })
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            _ = await output.ConfigureAwait(false);
            _ = await errors.ConfigureAwait(false);
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    /// <summary>Reads the pixels of a binary grey PNM file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="width">The expected width.</param>
    /// <param name="height">The expected height.</param>
    /// <returns>The pixels.</returns>
    /// <exception cref="InvalidDataException">The file is not a grey PNM of the expected size.</exception>
    private static byte[] ReadPixels(byte[] file, int width, int height)
    {
        var header = string.Create(CultureInfo.InvariantCulture, $"{GreyMagic}\n{width} {height}\n255\n");
        if (!file.AsSpan().StartsWith(Encoding.ASCII.GetBytes(header)))
        {
            throw new InvalidDataException("mutool did not write a grey image of the expected size.");
        }

        return file[header.Length..];
    }
}
