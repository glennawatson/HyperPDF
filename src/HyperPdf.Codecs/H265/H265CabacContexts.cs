// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Names where the context variables of each syntax element start in the context array, and sets them up for a slice (9.3.2.2).</summary>
internal static class H265CabacContexts
{
    /// <summary>The context of sao_merge_left_flag and sao_merge_up_flag.</summary>
    internal const int SaoMerge = 0;

    /// <summary>The context of the first bin of sao_type_idx_luma and sao_type_idx_chroma.</summary>
    internal const int SaoType = 1;

    /// <summary>The three contexts of split_cu_flag.</summary>
    internal const int SplitCu = 2;

    /// <summary>The context of cu_transquant_bypass_flag.</summary>
    internal const int TransquantBypass = 5;

    /// <summary>The three contexts of cu_skip_flag.</summary>
    internal const int CuSkip = 6;

    /// <summary>The context of pred_mode_flag.</summary>
    internal const int PredMode = 9;

    /// <summary>The four contexts of part_mode.</summary>
    internal const int PartMode = 10;

    /// <summary>The context of prev_intra_luma_pred_flag.</summary>
    internal const int PrevIntraLumaPred = 14;

    /// <summary>The context of intra_chroma_pred_mode.</summary>
    internal const int IntraChromaPredMode = 15;

    /// <summary>The context of rqt_root_cbf.</summary>
    internal const int RqtRootCbf = 16;

    /// <summary>The context of merge_flag.</summary>
    internal const int MergeFlag = 17;

    /// <summary>The context of merge_idx.</summary>
    internal const int MergeIdx = 18;

    /// <summary>The five contexts of inter_pred_idc.</summary>
    internal const int InterPredIdc = 19;

    /// <summary>The two contexts of ref_idx_l0 and ref_idx_l1.</summary>
    internal const int RefIdx = 24;

    /// <summary>The context of mvp_l0_flag and mvp_l1_flag.</summary>
    internal const int MvpFlag = 26;

    /// <summary>The three contexts of split_transform_flag.</summary>
    internal const int SplitTransform = 27;

    /// <summary>The two contexts of cbf_luma.</summary>
    internal const int CbfLuma = 30;

    /// <summary>The five contexts of cbf_cb and cbf_cr.</summary>
    internal const int CbfChroma = 32;

    /// <summary>The context of abs_mvd_greater0_flag.</summary>
    internal const int AbsMvdGreater0 = 37;

    /// <summary>The context of abs_mvd_greater1_flag.</summary>
    internal const int AbsMvdGreater1 = 38;

    /// <summary>The two contexts of cu_qp_delta_abs.</summary>
    internal const int CuQpDeltaAbs = 39;

    /// <summary>The two contexts of transform_skip_flag: luma, then chroma.</summary>
    internal const int TransformSkip = 41;

    /// <summary>The eighteen contexts of last_sig_coeff_x_prefix.</summary>
    internal const int LastSigCoeffX = 43;

    /// <summary>The eighteen contexts of last_sig_coeff_y_prefix.</summary>
    internal const int LastSigCoeffY = 61;

    /// <summary>The four contexts of coded_sub_block_flag.</summary>
    internal const int CodedSubBlock = 79;

    /// <summary>The 44 contexts of sig_coeff_flag: 27 for luma, 15 for chroma, then one for skipped luma and one for skipped chroma.</summary>
    internal const int SigCoeff = 83;

    /// <summary>The 24 contexts of coeff_abs_level_greater1_flag.</summary>
    internal const int Greater1 = 127;

    /// <summary>The six contexts of coeff_abs_level_greater2_flag.</summary>
    internal const int Greater2 = 151;

    /// <summary>The two contexts of explicit_rdpcm_flag.</summary>
    internal const int ExplicitRdpcm = 157;

    /// <summary>The two contexts of explicit_rdpcm_dir_flag.</summary>
    internal const int ExplicitRdpcmDir = 159;

    /// <summary>The context of cu_chroma_qp_offset_flag.</summary>
    internal const int CuChromaQpOffsetFlag = 161;

    /// <summary>The context of cu_chroma_qp_offset_idx.</summary>
    internal const int CuChromaQpOffsetIdx = 162;

    /// <summary>The eight contexts of log2_res_scale_abs_plus1.</summary>
    internal const int Log2ResScale = 163;

    /// <summary>The two contexts of res_scale_sign_flag.</summary>
    internal const int ResScaleSign = 171;

    /// <summary>The number of context variables.</summary>
    internal const int Count = 173;

    /// <summary>The largest quantiser the initialisation uses.</summary>
    private const int MaxInitQp = 51;

    /// <summary>The shift that finds the slope from an initValue.</summary>
    private const int SlopeShift = 4;

    /// <summary>The mask that finds the offset from an initValue.</summary>
    private const int OffsetMask = 15;

    /// <summary>The slope's step.</summary>
    private const int SlopeStep = 5;

    /// <summary>The slope's base.</summary>
    private const int SlopeBase = 45;

    /// <summary>The shift of the offset index.</summary>
    private const int OffsetShift = 3;

    /// <summary>The offset's base.</summary>
    private const int OffsetBase = 16;

    /// <summary>The shift that scales the slope by the quantiser.</summary>
    private const int QpShift = 4;

    /// <summary>The smallest state the clip allows.</summary>
    private const int MinState = 1;

    /// <summary>The largest state the clip allows.</summary>
    private const int MaxState = 126;

    /// <summary>The largest state that has the more probable symbol 0.</summary>
    private const int MaxZeroState = 63;

    /// <summary>The initialisation type of P slices, or of B slices with cabac_init_flag set.</summary>
    private const int TypeFirst = 1;

    /// <summary>The initialisation type of B slices, or of P slices with cabac_init_flag set.</summary>
    private const int TypeSecond = 2;

    /// <summary>The shift of the probability state in a stored context.</summary>
    private const int StateShift = 1;

    /// <summary>Picks the initialisation type of a slice (9-7).</summary>
    /// <param name="sliceType">0 for B, 1 for P, 2 for I.</param>
    /// <param name="cabacInit">The cabac_init_flag.</param>
    /// <returns>0, 1 or 2.</returns>
    internal static int InitTypeOf(int sliceType, bool cabacInit)
    {
        if (sliceType == H265SliceType.I)
        {
            return 0;
        }

        var swap = sliceType == H265SliceType.P ? cabacInit : !cabacInit;
        return swap ? TypeSecond : TypeFirst;
    }

    /// <summary>Sets every context variable to its starting state for a slice (9-5 and 9-6).</summary>
    /// <param name="states">Receives the states, one byte each: the probability state in the high bits and the more probable symbol in the low bit.</param>
    /// <param name="initType">The initialisation type from <see cref="InitTypeOf"/>.</param>
    /// <param name="sliceQp">The quantiser the slice starts from.</param>
    internal static void Initialize(Span<byte> states, int initType, int sliceQp)
    {
        var values = initType switch
        {
            0 => H265CabacInitValues.Intra,
            TypeFirst => H265CabacInitValues.First,
            _ => H265CabacInitValues.Second,
        };
        var qp = Math.Clamp(sliceQp, 0, MaxInitQp);
        for (var i = 0; i < Count; i++)
        {
            var slope = ((values[i] >> SlopeShift) * SlopeStep) - SlopeBase;
            var offset = ((values[i] & OffsetMask) << OffsetShift) - OffsetBase;
            var pre = Math.Clamp(((slope * qp) >> QpShift) + offset, MinState, MaxState);
            var mps = pre > MaxZeroState ? 1 : 0;
            var state = mps == 1 ? pre - MaxZeroState - 1 : MaxZeroState - pre;
            states[i] = (byte)((state << StateShift) | mps);
        }
    }
}
