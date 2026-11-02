// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Media.U3d;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>Checks that the pictures of textures are decoded from their continuation images, composed by channel, and refused with a reason when they cannot be used.</summary>
public sealed class U3dTextureTests
{
    /// <summary>The name of the texture.</summary>
    private const string Name = "Skin";

    /// <summary>The block type of a texture declaration.</summary>
    private const uint DeclarationType = 0xFFFFFF55;

    /// <summary>The block type of a texture continuation.</summary>
    private const uint ContinuationType = 0xFFFFFF5C;

    /// <summary>The side of the test pictures.</summary>
    private const uint Side = 2;

    /// <summary>The PNG compression type.</summary>
    private const byte Png = 2;

    /// <summary>The JPEG colour compression type.</summary>
    private const byte Jpeg = 1;

    /// <summary>The channel bits for red, green and blue.</summary>
    private const byte ColorChannels = 0x0E;

    /// <summary>The channel bit for alpha.</summary>
    private const byte AlphaChannel = 0x01;

    /// <summary>The channel bit for luminance.</summary>
    private const byte LuminanceChannel = 0x10;

    /// <summary>The channel bits for all of red, green, blue and alpha.</summary>
    private const byte AllChannels = 0x0F;

    /// <summary>The bytes of a pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The red of the first pixel.</summary>
    private const byte FirstRed = 200;

    /// <summary>The red of the second pixel of the colour picture.</summary>
    private const byte SecondRed = 30;

    /// <summary>The grey of the second pixel of the grey picture.</summary>
    private const byte SecondGrey = 90;

    /// <summary>The alpha of the first pixel of the alpha picture.</summary>
    private const byte FirstAlpha = 40;

    /// <summary>A count no block can hold.</summary>
    private const uint HugeCount = 4_000_000_000;

    /// <summary>A picture of 2 by 2 pixels with different colours and alpha.</summary>
    private static readonly byte[] Pixels = [FirstRed, 10, 20, 255, 30, 40, 50, 128, 60, 70, 80, 255, 90, 100, 110, 255];

    /// <summary>A PNG of red, green, blue and alpha pixels is decoded to the same bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodesAnRgbaPicture()
    {
        var texture = Decode(U3dPng.Rgba((int)Side, (int)Side, Pixels), AllChannels);

        await Assert.That(texture.IsDecoded).IsTrue();
        await Assert.That(texture.Rgba).IsEquivalentTo(Pixels);
        await Assert.That(texture.Width).IsEqualTo((int)Side);
    }

    /// <summary>A PNG of red, green and blue pixels fills those channels and leaves alpha opaque.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodesAnRgbPicture()
    {
        byte[] rgb = [FirstRed, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110];

        var texture = Decode(U3dPng.Rgb((int)Side, (int)Side, rgb), ColorChannels);

        await Assert.That(texture.Rgba[0]).IsEqualTo(FirstRed);
        await Assert.That(texture.Rgba[PixelBytes - 1]).IsEqualTo(byte.MaxValue);
        await Assert.That(texture.Rgba[PixelBytes]).IsEqualTo(SecondRed);
    }

    /// <summary>A grey picture marked as luminance fills red, green and blue.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodesALuminancePicture()
    {
        byte[] grey = [FirstRed, SecondGrey, 0, byte.MaxValue];

        var texture = Decode(U3dPng.Gray((int)Side, (int)Side, grey), LuminanceChannel);

        await Assert.That(texture.Rgba[PixelBytes]).IsEqualTo(SecondGrey);
        await Assert.That(texture.Rgba[PixelBytes + 1]).IsEqualTo(SecondGrey);
        await Assert.That(texture.Rgba[PixelBytes + (int)Side]).IsEqualTo(SecondGrey);
    }

    /// <summary>A black and white picture is decoded to black and white pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodesABilevelPicture()
    {
        byte[] rows = [0b0100_0000, 0b1000_0000];

        var texture = Decode(U3dPng.Bilevel((int)Side, (int)Side, rows), LuminanceChannel);

        await Assert.That(texture.Rgba[0]).IsEqualTo((byte)0);
        await Assert.That(texture.Rgba[PixelBytes]).IsEqualTo(byte.MaxValue);
        await Assert.That(texture.Rgba[(Side * PixelBytes)]).IsEqualTo(byte.MaxValue);
    }

    /// <summary>A colour image and a grey alpha image are composed into one picture.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ComposesColourAndAlphaImages()
    {
        byte[] alpha = [FirstAlpha, 0, 0, 0];
        var writer = new U3dFileWriter(false);
        var file = writer
            .Add(writer.TextureDeclaration(Name, Side, Side, new U3dImageSpec(Png, ColorChannels, false), new U3dImageSpec(Png, AlphaChannel, false)))
            .Add(writer.TextureContinuation(Name, 0, U3dPng.Rgba((int)Side, (int)Side, Pixels)))
            .Add(writer.TextureContinuation(Name, 1, U3dPng.Gray((int)Side, (int)Side, alpha)))
            .ToArray();

        var texture = U3dModelReader.Read(file).Textures.Single();

        await Assert.That(texture.Rgba[0]).IsEqualTo(FirstRed);
        await Assert.That(texture.Rgba[PixelBytes - 1]).IsEqualTo(FirstAlpha);
    }

    /// <summary>Pictures in another file, with no data, in JPEG form or damaged are refused with a reason and leave the texture without pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesPicturesItCannotUse()
    {
        var writer = new U3dFileWriter(false);
        var external = writer.Add(writer.TextureDeclaration(Name, Side, Side, new U3dImageSpec(Png, AllChannels, true))).ToArray();
        var empty = new U3dFileWriter(false);
        var noData = empty.Add(empty.TextureDeclaration(Name, Side, Side, Png, AllChannels)).ToArray();
        var jpeg = DeclaredWith(Jpeg, [byte.MaxValue, 0xD8, byte.MaxValue, 0xE0]);
        var damaged = DeclaredWith(Png, [0x89, 0x50, 0x4E, 0x47, 1, 1, 1]);

        foreach (var file in new[] { external, noData, jpeg, damaged })
        {
            var model = U3dModelReader.Read(file);

            await Assert.That(model.Textures.Single().IsDecoded).IsFalse();
            await Assert.That(model.Problems.Length).IsGreaterThan(0);
        }
    }

    /// <summary>A second picture of another size is left out, and a picture that is not the declared size is noted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotesPicturesOfOtherSizes()
    {
        byte[] one = [FirstRed, 1, 2, 3];
        var writer = new U3dFileWriter(false);
        var file = writer
            .Add(writer.TextureDeclaration(Name, Side, Side, new U3dImageSpec(Png, ColorChannels, false), new U3dImageSpec(Png, AlphaChannel, false)))
            .Add(writer.TextureContinuation(Name, 0, U3dPng.Rgba((int)Side, (int)Side, Pixels)))
            .Add(writer.TextureContinuation(Name, 1, U3dPng.Rgba(1, 1, one)))
            .ToArray();
        var declared = new U3dFileWriter(false);
        var resized = declared
            .Add(declared.TextureDeclaration(Name, Side + 1, Side + 1, Png, AllChannels))
            .Add(declared.TextureContinuation(Name, 0, U3dPng.Rgba((int)Side, (int)Side, Pixels)))
            .ToArray();

        var differ = U3dModelReader.Read(file);
        var resizedModel = U3dModelReader.Read(resized);

        await Assert.That(Array.Exists(differ.Problems, static p => p.Contains("differ in size", StringComparison.Ordinal))).IsTrue();
        await Assert.That(Array.Exists(resizedModel.Problems, static p => p.Contains("declaration gives", StringComparison.Ordinal))).IsTrue();
        await Assert.That(resizedModel.Textures.Single().Width).IsEqualTo((int)Side);
    }

    /// <summary>Continuations for textures or images the file does not declare, and declarations that are empty, too large or cut short, are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesDamagedBlocks()
    {
        var writer = new U3dFileWriter(false);
        var unknown = U3dFileWriter.Block(ContinuationType, new U3dValueWriter(true).String("Nobody").U32(0).ToArray());
        var badIndex = U3dFileWriter.Block(ContinuationType, new U3dValueWriter(true).String(Name).U32(Side).ToArray());
        var empty = U3dFileWriter.Block(DeclarationType, new U3dValueWriter(true).String("Empty").U32(0).U32(Side).U8(AllChannels).U32(0).ToArray());
        var many = U3dFileWriter.Block(DeclarationType, new U3dValueWriter(true).String("Many").U32(Side).U32(Side).U8(AllChannels).U32(HugeCount).ToArray());
        var cut = U3dFileWriter.Block(DeclarationType, new U3dValueWriter(true).String("Cut").ToArray());
        var cutImage = U3dFileWriter.Block(DeclarationType, new U3dValueWriter(true).String("CutImage").U32(Side).U32(Side).U8(AllChannels).U32(1).U8(Png).ToArray());
        var file = writer.Add(writer.TextureDeclaration(Name, Side, Side, Png, AllChannels)).Add(unknown).Add(badIndex).Add(empty).Add(many).Add(cut).Add(cutImage).ToArray();

        var model = U3dModelReader.Read(file);

        await Assert.That(Array.Exists(model.Problems, static p => p.Contains("does not declare", StringComparison.Ordinal))).IsTrue();
        await Assert.That(Array.Exists(model.Problems, static p => p.Contains("declaration is cut short", StringComparison.Ordinal))).IsTrue();
        await Assert.That(model.Textures.Length).IsEqualTo(1);
    }

    /// <summary>A declaration larger than the pixel limit, and picture data larger than the limit, are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StaysWithinThePixelLimit()
    {
        var writer = new U3dFileWriter(false);
        var picture = U3dPng.Rgba((int)Side, (int)Side, Pixels);
        var file = writer
            .Add(writer.TextureDeclaration(Name, Side, Side, Png, AllChannels))
            .Add(writer.TextureContinuation(Name, 0, picture))
            .ToArray();
        var tight = U3dLimits.Default with { MaxTexturePixels = 1 };
        var roomy = U3dLimits.Default with { MaxTexturePixels = (int)(Side * Side) };

        var declaration = U3dModelReader.Read(file, tight, CancellationToken.None);
        var data = U3dModelReader.Read(file, roomy, CancellationToken.None);

        await Assert.That(declaration.Textures).IsEmpty();
        await Assert.That(declaration.Problems.Length).IsGreaterThan(0);
        await Assert.That(data.Textures.Single().IsDecoded).IsFalse();
        await Assert.That(Array.Exists(data.Problems, static p => p.Contains("more picture data", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Decodes a one-picture texture.</summary>
    /// <param name="picture">The PNG file.</param>
    /// <param name="channels">The channel bits the continuation image supplies.</param>
    /// <returns>The texture.</returns>
    private static U3dTexture Decode(byte[] picture, byte channels)
    {
        var writer = new U3dFileWriter(false);
        var file = writer
            .Add(writer.TextureDeclaration(Name, Side, Side, Png, channels))
            .Add(writer.TextureContinuation(Name, 0, picture))
            .ToArray();
        return U3dModelReader.Read(file).Textures.Single();
    }

    /// <summary>Writes a texture whose only picture has the given compression and bytes.</summary>
    /// <param name="compression">The compression type.</param>
    /// <param name="bytes">The picture bytes.</param>
    /// <returns>The file.</returns>
    private static byte[] DeclaredWith(byte compression, byte[] bytes)
    {
        var writer = new U3dFileWriter(false);
        return writer
            .Add(writer.TextureDeclaration(Name, Side, Side, compression, AllChannels))
            .Add(writer.TextureContinuation(Name, 0, bytes))
            .ToArray();
    }
}
