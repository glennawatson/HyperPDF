// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The kinds of macroblock the decoder keeps about a picture's macroblocks, for the neighbours that follow them.</summary>
internal static class H264MbKind
{
    /// <summary>A macroblock that has not been decoded.</summary>
    internal const byte None = 0;

    /// <summary>An intra macroblock predicted in 4x4 blocks.</summary>
    internal const byte Intra4x4 = 1;

    /// <summary>An intra macroblock predicted as one 16x16 block.</summary>
    internal const byte Intra16x16 = 2;

    /// <summary>An intra macroblock whose samples are sent as they are.</summary>
    internal const byte Pcm = 3;

    /// <summary>An inter predicted macroblock.</summary>
    internal const byte Inter = 4;
}
