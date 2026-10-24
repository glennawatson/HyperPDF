// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Tests;

/// <summary>Checks that H.265 pictures decode to the same samples as an independent decoder gave.</summary>
public sealed class H265DecodeTests
{
    /// <summary>Checks that every picture has the samples the independent decoder gave, by comparing a hash of each frame's three planes.</summary>
    /// <param name="name">The name of the H.265 file and of its list of frame hashes, without the extension.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("h265-intra")]
    [Arguments("h265-intra-ctu16")]
    [Arguments("h265-intra-ctu32")]
    [Arguments("h265-intra-dqp")]
    [Arguments("h265-intra-constrained")]
    [Arguments("h265-intra-scaling-list")]
    [Arguments("h265-intra-transform-skip")]
    [Arguments("h265-intra-wavefront")]
    [Arguments("h265-intra-low-quality")]
    [Arguments("h265-intra-high-quality")]
    [Arguments("h265-p")]
    [Arguments("h265-p-asymmetric")]
    [Arguments("h265-p-no-temporal-mvp")]
    [Arguments("h265-p-multiref")]
    [Arguments("h265-b")]
    [Arguments("h265-b-pyramid")]
    [Arguments("h265-b-long-term")]
    [Arguments("h265-b-weighted")]
    [Arguments("h265-lossless")]
    [Arguments("h265-cropped")]
    [Arguments("h265-ctu16")]
    [Arguments("h265-wavefront")]
    [Arguments("h265-transform-skip")]
    [Arguments("h265-scaling-list")]
    [Arguments("h265-dqp")]
    [Arguments("h265-loop-filters-ctu32")]
    [Arguments("h265-loop-filters-ctu16-b")]
    [Arguments("h265-sao-only")]
    [Arguments("h265-deblock-only")]
    [Arguments("h265-deblock-offsets-ctu16")]
    [Arguments("h265-deblock-negative-offsets-ctu16")]
    public Task Pictures_MatchTheReferenceSampleForSample(string name) => VideoFixtures.AssertPicturesMatchAsync(name);
}
