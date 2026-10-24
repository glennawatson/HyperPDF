// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H264;

/// <summary>Reads the macroblocks of intra slices coded with CAVLC and has them reconstructed.</summary>
[DebuggerDisplay("H264IntraSliceDecoder")]
internal sealed class H264IntraSliceDecoder
{
    /// <summary>The largest chroma prediction mode.</summary>
    private const int MaxChromaMode = 3;

    /// <summary>The picture being decoded.</summary>
    private readonly H264Picture _picture;

    /// <summary>The picture parameter set.</summary>
    private readonly H264Pps _pps;

    /// <summary>Makes the samples of the macroblocks.</summary>
    private readonly H264IntraReconstructor _reconstructor;

    /// <summary>The number of the slice being decoded.</summary>
    private readonly int _slice;

    /// <summary>The macroblock being decoded.</summary>
    private readonly H264CurrentMb _mb;

    /// <summary>The luma quantiser of the macroblock before.</summary>
    private int _qp;

    /// <summary>Initializes a new instance of the <see cref="H264IntraSliceDecoder"/> class.</summary>
    /// <param name="picture">The picture to decode into.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <param name="scale">The level scale factors.</param>
    /// <param name="slice">The number of the slice.</param>
    /// <param name="startQp">The quantiser the slice starts from.</param>
    /// <param name="mb">The macroblock to decode into, which the decoder keeps from slice to slice.</param>
    internal H264IntraSliceDecoder(H264Picture picture, H264Pps pps, H264LevelScale scale, int slice, int startQp, H264CurrentMb mb)
    {
        _mb = mb;
        _picture = picture;
        _pps = pps;
        _reconstructor = new(picture, pps, scale);
        _slice = slice;
        _qp = startQp;
    }

    /// <summary>Gets or sets the luma quantiser of the macroblock before.</summary>
    internal int Qp
    {
        get => _qp;
        set => _qp = value;
    }

    /// <summary>Decodes the macroblocks of a slice.</summary>
    /// <param name="rbsp">The payload of the slice's NAL unit.</param>
    /// <param name="dataBit">The bit index where the slice data starts.</param>
    /// <param name="firstMb">The address of the slice's first macroblock.</param>
    /// <exception cref="InvalidDataException">The slice data is damaged.</exception>
    /// <exception cref="NotSupportedException">The slice uses a feature this decoder does not read.</exception>
    internal void Decode(ReadOnlySpan<byte> rbsp, long dataBit, int firstMb)
    {
        var reader = new MsbBitReader(rbsp);
        reader.Seek(dataBit);
        var stop = ExpGolomb.StopBit(rbsp);
        var total = _picture.MbWidth * _picture.MbHeight;
        var address = firstMb;
        do
        {
            if (address >= total)
            {
                throw new InvalidDataException("An H.264 slice has more macroblocks than the picture.");
            }

            DecodeMacroblock(ref reader, address, (int)ExpGolomb.ReadUe(ref reader));
            address++;
        }
        while (ExpGolomb.MoreData(reader.BitPosition, stop) && !reader.IsOverrun);
        if (reader.IsOverrun)
        {
            throw new InvalidDataException("An H.264 slice is cut short.");
        }
    }

    /// <summary>Reads and reconstructs one macroblock whose mb_type has been read.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="address">The macroblock's address.</param>
    /// <param name="type">The macroblock type, 0 to 25 as in an I slice.</param>
    /// <exception cref="InvalidDataException">The macroblock is damaged.</exception>
    /// <exception cref="NotSupportedException">The macroblock uses a feature this decoder does not read.</exception>
    internal void DecodeMacroblock(ref MsbBitReader reader, int address, int type)
    {
        var mb = _mb;
        mb.Address = address;
        mb.X = address % _picture.MbWidth;
        mb.Y = address / _picture.MbWidth;
        mb.ClearCoefficients();
        _picture.SliceOfMb[address] = _slice;
        Array.Clear(_picture.Counts, address * H264Picture.CountsPerMb, H264Picture.CountsPerMb);
        _picture.ClearMotion(address);
        mb.Neighbours = H264Availability.ForIntra(_picture, mb.X, mb.Y, _slice, _pps.ConstrainedIntraPred);
        if (type > H264IntraMbType.Pcm)
        {
            throw new InvalidDataException("An H.264 macroblock type is not valid for an intra slice.");
        }

        if (type == H264IntraMbType.Pcm)
        {
            H264PcmSamples.Read(_picture, mb, ref reader);
            return;
        }

        ReadHeader(ref reader, type);
        _picture.Kinds[address] = mb.Kind;
        if (mb.Kind == H264MbKind.Intra16x16 || mb.CbpLuma != 0 || mb.CbpChroma != 0)
        {
            _qp = (_qp + ExpGolomb.ReadSe(ref reader) + H264Layout.QpRange) % H264Layout.QpRange;
            H264CavlcResidual.Read(ref reader, _picture, mb, _slice);
        }

        _picture.Qp[address] = (sbyte)_qp;
        _reconstructor.Reconstruct(mb, _qp);
    }

    /// <summary>Reads the chroma prediction mode.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The mode.</returns>
    /// <exception cref="InvalidDataException">The mode is not valid.</exception>
    private static int ReadChromaMode(ref MsbBitReader reader)
    {
        var mode = (int)ExpGolomb.ReadUe(ref reader);
        return mode <= MaxChromaMode ? mode : throw new InvalidDataException("An H.264 chroma prediction mode is not valid.");
    }

    /// <summary>Reads the macroblock header that follows the macroblock type.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="type">The macroblock type.</param>
    private void ReadHeader(ref MsbBitReader reader, int type)
    {
        if (type == H264IntraMbType.IntraNxN)
        {
            ReadIntra4x4(ref reader);
            return;
        }

        H264IntraMbType.ApplyIntra16x16(_picture, _mb, type);
        _mb.ChromaMode = ReadChromaMode(ref reader);
    }

    /// <summary>Reads the macroblock header of an Intra 4x4 macroblock: the prediction modes and the coded block pattern.</summary>
    /// <param name="reader">The bits.</param>
    /// <exception cref="InvalidDataException">The coded block pattern is not valid.</exception>
    private void ReadIntra4x4(ref MsbBitReader reader)
    {
        var mb = _mb;
        mb.Kind = H264MbKind.Intra4x4;
        mb.Transform8x8 = _pps.Transform8x8 && reader.ReadBit();
        _picture.Transform8x8[mb.Address] = mb.Transform8x8;
        var blocks = mb.Transform8x8 ? H264IntraModes.Blocks8x8 : H264Layout.LumaBlocks;
        for (var block = 0; block < blocks; block++)
        {
            var predicted = H264IntraModes.Predict(_picture, mb, block);
            var mode = predicted;
            if (!reader.ReadBit())
            {
                var remaining = (int)reader.Read(H264Layout.RemainingModeBits);
                mode = remaining < predicted ? remaining : remaining + 1;
            }

            H264IntraModes.Store(_picture, mb, block, mode);
        }

        mb.ChromaMode = ReadChromaMode(ref reader);
        var code = (int)ExpGolomb.ReadUe(ref reader);
        if (code > H264CodedBlockPattern.MaxCodeNumber)
        {
            throw new InvalidDataException("An H.264 coded block pattern is not valid.");
        }

        var pattern = H264CodedBlockPattern.Lookup(code, true);
        mb.CbpLuma = pattern & H264CodedBlockPattern.LumaMask;
        mb.CbpChroma = pattern >> H264CodedBlockPattern.ChromaShift;
    }
}
