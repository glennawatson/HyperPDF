// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H264;

/// <summary>Reads sequence parameter sets.</summary>
internal static class H264SpsReader
{
    /// <summary>The largest sequence parameter set number.</summary>
    private const int MaxId = 31;

    /// <summary>The lists the scaling syntax carries when the colour planes are not all full size.</summary>
    private const int ScalingListsNarrow = 8;

    /// <summary>The lists the scaling syntax carries for 4:4:4 video.</summary>
    private const int ScalingListsWide = 12;

    /// <summary>The chroma format of 4:2:0 video, which a set that does not say has.</summary>
    private const int DefaultChromaFormat = 1;

    /// <summary>The chroma format of 4:4:4 video.</summary>
    private const int ChromaFormat444 = 3;

    /// <summary>The bits of the profile and of the level.</summary>
    private const int ProfileBits = 8;

    /// <summary>The bits of the constraint flags and the reserved bits.</summary>
    private const int ConstraintBits = 8;

    /// <summary>The offset added to the coded size of the frame number and picture order count fields.</summary>
    private const int FieldBitsBase = 4;

    /// <summary>The offset of the coded bit depth fields.</summary>
    private const int BitDepthBase = 8;

    /// <summary>The most bits of a frame number or low picture order count.</summary>
    private const int MaxFieldBits = 16;

    /// <summary>The most entries in the cycle of picture order offsets.</summary>
    private const int MaxCycle = 255;

    /// <summary>The largest picture size in macroblocks along one side that this reader accepts.</summary>
    private const int MaxSizeInMbs = 1_024;

    /// <summary>The value of the aspect ratio indicator that says the aspect ratio follows as two numbers.</summary>
    private const int ExtendedSar = 255;

    /// <summary>The bits of the two parts of an extended aspect ratio.</summary>
    private const int SarBits = 32;

    /// <summary>The bits of the video format and range flag, before the colour description flag.</summary>
    private const int VideoFormatBits = 4;

    /// <summary>The bits of the three colour description numbers.</summary>
    private const int ColourDescriptionBits = 24;

    /// <summary>The bits of the timing information numbers and flag.</summary>
    private const int TimingBits = 65;

    /// <summary>The most bits skipped in one step.</summary>
    private const int SkipStep = 32;

    /// <summary>The bits of the scales of a hypothetical reference decoder.</summary>
    private const int HrdScaleBits = 8;

    /// <summary>The bits of the four delay lengths of a hypothetical reference decoder.</summary>
    private const int HrdLengthBits = 20;

    /// <summary>The numbers of the bitstream restriction that come before the reorder count.</summary>
    private const int RestrictionNumbers = 4;

    /// <summary>The picture order count type that codes the low bits of the count.</summary>
    private const int PocLsbType = 0;

    /// <summary>The picture order count type that derives the count from a cycle of offsets.</summary>
    private const int PocCycleType = 1;

    /// <summary>The profile numbers whose sets carry the chroma format, bit depths and scaling lists.</summary>
    private static readonly int[] HighProfiles = [100, 110, 122, 244, 44, 83, 86, 118, 128, 138, 139, 134, 135];

    /// <summary>Reads a sequence parameter set.</summary>
    /// <param name="rbsp">The payload of the NAL unit, without its header and emulation prevention bytes.</param>
    /// <returns>The set.</returns>
    /// <exception cref="InvalidDataException">The set is damaged.</exception>
    /// <exception cref="NotSupportedException">The set uses a feature this decoder does not read.</exception>
    internal static H264Sps Read(ReadOnlySpan<byte> rbsp)
    {
        var reader = new MsbBitReader(rbsp);
        var profile = (int)reader.Read(ProfileBits);
        reader.Skip(ConstraintBits);
        var level = (int)reader.Read(ProfileBits);
        var id = (int)ExpGolomb.ReadUe(ref reader);
        var colour = Array.IndexOf(HighProfiles, profile) >= 0 ? ReadColour(ref reader) : new H264Colour(DefaultChromaFormat, false, BitDepthBase, BitDepthBase, false, null);
        var log2MaxFrameNum = FieldBitsBase + (int)ExpGolomb.ReadUe(ref reader);
        var poc = ReadPoc(ref reader);
        var maxRefFrames = (int)ExpGolomb.ReadUe(ref reader);
        var gaps = reader.ReadBit();
        var width = 1 + (int)ExpGolomb.ReadUe(ref reader);
        var height = 1 + (int)ExpGolomb.ReadUe(ref reader);
        var frameMbsOnly = reader.ReadBit();
        var mbaff = !frameMbsOnly && reader.ReadBit();
        var direct8x8 = reader.ReadBit();
        var crop = reader.ReadBit() ? ReadCrop(ref reader) : default;
        var reorder = reader.ReadBit() ? ReadVui(ref reader) : -1;
        var sps = new H264Sps(
            profile,
            level,
            id,
            colour.Format,
            colour.SeparatePlanes,
            colour.DepthLuma,
            colour.DepthChroma,
            colour.Bypass,
            colour.Scaling,
            log2MaxFrameNum,
            poc,
            maxRefFrames,
            gaps,
            width,
            height,
            frameMbsOnly,
            mbaff,
            direct8x8,
            crop,
            reorder);
        Validate(sps, reader.IsOverrun);
        return sps;
    }

    /// <summary>Reads the four cropping offsets.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The crop.</returns>
    private static VideoCrop ReadCrop(ref MsbBitReader reader)
    {
        var left = (int)ExpGolomb.ReadUe(ref reader);
        var right = (int)ExpGolomb.ReadUe(ref reader);
        var top = (int)ExpGolomb.ReadUe(ref reader);
        var bottom = (int)ExpGolomb.ReadUe(ref reader);
        return new(left, right, top, bottom);
    }

    /// <summary>Reads the colour format, bit depths and scaling lists that the high profiles carry.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>What was read.</returns>
    private static H264Colour ReadColour(ref MsbBitReader reader)
    {
        var format = (int)ExpGolomb.ReadUe(ref reader);
        var separate = format == ChromaFormat444 && reader.ReadBit();
        var depthLuma = BitDepthBase + (int)ExpGolomb.ReadUe(ref reader);
        var depthChroma = BitDepthBase + (int)ExpGolomb.ReadUe(ref reader);
        var bypass = reader.ReadBit();
        var scaling = reader.ReadBit() ? H264ScalingLists.Read(ref reader, format != ChromaFormat444 ? ScalingListsNarrow : ScalingListsWide, null) : null;
        return new(format, separate, depthLuma, depthChroma, bypass, scaling);
    }

    /// <summary>Checks the values of a set.</summary>
    /// <param name="sps">The set.</param>
    /// <param name="overrun">Whether the bits ran out.</param>
    /// <exception cref="InvalidDataException">A value is out of range.</exception>
    /// <exception cref="NotSupportedException">The set uses a feature this decoder does not read.</exception>
    private static void Validate(H264Sps sps, bool overrun)
    {
        if (overrun || sps.Id > MaxId || sps.Log2MaxFrameNum > MaxFieldBits || sps.WidthInMbs > MaxSizeInMbs || sps.HeightInMapUnits > MaxSizeInMbs)
        {
            throw new InvalidDataException("An H.264 sequence parameter set is damaged.");
        }

        if (sps.ChromaFormat > 1 || sps.SeparateColourPlanes || sps.BitDepthLuma != BitDepthBase || sps.BitDepthChroma != BitDepthBase)
        {
            throw new NotSupportedException("This H.264 decoder reads 8 bit 4:2:0 and monochrome video only.");
        }
    }

    /// <summary>Reads the picture order count settings.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="InvalidDataException">The settings are damaged.</exception>
    private static H264PocSettings ReadPoc(ref MsbBitReader reader)
    {
        var type = (int)ExpGolomb.ReadUe(ref reader);
        if (type == PocLsbType)
        {
            return new(type, FieldBitsBase + (int)ExpGolomb.ReadUe(ref reader), false, 0, 0, []);
        }

        if (type != PocCycleType)
        {
            return new(type, 0, false, 0, 0, []);
        }

        var alwaysZero = reader.ReadBit();
        var nonRef = ExpGolomb.ReadSe(ref reader);
        var topToBottom = ExpGolomb.ReadSe(ref reader);
        var count = (int)ExpGolomb.ReadUe(ref reader);
        if (count > MaxCycle)
        {
            throw new InvalidDataException("An H.264 sequence parameter set is damaged.");
        }

        var offsets = new int[count];
        for (var index = 0; index < count; index++)
        {
            offsets[index] = ExpGolomb.ReadSe(ref reader);
        }

        return new(type, 0, alwaysZero, nonRef, topToBottom, offsets);
    }

    /// <summary>Reads the video usability information and keeps the number of pictures that may be reordered.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The most pictures that precede a picture in decoding order and follow it in output order, or -1 when the information does not say.</returns>
    private static int ReadVui(ref MsbBitReader reader)
    {
        SkipPictureFormat(ref reader);
        if (reader.ReadBit())
        {
            Skip(ref reader, TimingBits);
        }

        var nal = reader.ReadBit();
        if (nal)
        {
            SkipHrd(ref reader);
        }

        var vcl = reader.ReadBit();
        if (vcl)
        {
            SkipHrd(ref reader);
        }

        if (nal || vcl)
        {
            reader.Skip(1);
        }

        reader.Skip(1);
        if (!reader.ReadBit())
        {
            return -1;
        }

        reader.Skip(1);
        for (var index = 0; index < RestrictionNumbers; index++)
        {
            _ = ExpGolomb.ReadUe(ref reader);
        }

        var reorder = (int)ExpGolomb.ReadUe(ref reader);
        _ = ExpGolomb.ReadUe(ref reader);
        return reader.IsOverrun ? -1 : reorder;
    }

    /// <summary>Skips the first part of the video usability information: the aspect ratio, overscan, video signal and chroma location.</summary>
    /// <param name="reader">The bits.</param>
    private static void SkipPictureFormat(ref MsbBitReader reader)
    {
        if (reader.ReadBit() && reader.Read(ProfileBits) == ExtendedSar)
        {
            reader.Skip(SarBits);
        }

        if (reader.ReadBit())
        {
            reader.Skip(1);
        }

        if (reader.ReadBit())
        {
            reader.Skip(VideoFormatBits);
            if (reader.ReadBit())
            {
                reader.Skip(ColourDescriptionBits);
            }
        }

        if (!reader.ReadBit())
        {
            return;
        }

        _ = ExpGolomb.ReadUe(ref reader);
        _ = ExpGolomb.ReadUe(ref reader);
    }

    /// <summary>Skips hypothetical reference decoder parameters.</summary>
    /// <param name="reader">The bits.</param>
    private static void SkipHrd(ref MsbBitReader reader)
    {
        var count = (int)ExpGolomb.ReadUe(ref reader) + 1;
        reader.Skip(HrdScaleBits);
        for (var index = 0; index < count && !reader.IsOverrun; index++)
        {
            _ = ExpGolomb.ReadUe(ref reader);
            _ = ExpGolomb.ReadUe(ref reader);
            reader.Skip(1);
        }

        reader.Skip(HrdLengthBits);
    }

    /// <summary>Skips bits of any number.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="bits">The number of bits.</param>
    private static void Skip(ref MsbBitReader reader, int bits)
    {
        while (bits > 0)
        {
            var step = Math.Min(bits, SkipStep);
            reader.Skip(step);
            bits -= step;
        }
    }
}
