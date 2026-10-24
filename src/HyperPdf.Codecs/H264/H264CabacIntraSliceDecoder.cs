// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H264;

/// <summary>Reads the macroblocks of intra slices coded with CABAC and has them reconstructed.</summary>
[DebuggerDisplay("H264CabacIntraSliceDecoder")]
internal sealed class H264CabacIntraSliceDecoder
{
    /// <summary>The first context of mb_type for I slices.</summary>
    private const int MbTypeBase = 3;

    /// <summary>The context of the bin that says whether a luma coefficient is coded in an Intra 16x16 macroblock.</summary>
    private const int MbTypeLuma = 6;

    /// <summary>The context of the bin that says whether chroma coefficients are coded.</summary>
    private const int MbTypeChroma = 7;

    /// <summary>The context of the bin that says whether chroma AC coefficients are coded.</summary>
    private const int MbTypeChromaAc = 8;

    /// <summary>The context of the first bin of the Intra 16x16 prediction mode.</summary>
    private const int MbTypeModeHigh = 9;

    /// <summary>The context of the second bin of the Intra 16x16 prediction mode.</summary>
    private const int MbTypeModeLow = 10;

    /// <summary>The context offset of the chroma bins of mb_type for an intra macroblock in a P or B slice.</summary>
    private const int InterIntraChroma = 2;

    /// <summary>The context offset of the prediction mode bins of mb_type for an intra macroblock in a P or B slice.</summary>
    private const int InterIntraMode = 3;

    /// <summary>The first context of transform_size_8x8_flag.</summary>
    private const int Transform8x8Context = 399;

    /// <summary>The coefficients of an 8x8 block.</summary>
    private const int LargeBlock = 64;

    /// <summary>The context of prev_intra4x4_pred_mode_flag.</summary>
    private const int PrevIntraModeContext = 68;

    /// <summary>The context of the bins of rem_intra4x4_pred_mode.</summary>
    private const int RemIntraModeContext = 69;

    /// <summary>The first context of intra_chroma_pred_mode.</summary>
    private const int ChromaModeBase = 64;

    /// <summary>The context of the bins of intra_chroma_pred_mode after the first.</summary>
    private const int ChromaModeLater = 67;

    /// <summary>The first context of the luma bins of coded_block_pattern.</summary>
    private const int CbpLumaBase = 73;

    /// <summary>The first context of the chroma bins of coded_block_pattern.</summary>
    private const int CbpChromaBase = 77;

    /// <summary>The first context of mb_qp_delta.</summary>
    private const int QpDeltaBase = 60;

    /// <summary>The context of the second bin of mb_qp_delta.</summary>
    private const int QpDeltaSecond = 62;

    /// <summary>The context of the later bins of mb_qp_delta.</summary>
    private const int QpDeltaLater = 63;

    /// <summary>The most bins a valid mb_qp_delta has.</summary>
    private const int MaxQpDeltaBins = 104;

    /// <summary>The Intra 16x16 mb_types that share a chroma pattern.</summary>
    private const int ModesPerGroup = 4;

    /// <summary>The Intra 16x16 mb_types that have the same luma flag.</summary>
    private const int TypesPerLumaFlag = 12;

    /// <summary>The luma blocks in an 8x8 group, which share a bit of the coded block pattern.</summary>
    private const int BlocksPerGroup = 4;

    /// <summary>The 8x8 groups in a macroblock.</summary>
    private const int Groups = 4;

    /// <summary>The coefficients of an Intra 16x16 DC block or of a luma block.</summary>
    private const int FullBlock = 16;

    /// <summary>The coefficients of a chroma DC block.</summary>
    private const int ChromaDcCoefficients = 4;

    /// <summary>The coded block pattern chroma value that has AC coefficients.</summary>
    private const int ChromaWithAc = 2;

    /// <summary>The largest chroma prediction mode.</summary>
    private const int MaxChromaMode = 3;

    /// <summary>The shift of the chroma value in a stored coded block pattern.</summary>
    private const int CbpChromaShift = 4;

    /// <summary>The bits of rem_intra4x4_pred_mode.</summary>
    private const int RemModeBits = 3;

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

    /// <summary>The state of each context.</summary>
    private readonly byte[] _contexts;

    /// <summary>The luma quantiser of the macroblock before.</summary>
    private int _qp;

    /// <summary>Initializes a new instance of the <see cref="H264CabacIntraSliceDecoder"/> class for an I slice.</summary>
    /// <param name="picture">The picture to decode into.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <param name="scale">The level scale factors.</param>
    /// <param name="slice">The number of the slice.</param>
    /// <param name="startQp">The quantiser the slice starts from.</param>
    /// <param name="work">The decoder's working memory.</param>
    internal H264CabacIntraSliceDecoder(H264Picture picture, H264Pps pps, H264LevelScale scale, int slice, int startQp, H264SliceWork work)
        : this(picture, pps, scale, slice, startQp, work.Mb, work.Contexts)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="H264CabacIntraSliceDecoder"/> class that shares its macroblock and contexts with the decoder of a P slice.</summary>
    /// <param name="picture">The picture to decode into.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <param name="scale">The level scale factors.</param>
    /// <param name="slice">The number of the slice.</param>
    /// <param name="startQp">The quantiser the slice starts from.</param>
    /// <param name="mb">The macroblock being decoded.</param>
    /// <param name="contexts">The state of each context.</param>
    internal H264CabacIntraSliceDecoder(H264Picture picture, H264Pps pps, H264LevelScale scale, int slice, int startQp, H264CurrentMb mb, byte[] contexts)
    {
        _picture = picture;
        _pps = pps;
        _reconstructor = new(picture, pps, scale);
        _slice = slice;
        _qp = startQp;
        _mb = mb;
        _contexts = contexts;
    }

    /// <summary>Gets or sets the luma quantiser of the macroblock before.</summary>
    internal int Qp
    {
        get => _qp;
        set => _qp = value;
    }

    /// <summary>Gets or sets a value indicating whether the macroblock before had a mb_qp_delta other than zero.</summary>
    internal bool PreviousHadQpDelta { get; set; }

    /// <summary>Decodes the macroblocks of a slice.</summary>
    /// <param name="rbsp">The payload of the slice's NAL unit.</param>
    /// <param name="dataBit">The bit index where the slice data starts (before the alignment bits).</param>
    /// <param name="firstMb">The address of the slice's first macroblock.</param>
    /// <exception cref="InvalidDataException">The slice data is damaged.</exception>
    /// <exception cref="NotSupportedException">The slice uses a feature this decoder does not read.</exception>
    internal void Decode(ReadOnlySpan<byte> rbsp, long dataBit, int firstMb)
    {
        H264CabacContexts.Initialize(_contexts, true, 0, _qp);
        var engine = new CabacEngine(rbsp, (dataBit + H264Layout.SampleBits - 1) / H264Layout.SampleBits * H264Layout.SampleBits, _contexts);
        var total = _picture.MbWidth * _picture.MbHeight;
        var address = firstMb;
        do
        {
            if (address >= total)
            {
                throw new InvalidDataException("An H.264 slice has more macroblocks than the picture.");
            }

            DecodeMacroblock(ref engine, rbsp, address);
            address++;
        }
        while (engine.DecodeTerminate() == 0 && !engine.IsOverrun);
        if (engine.IsOverrun)
        {
            throw new InvalidDataException("An H.264 slice is cut short.");
        }
    }

    /// <summary>Reads and reconstructs an intra macroblock of a P or B slice, after the slice's mb_type prefix said the macroblock is intra.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="rbsp">The slice's payload, for macroblocks of raw samples.</param>
    /// <param name="address">The macroblock's address.</param>
    /// <param name="baseContext">The first context of the rest of mb_type: 17 in a P slice, 32 in a B slice.</param>
    internal void DecodeIntraInInterSlice(ref CabacEngine engine, ReadOnlySpan<byte> rbsp, int address, int baseContext)
    {
        BeginMacroblock(address);
        FinishMacroblock(ref engine, rbsp, address, ReadMbTypeInInterSlice(ref engine, baseContext));
    }

    /// <summary>Reads the coded block pattern, the quantiser change and the residual of an inter macroblock whose motion has been read.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="allow8x8">Whether the macroblock's partitions are all at least 8x8, so it may use the 8x8 transform.</param>
    internal void ReadInterResidual(ref CabacEngine engine, bool allow8x8)
    {
        var left = H264CabacNeighbours.Left(_picture, _mb, _slice);
        var top = H264CabacNeighbours.Top(_picture, _mb, _slice);
        ReadCodedBlockPattern(ref engine, left, top);
        _picture.Cbp[_mb.Address] = (byte)(_mb.CbpLuma | (_mb.CbpChroma << CbpChromaShift));
        if (allow8x8 && _pps.Transform8x8 && _mb.CbpLuma != 0)
        {
            _mb.Transform8x8 = engine.DecodeDecision(Transform8x8Context + H264CabacNeighbours.Transform8x8Inc(_picture, left, top)) != 0;
            _picture.Transform8x8[_mb.Address] = _mb.Transform8x8;
        }

        if (_mb.CbpLuma != 0 || _mb.CbpChroma != 0)
        {
            ReadQpDelta(ref engine);
            ReadResidual(ref engine, left, top);
        }
        else
        {
            PreviousHadQpDelta = false;
        }

        _picture.Qp[_mb.Address] = (sbyte)_qp;
    }

    /// <summary>Reads the part of mb_type that follows the prefix of an intra macroblock in a P or B slice (Table 9-37, contexts from 17 or 32).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="baseContext">The first context.</param>
    /// <returns>The mb_type as it would be in an I slice, 0 to 25.</returns>
    private static int ReadMbTypeInInterSlice(ref CabacEngine engine, int baseContext)
    {
        if (engine.DecodeDecision(baseContext) == 0)
        {
            return H264IntraMbType.IntraNxN;
        }

        if (engine.DecodeTerminate() != 0)
        {
            return H264IntraMbType.Pcm;
        }

        var luma = engine.DecodeDecision(baseContext + 1);
        var chroma = 0;
        if (engine.DecodeDecision(baseContext + InterIntraChroma) != 0)
        {
            chroma = 1 + engine.DecodeDecision(baseContext + InterIntraChroma);
        }

        var mode = (engine.DecodeDecision(baseContext + InterIntraMode) << 1) | engine.DecodeDecision(baseContext + InterIntraMode);
        return 1 + mode + (ModesPerGroup * chroma) + (TypesPerLumaFlag * luma);
    }

    /// <summary>Clears what the neighbours of a macroblock will read from the macroblock before it is decoded.</summary>
    /// <param name="address">The macroblock's address.</param>
    private void BeginMacroblock(int address)
    {
        var mb = _mb;
        mb.Address = address;
        mb.X = address % _picture.MbWidth;
        mb.Y = address / _picture.MbWidth;
        mb.ClearCoefficients();
        _picture.SliceOfMb[address] = _slice;
        Array.Clear(_picture.Counts, address * H264Picture.CountsPerMb, H264Picture.CountsPerMb);
        _picture.ClearMotion(address);
        _picture.Cbp[address] = 0;
        _picture.ChromaModes[address] = 0;
        _picture.DcFlags[address] = 0;
        mb.Neighbours = H264Availability.ForIntra(_picture, mb.X, mb.Y, _slice, _pps.ConstrainedIntraPred);
    }

    /// <summary>Reads and reconstructs one macroblock of an I slice.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="rbsp">The slice's payload, for macroblocks of raw samples.</param>
    /// <param name="address">The macroblock's address.</param>
    private void DecodeMacroblock(ref CabacEngine engine, ReadOnlySpan<byte> rbsp, int address)
    {
        BeginMacroblock(address);
        var left = H264CabacNeighbours.Left(_picture, _mb, _slice);
        var top = H264CabacNeighbours.Top(_picture, _mb, _slice);
        FinishMacroblock(ref engine, rbsp, address, ReadMbType(ref engine, left, top));
    }

    /// <summary>Reads the rest of an intra macroblock once its mb_type is known, and reconstructs it.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="rbsp">The slice's payload, for macroblocks of raw samples.</param>
    /// <param name="address">The macroblock's address.</param>
    /// <param name="type">The mb_type, 0 to 25.</param>
    private void FinishMacroblock(ref CabacEngine engine, ReadOnlySpan<byte> rbsp, int address, int type)
    {
        var mb = _mb;
        var left = H264CabacNeighbours.Left(_picture, mb, _slice);
        var top = H264CabacNeighbours.Top(_picture, mb, _slice);
        if (type == H264IntraMbType.Pcm)
        {
            ReadPcm(ref engine, rbsp);
            return;
        }

        ReadHeader(ref engine, type, left, top);
        _picture.Kinds[address] = mb.Kind;
        _picture.Cbp[address] = (byte)(mb.CbpLuma | (mb.CbpChroma << CbpChromaShift));
        _picture.ChromaModes[address] = (byte)mb.ChromaMode;
        if (mb.Kind == H264MbKind.Intra16x16 || mb.CbpLuma != 0 || mb.CbpChroma != 0)
        {
            ReadQpDelta(ref engine);
            ReadResidual(ref engine, left, top);
        }
        else
        {
            PreviousHadQpDelta = false;
        }

        _picture.Qp[address] = (sbyte)_qp;
        _reconstructor.Reconstruct(mb, _qp);
    }

    /// <summary>Reads mb_type of an I slice (Table 9-36).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="left">The address of the left macroblock, or -1.</param>
    /// <param name="top">The address of the macroblock above, or -1.</param>
    /// <returns>The mb_type, 0 to 25.</returns>
    private int ReadMbType(ref CabacEngine engine, int left, int top)
    {
        if (engine.DecodeDecision(MbTypeBase + H264CabacNeighbours.MbTypeInc(_picture, left, top)) == 0)
        {
            return H264IntraMbType.IntraNxN;
        }

        if (engine.DecodeTerminate() != 0)
        {
            return H264IntraMbType.Pcm;
        }

        var luma = engine.DecodeDecision(MbTypeLuma);
        var chroma = 0;
        if (engine.DecodeDecision(MbTypeChroma) != 0)
        {
            chroma = 1 + engine.DecodeDecision(MbTypeChromaAc);
        }

        var mode = (engine.DecodeDecision(MbTypeModeHigh) << 1) | engine.DecodeDecision(MbTypeModeLow);
        return 1 + mode + (ModesPerGroup * chroma) + (TypesPerLumaFlag * luma);
    }

    /// <summary>Reads a macroblock of raw samples and starts the arithmetic decoder again after them.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="rbsp">The slice's payload.</param>
    /// <exception cref="InvalidDataException">The slice is cut short.</exception>
    private void ReadPcm(ref CabacEngine engine, ReadOnlySpan<byte> rbsp)
    {
        var reader = new MsbBitReader(rbsp);
        reader.Seek(engine.BitPosition);
        H264PcmSamples.Read(_picture, _mb, ref reader);
        if (reader.IsOverrun)
        {
            throw new InvalidDataException("An H.264 slice is cut short.");
        }

        _picture.DcFlags[_mb.Address] = H264CabacNeighbours.AllDcFlags;
        PreviousHadQpDelta = false;
        engine.Restart(reader.BitPosition);
    }

    /// <summary>Reads the macroblock header that follows mb_type.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="type">The mb_type.</param>
    /// <param name="left">The address of the left macroblock, or -1.</param>
    /// <param name="top">The address of the macroblock above, or -1.</param>
    /// <exception cref="NotSupportedException">The macroblock uses the 8x8 transform.</exception>
    private void ReadHeader(ref CabacEngine engine, int type, int left, int top)
    {
        if (type == H264IntraMbType.IntraNxN)
        {
            ReadIntra4x4(ref engine, left, top);
            return;
        }

        H264IntraMbType.ApplyIntra16x16(_picture, _mb, type);
        _mb.ChromaMode = ReadChromaMode(ref engine, left, top);
    }

    /// <summary>Reads the header of an Intra 4x4 macroblock: the prediction modes and the coded block pattern.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="left">The address of the left macroblock, or -1.</param>
    /// <param name="top">The address of the macroblock above, or -1.</param>
    /// <exception cref="NotSupportedException">The macroblock uses the 8x8 transform.</exception>
    private void ReadIntra4x4(ref CabacEngine engine, int left, int top)
    {
        var mb = _mb;
        mb.Kind = H264MbKind.Intra4x4;
        mb.Transform8x8 = _pps.Transform8x8 && engine.DecodeDecision(Transform8x8Context + H264CabacNeighbours.Transform8x8Inc(_picture, left, top)) != 0;
        _picture.Transform8x8[mb.Address] = mb.Transform8x8;
        var blocks = mb.Transform8x8 ? H264IntraModes.Blocks8x8 : H264Layout.LumaBlocks;
        for (var block = 0; block < blocks; block++)
        {
            var predicted = H264IntraModes.Predict(_picture, mb, block);
            var mode = predicted;
            if (engine.DecodeDecision(PrevIntraModeContext) == 0)
            {
                var remaining = 0;
                for (var bit = 0; bit < RemModeBits; bit++)
                {
                    remaining |= engine.DecodeDecision(RemIntraModeContext) << bit;
                }

                mode = remaining < predicted ? remaining : remaining + 1;
            }

            H264IntraModes.Store(_picture, mb, block, mode);
        }

        mb.ChromaMode = ReadChromaMode(ref engine, left, top);
        ReadCodedBlockPattern(ref engine, left, top);
    }

    /// <summary>Reads intra_chroma_pred_mode.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="left">The address of the left macroblock, or -1.</param>
    /// <param name="top">The address of the macroblock above, or -1.</param>
    /// <returns>The mode, 0 to 3.</returns>
    private int ReadChromaMode(ref CabacEngine engine, int left, int top)
    {
        if (engine.DecodeDecision(ChromaModeBase + H264CabacNeighbours.ChromaModeInc(_picture, left, top)) == 0)
        {
            return 0;
        }

        var mode = 1;
        while (mode < MaxChromaMode && engine.DecodeDecision(ChromaModeLater) != 0)
        {
            mode++;
        }

        return mode;
    }

    /// <summary>Reads coded_block_pattern.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="left">The address of the left macroblock, or -1.</param>
    /// <param name="top">The address of the macroblock above, or -1.</param>
    private void ReadCodedBlockPattern(ref CabacEngine engine, int left, int top)
    {
        var mb = _mb;
        mb.CbpLuma = 0;
        for (var group = 0; group < Groups; group++)
        {
            var inc = H264CabacNeighbours.CbpLumaInc(_picture, mb, group, left, top);
            mb.CbpLuma |= engine.DecodeDecision(CbpLumaBase + inc) << group;
        }

        mb.CbpChroma = 0;
        if (engine.DecodeDecision(CbpChromaBase + H264CabacNeighbours.CbpChromaInc(_picture, left, top, false)) == 0)
        {
            return;
        }

        mb.CbpChroma = 1 + engine.DecodeDecision(CbpChromaBase + H264CabacNeighbours.CbpChromaInc(_picture, left, top, true));
    }

    /// <summary>Reads mb_qp_delta and applies it.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <exception cref="InvalidDataException">The value is not valid.</exception>
    private void ReadQpDelta(ref CabacEngine engine)
    {
        var count = 0;
        var context = QpDeltaBase + (PreviousHadQpDelta ? 1 : 0);
        while (engine.DecodeDecision(context) != 0)
        {
            count++;
            context = count == 1 ? QpDeltaSecond : QpDeltaLater;
            if (count > MaxQpDeltaBins)
            {
                throw new InvalidDataException("An H.264 quantiser change is not valid.");
            }
        }

        var delta = (count & 1) != 0 ? (count + 1) >> 1 : -(count >> 1);
        PreviousHadQpDelta = delta != 0;
        _qp = (_qp + delta + H264Layout.QpRange) % H264Layout.QpRange;
    }

    /// <summary>Reads the residual blocks of the macroblock.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="left">The address of the left macroblock, or -1.</param>
    /// <param name="top">The address of the macroblock above, or -1.</param>
    private void ReadResidual(ref CabacEngine engine, int left, int top)
    {
        var mb = _mb;
        Span<int> levels = stackalloc int[FullBlock];
        var zigzag = H264Tables.ZigZag4x4;
        var intra16 = mb.Kind == H264MbKind.Intra16x16;
        if (intra16)
        {
            var inc = H264CabacNeighbours.DcFlagInc(_picture, left, top, 0, true);
            if (H264CabacResidual.Read(ref engine, H264CabacResidual.LumaDc, inc, FullBlock, levels) != 0)
            {
                _picture.DcFlags[mb.Address] |= 1;
            }

            for (var scan = 0; scan < FullBlock; scan++)
            {
                mb.LumaDc[zigzag[scan]] = levels[scan];
            }
        }

        ReadLuma(ref engine, levels, intra16);
        ReadChroma(ref engine, levels, left, top);
    }

    /// <summary>Reads the luma blocks.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="levels">A place to read the levels into.</param>
    /// <param name="intra16">Whether the macroblock is Intra 16x16, whose blocks hold AC levels only.</param>
    private void ReadLuma(ref CabacEngine engine, scoped Span<int> levels, bool intra16)
    {
        var mb = _mb;
        var zigzag = H264Tables.ZigZag4x4;
        var first = intra16 ? 1 : 0;
        var limit = intra16 ? H264Layout.AcCoefficients : FullBlock;
        var category = intra16 ? H264CabacResidual.LumaAc : H264CabacResidual.Luma4x4;
        if (mb.Transform8x8)
        {
            ReadLuma8x8(ref engine);
            return;
        }

        for (var block = 0; block < H264Layout.LumaBlocks; block++)
        {
            if ((mb.CbpLuma & (1 << (block / BlocksPerGroup))) == 0)
            {
                continue;
            }

            int column = H264Tables.BlockColumn[block];
            int row = H264Tables.BlockRow[block];
            var inc = H264CabacNeighbours.BlockFlagInc(_picture, mb, _slice, 0, H264Layout.BlocksPerRow, column, row);
            var count = H264CabacResidual.Read(ref engine, category, inc, limit, levels);
            _picture.Counts[(mb.Address * H264Picture.CountsPerMb) + (row * H264Layout.BlocksPerRow) + column] = (byte)count;
            for (var scan = 0; scan < limit; scan++)
            {
                mb.Luma[(block * H264Layout.BlockSamples) + zigzag[scan + first]] = levels[scan];
            }
        }
    }

    /// <summary>Reads the luma blocks of a macroblock that uses the 8x8 transform.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    private void ReadLuma8x8(ref CabacEngine engine)
    {
        var mb = _mb;
        Span<int> levels = stackalloc int[LargeBlock];
        var zigzag = H264Tables.ZigZag8x8;
        for (var group = 0; group < Groups; group++)
        {
            if ((mb.CbpLuma & (1 << group)) == 0)
            {
                continue;
            }

            var count = H264CabacResidual.Read8x8(ref engine, levels);
            for (var scan = 0; scan < LargeBlock; scan++)
            {
                mb.Luma[(group * LargeBlock) + zigzag[scan]] = levels[scan];
            }

            // Neighbouring blocks see the whole 8x8 block as coded, so each of its four 4x4 blocks gets the count.
            for (var part = 0; part < BlocksPerGroup; part++)
            {
                var block = (group * BlocksPerGroup) + part;
                _picture.Counts[(mb.Address * H264Picture.CountsPerMb) + (H264Tables.BlockRow[block] * H264Layout.BlocksPerRow) + H264Tables.BlockColumn[block]] = (byte)count;
            }
        }
    }

    /// <summary>Reads the chroma residual: the DC levels of both components, then the AC blocks.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="levels">A place to read the levels into.</param>
    /// <param name="left">The address of the left macroblock, or -1.</param>
    /// <param name="top">The address of the macroblock above, or -1.</param>
    private void ReadChroma(ref CabacEngine engine, scoped Span<int> levels, int left, int top)
    {
        var mb = _mb;
        if (mb.CbpChroma == 0)
        {
            return;
        }

        for (var component = 0; component < H264Layout.Components; component++)
        {
            var bit = H264CabacNeighbours.ChromaDcFlagFirstBit + component;
            var inc = H264CabacNeighbours.DcFlagInc(_picture, left, top, bit, mb.Kind != H264MbKind.Inter);
            if (H264CabacResidual.Read(ref engine, H264CabacResidual.ChromaDc, inc, ChromaDcCoefficients, levels) != 0)
            {
                _picture.DcFlags[mb.Address] |= (byte)(1 << bit);
            }

            for (var index = 0; index < ChromaDcCoefficients; index++)
            {
                mb.ChromaDc[(component * ChromaDcCoefficients) + index] = levels[index];
            }
        }

        if (mb.CbpChroma >= ChromaWithAc)
        {
            ReadChromaAc(ref engine, levels);
        }
    }

    /// <summary>Reads the chroma AC blocks.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="levels">A place to read the levels into.</param>
    private void ReadChromaAc(ref CabacEngine engine, scoped Span<int> levels)
    {
        var mb = _mb;
        var zigzag = H264Tables.ZigZag4x4;
        for (var component = 0; component < H264Layout.Components; component++)
        {
            for (var block = 0; block < H264Layout.ChromaBlocks; block++)
            {
                var index = (component * H264Layout.ChromaBlocks) + block;
                var first = H264Picture.ChromaCounts + (component * H264Layout.ChromaBlocks);
                var inc = H264CabacNeighbours.BlockFlagInc(_picture, mb, _slice, first, H264Layout.ChromaBlocksPerRow, block % H264Layout.ChromaBlocksPerRow, block / H264Layout.ChromaBlocksPerRow);
                var count = H264CabacResidual.Read(ref engine, H264CabacResidual.ChromaAc, inc, H264Layout.AcCoefficients, levels);
                _picture.Counts[(mb.Address * H264Picture.CountsPerMb) + H264Picture.ChromaCounts + index] = (byte)count;
                for (var scan = 0; scan < H264Layout.AcCoefficients; scan++)
                {
                    mb.ChromaAc[(index * H264Layout.BlockSamples) + zigzag[scan + 1]] = levels[scan];
                }
            }
        }
    }
}
