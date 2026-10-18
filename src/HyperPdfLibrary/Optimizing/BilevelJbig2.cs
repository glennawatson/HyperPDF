// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

namespace HyperPdfLibrary.Optimizing;

/// <summary>Codes a black-and-white image as JBIG2 for the optimiser: lossless always, lossy symbol merging only when asked for.</summary>
internal static class BilevelJbig2
{
    /// <summary>The fixed settings for explicitly requested lossy symbol sharing.</summary>
    private static readonly Jbig2EncodeOptions LossyOptions = new() { Mode = Jbig2EncodeMode.LossySymbolMerging };

    /// <summary>Finds the smallest JBIG2 coding of 1-bit rows that is smaller than a limit.</summary>
    /// <param name="rows">The packed rows, in the layout of a PDF image's samples.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="mode">Whether lossy symbol merging may be tried.</param>
    /// <param name="limit">The size to beat, in bytes.</param>
    /// <param name="pools">The original caller's encoding scratch.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The coding, or <see langword="null"/> when none is smaller than the limit.</returns>
    internal static Jbig2EncodedImage? Choose(ReadOnlySpan<byte> rows, int width, int height, PdfJbig2Mode mode, long limit, ScratchPools pools, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (limit <= 0)
        {
            return null;
        }

        var data = default(PooledBuffer);
        var globals = default(PooledBuffer);
        try
        {
            var chosen = Jbig2Encoder.EncodeInto(rows, new(new(width, height), Jbig2EncodeOptions.Default), ref data, ref globals, pools, cancellationToken);
            if (mode == PdfJbig2Mode.Lossy && TryLossy(rows, new(width, height), ref data, ref globals, pools, cancellationToken))
            {
                chosen = Jbig2EncodeMode.LossySymbolMerging;
            }

            return data.Length < limit
                ? new(data.ToArray(), globals.Length == 0 ? null : globals.ToArray(), chosen, chosen != Jbig2EncodeMode.LossySymbolMerging)
                : null;
        }
        finally
        {
            data.Dispose();
            globals.Dispose();
        }
    }

    /// <summary>Replaces the lossless coding only when lossy sharing is actually used and is smaller.</summary>
    /// <param name="rows">The packed page samples.</param>
    /// <param name="size">The page size.</param>
    /// <param name="data">The smallest page coding.</param>
    /// <param name="globals">The smallest global coding.</param>
    /// <param name="pools">The original caller's encoding scratch.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>True when lossy coding replaced the lossless buffers.</returns>
    private static bool TryLossy(ReadOnlySpan<byte> rows, Jbig2Size size, ref PooledBuffer data, ref PooledBuffer globals, ScratchPools pools, CancellationToken cancellationToken)
    {
        var candidate = default(PooledBuffer);
        var extra = default(PooledBuffer);
        try
        {
            var mode = Jbig2Encoder.EncodeInto(rows, new(new(size.Width, size.Height), LossyOptions), ref candidate, ref extra, pools, cancellationToken);
            if (mode != Jbig2EncodeMode.LossySymbolMerging || candidate.Length >= data.Length)
            {
                return false;
            }

            data.Swap(ref candidate);
            globals.Swap(ref extra);
            return true;
        }
        finally
        {
            candidate.Dispose();
            extra.Dispose();
        }
    }
}
