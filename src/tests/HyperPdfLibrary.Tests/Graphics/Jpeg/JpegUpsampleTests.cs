// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jpeg;
using HyperPdfLibrary.Tests.Filters;

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>Tests that <see cref="JpegUpsample"/> writes each sample twice, in order, and stops inside the destination.</summary>
public sealed class JpegUpsampleTests
{
    /// <summary>The seed of the random samples.</summary>
    private const uint Seed = 5_150;

    /// <summary>The factor by which a row widens.</summary>
    private const int Doubling = 2;

    /// <summary>The widths tried, around the sixteen samples a step reads.</summary>
    private static readonly int[] Widths = [1, 2, 15, 16, 17, 31, 32, 33, 100, 1_001];

    /// <summary>The samples written by the steps equal the first samples of a plain repeat, and no byte past them is touched.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Double_RandomRows_RepeatsEachSample()
    {
        var random = new SampleRandom(Seed);
        foreach (var width in Widths)
        {
            var source = random.NextBytes((width + 1) / Doubling);
            var destination = new byte[width];
            Array.Fill(destination, byte.MaxValue);

            var written = JpegUpsample.Double(source, destination);

            await Assert.That(written).IsLessThanOrEqualTo(width);
            for (var i = 0; i < written; i++)
            {
                await Assert.That(destination[i]).IsEqualTo(source[i / Doubling]).Because($"width {width}, sample {i}");
            }

            for (var i = written; i < width; i++)
            {
                await Assert.That(destination[i]).IsEqualTo(byte.MaxValue).Because($"width {width}, untouched {i}");
            }
        }
    }
}
