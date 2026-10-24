// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Tests;

/// <summary>Checks that decoders give the same pictures when the caller hands each one back for reuse.</summary>
public sealed class RecycledPictureTests
{
    /// <summary>The pictures match the reference sample for sample when every picture is recycled after it is read.</summary>
    /// <param name="name">The fixture name.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("h264-intra-baseline")]
    [Arguments("h264-p-cabac")]
    [Arguments("h264-b-cabac-deblock")]
    [Arguments("h264-b-cavlc-pyramid")]
    [Arguments("h265-intra")]
    [Arguments("h265-p")]
    [Arguments("h265-b")]
    [Arguments("h265-b-pyramid")]
    [Arguments("h265-cropped")]
    public Task RecycledPicturesMatchTheReference(string name) => VideoFixtures.AssertRecycledPicturesMatchAsync(name);
}
