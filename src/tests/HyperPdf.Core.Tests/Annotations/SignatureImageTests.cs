// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;

namespace HyperPdf.Core.Tests.Annotations;

/// <summary>Preparing imported signatures preserves ink while trimming empty margins.</summary>
public sealed class SignatureImageTests
{
    /// <summary>The source image width and height.</summary>
    private const int Size = 3;

    /// <summary>The channels in BGRA.</summary>
    private const int Channels = 4;

    /// <summary>The middle pixel's offset.</summary>
    private const int CentreOffset = 16;

    /// <summary>The prepared centre pixel.</summary>
    private static readonly byte[] SoftInk = [0, 0, 0, 127];

    /// <summary>Paper removal crops empty margins without discarding translucent ink.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovesPaperAndTrimsMargins()
    {
        const byte grey = 128;
        const byte opaque = 255;
        var source = new byte[Size * Size * Channels];
        source.AsSpan().Fill(opaque);
        source.AsSpan(CentreOffset, Channels - 1).Fill(grey);
        var image = await Assert.That(SignatureImage.Create(source, Size, Size, true)).IsNotNull();
        await Assert.That(image.Width).IsEqualTo(1);
        await Assert.That(image.Height).IsEqualTo(1);
        await Assert.That(image.Pixels.Span.SequenceEqual(SoftInk)).IsTrue();
        await Assert.That(source[CentreOffset]).IsEqualTo(grey);
    }

    /// <summary>A fully transparent image has no signature to place.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsEmptyInk()
    {
        var source = new byte[Size * Size * Channels];
        await Assert.That(SignatureImage.Create(source, Size, Size, false)).IsNull();
    }

    /// <summary>Translucent colour survives cropping without changing the source.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreservesColouredSoftInk()
    {
        byte[] ink = [255, 0, 0, 1];
        var source = new byte[Size * Size * Channels];
        ink.CopyTo(source, CentreOffset);
        var image = await Assert.That(SignatureImage.Create(source, Size, Size, false)).IsNotNull();
        source.AsSpan().Clear();
        await Assert.That(image.Width).IsEqualTo(1);
        await Assert.That(image.Height).IsEqualTo(1);
        await Assert.That(image.Pixels.Span.SequenceEqual(ink)).IsTrue();
    }

    /// <summary>An uncropped image owns its pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopiesUncroppedImage()
    {
        var source = SoftInk.ToArray();
        var image = await Assert.That(SignatureImage.Create(source, 1, 1, false)).IsNotNull();
        source.AsSpan().Clear();
        await Assert.That(image.Pixels.Span.SequenceEqual(SoftInk)).IsTrue();
    }

    /// <summary>Pure white paper contains no ink after removal.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsWhitePaper()
    {
        byte[] source = [255, 255, 255, 255];
        await Assert.That(SignatureImage.Create(source, 1, 1, true)).IsNull();
    }

    /// <summary>Incomplete pixel buffers are rejected.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsMismatchedDimensions() =>
        await Assert.That(static () => SignatureImage.Create(SoftInk, Size, Size, false)).Throws<ArgumentException>();
}
