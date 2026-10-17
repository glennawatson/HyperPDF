// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks transfer identity in shared image caches.</summary>
public sealed class ImageKeyTests
{
    /// <summary>The cache key's size before transfer identity replaced its counter.</summary>
    private const int KeyByteLimit = 24;

    /// <summary>Transfer references fit without increasing the cache entry's key storage.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Storage_TransferReference_DoesNotGrowKey() =>
        await Assert.That(Unsafe.SizeOf<ImageKey>()).IsLessThanOrEqualTo(KeyByteLimit);

    /// <summary>Every ICC choice round-trips through the compact cache key.</summary>
    /// <param name="intent">The rendering intent.</param>
    /// <param name="compensate">Whether to compensate the black point.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    public async Task Rendering_AllChoices_PreservesChoice(
        [Matrix((int)IccIntent.Perceptual, (int)IccIntent.RelativeColorimetric, (int)IccIntent.Saturation, (int)IccIntent.AbsoluteColorimetric)] int intent,
        [Matrix(false, true)] bool compensate)
    {
        var rendering = new IccRendering((IccIntent)intent, compensate);
        var key = new ImageKey(new(new PdfDictionary(null), []), 0, rendering);

        await Assert.That(key.Rendering).IsEqualTo(rendering);
    }

    /// <summary>Different transfers remain separate even when their sampled tables match.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Equality_DistinctTransfers_KeepsSeparateKeys()
    {
        var stream = new PdfStream(new PdfDictionary(null), []);
        var first = new PdfTransfer(new byte[PdfTransfer.Entries * PdfTransfer.Channels]);
        var second = new PdfTransfer(new byte[PdfTransfer.Entries * PdfTransfer.Channels]);
        var firstKey = new ImageKey(stream, 0, IccRendering.Default, first);
        var sameKey = new ImageKey(stream, 0, IccRendering.Default, first);
        var secondKey = new ImageKey(stream, 0, IccRendering.Default, second);
        var noTransfer = new ImageKey(stream, 0);
        var keys = new HashSet<ImageKey> { firstKey, sameKey, secondKey, noTransfer };

        await Assert.That(firstKey).IsEqualTo(sameKey);
        await Assert.That(firstKey).IsNotEqualTo(secondKey);
        await Assert.That(firstKey).IsNotEqualTo(noTransfer);
        await Assert.That(keys.Count).IsEqualTo(PdfTransfer.Channels);
    }
}
