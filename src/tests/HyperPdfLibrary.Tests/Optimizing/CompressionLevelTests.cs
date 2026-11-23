// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Checks the level the optimiser recompresses streams at.</summary>
public sealed class CompressionLevelTests
{
    /// <summary>The side of the sample image in pixels, large enough for the levels to differ.</summary>
    private const int Pixels = 256;

    /// <summary>The size the sample image is drawn at, in points.</summary>
    private const int DrawPoints = 256;

    /// <summary>The balanced and quality presets recompress at the balanced level, and the smaller preset at the smallest.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PresetsNameTheirLevel()
    {
        await Assert.That(PdfOptimizeOptions.Balanced.Compression).IsEqualTo(PdfCompressionLevel.Balanced);
        await Assert.That(PdfOptimizeOptions.KeepQuality.Compression).IsEqualTo(PdfCompressionLevel.Balanced);
        await Assert.That(PdfOptimizeOptions.Smaller.Compression).IsEqualTo(PdfCompressionLevel.Smallest);
    }

    /// <summary>The smallest level never makes a larger file than the balanced one, and both files hold the same text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheSmallestLevelIsNoLargerThanTheBalancedOne()
    {
        var source = OptimizerSamples.Photo(Pixels, DrawPoints, string.Empty);
        var options = PdfOptimizeOptions.KeepQuality with { OptimizeImages = false };

        var balanced = OptimizerTestKit.Optimize(source, options with { Compression = PdfCompressionLevel.Balanced });
        var smallest = OptimizerTestKit.Optimize(source, options with { Compression = PdfCompressionLevel.Smallest });

        await Assert.That(smallest.Bytes.Length).IsLessThanOrEqualTo(balanced.Bytes.Length);
        await Assert.That(balanced.Bytes.Length).IsLessThan(source.Length);
        await Assert.That(OptimizerTestKit.Text(balanced.Bytes)).IsEquivalentTo(OptimizerTestKit.Text(smallest.Bytes));
    }
}
