// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>Reads what an mb_type of an intra slice says about the macroblock (Table 7-11 of ITU-T H.264).</summary>
internal static class H264IntraMbType
{
    /// <summary>The mb_type that says a macroblock is coded in 4x4 blocks.</summary>
    internal const int IntraNxN = 0;

    /// <summary>The mb_type of a macroblock whose samples are sent as they are.</summary>
    internal const int Pcm = 25;

    /// <summary>The first mb_type of Intra 16x16 macroblocks.</summary>
    private const int FirstIntra16x16 = 1;

    /// <summary>The Intra 16x16 mb_types that share a prediction mode.</summary>
    private const int ModesPerGroup = 4;

    /// <summary>The Intra 16x16 mb_types that share a chroma pattern.</summary>
    private const int ChromaGroups = 3;

    /// <summary>The first Intra 16x16 mb_type that has luma coefficients.</summary>
    private const int FirstWithLuma = 13;

    /// <summary>The luma bits of a pattern with every block coded.</summary>
    private const int AllLuma = 0xF;

    /// <summary>Sets the macroblock's kind, prediction mode and coded block pattern from an Intra 16x16 mb_type.</summary>
    /// <param name="picture">The picture, whose Intra 4x4 modes are set to DC for the neighbours that follow.</param>
    /// <param name="mb">The macroblock.</param>
    /// <param name="type">The mb_type, 1 to 24.</param>
    internal static void ApplyIntra16x16(H264Picture picture, H264CurrentMb mb, int type)
    {
        var index = type - FirstIntra16x16;
        mb.Kind = H264MbKind.Intra16x16;
        mb.Intra16x16Mode = index % ModesPerGroup;
        mb.CbpChroma = (index / ModesPerGroup) % ChromaGroups;
        mb.CbpLuma = type >= FirstWithLuma ? AllLuma : 0;
        Array.Fill(picture.IntraModes, (byte)H264IntraModes.DcMode, mb.Address * H264Layout.LumaBlocks, H264Layout.LumaBlocks);
    }
}
