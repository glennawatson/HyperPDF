// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Drawing;

/// <summary>Provides image codecs and resampling independently of page rendering.</summary>
/// <remarks>Spans are borrowed synchronously for the duration of each call and must never be retained.</remarks>
public interface IPdfImageCodec
{
    /// <summary>Decodes JPEG pixels into the requested layout, accepting incomplete input when pixels are usable.</summary>
    /// <param name="encoded">The JPEG bytes.</param>
    /// <param name="layout">The expected dimensions and destination format.</param>
    /// <param name="pixels">The caller-owned destination with tightly packed rows, large enough for the layout.</param>
    /// <returns>True when the JPEG dimensions match and usable opaque pixels were decoded.</returns>
    bool TryDecodeJpeg(ReadOnlySpan<byte> encoded, PdfImagePixelLayout layout, Span<byte> pixels);

    /// <summary>
    /// Encodes pixels as JPEG, compositing premultiplied BGRA over black because JPEG has no alpha. The library no longer
    /// calls this: the optimiser and image creation use the managed <c>PdfJpegEncoder</c>. It stays for hosts that call it themselves.
    /// </summary>
    /// <param name="pixels">The source pixels with tightly packed rows.</param>
    /// <param name="layout">The source dimensions and format.</param>
    /// <param name="quality">The quality, clamped to 1 through 100.</param>
    /// <returns>An owned JPEG byte array, or null when encoding fails.</returns>
    byte[]? EncodeJpeg(ReadOnlySpan<byte> pixels, PdfImagePixelLayout layout, int quality);

    /// <summary>Resamples pixels with a Mitchell cubic filter, preserving their alpha representation.</summary>
    /// <param name="source">The source pixels with tightly packed rows.</param>
    /// <param name="layout">The source dimensions and format.</param>
    /// <param name="width">The target width.</param>
    /// <param name="height">The target height.</param>
    /// <param name="output">The caller-owned destination with tightly packed rows, large enough for the target dimensions.</param>
    /// <returns>True when resampling succeeds.</returns>
    bool Resample(ReadOnlySpan<byte> source, PdfImagePixelLayout layout, int width, int height, Span<byte> output);
}
