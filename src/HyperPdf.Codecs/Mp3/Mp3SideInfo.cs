// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Mp3;

/// <summary>The side information of a layer 3 frame: where its main data starts, and the coding of each granule and channel.</summary>
internal sealed class Mp3SideInfo
{
    /// <summary>The most granules in a frame.</summary>
    private const int MostGranules = 2;

    /// <summary>The most channels in a frame.</summary>
    private const int MostChannels = 2;

    /// <summary>The bits of the offset of the main data in MPEG 1.</summary>
    private const int Mpeg1MainDataBits = 9;

    /// <summary>The bits of the offset of the main data in MPEG 2.</summary>
    private const int Mpeg2MainDataBits = 8;

    /// <summary>The private bits in an MPEG 1 frame with one channel.</summary>
    private const int Mpeg1MonoPrivate = 5;

    /// <summary>The private bits in an MPEG 1 frame with two channels.</summary>
    private const int Mpeg1StereoPrivate = 3;

    /// <summary>The private bits in an MPEG 2 frame with one channel.</summary>
    private const int Mpeg2MonoPrivate = 1;

    /// <summary>The private bits in an MPEG 2 frame with two channels.</summary>
    private const int Mpeg2StereoPrivate = 2;

    /// <summary>The groups of scale factors that may repeat those of the first granule.</summary>
    private const int ScfsiGroups = 4;

    /// <summary>The bits of the length of a granule's data.</summary>
    private const int Part23Bits = 12;

    /// <summary>The bits of the number of big value pairs.</summary>
    private const int BigValueBits = 9;

    /// <summary>The most big value pairs.</summary>
    private const int MostBigValues = 288;

    /// <summary>The bits of the global gain.</summary>
    private const int GainBits = 8;

    /// <summary>The bits of the scale factor code in MPEG 1.</summary>
    private const int Mpeg1CompressBits = 4;

    /// <summary>The bits of the scale factor code in MPEG 2.</summary>
    private const int Mpeg2CompressBits = 9;

    /// <summary>The bits of the block type.</summary>
    private const int BlockTypeBits = 2;

    /// <summary>The bits of a table number.</summary>
    private const int TableBits = 5;

    /// <summary>The bits of a short window gain.</summary>
    private const int SubblockGainBits = 3;

    /// <summary>The bits of the first region's band count.</summary>
    private const int Region0Bits = 4;

    /// <summary>The bits of the second region's band count.</summary>
    private const int Region1Bits = 3;

    /// <summary>The scale factor code from which MPEG 2 raises the upper bands.</summary>
    private const int Mpeg2PreflagFrom = 500;

    /// <summary>The first region's band count when a granule switches windows.</summary>
    private const int SwitchedRegion0Count = 7;

    /// <summary>The second region's band count when a granule switches windows, enough to reach the end.</summary>
    private const int SwitchedRegion1Count = 255;

    /// <summary>The groups of scale factors each channel repeats, found once for the frame.</summary>
    private readonly int[] _scfsi = new int[MostChannels];

    /// <summary>Gets the offset, back from the start of this frame's main data, where the first granule's data starts.</summary>
    internal int MainDataBegin { get; private set; }

    /// <summary>Gets the coding of each granule and channel, indexed by granule then channel.</summary>
    internal Mp3GranuleChannel[][] Granules { get; } = BuildGranules();

    /// <summary>Reads the side information.</summary>
    /// <param name="header">The frame header.</param>
    /// <param name="data">The side information bytes.</param>
    /// <returns><see langword="false"/> when the bytes say something impossible.</returns>
    internal bool Read(in Mp3FrameHeader header, ReadOnlySpan<byte> data)
    {
        var reader = new MsbBitReader(data);
        ReadFrameFields(ref reader, header);
        var granules = header.IsMpeg1 ? MostGranules : 1;
        for (var granule = 0; granule < granules; granule++)
        {
            for (var channel = 0; channel < header.Channels; channel++)
            {
                var info = Granules[granule][channel];
                info.Scfsi = granule == 1 ? _scfsi[channel] : 0;
                if (!ReadGranule(ref reader, header.IsMpeg1, info))
                {
                    return false;
                }
            }
        }

        return !reader.IsOverrun;
    }

    /// <summary>Makes the room for every granule and channel.</summary>
    /// <returns>The records.</returns>
    private static Mp3GranuleChannel[][] BuildGranules()
    {
        var granules = new Mp3GranuleChannel[MostGranules][];
        for (var granule = 0; granule < granules.Length; granule++)
        {
            granules[granule] = new Mp3GranuleChannel[MostChannels];
            for (var channel = 0; channel < MostChannels; channel++)
            {
                granules[granule][channel] = new();
            }
        }

        return granules;
    }

    /// <summary>Reads the information for one channel of one granule.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="isMpeg1">Whether the frame is MPEG 1.</param>
    /// <param name="info">Receives the information.</param>
    /// <returns><see langword="false"/> when the information is impossible.</returns>
    private static bool ReadGranule(ref MsbBitReader reader, bool isMpeg1, Mp3GranuleChannel info)
    {
        info.Part23Length = (int)reader.Read(Part23Bits);
        info.BigValues = (int)reader.Read(BigValueBits);
        info.GlobalGain = (int)reader.Read(GainBits);
        info.ScalefacCompress = (int)reader.Read(isMpeg1 ? Mpeg1CompressBits : Mpeg2CompressBits);
        info.WindowSwitching = reader.ReadBit();
        if (info.WindowSwitching)
        {
            ReadSwitched(ref reader, info);
        }
        else
        {
            ReadPlain(ref reader, info);
        }

        info.Preflag = isMpeg1 ? reader.ReadBit() : info.ScalefacCompress >= Mpeg2PreflagFrom;
        info.ScalefacScale = (int)reader.Read(1);
        info.Count1Table = (int)reader.Read(1);
        return info.BigValues <= MostBigValues && (!info.WindowSwitching || info.BlockType != 0);
    }

    /// <summary>Reads the fields of a granule that uses a block type other than the normal long window.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="info">Receives the information.</param>
    private static void ReadSwitched(ref MsbBitReader reader, Mp3GranuleChannel info)
    {
        info.BlockType = (int)reader.Read(BlockTypeBits);
        info.MixedBlock = reader.ReadBit();
        info.TableSelect[0] = (int)reader.Read(TableBits);
        info.TableSelect[1] = (int)reader.Read(TableBits);
        info.TableSelect[2] = 0;
        for (var window = 0; window < info.SubblockGain.Length; window++)
        {
            info.SubblockGain[window] = (int)reader.Read(SubblockGainBits);
        }

        info.Region0Count = SwitchedRegion0Count;
        info.Region1Count = SwitchedRegion1Count;
    }

    /// <summary>Reads the fields of a granule that uses the normal long window.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="info">Receives the information.</param>
    private static void ReadPlain(ref MsbBitReader reader, Mp3GranuleChannel info)
    {
        info.BlockType = 0;
        info.MixedBlock = false;
        for (var region = 0; region < info.TableSelect.Length; region++)
        {
            info.TableSelect[region] = (int)reader.Read(TableBits);
        }

        info.SubblockGain.AsSpan().Clear();
        info.Region0Count = (int)reader.Read(Region0Bits);
        info.Region1Count = (int)reader.Read(Region1Bits);
    }

    /// <summary>Reads the fields at the start of the side information: where the main data starts and which scale factor groups repeat.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="header">The frame header.</param>
    private void ReadFrameFields(ref MsbBitReader reader, in Mp3FrameHeader header)
    {
        var mono = header.Channels == 1;
        if (!header.IsMpeg1)
        {
            MainDataBegin = (int)reader.Read(Mpeg2MainDataBits);
            reader.Skip(mono ? Mpeg2MonoPrivate : Mpeg2StereoPrivate);
            return;
        }

        MainDataBegin = (int)reader.Read(Mpeg1MainDataBits);
        reader.Skip(mono ? Mpeg1MonoPrivate : Mpeg1StereoPrivate);
        for (var channel = 0; channel < header.Channels; channel++)
        {
            _scfsi[channel] = 0;
            for (var group = 0; group < ScfsiGroups; group++)
            {
                _scfsi[channel] |= reader.ReadBit() ? 1 << group : 0;
            }
        }
    }
}
