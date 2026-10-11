// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Tests for ICCBased colour spaces that apply matrix/TRC and gray profiles.</summary>
public sealed class IccProfileTests
{
    /// <summary>The tolerance for float colours.</summary>
    private const float Tolerance = 0.02F;

    /// <summary>The sRGB encoding of a linear one half.</summary>
    private const float EncodedHalf = 0.7354F;

    /// <summary>The largest byte difference allowed in row tests.</summary>
    private const int ByteTolerance = 2;

    /// <summary>The size of the profile header.</summary>
    private const int HeaderSize = 128;

    /// <summary>The bytes in a tag table entry.</summary>
    private const int TagEntrySize = 12;

    /// <summary>The scale of an s15Fixed16 number.</summary>
    private const float FixedScale = 65536F;

    /// <summary>The offset of the colour space signature in the header.</summary>
    private const int ColorSpaceOffset = 16;

    /// <summary>The offset of the connection space signature in the header.</summary>
    private const int ConnectionSpaceOffset = 20;

    /// <summary>The offset of the file signature in the header.</summary>
    private const int FileSignatureOffset = 36;

    /// <summary>The bytes in an XYZ tag.</summary>
    private const int XyzTagSize = 20;

    /// <summary>The bytes before the data of a curve tag.</summary>
    private const int CurveHeader = 12;

    /// <summary>The alignment of tag data.</summary>
    private const int TagAlignment = 4;

    /// <summary>The bytes of a 32-bit number.</summary>
    private const int Number32Size = 4;

    /// <summary>The bytes of a type signature plus its reserved bytes, before the tag's content.</summary>
    private const int TypeHeaderSize = 8;

    /// <summary>The index in the colorant table of the red colorant.</summary>
    private const int RedColorant = 0;

    /// <summary>The index in the colorant table of the green colorant.</summary>
    private const int GreenColorant = 3;

    /// <summary>The index in the colorant table of the blue colorant.</summary>
    private const int BlueColorant = 6;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The components of RGB.</summary>
    private const int RgbComponents = 3;

    /// <summary>The u8Fixed8 value 1.0, a linear gamma.</summary>
    private const ushort LinearGamma = 0x0100;

    /// <summary>The u8Fixed8 value of about 2.2.</summary>
    private const ushort Gamma22 = 0x0233;

    /// <summary>The mid sample.</summary>
    private const byte MidSample = 0x80;

    /// <summary>The byte scale.</summary>
    private const float ByteScale = 255F;

    /// <summary>The parametric function type with a linear segment below d.</summary>
    private const int ParametricLinearType = 3;

    /// <summary>The sRGB decode curve exponent.</summary>
    private const float SrgbGamma = 2.4F;

    /// <summary>The sRGB decode curve parameter a.</summary>
    private const float SrgbA = 1F / 1.055F;

    /// <summary>The sRGB decode curve parameter b.</summary>
    private const float SrgbB = 0.055F / 1.055F;

    /// <summary>The sRGB decode curve parameter c.</summary>
    private const float SrgbC = 1F / 12.92F;

    /// <summary>The sRGB decode curve parameter d.</summary>
    private const float SrgbD = 0.04045F;

    /// <summary>The D50 colorants of the sRGB primaries: red, green and blue, each X, Y, Z.</summary>
    private static readonly float[] SrgbColorants = [0.4360747F, 0.2225045F, 0.0139322F, 0.3850649F, 0.7168786F, 0.0971045F, 0.1430804F, 0.0606169F, 0.7141733F];

    /// <summary>An sRGB profile uses the device RGB conversion: the row matches DeviceRGB.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SrgbProfileConvertsLikeDeviceRgb()
    {
        var curve = ParametricCurve(SrgbGamma, SrgbA, SrgbB, SrgbC, SrgbD);
        var space = IccSpace(Rgb(curve, curve, curve), RgbComponents);
        byte[] samples = [0x10, 0x80, 0xF0, 0x00, 0x30, 0xFF];
        var icc = new byte[samples.Length / RgbComponents * BytesPerPixel];
        var device = new byte[icc.Length];
        space.ConvertRow(samples, icc, samples.Length / RgbComponents);
        PdfColorSpace.DeviceRgb.ConvertRow(samples, device, samples.Length / RgbComponents);

        await Assert.That(space.Kind).IsEqualTo(PdfColorSpaceKind.IccBased);
        await Assert.That(icc).IsEquivalentTo(device);
    }

    /// <summary>A linear-gamma matrix profile brightens mid gray to the sRGB encoding of one half.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinearMatrixProfileIsEncodedToSrgb()
    {
        var curve = GammaCurve(LinearGamma);
        var space = IccSpace(Rgb(curve, curve, curve), RgbComponents);
        var bgra = new byte[BytesPerPixel];
        space.ConvertRow([MidSample, MidSample, MidSample], bgra, 1);
        var rgb = new float[RgbComponents];
        space.ToRgb([MidSample / ByteScale, MidSample / ByteScale, MidSample / ByteScale], rgb);

        await Assert.That(rgb[0]).IsEqualTo(EncodedHalf).Within(Tolerance);
        await Assert.That(Math.Abs(bgra[0] - (EncodedHalf * ByteScale))).IsLessThanOrEqualTo(ByteTolerance);
        await Assert.That(Math.Abs(bgra[1] - (EncodedHalf * ByteScale))).IsLessThanOrEqualTo(ByteTolerance);
        await Assert.That(Math.Abs(bgra[2] - (EncodedHalf * ByteScale))).IsLessThanOrEqualTo(ByteTolerance);
    }

    /// <summary>A gray profile with a gamma curve is applied to gray samples.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GrayProfileAppliesCurve()
    {
        var space = IccSpace(Gray(GammaCurve(Gamma22)), 1);
        var bgra = new byte[BytesPerPixel];
        space.ConvertRow([MidSample], bgra, 1);
        var rgb = new float[RgbComponents];
        space.ToRgb([MidSample / ByteScale], rgb);

        // 0.502 ^ 2.2 is 0.219 in linear light, which sRGB encodes to about 0.505.
        await Assert.That(rgb[1]).IsEqualTo(MidSample / ByteScale).Within(Tolerance);
        await Assert.That(Math.Abs(bgra[1] - MidSample)).IsLessThanOrEqualTo(ByteTolerance);
    }

    /// <summary>A profile that cannot be read falls back to the alternate by component count.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnreadableProfileFallsBackToAlternate()
    {
        var space = IccSpace([0x00, 0x01, 0x02], RgbComponents);
        var rgb = new float[RgbComponents];
        space.ToRgb([1F, 0F, 0F], rgb);

        await Assert.That(space.Components).IsEqualTo(RgbComponents);
        await Assert.That(rgb[0]).IsEqualTo(1F).Within(Tolerance);
    }

    /// <summary>A profile whose colour space does not match /N is ignored.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProfileWithOtherComponentCountIsIgnored()
    {
        var space = IccSpace(Gray(GammaCurve(Gamma22)), RgbComponents);

        await Assert.That(space.Components).IsEqualTo(RgbComponents);
    }

    /// <summary>Documents with one drawing service share a transform; a separate service keeps its own.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProfileCachesKeepIndependentTransformInstances()
    {
        var curve = GammaCurve(LinearGamma);
        var pdf = new RenderTestPdf(1, 1);
        var profileId = pdf.AddStream("/N 3", Rgb(curve, curve, curve));
        pdf.CatalogEntries = string.Create(CultureInfo.InvariantCulture, $"/Metadata {profileId} 0 R");
        var bytes = pdf.ToBytes();
        var shared = new PdfDrawingServices(new());
        using var firstDocument = PdfDocumentReader.OpenWith(bytes, new PdfOpenOptions { DrawingServices = shared });
        using var duplicateDocument = PdfDocumentReader.OpenWith(bytes, new PdfOpenOptions { DrawingServices = shared });
        using var separateDocument = PdfDocumentReader.OpenWith(bytes, new PdfOpenOptions { DrawingServices = new(new()) });

        var first = IccProfile.Get(firstDocument.Catalog.GetStream(KnownName.Metadata)!, RgbComponents);
        var duplicate = IccProfile.Get(duplicateDocument.Catalog.GetStream(KnownName.Metadata)!, RgbComponents);
        var isolated = IccProfile.Get(separateDocument.Catalog.GetStream(KnownName.Metadata)!, RgbComponents);

        await Assert.That(first).IsNotNull();
        await Assert.That(ReferenceEquals(first, duplicate)).IsTrue();
        await Assert.That(ReferenceEquals(first, isolated)).IsFalse();
    }

    /// <summary>Creates an ICCBased colour space from profile bytes.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="components">The /N value.</param>
    /// <returns>The space.</returns>
    private static PdfColorSpace IccSpace(byte[] profile, int components)
    {
        var stream = new PdfStream(FixTestHelpers.Dictionary(KnownName.N, PdfValue.FromInteger(components)), profile);
        return PdfColorSpace.Parse(FixTestHelpers.Array(FixTestHelpers.Name(KnownName.ICCBased), PdfValue.FromStream(stream)), null);
    }

    /// <summary>Builds an RGB matrix profile with the sRGB colorants.</summary>
    /// <param name="red">The red curve tag.</param>
    /// <param name="green">The green curve tag.</param>
    /// <param name="blue">The blue curve tag.</param>
    /// <returns>The profile.</returns>
    private static byte[] Rgb(byte[] red, byte[] green, byte[] blue) => Profile(
        "RGB ",
        [
            new("rXYZ", Xyz(RedColorant)),
            new("gXYZ", Xyz(GreenColorant)),
            new("bXYZ", Xyz(BlueColorant)),
            new("rTRC", red),
            new("gTRC", green),
            new("bTRC", blue),
        ]);

    /// <summary>Builds a gray profile.</summary>
    /// <param name="curve">The gray curve tag.</param>
    /// <returns>The profile.</returns>
    private static byte[] Gray(byte[] curve) => Profile("GRAY", [new("kTRC", curve)]);

    /// <summary>Builds a profile from tags.</summary>
    /// <param name="colorSpace">The data colour space signature.</param>
    /// <param name="tags">The tags: signature and data.</param>
    /// <returns>The profile bytes.</returns>
    private static byte[] Profile(string colorSpace, Tag[] tags)
    {
        var tableEnd = HeaderSize + Number32Size + (tags.Length * TagEntrySize);
        var offset = tableEnd;
        var total = tableEnd;
        foreach (var tag in tags)
        {
            total += Aligned(tag.Data.Length);
        }

        var profile = new byte[total];
        BinaryPrimitives.WriteUInt32BigEndian(profile, (uint)total);
        Encoding.ASCII.GetBytes(colorSpace).CopyTo(profile, ColorSpaceOffset);
        "XYZ "u8.CopyTo(profile.AsSpan(ConnectionSpaceOffset));
        "acsp"u8.CopyTo(profile.AsSpan(FileSignatureOffset));
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(HeaderSize), (uint)tags.Length);
        for (var i = 0; i < tags.Length; i++)
        {
            var entry = HeaderSize + Number32Size + (i * TagEntrySize);
            Encoding.ASCII.GetBytes(tags[i].Signature).CopyTo(profile, entry);
            BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(entry + Number32Size), (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(entry + TypeHeaderSize), (uint)tags[i].Data.Length);
            tags[i].Data.CopyTo(profile, offset);
            offset += Aligned(tags[i].Data.Length);
        }

        return profile;
    }

    /// <summary>Rounds a length up to the tag alignment.</summary>
    /// <param name="length">The length.</param>
    /// <returns>The aligned length.</returns>
    private static int Aligned(int length) => (length + TagAlignment - 1) / TagAlignment * TagAlignment;

    /// <summary>Builds an XYZ tag from three colorant numbers.</summary>
    /// <param name="first">The index of the colorant's X in <see cref="SrgbColorants"/>.</param>
    /// <returns>The tag data.</returns>
    private static byte[] Xyz(int first)
    {
        var tag = new byte[XyzTagSize];
        "XYZ "u8.CopyTo(tag);
        for (var i = 0; i < RgbComponents; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(tag.AsSpan(TypeHeaderSize + (i * Number32Size)), (int)(SrgbColorants[first + i] * FixedScale));
        }

        return tag;
    }

    /// <summary>Builds a 'curv' tag holding one gamma.</summary>
    /// <param name="gamma">The u8Fixed8 gamma.</param>
    /// <returns>The tag data.</returns>
    private static byte[] GammaCurve(ushort gamma)
    {
        var tag = new byte[CurveHeader + sizeof(ushort)];
        "curv"u8.CopyTo(tag);
        BinaryPrimitives.WriteUInt32BigEndian(tag.AsSpan(TypeHeaderSize), 1);
        BinaryPrimitives.WriteUInt16BigEndian(tag.AsSpan(CurveHeader), gamma);
        return tag;
    }

    /// <summary>Builds a type 3 'para' tag.</summary>
    /// <param name="g">The exponent.</param>
    /// <param name="a">The scale.</param>
    /// <param name="b">The offset.</param>
    /// <param name="c">The slope of the linear segment.</param>
    /// <param name="d">The break point.</param>
    /// <returns>The tag data.</returns>
    private static byte[] ParametricCurve(float g, float a, float b, float c, float d)
    {
        float[] values = [g, a, b, c, d];
        var tag = new byte[CurveHeader + (values.Length * Number32Size)];
        "para"u8.CopyTo(tag);
        BinaryPrimitives.WriteUInt16BigEndian(tag.AsSpan(TypeHeaderSize), ParametricLinearType);
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(tag.AsSpan(CurveHeader + (i * Number32Size)), (int)MathF.Round(values[i] * FixedScale));
        }

        return tag;
    }

    /// <summary>A profile tag.</summary>
    /// <param name="Signature">The four-character signature.</param>
    /// <param name="Data">The tag data.</param>
    private readonly record struct Tag(string Signature, byte[] Data);
}
