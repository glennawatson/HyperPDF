// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpx;
using HyperPdfLibrary.Tests.Graphics.Jpeg;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// The JP2 and JPX file boxes (ISO 15444-1 annex I and ISO 15444-2 annex M with corrigendum 4): what the encoder writes for
/// CMYK and ICC images, and how the decoder reads the compositing layer boxes, the Opacity box, CIELab and a fragment table.
/// </summary>
public sealed class JpxFileBoxTests
{
    /// <summary>The side of the small test images.</summary>
    private const int Side = 8;

    /// <summary>The components of an RGB image.</summary>
    private const int Rgb = 3;

    /// <summary>The components of a CMYK image.</summary>
    private const int Cmyk = 4;

    /// <summary>The bits of a sample.</summary>
    private const int Eight = 8;

    /// <summary>The bytes of a four-character code, and of a 32-bit field.</summary>
    private const int Code = 4;

    /// <summary>The bytes of a 16-bit field.</summary>
    private const int Short = 2;

    /// <summary>The bytes of a box header.</summary>
    private const int BoxHeader = 8;

    /// <summary>The offset of the file type box: after the 12-byte signature box.</summary>
    private const int FileTypeOffset = 12;

    /// <summary>The offset of the brand in the file type box.</summary>
    private const int BrandOffset = FileTypeOffset + BoxHeader;

    /// <summary>The offset of the minor version in the file type box.</summary>
    private const int MinorVersionOffset = BrandOffset + Code;

    /// <summary>The offset of the first compatibility entry in the file type box.</summary>
    private const int CompatibilityOffset = MinorVersionOffset + Code;

    /// <summary>The length of a file type box with one compatibility entry.</summary>
    private const int ShortFileTypeLength = BoxHeader + (3 * Code);

    /// <summary>The bytes of the Reader Requirements box before its feature list: the mask length and the two masks.</summary>
    private const int MaskBytes = 3;

    /// <summary>The bytes of one feature entry: a flag and a one-byte mask.</summary>
    private const int FeatureBytes = Short + 1;

    /// <summary>The offset of the first feature in the Reader Requirements box contents.</summary>
    private const int FirstFeatureOffset = MaskBytes + Short;

    /// <summary>The colour method of an enumerated space.</summary>
    private const byte EnumeratedMethod = 1;

    /// <summary>The colour method of a restricted ICC profile.</summary>
    private const byte RestrictedMethod = 2;

    /// <summary>The colour method of any ICC profile.</summary>
    private const byte AnyIccMethod = 3;

    /// <summary>The APPROX value of an accurate description.</summary>
    private const byte Accurate = 1;

    /// <summary>The standard feature flag of any ICC profile.</summary>
    private const ushort AnyIccFlag = 44;

    /// <summary>The standard feature flag of non-premultiplied opacity.</summary>
    private const ushort OpacityFlag = 9;

    /// <summary>The mask of three requirements.</summary>
    private const byte ThreeRequirements = 7;

    /// <summary>The CMYK enumerated colour space.</summary>
    private const int CmykSpace = 12;

    /// <summary>The CIELab enumerated colour space.</summary>
    private const int LabSpace = 14;

    /// <summary>The sRGB enumerated colour space.</summary>
    private const int SrgbSpace = 16;

    /// <summary>The code value of a mid-grey L* of 50 in default CIELab.</summary>
    private const int MidLightness = 128;

    /// <summary>The default a* code of zero.</summary>
    private const int NeutralA = 128;

    /// <summary>The default b* code of zero.</summary>
    private const int NeutralB = 96;

    /// <summary>The sRGB value of L* 50.</summary>
    private const int MidGray = 119;

    /// <summary>The allowed difference from the expected grey.</summary>
    private const int GrayTolerance = 6;

    /// <summary>The highest sample.</summary>
    private const int MaxSample = 255;

    /// <summary>The bytes of an ICC profile header and an empty tag table.</summary>
    private const int ProfileBytes = 132;

    /// <summary>The bytes of a profile with one tag table entry.</summary>
    private const int TaggedProfileBytes = ProfileBytes + 12;

    /// <summary>The offset of the profile class.</summary>
    private const int ClassOffset = 12;

    /// <summary>The offset of the data colour space.</summary>
    private const int SpaceOffset = 16;

    /// <summary>The offset of the connection space.</summary>
    private const int ConnectionOffset = 20;

    /// <summary>The offset of the tag count.</summary>
    private const int TagCountOffset = 128;

    /// <summary>The bytes of a fragment list with one entry: the count and the offset, length and data reference.</summary>
    private const int FragmentListBytes = Short + 14;

    /// <summary>The offset of the length in the one entry of a fragment list.</summary>
    private const int FragmentLengthOffset = Short + 8;

    /// <summary>The offset of the data reference in the one entry of a fragment list.</summary>
    private const int FragmentReferenceOffset = Short + 12;

    /// <summary>The seed of the generated pixels.</summary>
    private const uint Seed = 9;

    /// <summary>The EP field of the default CIELab encoding: ranges 100, 170 and 200, offsets 0, 128 and 96, and illuminant D50.</summary>
    private static readonly byte[] DefaultLabParameters =
    [
        0, 0, 0, 0x64, 0, 0, 0, 0, 0, 0, 0, 0xAA, 0, 0, 0, 0x80, 0, 0, 0, 0xC8, 0, 0, 0, 0x60, 0, 0x44, 0x35, 0x30,
    ];

    /// <summary>The Reader Requirements box contents of a CMYK image: mask length 1, masks 3, features 5 and 55, no vendor features.</summary>
    private static readonly byte[] CmykRequirements = [0x01, 0x03, 0x03, 0x00, 0x02, 0x00, 0x05, 0x01, 0x00, 0x37, 0x02, 0x00, 0x00];

    /// <summary>A CMYK image is a JPX file: brand 'jpx ', minor version 1, and the Reader Requirements box follows the file type box.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykFileIsJpxWithReaderRequirements()
    {
        var file = PdfJpxEncoder.Encode(Image(Cmyk) with { ColorSpace = PdfJpxColorSpace.Cmyk });
        var fileTypeLength = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(FileTypeOffset));
        var rreq = FileTypeOffset + fileTypeLength;

        await Assert.That(file.AsSpan(BrandOffset, Code).SequenceEqual("jpx "u8)).IsTrue();
        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(MinorVersionOffset))).IsEqualTo(1);
        await Assert.That(file.AsSpan(CompatibilityOffset, Code).SequenceEqual("jpx "u8)).IsTrue();
        await Assert.That(fileTypeLength).IsEqualTo(ShortFileTypeLength);
        await Assert.That(file.AsSpan(rreq + Code, Code).SequenceEqual("rreq"u8)).IsTrue();
        await Assert.That(file.AsSpan(rreq + BoxHeader, CmykRequirements.Length).SequenceEqual(CmykRequirements)).IsTrue();
    }

    /// <summary>The colour specification box of a JPX file gives APPROX 1, as APPROX 0 is illegal there (ISO 15444-2 M.11.7.2).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JpxColourBoxStatesTheApproximation()
    {
        var file = PdfJpxEncoder.Encode(Image(Cmyk) with { ColorSpace = PdfJpxColorSpace.Cmyk });
        var colr = file.AsSpan().IndexOf("colr"u8) + Code;

        await Assert.That(file[colr]).IsEqualTo(EnumeratedMethod);
        await Assert.That(file[colr + 1]).IsEqualTo((byte)0);
        await Assert.That(file[colr + Short]).IsEqualTo(Accurate);
        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(colr + MaskBytes))).IsEqualTo(CmykSpace);
    }

    /// <summary>An RGB image stays a JP2 file: brand 'jp2 ', minor version 0, no Reader Requirements box and APPROX 0.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RgbFileStaysJp2()
    {
        var file = PdfJpxEncoder.Encode(Image(Rgb));
        var fileTypeLength = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(FileTypeOffset));
        var colr = file.AsSpan().IndexOf("colr"u8) + Code;

        await Assert.That(file.AsSpan(BrandOffset, Code).SequenceEqual("jp2 "u8)).IsTrue();
        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(MinorVersionOffset))).IsEqualTo(0);
        await Assert.That(file.AsSpan(FileTypeOffset + fileTypeLength + Code, Code).SequenceEqual("jp2h"u8)).IsTrue();
        await Assert.That(file[colr + Short]).IsEqualTo((byte)0);
    }

    /// <summary>A matrix-based RGB input profile is a restricted ICC profile (method 2) in a JP2 file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RestrictedProfileKeepsTheJp2Method()
    {
        var file = PdfJpxEncoder.Encode(Image(Rgb) with { IccProfile = Profile("scnr"u8, "RGB "u8, "rXYZ"u8) });
        var colr = file.AsSpan().IndexOf("colr"u8) + Code;

        await Assert.That(file.AsSpan(BrandOffset, Code).SequenceEqual("jp2 "u8)).IsTrue();
        await Assert.That(file[colr]).IsEqualTo(RestrictedMethod);
        await Assert.That(file[colr + Short]).IsEqualTo((byte)0);
    }

    /// <summary>A profile that is not restricted needs the Any ICC method (3), so the file is JPX with the baseline entry and flag 44.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnrestrictedProfileUsesAnyIcc()
    {
        var file = PdfJpxEncoder.Encode(Image(Rgb) with { IccProfile = Profile("prtr"u8, "RGB "u8, "A2B0"u8) });
        var colr = file.AsSpan().IndexOf("colr"u8) + Code;
        var features = file.AsSpan().IndexOf("rreq"u8) + Code + FirstFeatureOffset;

        await Assert.That(file.AsSpan(BrandOffset, Code).SequenceEqual("jpx "u8)).IsTrue();
        await Assert.That(file.AsSpan().IndexOf("jpxb"u8)).IsGreaterThan(0);
        await Assert.That(file[colr]).IsEqualTo(AnyIccMethod);
        await Assert.That(file[colr + Short]).IsEqualTo(Accurate);
        await Assert.That(BinaryPrimitives.ReadUInt16BigEndian(file.AsSpan(features + FeatureBytes))).IsEqualTo(AnyIccFlag);
    }

    /// <summary>An image with opacity lists the non-premultiplied opacity flag (9) as a third requirement.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpacityAddsARequirement()
    {
        var file = PdfJpxEncoder.Encode(Image(Cmyk + 1) with { ColorSpace = PdfJpxColorSpace.Cmyk, HasAlpha = true });
        var contents = file.AsSpan().IndexOf("rreq"u8) + Code;
        var features = contents + FirstFeatureOffset;

        await Assert.That(file[contents + 1]).IsEqualTo(ThreeRequirements);
        await Assert.That(BinaryPrimitives.ReadUInt16BigEndian(file.AsSpan(contents + MaskBytes))).IsEqualTo((ushort)Rgb);
        await Assert.That(BinaryPrimitives.ReadUInt16BigEndian(file.AsSpan(features + (Short * FeatureBytes)))).IsEqualTo(OpacityFlag);
    }

    /// <summary>The decoder reads a JPX file the encoder wrote, with the Reader Requirements box before the header.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecoderReadsAJpxCmykFile()
    {
        var file = PdfJpxEncoder.Encode(Image(Cmyk) with { ColorSpace = PdfJpxColorSpace.Cmyk });
        var info = JpxFileFormat.Read(file);

        await Assert.That(info).IsNotNull();
        await Assert.That(info!.ColorSpace).IsEqualTo(JpxColorSpace.Cmyk);
    }

    /// <summary>CIELab with default parameters (EnumCS 14, no EP) decodes: a mid-grey code is a neutral grey, a full L* code is white.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CieLabDecodesWithDefaultParameters()
    {
        var mid = Decode(LabFile(MidLightness, []));
        var white = Decode(LabFile(MaxSample, []));

        await Assert.That(mid).IsNotNull();
        await Assert.That(IsGray(mid!, MidGray)).IsTrue();
        await Assert.That(white!.Pixels.AsSpan()[1]).IsGreaterThanOrEqualTo((byte)(MaxSample - GrayTolerance));
        await Assert.That(white.Pixels.AsSpan()[Short]).IsGreaterThanOrEqualTo((byte)(MaxSample - GrayTolerance));
    }

    /// <summary>CIELab with explicit parameters that equal the defaults decodes the same as without them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CieLabReadsExplicitParameters()
    {
        var explicitFile = LabFile(MidLightness, DefaultLabParameters);
        var defaults = Decode(LabFile(MidLightness, []));
        var explicitImage = Decode(explicitFile);

        await Assert.That(explicitImage).IsNotNull();
        await Assert.That(explicitImage!.Pixels.AsSpan().SequenceEqual(defaults!.Pixels)).IsTrue();
        await Assert.That(JpxFileFormat.Read(explicitFile)!.Lab).IsNotNull();
    }

    /// <summary>The first compositing layer's colour group replaces the colour specification of the JP2 header (ISO 15444-2 M.11.7).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompositingLayerColourOverridesTheHeader()
    {
        var codestream = Codestream(Rgb);
        var colourBox = JpxTestFileWriter.Box("colr", [EnumeratedMethod, 0, Accurate, 0, 0, 0, LabSpace]);
        var group = JpxTestFileWriter.Box("jplh", JpxTestFileWriter.Box("cgrp", colourBox));
        var file = WithBoxBeforeCodestream(JpxTestFileWriter.Wrap(codestream, Rgb, Side, Side, SrgbSpace, []), group, codestream.Length);

        await Assert.That(JpxFileFormat.Read(file)!.ColorSpace).IsEqualTo(JpxColorSpace.Lab);
    }

    /// <summary>An Opacity box in the compositing layer header makes the last channel opacity, so /SMaskInData finds it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpacityBoxDeclaresTheLastChannel()
    {
        var codestream = Codestream(Cmyk);
        var layer = JpxTestFileWriter.Box("jplh", JpxTestFileWriter.Box("opct", [0]));
        var file = WithBoxBeforeCodestream(JpxTestFileWriter.Wrap(codestream, Cmyk, Side, Side, SrgbSpace, []), layer, codestream.Length);
        var header = new ImageHeader(Side, Side, Eight, false, false, null, null, null);
        var plain = JpxImageDecoder.Decode(header, file, 0);
        var masked = JpxImageDecoder.Decode(header, file, 1);

        await Assert.That(JpxFileFormat.Read(file)!.OpacityKind).IsEqualTo(0);
        await Assert.That(plain).IsNotNull();
        await Assert.That(masked).IsNotNull();
        await Assert.That(masked!.Pixels.AsSpan().SequenceEqual(plain!.Pixels)).IsFalse();
    }

    /// <summary>A chroma-key Opacity box is ignored, as PDF ignores chroma-key transparency.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChromaKeyOpacityIsIgnored()
    {
        var codestream = Codestream(Rgb);
        var layer = JpxTestFileWriter.Box("jplh", JpxTestFileWriter.Box("opct", [Short]));
        var file = WithBoxBeforeCodestream(JpxTestFileWriter.Wrap(codestream, Rgb, Side, Side, SrgbSpace, []), layer, codestream.Length);

        await Assert.That(JpxFileFormat.Read(file)!.OpacityKind).IsEqualTo(-1);
    }

    /// <summary>A codestream that a table lists as one piece in the file is found (ISO 15444-2 M.11.3).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SingleFragmentCodestreamIsRead()
    {
        var codestream = Codestream(Rgb);
        var head = HeaderBoxes(codestream);
        var flst = new byte[FragmentListBytes];
        BinaryPrimitives.WriteUInt16BigEndian(flst, 1);
        BinaryPrimitives.WriteUInt32BigEndian(flst.AsSpan(FragmentLengthOffset), (uint)codestream.Length);
        var table = JpxTestFileWriter.Box("ftbl", JpxTestFileWriter.Box("flst", flst));

        // The codestream follows the table, so the offset in the table is known once the table's size is.
        var offset = head.Length + table.Length;
        BinaryPrimitives.WriteUInt64BigEndian(flst.AsSpan(Short), (ulong)offset);
        byte[] file = [.. head, .. JpxTestFileWriter.Box("ftbl", JpxTestFileWriter.Box("flst", flst)), .. codestream];
        var info = JpxFileFormat.Read(file);

        await Assert.That(info).IsNotNull();
        await Assert.That(info!.Codestream.Offset).IsEqualTo(offset);
        await Assert.That(info.Codestream.Length).IsEqualTo(codestream.Length);
    }

    /// <summary>A codestream held in another file (a non-zero data reference) is not read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FragmentInAnotherFileIsRefused()
    {
        var codestream = Codestream(Rgb);
        var flst = new byte[FragmentListBytes];
        BinaryPrimitives.WriteUInt16BigEndian(flst, 1);
        BinaryPrimitives.WriteUInt16BigEndian(flst.AsSpan(FragmentReferenceOffset), 1);
        byte[] file = [.. HeaderBoxes(codestream), .. JpxTestFileWriter.Box("ftbl", JpxTestFileWriter.Box("flst", flst))];

        await Assert.That(JpxFileFormat.Read(file)).IsNull();
    }

    /// <summary>Makes a small image of a number of components.</summary>
    /// <param name="components">The components.</param>
    /// <returns>The image.</returns>
    private static PdfJpxImage Image(int components)
    {
        var samples = new byte[Side * Side * components];
        var random = new JpegTestRandom(Seed);
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (byte)random.Next(0, MaxSample + 1);
        }

        return new(Side, Side, components, Eight, samples);
    }

    /// <summary>Builds a minimal ICC profile header with one tag table entry or none.</summary>
    /// <param name="profileClass">The profile class code.</param>
    /// <param name="space">The data colour space code.</param>
    /// <param name="tag">The tag code, or empty for none.</param>
    /// <returns>The profile bytes.</returns>
    private static byte[] Profile(ReadOnlySpan<byte> profileClass, ReadOnlySpan<byte> space, ReadOnlySpan<byte> tag)
    {
        var profile = new byte[tag.IsEmpty ? ProfileBytes : TaggedProfileBytes];
        profileClass.CopyTo(profile.AsSpan(ClassOffset));
        space.CopyTo(profile.AsSpan(SpaceOffset));
        "XYZ "u8.CopyTo(profile.AsSpan(ConnectionOffset));
        if (!tag.IsEmpty)
        {
            BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(TagCountOffset), 1);
            tag.CopyTo(profile.AsSpan(ProfileBytes));
        }

        return profile;
    }

    /// <summary>Encodes a codestream of a number of components.</summary>
    /// <param name="components">The components.</param>
    /// <returns>The codestream.</returns>
    private static byte[] Codestream(int components)
    {
        var options = new JpxTestOptions { Width = Side, Height = Side, Components = components };
        return JpxTestEncoder.Encode(options, JpxDecoderTests.Planes(options));
    }

    /// <summary>Gets the signature, file type and header boxes of a JP2 file, without the codestream box.</summary>
    /// <param name="codestream">The codestream.</param>
    /// <returns>The boxes before the codestream box.</returns>
    private static byte[] HeaderBoxes(byte[] codestream)
    {
        var whole = JpxTestFileWriter.Wrap(codestream, Rgb, Side, Side, SrgbSpace, []);
        return whole.AsSpan(0, whole.Length - codestream.Length - BoxHeader).ToArray();
    }

    /// <summary>Makes a CIELab JP2 file whose pixels are all one code value.</summary>
    /// <param name="lightness">The L* code.</param>
    /// <param name="parameters">The EP field, or empty for the defaults.</param>
    /// <returns>The file.</returns>
    private static byte[] LabFile(int lightness, byte[] parameters)
    {
        var options = new JpxTestOptions { Width = Side, Height = Side, Components = Rgb };
        var codestream = JpxTestEncoder.Encode(options, [Fill(lightness), Fill(NeutralA), Fill(NeutralB)]);
        var colour = JpxTestFileWriter.Box("colr", [EnumeratedMethod, 0, Accurate, 0, 0, 0, LabSpace, .. parameters]);
        var wrapped = JpxTestFileWriter.Wrap(codestream, Rgb, Side, Side, SrgbSpace, []);

        // Replace the header's sRGB specification by the CIELab one, and the header box's length with it.
        var colr = wrapped.AsSpan().IndexOf("colr"u8) - Code;
        var oldLength = BinaryPrimitives.ReadInt32BigEndian(wrapped.AsSpan(colr));
        byte[] file = [.. wrapped.AsSpan(0, colr), .. colour, .. wrapped.AsSpan(colr + oldLength)];
        var header = file.AsSpan().IndexOf("jp2h"u8) - Code;
        BinaryPrimitives.WriteInt32BigEndian(file.AsSpan(header), BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(header)) + colour.Length - oldLength);
        return file;
    }

    /// <summary>Makes a plane of one value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The plane.</returns>
    private static int[] Fill(int value)
    {
        var plane = new int[Side * Side];
        Array.Fill(plane, value);
        return plane;
    }

    /// <summary>Puts a box in front of the codestream box.</summary>
    /// <param name="file">The file with the codestream box last.</param>
    /// <param name="box">The box.</param>
    /// <param name="codestreamLength">The codestream bytes.</param>
    /// <returns>The new file.</returns>
    private static byte[] WithBoxBeforeCodestream(byte[] file, byte[] box, int codestreamLength)
    {
        var split = file.Length - codestreamLength - BoxHeader;
        return [.. file.AsSpan(0, split), .. box, .. file.AsSpan(split)];
    }

    /// <summary>Decodes a file with no image dictionary colour space.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The image.</returns>
    private static PdfImageData? Decode(byte[] file) => JpxImageDecoder.Decode(new(Side, Side, Eight, false, false, null, null, null), file, 0);

    /// <summary>Determines whether the first pixel is grey at a value, within a tolerance.</summary>
    /// <param name="image">The image.</param>
    /// <param name="gray">The expected value.</param>
    /// <returns><see langword="true"/> when blue, green and red are all near it.</returns>
    private static bool IsGray(PdfImageData image, int gray)
    {
        var pixel = image.Pixels.AsSpan();
        return Math.Abs(pixel[0] - gray) <= GrayTolerance && Math.Abs(pixel[1] - gray) <= GrayTolerance && Math.Abs(pixel[Short] - gray) <= GrayTolerance;
    }
}
