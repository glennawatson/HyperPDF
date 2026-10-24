// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H265;

/// <summary>Steps over the video usability information of a sequence parameter set (E.2.1), which does not change the decoded samples.</summary>
internal static class H265VuiReader
{
    /// <summary>The aspect ratio indicator that says the ratio follows as two 16 bit numbers.</summary>
    private const int ExtendedSar = 255;

    /// <summary>The bits of the two parts of an extended aspect ratio.</summary>
    private const int SarBits = 32;

    /// <summary>The bits of the aspect ratio indicator.</summary>
    private const int AspectBits = 8;

    /// <summary>The bits of the video format and the full range flag.</summary>
    private const int VideoFormatBits = 4;

    /// <summary>The bits of the three colour description numbers.</summary>
    private const int ColourDescriptionBits = 24;

    /// <summary>The bits of the number of units in a tick and the time scale.</summary>
    private const int TimingBits = 64;

    /// <summary>The bits of the flags that follow the chroma location: neutral chroma, field sequence and frame field information.</summary>
    private const int ThreeFlags = 3;

    /// <summary>The unsigned numbers of the default display window.</summary>
    private const int WindowNumbers = 4;

    /// <summary>The unsigned numbers of the bitstream restriction after its three flags.</summary>
    private const int RestrictionNumbers = 5;

    /// <summary>The flags that start the bitstream restriction.</summary>
    private const int RestrictionFlags = 3;

    /// <summary>The bits of the scales of a hypothetical reference decoder.</summary>
    private const int HrdScaleBits = 8;

    /// <summary>The bits of the four fields that make up the sub-picture parameters.</summary>
    private const int SubPictureBits = 19;

    /// <summary>The bits of the three delay length fields.</summary>
    private const int DelayLengthBits = 15;

    /// <summary>The unsigned numbers of the chroma sample location.</summary>
    private const int ChromaLocationNumbers = 2;

    /// <summary>The unsigned numbers of a coded picture buffer entry: bit rate and size.</summary>
    private const int CpbNumbers = 2;

    /// <summary>The unsigned numbers of a coded picture buffer entry with sub-picture parameters.</summary>
    private const int SubPictureCpbNumbers = 4;

    /// <summary>The bits of the cpb size scale of sub-pictures.</summary>
    private const int DuScaleBits = 4;

    /// <summary>Skips the video usability information.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="maxSubLayersMinus1">The number of temporal sub-layers less one.</param>
    internal static void Skip(ref MsbBitReader reader, int maxSubLayersMinus1)
    {
        if (reader.ReadBit() && reader.Read(AspectBits) == ExtendedSar)
        {
            reader.Skip(SarBits);
        }

        if (reader.ReadBit())
        {
            reader.Skip(1);
        }

        SkipSignalType(ref reader);
        if (reader.ReadBit())
        {
            SkipNumbers(ref reader, ChromaLocationNumbers);
        }

        BitHelpers.Skip(ref reader, ThreeFlags);
        if (reader.ReadBit())
        {
            SkipNumbers(ref reader, WindowNumbers);
        }

        SkipTiming(ref reader, maxSubLayersMinus1);
        if (!reader.ReadBit())
        {
            return;
        }

        BitHelpers.Skip(ref reader, RestrictionFlags);
        SkipNumbers(ref reader, RestrictionNumbers);
    }

    /// <summary>Skips the video signal type and colour description.</summary>
    /// <param name="reader">The bits.</param>
    private static void SkipSignalType(ref MsbBitReader reader)
    {
        if (!reader.ReadBit())
        {
            return;
        }

        reader.Skip(VideoFormatBits);
        if (reader.ReadBit())
        {
            reader.Skip(ColourDescriptionBits);
        }
    }

    /// <summary>Skips the timing information and the hypothetical reference decoder parameters.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="maxSubLayersMinus1">The number of temporal sub-layers less one.</param>
    private static void SkipTiming(ref MsbBitReader reader, int maxSubLayersMinus1)
    {
        if (!reader.ReadBit())
        {
            return;
        }

        BitHelpers.Skip(ref reader, TimingBits);
        if (reader.ReadBit())
        {
            _ = ExpGolomb.ReadUe(ref reader);
        }

        if (reader.ReadBit())
        {
            SkipHrd(ref reader, true, maxSubLayersMinus1);
        }
    }

    /// <summary>Skips unsigned Exp-Golomb numbers.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="count">How many.</param>
    private static void SkipNumbers(ref MsbBitReader reader, int count)
    {
        for (var i = 0; i < count; i++)
        {
            _ = ExpGolomb.ReadUe(ref reader);
        }
    }

    /// <summary>Skips the hypothetical reference decoder parameters (E.2.2).</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="commonInfoPresent">Whether the parameters common to all sub-layers are present.</param>
    /// <param name="maxSubLayersMinus1">The number of temporal sub-layers less one.</param>
    private static void SkipHrd(ref MsbBitReader reader, bool commonInfoPresent, int maxSubLayersMinus1)
    {
        var nal = false;
        var vcl = false;
        var subPicture = false;
        if (commonInfoPresent)
        {
            nal = reader.ReadBit();
            vcl = reader.ReadBit();
            if (nal || vcl)
            {
                subPicture = reader.ReadBit();
                if (subPicture)
                {
                    reader.Skip(SubPictureBits);
                }

                reader.Skip(HrdScaleBits);
                if (subPicture)
                {
                    reader.Skip(DuScaleBits);
                }

                reader.Skip(DelayLengthBits);
            }
        }

        for (var i = 0; i <= maxSubLayersMinus1; i++)
        {
            SkipSubLayerHrd(ref reader, nal, vcl, subPicture);
        }
    }

    /// <summary>Skips the parameters of one sub-layer.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="nal">Whether NAL parameters follow.</param>
    /// <param name="vcl">Whether VCL parameters follow.</param>
    /// <param name="subPicture">Whether the sub-picture parameters are present.</param>
    private static void SkipSubLayerHrd(ref MsbBitReader reader, bool nal, bool vcl, bool subPicture)
    {
        var fixedGeneral = reader.ReadBit();
        var fixedWithinCvs = fixedGeneral || reader.ReadBit();
        var lowDelay = false;
        if (fixedWithinCvs)
        {
            _ = ExpGolomb.ReadUe(ref reader);
        }
        else
        {
            lowDelay = reader.ReadBit();
        }

        var cpbCount = 1;
        if (!lowDelay)
        {
            cpbCount += (int)ExpGolomb.ReadUe(ref reader);
        }

        for (var pass = 0; pass < (nal ? 1 : 0) + (vcl ? 1 : 0); pass++)
        {
            for (var i = 0; i < cpbCount; i++)
            {
                SkipNumbers(ref reader, subPicture ? SubPictureCpbNumbers : CpbNumbers);
                reader.Skip(1);
            }
        }
    }
}
