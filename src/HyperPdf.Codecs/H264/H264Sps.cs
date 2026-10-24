// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H264;

/// <summary>A sequence parameter set: what is the same for every picture of a coded video sequence.</summary>
/// <param name="ProfileIdc">The profile.</param>
/// <param name="LevelIdc">The level.</param>
/// <param name="Id">The set's number.</param>
/// <param name="ChromaFormat">How the colour planes are sampled: 0 for grey, 1 for 4:2:0, 2 for 4:2:2, 3 for 4:4:4.</param>
/// <param name="SeparateColourPlanes">Whether the three colour planes of 4:4:4 video are coded as three pictures.</param>
/// <param name="BitDepthLuma">The bits of a luma sample.</param>
/// <param name="BitDepthChroma">The bits of a chroma sample.</param>
/// <param name="TransformBypass">Whether blocks may bypass the transform when the quantiser is 0.</param>
/// <param name="SequenceScaling">The scaling lists the set carries, or <see langword="null"/> when it carries none.</param>
/// <param name="Log2MaxFrameNum">The bits of the frame number.</param>
/// <param name="Poc">How the picture order count is worked out.</param>
/// <param name="MaxRefFrames">The most reference frames.</param>
/// <param name="GapsAllowed">Whether frame numbers may skip.</param>
/// <param name="WidthInMbs">The width of the pictures in macroblocks.</param>
/// <param name="HeightInMapUnits">The height of the pictures in map units: macroblocks, or pairs of them when fields can be coded.</param>
/// <param name="FrameMbsOnly">Whether every picture is a frame of progressive macroblocks.</param>
/// <param name="Mbaff">Whether frames can code macroblocks as field or frame pairs.</param>
/// <param name="Direct8x8Inference">Whether direct prediction works on 8x8 blocks.</param>
/// <param name="Crop">The cropping.</param>
/// <param name="MaxNumReorderFrames">The most pictures that precede any picture in decoding order and follow it in output order, or -1 when the sequence does not say.</param>
[DebuggerDisplay("H264Sps: id {Id}, {WidthInMbs}x{HeightInMapUnits} MBs, profile {ProfileIdc}")]
internal sealed record H264Sps(
    int ProfileIdc,
    int LevelIdc,
    int Id,
    int ChromaFormat,
    bool SeparateColourPlanes,
    int BitDepthLuma,
    int BitDepthChroma,
    bool TransformBypass,
    H264ScalingLists? SequenceScaling,
    int Log2MaxFrameNum,
    H264PocSettings Poc,
    int MaxRefFrames,
    bool GapsAllowed,
    int WidthInMbs,
    int HeightInMapUnits,
    bool FrameMbsOnly,
    bool Mbaff,
    bool Direct8x8Inference,
    VideoCrop Crop,
    int MaxNumReorderFrames)
{
    /// <summary>The most frames a decoded picture buffer ever holds.</summary>
    private const int MaxDpbFrames = 16;

    /// <summary>Gets the scaling lists of the sequence: the ones it carries, or lists that scale nothing.</summary>
    internal H264ScalingLists Scaling => SequenceScaling ?? H264ScalingLists.FlatLists;

    /// <summary>Gets the most frames the decoded picture buffer may hold for the level and picture size, at most 16 (Table A-1).</summary>
    internal int DpbFrames
    {
        get
        {
            var levels = LevelIdcs;
            var limits = MaxDpbMbs;
            var index = 0;
            while (index < levels.Length - 1 && levels[index] < LevelIdc)
            {
                index++;
            }

            return Math.Clamp(limits[index] / (WidthInMbs * HeightInMbs), 1, MaxDpbFrames);
        }
    }

    /// <summary>Gets the height of the pictures in macroblocks.</summary>
    internal int HeightInMbs => FrameMbsOnly ? HeightInMapUnits : HeightInMapUnits << 1;

    /// <summary>Gets the type of colour array the slices use: the chroma format, or 0 when the planes are coded separately.</summary>
    internal int ChromaArrayType => SeparateColourPlanes ? 0 : ChromaFormat;

    /// <summary>Gets the level_idc values of Table A-1, in order.</summary>
    private static ReadOnlySpan<byte> LevelIdcs => [0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x14, 0x15, 0x16, 0x1E, 0x1F, 0x20, 0x28, 0x29, 0x2A, 0x32, 0x33, 0x34];

    /// <summary>Gets the macroblocks the decoded picture buffer may hold at each of those levels (MaxDpbMbs).</summary>
    private static ReadOnlySpan<int> MaxDpbMbs =>
    [
        0x018C, 0x018C, 0x0384, 0x0948, 0x0948, 0x0948, 0x1290, 0x1FA4, 0x1FA4, 0x4650, 0x5000, 0x8000, 0x8000, 0x8800, 0x1AF40, 0x2D000, 0x2D000,
    ];
}
