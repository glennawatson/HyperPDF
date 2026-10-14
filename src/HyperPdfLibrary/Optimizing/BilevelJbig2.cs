// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

namespace HyperPdfLibrary.Optimizing;

/// <summary>Codes a black-and-white image as JBIG2 for the optimiser: lossless always, lossy symbol merging only when asked for.</summary>
internal static class BilevelJbig2
{
    /// <summary>Finds the smallest JBIG2 coding of 1-bit rows that is smaller than a limit.</summary>
    /// <param name="rows">The packed rows, in the layout of a PDF image's samples.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="mode">Whether lossy symbol merging may be tried.</param>
    /// <param name="limit">The size to beat, in bytes.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The coding, or <see langword="null"/> when none is smaller than the limit.</returns>
    internal static Jbig2EncodedImage? Choose(ReadOnlySpan<byte> rows, int width, int height, PdfJbig2Mode mode, long limit, CancellationToken cancellationToken)
    {
        var best = Jbig2Encoder.Encode(rows, width, height, Jbig2EncodeOptions.Default, cancellationToken);
        var bestLength = (long)best.Data.Length;
        if (mode == PdfJbig2Mode.Lossy)
        {
            var lossy = Jbig2Encoder.Encode(rows, width, height, new() { Mode = Jbig2EncodeMode.LossySymbolMerging }, cancellationToken);
            if (!lossy.IsLossless && lossy.Data.Length < bestLength)
            {
                best = lossy;
                bestLength = lossy.Data.Length;
            }
        }

        return bestLength < limit ? best : null;
    }
}
