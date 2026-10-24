// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Tests;

/// <summary>Checks that H.264 pictures decode to the same samples as an independent decoder gave.</summary>
public sealed class H264DecodeTests
{
    /// <summary>Checks that every picture has the samples the independent decoder gave, by comparing a hash of each frame's three planes.</summary>
    /// <param name="name">The name of the H.264 file and of its list of frame hashes, without the extension.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("h264-intra-baseline")]
    [Arguments("h264-intra-cabac")]
    [Arguments("h264-p-cavlc-multiref")]
    [Arguments("h264-p-cavlc-partitions")]
    [Arguments("h264-deblock-intra")]
    [Arguments("h264-deblock-p")]
    [Arguments("h264-deblock-offsets")]
    [Arguments("h264-p-cabac")]
    [Arguments("h264-p-cabac-weighted")]
    [Arguments("h264-p-cabac-fade")]
    [Arguments("h264-high-intra-cavlc")]
    [Arguments("h264-high-intra-cabac")]
    [Arguments("h264-high-p-cavlc")]
    [Arguments("h264-high-p-cabac")]
    [Arguments("h264-high-p-cabac-mandelbrot")]
    [Arguments("h264-b-cavlc-spatial")]
    [Arguments("h264-b-cavlc-temporal")]
    [Arguments("h264-b-cavlc-deblock")]
    [Arguments("h264-b-cavlc-implicit")]
    [Arguments("h264-b-cavlc-pyramid")]
    [Arguments("h264-b-cavlc-high")]
    [Arguments("h264-b-cabac-spatial")]
    [Arguments("h264-b-cabac-deblock")]
    [Arguments("h264-b-cabac-high")]
    [Arguments("h264-b-cabac-default")]
    public Task Pictures_MatchTheReferenceSampleForSample(string name) => VideoFixtures.AssertPicturesMatchAsync(name);
}
