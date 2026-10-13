// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpx;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// The pixels of the 5/3 and 9/7 fixtures at every reduced resolution, recorded from the decoder that rebuilt each
/// component as whole planes. The row-by-row decoder must give the same bytes.
/// </summary>
public sealed class JpxFixtureReductionTests
{
    /// <summary>The bits of a sample.</summary>
    private const int SampleBits = 8;

    /// <summary>Decodes a fixture at a reduced resolution and checks its size and the hash of its pixels.</summary>
    /// <param name="reversible">Whether to use the 5/3 fixture; otherwise the 9/7 fixture.</param>
    /// <param name="reduction">The finest levels left out.</param>
    /// <param name="width">The expected width.</param>
    /// <param name="height">The expected height.</param>
    /// <param name="hash">The expected SHA-256 of the BGRA pixels.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true, 0, 1816, 2983, "2464A348B9C1326555B8E72BE8B5B115DB02CEE2DB04B78A6CE720877C6C7B32")]
    [Arguments(true, 1, 908, 1492, "3B15575CD753AB969DA71688DD794F9503697D4347B1283B1EC1E27B510BB0D0")]
    [Arguments(true, 2, 454, 746, "043642B6D0968388AB98039D4D76D90C1ED0881BE811843CF0C016858602659F")]
    [Arguments(true, 3, 227, 373, "9C0287DAA12CB2302243BB0AD24AF85C1C3601D52492B1D1E6E099CA72063961")]
    [Arguments(false, 0, 610, 999, "31322614A13419582DFA5E9680CAE2DFF48CEB1366B7238823F641C36CF7A77E")]
    [Arguments(false, 1, 305, 500, "684BCB955F8ACDBBA0A3DAB523AE304E95DEE169EDB7580010A07198A15DB6F9")]
    [Arguments(false, 2, 153, 250, "AE05552C2AA5B5F38386D14ECE4E6FF0EBDCF813BD43F78B41F42C9B04AC2D0D")]
    [Arguments(false, 3, 77, 125, "A4FFB218E3CF67C6CCD5BEEB4F7D1FB3EDB23F616E6D7EF73CCD4990F0B945D8")]
    public async Task FixturePixelsAreUnchanged(bool reversible, int reduction, int width, int height, string hash)
    {
        var fixture = reversible ? JpxFixtures.Reversible : JpxFixtures.Irreversible;
        var fullWidth = reversible ? JpxFixtures.ReversibleWidth : JpxFixtures.IrreversibleWidth;
        var fullHeight = reversible ? JpxFixtures.ReversibleHeight : JpxFixtures.IrreversibleHeight;
        var header = new ImageHeader(fullWidth, fullHeight, SampleBits, false, false, PdfColorSpace.DeviceRgb, null, null);

        var image = JpxImageDecoder.Decode(header, Convert.FromBase64String(fixture), 0, reduction);

        await Assert.That(image).IsNotNull();
        await Assert.That(image!.Width).IsEqualTo(width);
        await Assert.That(image.Height).IsEqualTo(height);
        await Assert.That(Convert.ToHexString(SHA256.HashData(image.Pixels))).IsEqualTo(hash);
    }
}
