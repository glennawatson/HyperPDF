// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Tests for image budgets, resource colour spaces, soft-mask matte and mask resampling.</summary>
public sealed class ImageFixTests
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The index of the alpha byte in a BGRA pixel.</summary>
    private const int AlphaByte = 3;

    /// <summary>The index of the red byte in a BGRA pixel.</summary>
    private const int RedByte = 2;

    /// <summary>The side of the larger soft mask.</summary>
    private const int MaskSide = 2;

    /// <summary>The bits per component of the test images.</summary>
    private const int EightBits = 8;

    /// <summary>A side whose square needs 576 MB of pixels, over the 512 MB budget but under the old pixel cap.</summary>
    private const int OverBudgetSide = 12_000;

    /// <summary>A side whose image data a one-byte stream cannot come close to filling.</summary>
    private const int DeclaredSide = 3000;

    /// <summary>The blended value of the matte test pixel.</summary>
    private const byte Blended = 0xC8;

    /// <summary>The soft-mask alpha of the matte test.</summary>
    private const byte HalfAlpha = 0x80;

    /// <summary>The blue value the matte test expects after the matte is removed and the colour is premultiplied.</summary>
    private const int MatteRemoved = 73;

    /// <summary>The blue value the matte test expects when no matte is removed.</summary>
    private const int NoMatte = 100;

    /// <summary>The largest byte difference allowed.</summary>
    private const int Tolerance = 1;

    /// <summary>The average of the shrunk plane.</summary>
    private const byte Average = 100;

    /// <summary>The value of the enlarged plane.</summary>
    private const byte Constant = 50;

    /// <summary>A named colour space in the resources decodes with those resources and not without them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamedColorSpaceResolvesThroughResources()
    {
        var names = new PdfNameTable();
        var name = names.Intern("CS0");
        var spaces = new PdfDictionary(null);
        spaces.Add(name, FixTestHelpers.Array(
            FixTestHelpers.Name(KnownName.Indexed),
            FixTestHelpers.Name(KnownName.DeviceRGB),
            PdfValue.FromInteger(1),
            PdfValue.FromString([0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00])));
        var resources = FixTestHelpers.Dictionary(KnownName.ColorSpace, PdfValue.FromDictionary(spaces));
        var stream = Image(1, 1, PdfValue.FromName(name), [0x01]);

        var with = PdfImageDecoder.Decode(stream, resources)!;
        var without = PdfImageDecoder.Decode(stream)!;

        await Assert.That(with.Pixels).IsEquivalentTo(FixTestHelpers.Bgra(0x00, 0xFF, 0x00));
        await Assert.That(without.Pixels[RedByte]).IsEqualTo((byte)1);
    }

    /// <summary>An image whose pixels need more than the byte budget is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImageOverByteBudgetIsRefused()
    {
        var image = PdfImageDecoder.Decode(Image(OverBudgetSide, OverBudgetSide, FixTestHelpers.Name(KnownName.DeviceGray), []));

        await Assert.That(image).IsNull();
    }

    /// <summary>A tiny stream that declares a large image is refused, but slightly short data still decodes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TinyStreamDeclaringLargeImageIsRefused()
    {
        var tiny = PdfImageDecoder.Decode(Image(DeclaredSide, DeclaredSide, FixTestHelpers.Name(KnownName.DeviceGray), [0xFF]));
        var padded = PdfImageDecoder.Decode(Image(MaskSide, MaskSide, FixTestHelpers.Name(KnownName.DeviceGray), [0xFF]));

        await Assert.That(tiny).IsNull();
        await Assert.That(padded).IsNotNull();
    }

    /// <summary>A soft mask's /Matte is removed from the colour before the colour is premultiplied.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SoftMaskMatteIsRemoved()
    {
        using var store = FixTestHelpers.Store();
        var mask = MaskStream(store, 1, 1, [HalfAlpha]);
        mask.Dictionary.Add(store.Names.Intern("Matte"), FixTestHelpers.Numbers(1, 1, 1));
        var plain = MaskStream(store, 1, 1, [HalfAlpha]);

        var withMatte = PdfImageDecoder.Decode(WithSoftMask(mask))!;
        var without = PdfImageDecoder.Decode(WithSoftMask(plain))!;

        await Assert.That(Math.Abs(withMatte.Pixels[0] - MatteRemoved)).IsLessThanOrEqualTo(Tolerance);
        await Assert.That(withMatte.Pixels[AlphaByte]).IsEqualTo(HalfAlpha);
        await Assert.That(Math.Abs(without.Pixels[0] - NoMatte)).IsLessThanOrEqualTo(Tolerance);
    }

    /// <summary>A mask larger than the image enlarges the image to the mask's size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LargerMaskEnlargesImage()
    {
        using var store = FixTestHelpers.Store();
        byte[] alphas = [0xFF, 0x80, 0x40, 0x00];
        var mask = MaskStream(store, MaskSide, MaskSide, alphas);
        var stream = Image(1, 1, FixTestHelpers.Name(KnownName.DeviceGray), [0xFF]);
        stream.Dictionary.Add(KnownName.SMask, PdfValue.FromStream(mask));
        var image = PdfImageDecoder.Decode(stream)!;

        await Assert.That(image.Width).IsEqualTo(MaskSide);
        await Assert.That(image.Height).IsEqualTo(MaskSide);
        for (var i = 0; i < alphas.Length; i++)
        {
            await Assert.That(image.Pixels[(i * BytesPerPixel) + AlphaByte]).IsEqualTo(alphas[i]);
        }
    }

    /// <summary>A plane that shrinks is averaged and a plane that grows reads the pixel under each centre.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlaneResamplingAveragesAndEnlarges()
    {
        var shrunk = new byte[1];
        ImageAlpha.ResamplePlane([0x00, 0x64, 0x64, 0xC8], MaskSide, MaskSide, shrunk, 1, 1);
        var grown = new byte[MaskSide * MaskSide];
        ImageAlpha.ResamplePlane([Constant], 1, 1, grown, MaskSide, MaskSide);
        var expected = new byte[grown.Length];
        expected.AsSpan().Fill(Constant);

        await Assert.That(shrunk[0]).IsEqualTo(Average);
        await Assert.That(grown).IsEquivalentTo(expected);
    }

    /// <summary>Creates an RGB image with a 3-byte gray pixel plus a soft mask.</summary>
    /// <param name="mask">The soft mask stream.</param>
    /// <returns>The image stream.</returns>
    private static PdfStream WithSoftMask(PdfStream mask)
    {
        var stream = Image(1, 1, FixTestHelpers.Name(KnownName.DeviceRGB), [Blended, Blended, Blended]);
        stream.Dictionary.Add(KnownName.SMask, PdfValue.FromStream(mask));
        return stream;
    }

    /// <summary>Creates a gray soft-mask stream owned by a document.</summary>
    /// <param name="owner">The document.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="data">The data.</param>
    /// <returns>The stream.</returns>
    private static PdfStream MaskStream(PdfObjectStore owner, int width, int height, byte[] data)
    {
        var dictionary = new PdfDictionary(owner);
        dictionary.Add(KnownName.Width, PdfValue.FromInteger(width));
        dictionary.Add(KnownName.Height, PdfValue.FromInteger(height));
        dictionary.Add(KnownName.BitsPerComponent, PdfValue.FromInteger(EightBits));
        dictionary.Add(KnownName.ColorSpace, FixTestHelpers.Name(KnownName.DeviceGray));
        return new(dictionary, data);
    }

    /// <summary>Creates an 8-bit image XObject stream.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="colorSpace">The colour space.</param>
    /// <param name="data">The data.</param>
    /// <returns>The stream.</returns>
    private static PdfStream Image(int width, int height, PdfValue colorSpace, byte[] data)
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.Width, PdfValue.FromInteger(width));
        dictionary.Add(KnownName.Height, PdfValue.FromInteger(height));
        dictionary.Add(KnownName.BitsPerComponent, PdfValue.FromInteger(EightBits));
        dictionary.Add(KnownName.ColorSpace, colorSpace);
        return new(dictionary, data);
    }
}
